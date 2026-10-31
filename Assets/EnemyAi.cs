using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// AI врага: рейд на здания, бой с юнитами и отдельная анимация
/// атаки здания через параметр isAttackingBuilding.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAi : MonoBehaviour
{
    [Header("Ссылки")] public Animator animator; public NavMeshAgent agent; public AttackController attack;
    [Header("Рейд")] public bool IsRaid;
    public float detectRadius = 14f, unitRange = 2f, buildingRange = 3.5f;
    public float unitInterval = 1f, buildingInterval = 1f, scanInterval = 0.35f;
    public bool fightUnits = true, randomBuilding = true;
    [Header("Здоровье")] public bool addUnitIfMissing = true; public float maxHealth = 100f;
    [Header("Анимация")] public string moveParameter = "isMoving", attackParameter = "isAttacking", buildingParameter = "isAttackingBuilding";
    [Header("Текущие цели")] [SerializeField] private Unit unitTarget;
    [SerializeField] private BuildingHealth buildingTarget;

    public Unit UnitTarget => unitTarget;
    public BuildingHealth BuildingTarget => buildingTarget;

    private Unit self;
    private float nextScan, nextUnitHit, nextBuildingHit;
    private bool hasMove, hasAttack, hasBuilding;

    private void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!attack) attack = GetComponent<AttackController>();
        if (!attack) { attack = gameObject.AddComponent<AttackController>(); attack.IsPlayer = false; attack.unitDamage = 10; }
        attack.damageHandledExternally = true;

        self = GetComponent<Unit>();
        if (!self && addUnitIfMissing) { self = gameObject.AddComponent<Unit>(); self.animator = animator; self.navMeshAgent = agent; }
        if (self && self.unitMaxHealth <= 0) self.unitMaxHealth = maxHealth;
        if (agent && agent.isOnNavMesh) agent.stoppingDistance = 0.1f;
        if (animator) { hasMove = HasParam(moveParameter); hasAttack = HasParam(attackParameter); hasBuilding = HasParam(buildingParameter); }
    }

    private void Start() { if (IsRaid) PickBuilding(); }

    private void Update()
    {
        if (!agent) return;
        if (unitTarget && (!unitTarget.gameObject.activeInHierarchy || unitTarget.unitHealth <= 0)) unitTarget = null;
        if (buildingTarget && (!buildingTarget.gameObject.activeInHierarchy || buildingTarget.IsDead)) buildingTarget = null;
        if (!IsRaid) buildingTarget = null;
        else if (!buildingTarget && !unitTarget) PickBuilding();

        if (Time.time >= nextScan) { nextScan = Time.time + Mathf.Max(0.05f, scanInterval); unitTarget = fightUnits ? FindUnit() : null; }

        if (unitTarget) FightUnit();
        else if (IsRaid && buildingTarget) FightBuilding();
        else Idle();
    }

    public void Raid() { IsRaid = true; if (!buildingTarget || buildingTarget.IsDead) PickBuilding(); }
    public void StopRaid() { IsRaid = false; buildingTarget = null; }

    private void PickBuilding()
    {
        buildingTarget = null;
        float nearest = float.MaxValue;
        int count = 0;
        foreach (var b in BuildingManager.AllBuildings)
        {
            if (!b || b.IsDead || !b.gameObject.activeInHierarchy) continue;
            if (randomBuilding) { count++; if (Random.Range(0, count) == 0) buildingTarget = b; }
            else { float d = (b.transform.position - transform.position).sqrMagnitude; if (d < nearest) { nearest = d; buildingTarget = b; } }
        }
    }

    private Unit FindUnit()
    {
        Unit best = null;
        float bestSqr = detectRadius * detectRadius;
        if (ManagerGO.Instance && ManagerGO.Instance.allUnitsList != null)
            foreach (var go in ManagerGO.Instance.allUnitsList)
                if (go) Consider(go.GetComponent<Unit>(), ref best, ref bestSqr);

        if (!best)
            foreach (var hit in Physics.OverlapSphere(transform.position, detectRadius, ~0, QueryTriggerInteraction.Collide))
                if (hit) Consider(hit.GetComponentInParent<Unit>(), ref best, ref bestSqr);

        return best;
    }

    private void Consider(Unit unit, ref Unit best, ref float bestSqr)
    {
        if (!unit || unit == self || !unit.gameObject.activeInHierarchy || unit.unitHealth <= 0) return;
        if (unit.CompareTag("Enemy") || unit.GetComponent<EnemyAi>()) return;
        var ac = unit.GetComponent<AttackController>();
        if ((!ac || !ac.IsPlayer) && !unit.GetComponent<Builder>()) return;
        float sqr = (unit.transform.position - transform.position).sqrMagnitude;
        if (sqr <= bestSqr) { bestSqr = sqr; best = unit; }
    }

    private void FightUnit()
    {
        if (attack) attack.targetToAttack = unitTarget.transform;
        if (Vector3.Distance(transform.position, unitTarget.transform.position) > unitRange)
        { Move(unitTarget.transform.position); Animate(true, false, false); return; }

        Stop();
        Face(unitTarget.transform.position);
        Animate(false, true, false);
        if (Time.time < nextUnitHit) return;
        nextUnitHit = Time.time + Mathf.Max(0.05f, unitInterval);
        unitTarget.TakeDamage(Damage, gameObject);
    }

    private void FightBuilding()
    {
        Vector3 point = BuildingPoint(buildingTarget);
        if (attack) attack.targetToAttack = buildingTarget.transform;
        if (Vector3.Distance(transform.position, point) > buildingRange)
        { Move(point); Animate(true, false, false); return; }

        Stop();
        Face(buildingTarget.transform.position);
        Animate(false, false, true);
        AttackBuildingTarget();
    }

    /// <summary>Урон зданию с общим интервалом (вызывается и из EnemyAttackBuilding).</summary>
    public void AttackBuildingTarget()
    {
        if (!buildingTarget || buildingTarget.IsDead || Time.time < nextBuildingHit) return;
        nextBuildingHit = Time.time + Mathf.Max(0.05f, buildingInterval);
        buildingTarget.TakeDamage(Damage);
        if (buildingTarget && buildingTarget.IsDead) buildingTarget = null;
    }

    private Vector3 BuildingPoint(BuildingHealth b)
    {
        Vector3 point = b.transform.position;
        var col = b.GetComponentInChildren<Collider>();
        if (col)
        {
            point = col.ClosestPoint(transform.position);
            Vector3 away = transform.position - point; away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) point += away.normalized * 0.5f;
        }
        return NavMesh.SamplePosition(point, out var hit, 2f, NavMesh.AllAreas) ? hit.position : point;
    }

    private void Idle() { Stop(); if (attack) attack.targetToAttack = null; Animate(false, false, false); }

    private void Move(Vector3 p)
    {
        if (!agent || !agent.isOnNavMesh) return;
        if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) p = hit.position;
        agent.isStopped = false;
        agent.SetDestination(p);
    }

    private void Stop() { if (agent && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); } }

    private void Face(Vector3 p)
    {
        Vector3 d = p - transform.position; d.y = 0f;
        if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
    }

    private int Damage => attack ? Mathf.Max(1, attack.unitDamage) : 10;

    private bool HasParam(string parameterName)
    {
        foreach (var p in animator.parameters) if (p.name == parameterName) return true;
        return false;
    }

    private void Animate(bool moving, bool attacking, bool building)
    {
        if (!animator) return;
        if (hasMove) animator.SetBool(moveParameter, moving);
        if (hasAttack) animator.SetBool(attackParameter, attacking);
        if (hasBuilding) animator.SetBool(buildingParameter, building);
    }
}
