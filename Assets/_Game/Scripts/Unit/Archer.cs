using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

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
    [Min(0f)] public float closeRange = 3f;
    [Range(0.1f, 1f)] public float closeRangeDamageMultiplier = 0.4f;
    [Tooltip("Слои стен и прочих препятствий для стрел. Исключите слой земли, если точка выстрела внутри неё.")]
    public LayerMask shotBlockingLayers = ~0;

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
    private BattleCombatant battle;
    private readonly RaycastHit[] shotHits = new RaycastHit[64];
    private string[] attackStateNames;
    private static readonly string[] movingStateNames = { "Moving", "Following State" };
    private static readonly string[] idleStateNames = { "Idle", "idle State" };
    private static readonly int attackingHash = Animator.StringToHash("isAttacking");

    private float cooldown;
    private float searchTimer;
    private float nextAutoVisibilityCheck;
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
        attackStateNames = new[] { attackAnimationState, "Attack State", "Attack" };

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
        nextAutoVisibilityCheck = 0f;
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
        if(SaveGameManager.IsLoading)return;
        if(ShipPassenger.Controls(gameObject)||RamCrewMember.Assigned(gameObject)){StopAttackAnimation();ReleaseRotation();return;}
        if(SiegeLadderAnimation.Controls(this)){StopAttackAnimation();ReleaseRotation();return;}
        if (UnitEat.ControlsMovement(this)) { StopAttackAnimation(); ReleaseRotation(); return; }
        if (BattleCombatant.BlocksActions(this)) { StopAttackAnimation(); ReleaseRotation(); return; }
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

        float distanceSquared = (transform.position - target.position).sqrMagnitude;
        if (distanceSquared > attackRange * attackRange)
        {
            // Цель ушла из зоны — прекращаем анимацию атаки
            StopAttackAnimation();
            ReleaseRotation();

            if (moveIntoRange && !WallStation.Holding(this) && !BattleCombatant.Defends(this)&&!MilitaryOrders.BlocksChase(this))
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

        if (!HasClearShot(target))
        { StopAttackAnimation(); cooldown = 0.2f; return; }

        Shoot(target);
        if (battle == null) battle = GetComponent<BattleCombatant>();
        cooldown = attackCooldown / Mathf.Max(0.1f, battle != null ? battle.AttackRateMultiplier : 1f);
    }

    // ------------------------------------------------------------------ Цель

    // Цель берём из AttackController (ПКМ / авто-триггер), иначе ищем сами
    private Transform GetTarget()
    {
        if (attackController != null && attackController.targetToAttack != null)
        {
            bool lostAutomaticTarget = attackController.targetToAttack == autoTarget
                && (transform.position - autoTarget.position).sqrMagnitude > attackRange * attackRange;
            if (!lostAutomaticTarget && autoTarget != null && attackController.targetToAttack == autoTarget && Time.time >= nextAutoVisibilityCheck)
            {
                nextAutoVisibilityCheck = Time.time + Mathf.Max(0.2f, targetSearchInterval);
                lostAutomaticTarget = !HasClearShot(autoTarget);
            }
            if (IsAlive(attackController.targetToAttack) && FactionMember.AreEnemies(gameObject,attackController.targetToAttack.gameObject) && MilitaryOrders.AllowsTarget(gameObject,attackController.targetToAttack)&& !lostAutomaticTarget)
                return attackController.targetToAttack;

            attackController.targetToAttack = null;
            autoTarget = null;
        }

        if (!autoAcquireTarget || (attackController!=null&&!attackController.autoAcquireTargets))
            return null;

        // Поиск по реестру владельцев выполняется с интервалом.
        searchTimer -= Time.deltaTime;
        if (searchTimer > 0f)
            return autoTarget != null && IsAlive(autoTarget)&&FactionMember.AreEnemies(gameObject,autoTarget.gameObject)&&MilitaryOrders.AllowsTarget(gameObject,autoTarget) ? autoTarget : null;

        searchTimer = Mathf.Max(0.1f, targetSearchInterval);
        autoTarget = FindNearestEnemy();

        // Сообщаем остальной системе, что цель есть (анимации, курсор и т.п.)
        if (autoTarget != null && attackController != null && attackController.targetToAttack == null)
            attackController.targetToAttack = autoTarget;

        return autoTarget;
    }

    private Transform FindNearestEnemy()
    {
        Transform nearest = null;
        float nearestDistance = attackRange * attackRange;

        foreach (var member in FactionMember.All)
        {
            if(member==null||!FactionMember.AreEnemies(gameObject,member.gameObject))continue;
            if(!MilitaryOrders.AllowsTarget(gameObject,member.transform))continue;
            GameObject enemy=member.gameObject;
            if (enemy == null || !enemy.activeInHierarchy)
                continue;

            float distance = (transform.position - enemy.transform.position).sqrMagnitude;
            if (distance >= nearestDistance) continue;

            Unit unit = enemy.GetComponent<Unit>();
            if (unit == null || unit.unitHealth <= 0f)
                continue;

            // Деревья тоже помечены тегом Enemy — пропускаем всё, что не боевой юнит
            var side = enemy.GetComponent<AttackController>();
            if (onlyCombatTargets && side == null) continue;
            if (!HasClearShot(enemy.transform)) continue;

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

        // Arrow сам совмещает продольную ось геометрии с касательной траектории.
        Quaternion spawnRotation = arrowPrefab.transform.rotation;

        Arrow arrow = Instantiate(arrowPrefab, start, spawnRotation);
        int shotDamage = (transform.position - target.position).sqrMagnitude < closeRange * closeRange
            ? Mathf.Max(1, Mathf.RoundToInt(damage * closeRangeDamageMultiplier)) : damage;
        arrow.Launch(target, shotDamage, arrowSpeed, gameObject, aimHeight, shotBlockingLayers.value);
    }

    internal bool TryShootFromDeck(ShipHull ship, ShipHull target, float damageMultiplier)
    {
        if (!isActiveAndEnabled || arrowPrefab == null) return false;
        const float hullAimHeight = .65f;
        if (!HasClearShot(target.transform, ship.transform, hullAimHeight)) return false;

        FaceTarget(target.transform, instant: true);
        var arrow = Instantiate(arrowPrefab, GetShootPoint(), arrowPrefab.transform.rotation);
        int shotDamage = Mathf.Max(1, Mathf.RoundToInt(damage * damageMultiplier));
        arrow.Launch(target.transform, shotDamage, arrowSpeed, ship.gameObject, hullAimHeight, shotBlockingLayers.value);
        return true;
    }

    private bool HasClearShot(Transform target, Transform ownHull = null, float? shotAimHeight = null)
    {
        if (arrowPrefab == null) return true; // Shoot выдаст понятное сообщение о неназначенном префабе.
        Vector3 origin = GetShootPoint();
        arrowPrefab.FlightPlan(origin, target, arrowSpeed, shotAimHeight ?? aimHeight, out var end, out _, out float height);
        int segments = Mathf.Clamp(Mathf.CeilToInt((Vector3.Distance(origin, end) + height * 2f) / 1.5f), 4, 32);
        Vector3 from = origin;
        for (int segment = 1; segment <= segments; segment++)
        {
            Vector3 to = ArrowTrajectory.Position(origin, end, (float)segment / segments, height);
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance > 0.00001f)
            {
                int count = Physics.SphereCastNonAlloc(from, arrowPrefab.CollisionRadius, delta / distance,
                    shotHits, distance, shotBlockingLayers, QueryTriggerInteraction.Ignore);
                if (count == shotHits.Length) return false;
                for (int i = 0; i < count; i++)
                {
                    Transform hit = shotHits[i].transform;
                    if (hit == transform || hit.IsChildOf(transform)) continue;
                    if (ownHull != null && (hit == ownHull || hit.IsChildOf(ownHull))) continue;
                    if (hit == target || hit.IsChildOf(target) || target.IsChildOf(hit)) continue;
                    return false;
                }
            }
            from = to;
        }
        return true;
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

    private void FaceTarget(Transform target, bool instant = false)
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion lookRotation = Quaternion.LookRotation(direction);

        transform.rotation = !instant && turnSpeed > 0f
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

        animator.SetBool(attackingHash, true);

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

    private string[] AttackStateNames => attackStateNames;

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

        animator.SetBool(attackingHash, false);

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
        string[] stateNames = moving ? movingStateNames : idleStateNames;

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
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        var camp = target.GetComponentInParent<BanditCamp>();
        if (camp != null) return !camp.IsDestroyed && camp.isActiveAndEnabled;
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
