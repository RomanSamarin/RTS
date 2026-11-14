using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Unit), typeof(NavMeshAgent), typeof(AttackController))]
[RequireComponent(typeof(GOScript), typeof(ResourcesMove))]
public sealed class BatteringRam : MonoBehaviour
{
    [Min(.1f)] public float moveSpeed = 1.5f;
    [Min(.5f)] public float strikeRange = 2.4f;
    [Min(.1f)] public float windupSeconds = 1f;
    [Min(.1f)] public float reloadSeconds = 3f;
    [Min(1)] public int gateDamage = 100;
    [Min(1)] public int wallDamage = 55;
    public Animator animator;
    public Transform beam;
    [SerializeField] private Transform leftCrewPoint;
    [SerializeField] private Transform rightCrewPoint;
    public string Status { get; private set; } = "Ожидает приказ";
    public int ReadyCrewCount => (IsReady(leftCrew) ? 1 : 0) + (IsReady(rightCrew) ? 1 : 0);
    public bool HasCrew => ReadyCrewCount == 2;
    public bool IsMoving => HasCrew && agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped &&
        agent.velocity.sqrMagnitude > .01f;

    private RamCrewMember leftCrew, rightCrew;
    private Unit unit;
    private NavMeshAgent agent;
    private GOScript command;
    private AttackController attack;
    private BuildingHealth windupTarget;
    private float strikeAt, nextStrike, nextApproach;
    private bool approaching;
    private Vector3 beamRest;

    private void Awake()
    {
        unit = GetComponent<Unit>();
        agent = GetComponent<NavMeshAgent>();
        command = GetComponent<GOScript>();
        attack = GetComponent<AttackController>();
        GetComponent<ResourcesMove>().role = ResourcesMove.UnitRole.Transport;
        attack.autoAcquireTargets = false;
        attack.damageHandledExternally = true;
        attack.unitDamage = 0;
        command.ManualOrderIssued += ResetAttack;
    }

    private void Start()
    {
        command.SetBaseSpeed(moveSpeed);
        agent.areaMask &= ~WallNavigation.AreaMask;
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (beam != null) beamRest = beam.localPosition;
    }

    public bool CanAttack(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        var wall = target.GetComponentInParent<WallSegment>();
        return wall != null && wall.isActiveAndEnabled && !(wall is WallTower) &&
            wall.TryGetComponent<BuildingHealth>(out var health) && !health.IsDead &&
            FactionMember.AreEnemies(gameObject, wall.gameObject);
    }

    private bool IsReady(RamCrewMember member) => member != null && member.Ram == this && member.InPosition;

    internal Vector3 CrewPosition(int slot)
    {
        var point = slot == 0 ? leftCrewPoint : rightCrewPoint;
        return point != null ? point.position : transform.TransformPoint(slot == 0 ? -.65f : .65f, 0, -1.9f);
    }

    internal Quaternion CrewRotation(int slot)
    {
        var point = slot == 0 ? leftCrewPoint : rightCrewPoint;
        return point != null ? point.rotation : transform.rotation;
    }

    internal bool TryAssignCrew(GameObject soldier)
    {
        if (!isActiveAndEnabled || unit.unitHealth <= 0 || ShipPassenger.Controls(gameObject) ||
            (leftCrew != null && rightCrew != null) || !RamCrewMember.CanAssign(soldier, this)) return false;
        if (!soldier.TryGetComponent<RamCrewMember>(out var member))
            member = soldier.AddComponent<RamCrewMember>();
        int slot = leftCrew == null ? 0 : 1;
        if (!member.TryAssign(this, slot)) return false;
        if (slot == 0) leftCrew = member;
        else rightCrew = member;
        return true;
    }

    internal void ReleaseCrew(RamCrewMember member)
    {
        if (leftCrew == member) leftCrew = null;
        else if (rightCrew == member) rightCrew = null;
        else return;
        if (!SaveGameManager.IsLoading && agent.enabled && agent.isOnNavMesh)
        {
            StopOrder();
            agent.isStopped = true;
        }
    }

    public void ReleaseCrew()
    {
        if (leftCrew != null) leftCrew.Release();
        if (rightCrew != null) rightCrew.Release();
    }

    public void StopOrder()
    {
        command.StopMoving();
        attack.targetToAttack = null;
        command.NotifyMilitaryOrder();
        ResetAttack();
    }

    private void ResetAttack()
    {
        windupTarget = null;
        approaching = false;
        nextApproach = 0;
    }

    private void Update()
    {
        if (SaveGameManager.IsLoading || unit.unitHealth <= 0) return;
        int ready = ReadyCrewCount;
        if (ready < 2)
        {
            Status = $"Расчёт тарана: {ready}/2";
            if (agent.enabled && agent.isOnNavMesh)
            {
                if (agent.hasPath || agent.pathPending || command.isCommandToMove || attack.targetToAttack != null)
                    StopOrder();
                agent.isStopped = true;
            }
            return;
        }
        if (!agent.enabled || !agent.isOnNavMesh) return;
        if (ShipPassenger.Controls(gameObject) || command.isCommandToMove || command.IsRetreating)
        {
            ResetAttack();
            Status = "Перемещается";
            return;
        }
        var target = attack.targetToAttack;
        if (!CanAttack(target))
        {
            attack.targetToAttack = null;
            if (approaching) agent.ResetPath();
            ResetAttack();
            Status = "Ожидает приказ";
            return;
        }

        var health = target.GetComponentInParent<BuildingHealth>();
        var body = health.GetComponent<BoxCollider>();
        if (body == null || !body.enabled) body = null;
        Vector3 point = ContactPoint(body, health.transform, transform.position);
        Vector3 direction = point - transform.position;
        direction.y = 0;
        if (direction.sqrMagnitude > strikeRange * strikeRange || Mathf.Abs(point.y - transform.position.y) > .75f)
        {
            windupTarget = null;
            if (Time.time < nextApproach) return;
            nextApproach = Time.time + .65f;
            approaching = TryApproach(health.transform, body, direction);
            if (!approaching) agent.ResetPath();
            Status = approaching ? "Подходит к укреплению" : "Нет пути к укреплению";
            return;
        }

        if (agent.hasPath) agent.ResetPath();
        agent.isStopped = true;
        approaching = false;
        if (direction.sqrMagnitude > .001f)
        {
            var facing = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 100 * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, facing) > 8) return;
        }
        if (Time.time < nextStrike) { Status = "Готовится к следующему удару"; return; }
        if (windupTarget != health)
        {
            windupTarget = health;
            strikeAt = Time.time + Mathf.Max(.1f, windupSeconds);
        }
        Status = "Бьёт по укреплению";
        if (Time.time < strikeAt) return;
        int damage = health.GetComponent<WallGate>() != null ? gateDamage : wallDamage;
        health.TakeDamage(damage, gameObject);
        windupTarget = null;
        nextStrike = Time.time + Mathf.Max(.1f, reloadSeconds);
    }

    private static Vector3 ContactPoint(Collider body, Transform target, Vector3 position)
        => body != null ? body.ClosestPoint(position) : target.position;

    private bool TryApproach(Transform target, Collider body, Vector3 direction)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        var path = new NavMeshPath();
        Vector3 outward = direction.sqrMagnitude > .001f ? -direction.normalized : -target.forward;
        Vector3 center = body != null ? body.bounds.center : target.position;
        float extent = body != null ? body.bounds.extents.magnitude : 1f;
        float distance = Mathf.Clamp(strikeRange * .65f, .2f, strikeRange);
        for (int step = 0; step < 8; step++)
        {
            Vector3 side = Quaternion.AngleAxis(step * 45f, Vector3.up) * outward;
            Vector3 probe = center + side * (extent + distance);
            probe.y = target.position.y;
            Vector3 desired = ContactPoint(body, target, probe) + side * distance;
            if (!NavMesh.SamplePosition(desired, out var hit, 1f, filter)) continue;
            Vector3 contact = ContactPoint(body, target, hit.position);
            if (Vector3.Distance(contact, hit.position) > strikeRange ||
                !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            agent.isStopped = false;
            return agent.SetPath(path);
        }
        return false;
    }

    private void LateUpdate()
    {
        if (SaveGameManager.IsLoading) return;
        bool striking = windupTarget != null;
        bool moving = IsMoving;
        WorkerAnimation.Bool(animator, "idle", !moving && !striking);
        WorkerAnimation.Bool(animator, "isMoving", moving);
        WorkerAnimation.Bool(animator, "isAttacking", striking);
        if (beam != null && (animator == null || animator.runtimeAnimatorController == null))
        {
            float phase = striking ? Mathf.Clamp01(1 - (strikeAt - Time.time) / Mathf.Max(.1f, windupSeconds)) : 0;
            beam.localPosition = beamRest + Vector3.forward * (.5f * phase);
        }
    }

    private void OnDisable()
    {
        if (!SaveGameManager.IsLoading) ReleaseCrew();
    }

    private void OnDestroy()
    {
        if (command != null) command.ManualOrderIssued -= ResetAttack;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int slot = 0; slot < 2; slot++)
        {
            Vector3 point = CrewPosition(slot);
            Gizmos.DrawWireSphere(point, .25f);
            Gizmos.DrawRay(point, CrewRotation(slot) * Vector3.forward * .65f);
        }
    }

    public static GameObject CreatePlaceholder(Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("Таран");
        root.SetActive(false);
        root.layer = LayerMask.NameToLayer("Clickable");
        root.transform.SetPositionAndRotation(position, rotation);
        var ram = root.AddComponent<BatteringRam>();
        var nav = root.GetComponent<NavMeshAgent>();
        nav.radius = 1.05f;
        nav.height = 2.2f;
        nav.speed = 1.5f;
        var body = root.AddComponent<BoxCollider>();
        body.center = Vector3.up;
        body.size = new Vector3(1.9f, 1.8f, 2.8f);
        root.GetComponent<Unit>().unitMaxHealth = 220;
        Part(root, "Рама", new Vector3(0, .75f, 0), new Vector3(1.8f, .35f, 2.7f));
        Part(root, "Навес", new Vector3(0, 1.8f, -.25f), new Vector3(2f, .25f, 2.25f));
        ram.beam = Part(root, "Бревно", new Vector3(0, 1.15f, .35f), new Vector3(.4f, .4f, 3.2f));
        foreach (float x in new[] { -.9f, .9f })
            foreach (float z in new[] { -.85f, .85f })
                Part(root, "Колесо", new Vector3(x, .4f, z), new Vector3(.24f, .8f, .8f));
        root.SetActive(true);
        return root;
    }

    private static Transform Part(GameObject root, string label, Vector3 position, Vector3 size)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = label;
        part.layer = root.layer;
        part.transform.SetParent(root.transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        var collider = part.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        return part.transform;
    }
}
