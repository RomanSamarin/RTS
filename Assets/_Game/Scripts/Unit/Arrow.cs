using UnityEngine;

/// <summary>
/// Стрела лучника: сама летит к цели (самонаведение), при попадании
/// наносит урон и исчезает. Вешается на префаб стрелы.
/// Коллайдер и Rigidbody не нужны — попадание считается по дистанции.
/// </summary>
public class Arrow : MonoBehaviour
{
    [Header("Полёт")]
    [Tooltip("Скорость полёта, м/с (переопределяется лучником при выстреле)")]
    [SerializeField] private float speed = 25f;

    [Tooltip("Сколько секунд живёт стрела, если никуда не попала")]
    [SerializeField] private float lifetime = 5f;

    [Tooltip("Минимальное время полёта, сек. Стрела не долетит до цели быстрее, " +
             "иначе при стрельбе вблизи она исчезает за 1–2 кадра и её не видно")]
    [SerializeField] private float minFlightTime = 0.4f;

    [Tooltip("Радиус попадания (если стрела пролетела ближе этого к цели)")]
    [SerializeField] private float hitRadius = 0.35f;

    [Tooltip("На какой высоте от пивота цели считается попадание")]
    [SerializeField] private float hitHeight = 1f;

    [Tooltip("Поворачивать стрелу по направлению полёта (с сохранением поворота префаба)")]
    [SerializeField] private bool rotateToDirection = true;

    [Header("Урон")]
    [Tooltip("Урон одной стрелы (переопределяется лучником при выстреле)")]
    [SerializeField] private int damage = 10;

    [Header("Эффекты")]
    [Tooltip("Необязательно: эффект (частицы), который создаётся при попадании")]
    [SerializeField] private GameObject hitEffect;

    [Tooltip("Писать в консоль дистанцию и время полёта (для отладки)")]
    [SerializeField] private bool debugLog;

    private Transform target;
    private Vector3 lastTargetPosition;
    private GameObject owner;
    private bool launched;
    private float lifeTimer;
    private float launchTime;
    private float launchDistance;
    private Quaternion baseRotation;
    private Vector3 baseForward;

    /// <summary>
    /// Вызывается лучником сразу после создания стрелы.
    /// </summary>
    /// <param name="target">Цель, за которой летит стрела</param>
    /// <param name="damage">Урон при попадании</param>
    /// <param name="speed">Скорость полёта</param>
    /// <param name="owner">Кто выстрелил (нужен для награды за убийство)</param>
    /// <param name="hitHeight">Высота точки попадания над пивотом цели</param>
    public void Launch(Transform target, int damage, float speed, GameObject owner, float hitHeight = 1f)
    {
        this.target = target;
        this.damage = damage;
        this.speed = speed;
        this.owner = owner;
        this.hitHeight = hitHeight;

        // Запоминаем поворот, с которым стрела создана (поворот префаба).
        // Он будет базой для наведения: так модель сохраняет свою ориентацию
        // из префаба, а не заменяется на LookRotation.
        baseRotation = transform.rotation;
        baseForward = baseRotation * Vector3.forward;

        // Запоминаем, куда лететь, даже если цель уничтожат прямо в полёте
        lastTargetPosition = target != null
            ? target.position + Vector3.up * hitHeight
            : transform.position + transform.forward * 10f;

        launchTime = Time.time;
        launchDistance = Vector3.Distance(transform.position, lastTargetPosition);

        // Не даём стреле «телепортироваться» в цель за один кадр:
        // при стрельбе вблизи ограничиваем скорость, чтобы полёт длился minFlightTime.
        if (this.speed < 0.01f)
            this.speed = 0.01f;

        if (minFlightTime > 0f && launchDistance > 0.05f)
        {
            float maxSpeed = launchDistance / minFlightTime;
            if (this.speed > maxSpeed)
                this.speed = maxSpeed;
        }

        launched = true;
        // Страховка: стрела не должна исчезнуть раньше, чем долетит
        lifeTimer = Mathf.Max(lifetime, minFlightTime + 0.25f);

        if (debugLog)
            Debug.Log($"[Arrow] вылет: цель={(target != null ? target.name : "нет")}, " +
                      $"дистанция={launchDistance:F2} м, скорость={this.speed:F2} м/с, " +
                      $"время полёта≈{(launchDistance / this.speed):F2} с", this);
    }

    private void Update()
    {
        if (!launched)
            return;

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // Цель жива — обновляем точку прицеливания (стрела самонаводится)
        if (target != null)
            lastTargetPosition = target.position + Vector3.up * hitHeight;

        Vector3 toTarget = lastTargetPosition - transform.position;
        float step = speed * Time.deltaTime;

        // Если за этот кадр долетим — попадаем (без "проскакивания" сквозь цель)
        if (toTarget.magnitude <= Mathf.Max(hitRadius, step))
        {
            transform.position = lastTargetPosition;
            Hit();
            return;
        }

        Vector3 direction = toTarget.normalized;
        transform.position += direction * step;

        if (rotateToDirection)
        {
            // Доворачиваем стрелу от её исходного направления (поворот префаба)
            // к направлению полёта. Если направление совпадает с префабным,
            // стрела остаётся ровно в повороте префаба.
            transform.rotation = Quaternion.FromToRotation(baseForward, direction) * baseRotation;
        }
    }

    private void Hit()
    {
        if (target != null)
        {
            // Основная система здоровья в проекте — Unit (деревья, враги, юниты)
            Unit unit = target.GetComponent<Unit>();
            if (unit == null) unit = target.GetComponentInParent<Unit>();

            if (unit != null && unit.unitHealth > 0f)
            {
                unit.TakeDamage(damage, owner);
            }
            else
            {
                // Запасной вариант — отдельный скрипт Enemy
                Enemy enemy = target.GetComponent<Enemy>();
                if (enemy != null && enemy.health > 0)
                    enemy.ReceiveDamage(damage);
            }
        }

        if (hitEffect != null)
            Instantiate(hitEffect, transform.position, Quaternion.identity);

        if (debugLog)
            Debug.Log($"[Arrow] попадание в {(target != null ? target.name : "точку")} " +
                      $"через {(Time.time - launchTime):F2} с (урон {damage})", this);

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
