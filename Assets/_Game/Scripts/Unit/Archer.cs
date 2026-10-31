using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Лучник: стреляет стрелами (Arrow) по цели из AttackController.targetToAttack.
/// Если цели нет — по желанию сам находит ближайшего врага.
/// Движением (подходом к цели) занимается текущая система юнитов
/// (UnitFollowState останавливает юнита на дистанции attackRange),
/// а урон наносит стрела при попадании.
/// Вешается на юнита рядом с AttackController / Unit / GOScript.
/// </summary>
[RequireComponent(typeof(AttackController))]
public class Archer : MonoBehaviour
{
    [Header("Дальность и темп стрельбы")]
    [Tooltip("С какой дистанции лучник стреляет (UnitFollowState остановит его на этом расстоянии)")]
    public float attackRange = 12f;

    [Tooltip("Пауза между выстрелами в секундах")]
    public float attackCooldown = 1.2f;

    [Tooltip("Задержка (прицеливание) перед первым выстрелом по новой цели, сек")]
    public float firstShotDelay = 1f;

    [Header("Стрела")]
    [Tooltip("Префаб стрелы со скриптом Arrow")]
    public Arrow arrowPrefab;

    [Tooltip("Точка вылета стрелы (лук). Если не задана — используется центр юнита")]
    public Transform bowPoint;

    [Tooltip("Урон одной стрелы")]
    public int damage = 10;

    [Tooltip("Скорость полёта стрелы, м/с")]
    public float arrowSpeed = 25f;

    [Tooltip("Высота точки прицеливания над пивотом цели")]
    public float aimHeight = 1f;

    [Header("Поиск цели")]
    [Tooltip("Если цель не назначена — искать ближайшего врага самому")]
    public bool autoAcquireTarget = true;

    [Tooltip("Тег врагов")]
    public string enemyTag = "Enemy";

    [Tooltip("Бить только цели с AttackController (деревья с тегом Enemy при этом игнорируются)")]
    public bool onlyCombatTargets = true;

    [Tooltip("Как часто искать новую цель, сек")]
    public float targetSearchInterval = 0.5f;

    [Header("Движение (только если нет Animator юнитов)")]
    [Tooltip("Подходить к цели самому. НЕ включать, если у лучника стандартный Animator: " +
             "движением уже управляет UnitFollowState")]
    public bool moveIntoRange = false;

    [Header("Поворот к цели")]
    [Tooltip("Разворачивать лучника к цели во время боя (даже когда он стоит)")]
    public bool faceTargetWhileAttacking = true;

    [Tooltip("Скорость разворота к цели, град/с. 0 — мгновенно")]
    public float turnSpeed = 720f;

    [Header("Анимация атаки")]
    [Tooltip("Имя состояния атаки в Animator Controller (у лучника — Attack)")]
    public string attackAnimationState = "Attack";

    [Tooltip("Слой аниматора с состоянием атаки (0 — базовый)")]
    public int attackAnimationLayer = 0;

    private AttackController attackController;
    private GOScript goScript;
    private NavMeshAgent agent;
    private Animator animator;

    private float cooldown;
    private float searchTimer;
    private Transform autoTarget;

    private float aimTimer;
    private Transform aimTarget;

    private bool attackPlaying;
    private int attackLayer = -1;
    private int attackStateHash;
    private float nextAttackAnimCheck;

    private bool rotationLocked;
    private bool defaultUpdateRotation = true;

    private void Awake()
    {
        attackController = GetComponent<AttackController>();
        goScript = GetComponent<GOScript>();
        agent = GetComponent<NavMeshAgent>();

        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (agent != null)
            defaultUpdateRotation = agent.updateRotation;
    }

    private void OnEnable()
    {
        cooldown = 0f;
        searchTimer = 0f;
        autoTarget = null;
        aimTimer = 0f;
        aimTarget = null;
        attackPlaying = false;
        attackLayer = -1;
        nextAttackAnimCheck = 0f;
        ReleaseRotation();
    }

    private void OnDisable()
    {
        ReleaseRotation();
    }

    private void Update()
    {
        if (cooldown > 0f)
            cooldown -= Time.deltaTime;

        // Пока юнит выполняет приказ игрока на перемещение — не стреляем
        if (goScript != null && goScript.isCommandToMove)
        {
            StopAttackAnimation();
            ReleaseRotation();
            return;
        }

        Transform target = GetTarget();
        if (target == null)
        {
            StopAttackAnimation();
            ReleaseRotation();
            aimTarget = null;
            aimTimer = 0f;
            return;
        }

        // Новая цель — сначала прицелиться
        if (target != aimTarget)
        {
            aimTarget = target;
            aimTimer = firstShotDelay;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > attackRange)
        {
            // Цель ушла из зоны — прекращаем анимацию атаки
            StopAttackAnimation();
            ReleaseRotation();

            if (moveIntoRange)
                MoveTowards(target);

            // Прицеливание начнётся заново, когда войдём в радиус стрельбы
            aimTimer = firstShotDelay;
            return;
        }

        if (moveIntoRange && agent != null && agent.isOnNavMesh && agent.hasPath)
            agent.ResetPath();

        // Пока лучник бежит к цели — показываем бег, а не атаку.
        // Анимация атаки включается только после остановки на дистанции.
        if (IsAgentMoving())
        {
            StopAttackAnimation();
            ReleaseRotation();
            aimTimer = firstShotDelay;
            return;
        }

        // На время боя агент не должен крутить юнита: направление задаём сами,
        // иначе после остановки лучник остаётся спиной или боком к цели.
        if (faceTargetWhileAttacking)
        {
            LockRotation();
            FaceTarget(target);
        }
        else
        {
            ReleaseRotation();
        }

        // Держим анимацию атаки, пока цель в зоне: клип зациклен и не должен
        // останавливаться между выстрелами.
        EnsureAttackAnimation();

        // Пока прицеливается — стрелу не выпускаем
        if (aimTimer > 0f)
        {
            aimTimer -= Time.deltaTime;
            return;
        }

        if (cooldown > 0f)
            return;

        Shoot(target);
        cooldown = attackCooldown;
    }

    // ------------------------------------------------------------------ Цель

    // Цель берём из AttackController (ПКМ / авто-триггер), иначе ищем сами
    private Transform GetTarget()
    {
        if (attackController != null && attackController.targetToAttack != null)
        {
            if (IsAlive(attackController.targetToAttack))
                return attackController.targetToAttack;

            attackController.targetToAttack = null;
        }

        if (!autoAcquireTarget)
            return null;

        // Поиск по тегу — не каждый кадр, это дорого
        searchTimer -= Time.deltaTime;
        if (searchTimer > 0f && autoTarget != null && IsAlive(autoTarget))
            return autoTarget;

        searchTimer = targetSearchInterval;
        autoTarget = FindNearestEnemy();

        // Сообщаем остальной системе, что цель есть (анимации, курсор и т.п.)
        if (autoTarget != null && attackController != null && attackController.targetToAttack == null)
            attackController.targetToAttack = autoTarget;

        return autoTarget;
    }

    private Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);

        Transform nearest = null;
        float nearestDistance = attackRange;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null || !enemy.activeInHierarchy)
                continue;

            Unit unit = enemy.GetComponent<Unit>();
            if (unit == null || unit.unitHealth <= 0f)
                continue;

            // Деревья тоже помечены тегом Enemy — пропускаем всё, что не боевой юнит
            if (onlyCombatTargets && enemy.GetComponent<AttackController>() == null)
                continue;

            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = enemy.transform;
            }
        }

        return nearest;
    }

    // ------------------------------------------------------------------ Выстрел

    private void Shoot(Transform target)
    {
        if (arrowPrefab == null)
        {
            Debug.LogError($"[Archer] У \"{name}\" не назначен префаб стрелы (arrowPrefab)!", this);
            return;
        }

        Vector3 start = GetShootPoint();

        // Включаем анимацию атаки синхронно с выстрелом
        PlayAttackAnimation();

        // Берём поворот из префаба, а не LookRotation: стрела должна сохранить
        // ориентацию модели, заданную в префабе (поворот вокруг осей, наклон и т.п.).
        // Довернуть её по направлению полёта Arrow умеет сам — от исходного поворота.
        Quaternion spawnRotation = arrowPrefab.transform.rotation;

        Arrow arrow = Instantiate(arrowPrefab, start, spawnRotation);
        arrow.Launch(target, damage, arrowSpeed, gameObject, aimHeight);
    }

    private Vector3 GetShootPoint()
    {
        if (bowPoint != null)
        {
            Vector3 point = bowPoint.position;

            // Защита от неверно назначенной точки вылета: если она «улетела» от лучника,
            // стрела появится рядом с целью и исчезнет мгновенно.
            if ((point - transform.position).sqrMagnitude > 25f)
            {
                Debug.LogWarning($"[Archer] bowPoint у \"{name}\" находится дальше 5 м от лучника — " +
                                 "использую центр юнита. Проверьте назначенный Transform.", this);
                return transform.position + Vector3.up * aimHeight;
            }

            return point;
        }

        return transform.position + Vector3.up * aimHeight;
    }

    // ------------------------------------------------------------------ Поворот к цели

    private void LockRotation()
    {
        if (agent == null)
            return;

        if (!rotationLocked)
        {
            defaultUpdateRotation = agent.updateRotation;
            rotationLocked = true;
        }

        agent.updateRotation = false;
    }

    private void ReleaseRotation()
    {
        if (!rotationLocked)
            return;

        rotationLocked = false;

        if (agent != null)
            agent.updateRotation = defaultUpdateRotation;
    }

    private void FaceTarget(Transform target)
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion lookRotation = Quaternion.LookRotation(direction);

        transform.rotation = turnSpeed > 0f
            ? Quaternion.RotateTowards(transform.rotation, lookRotation, turnSpeed * Time.deltaTime)
            : lookRotation;
    }

    // ------------------------------------------------------------------ Анимация атаки

    /// <summary>
    /// Запускает анимацию выстрела. Состояние атаки проигрывается напрямую
    /// (CrossFade), потому что из состояния Idle в контроллере лучника
    /// нет перехода в атаку — параметр isAttacking сам по себе не срабатывал.
    /// </summary>
    private void PlayAttackAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        animator.SetBool("isAttacking", true);

        attackPlaying = true;
        attackLayer = -1;

        string[] stateNames = AttackStateNames;

        // Играем на всех видимых слоях, где есть состояние атаки
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            if (animator.GetLayerWeight(layer) <= 0.01f)
                continue;

            if (!TryCrossFadeAttack(layer, stateNames))
                continue;

            if (attackLayer < 0)
                attackLayer = layer;
        }

        // Если весов не видно (все слои 0) — пробуем указанный слой напрямую
        if (attackLayer < 0)
        {
            int layer = Mathf.Clamp(attackAnimationLayer, 0, Mathf.Max(0, animator.layerCount - 1));

            if (TryCrossFadeAttack(layer, stateNames))
                attackLayer = layer;
            else
                attackPlaying = false;
        }
    }

    private string[] AttackStateNames =>
        new[] { attackAnimationState, "Attack State", "Attack" };

    /// <summary>
    /// Проверяет, что анимация атаки всё ещё играет (контроллер мог перевести
    /// юнита в Moving/Idle), и при необходимости возвращает её.
    /// </summary>
    private void EnsureAttackAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        if (!attackPlaying)
        {
            PlayAttackAnimation();
            return;
        }

        if (attackLayer < 0 || Time.time < nextAttackAnimCheck)
            return;

        nextAttackAnimCheck = Time.time + 0.3f;

        if (animator.IsInTransition(attackLayer))
            return;

        if (animator.GetCurrentAnimatorStateInfo(attackLayer).shortNameHash == attackStateHash)
            return;

        TryCrossFadeAttack(attackLayer, AttackStateNames);
    }

    private bool TryCrossFadeAttack(int layer, string[] stateNames)
    {
        foreach (string stateName in stateNames)
        {
            if (string.IsNullOrEmpty(stateName))
                continue;

            int hash = Animator.StringToHash(stateName);
            if (!animator.HasState(layer, hash))
                continue;

            animator.CrossFadeInFixedTime(hash, 0.08f, layer, 0f);
            attackStateHash = hash;
            return true;
        }

        return false;
    }

    private void StopAttackAnimation()
    {
        if (animator == null)
        {
            attackPlaying = false;
            return;
        }

        animator.SetBool("isAttacking", false);

        if (!attackPlaying)
            return;

        attackPlaying = false;

        if (attackLayer < 0)
            return;

        // Выходим из атаки принудительно: из состояния Attack в контроллере есть
        // переходы только по isMoving / isHolding, поэтому стоящий лучник
        // замирал в последнем кадре анимации атаки.
        TryCrossFadeLocomotion(attackLayer, IsAgentMoving());
    }

    private bool TryCrossFadeLocomotion(int layer, bool moving)
    {
        string[] stateNames = moving
            ? new[] { "Moving", "Following State" }
            : new[] { "Idle", "idle State" };

        foreach (string stateName in stateNames)
        {
            int hash = Animator.StringToHash(stateName);
            if (!animator.HasState(layer, hash))
                continue;

            animator.CrossFadeInFixedTime(hash, 0.15f, layer, 0f);
            return true;
        }

        return false;
    }

    private bool IsAgentMoving()
    {
        if (agent == null || !agent.isOnNavMesh)
            return false;

        if (agent.velocity.sqrMagnitude > 0.01f)
            return true;

        // Агент уже получил путь, но ещё не успел разогнаться
        return agent.hasPath
               && !agent.pathPending
               && agent.remainingDistance > agent.stoppingDistance + 0.1f;
    }

    // Только для лучника без Animator-контроллера юнитов
    private void MoveTowards(Transform target)
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        if (Vector3.Distance(transform.position, target.position) > attackRange - 0.5f)
            agent.SetDestination(target.position);
    }

    private static bool IsAlive(Transform target)
    {
        Unit unit = target.GetComponent<Unit>();
        if (unit == null) unit = target.GetComponentInParent<Unit>();

        // Если Unit нет — пусть решает стрела (запасной скрипт Enemy)
        return unit == null || unit.unitHealth > 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (bowPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(bowPoint.position, 0.2f);
        }
    }
}
