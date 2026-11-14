using UnityEngine;
using UnityEngine.AI;

/// <summary>Woodcutting, fishing and quarry work run without an attack Animator state.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ResourcesMove), typeof(NavMeshAgent))]
public class ResourceWorker : MonoBehaviour
{
    [Min(.2f)] public float workDistance = 2f;
    [Min(.05f)] public float workRate = 1f;
    [field: SerializeField] public bool isWorking { get; private set; }
    private ResourcesMove worker;
    private NavMeshAgent agent;
    private AttackController targetController;
    private Transform previousTarget;
    private float nextPath;
    private readonly ResourceWorkClock clock = new ResourceWorkClock();
    public static bool UsesRole(ResourcesMove.UnitRole role) => role == ResourcesMove.UnitRole.Lumberjack
        || role == ResourcesMove.UnitRole.Fisher || role == ResourcesMove.UnitRole.StoneMason;
    public static bool OwnsTarget(ResourcesMove worker, Transform target)
    {
        if (worker == null || !UsesRole(worker.role) || target == null || !target.gameObject.activeInHierarchy) return false;
        if (FactionMember.AreEnemies(worker.gameObject, target.gameObject)) return false;
        switch (worker.role)
        {
            case ResourcesMove.UnitRole.Lumberjack:
                var tree = target.GetComponent<Unit>(); return tree != null && tree.woodReward > 0 && tree.unitHealth > 0;
            case ResourcesMove.UnitRole.Fisher:
                var water = target.GetComponent<FishingSpot>(); return water != null && water.fishHealth > 0;
            case ResourcesMove.UnitRole.StoneMason:
                var stone = target.GetComponent<StoneQuarry>(); return stone != null && stone.currentStoneLeft > 0 && stone.stoneHealth > 0;
            default: return false;
        }
    }
    private void Awake()
    {
        worker = GetComponent<ResourcesMove>(); agent = GetComponent<NavMeshAgent>();
        targetController = GetComponent<AttackController>();
    }
    private void Update()
    {
        if (SaveGameManager.IsLoading) return;
        isWorking = false;
        var target = targetController != null ? targetController.targetToAttack : null;
        if (UsesRole(worker.role) && target != null && !FactionMember.AreEnemies(gameObject, target.gameObject)
            && FactionMember.IsResource(target.gameObject) && !OwnsTarget(worker, target))
        { targetController.targetToAttack = null; target = null; }
        if (!worker.CanPerformWork || worker.IsDeliveringResources || !agent.enabled || !agent.isOnNavMesh || !OwnsTarget(worker, target))
        { previousTarget = null; clock.Reset(); return; }
        var cargo = GetComponent<GettingResources>();
        if (cargo != null)
        {
            int carried = 0;
            foreach (_Game.Scripts.UI.ResourceType type in System.Enum.GetValues(typeof(_Game.Scripts.UI.ResourceType))) carried += cargo.GetStoredAmount(type);
            if (carried >= Mathf.Max(1, worker.MaxUnitResources)) { worker.SetInventoryFull(); clock.Reset(); return; }
        }
        if (target != previousTarget) { previousTarget = target; clock.Reset(); nextPath = 0; }
        Vector3 point = worker.GetApproachPoint(target);
        if (Vector3.Distance(transform.position, point) > workDistance)
        {
            clock.Reset();
            if (Time.time < nextPath) return;
            nextPath = Time.time + .5f;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(point, out var hit, workDistance, filter)
                || Vector3.Distance(hit.position, point) > workDistance) return;
            var path = new NavMeshPath();
            if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
            { agent.isStopped = false; agent.SetPath(path); }
            return;
        }
        agent.ResetPath();
        Vector3 forward = point - transform.position; forward.y = 0;
        if (forward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(forward);
        isWorking = true;
        if (!clock.Tick(Time.deltaTime, 1f / Mathf.Max(.05f, workRate))) return;
        switch (worker.role)
        {
            case ResourcesMove.UnitRole.Lumberjack:
                target.GetComponent<Unit>().TakeDamage(Mathf.Max(1, targetController.unitDamage), gameObject); break;
            case ResourcesMove.UnitRole.Fisher:
                target.GetComponent<FishingSpot>().TakeDamage(1, gameObject); break;
            case ResourcesMove.UnitRole.StoneMason:
                target.GetComponent<StoneQuarry>().TakeDamage(1, gameObject); break;
        }
        if (target == null || targetController.targetToAttack != target || !OwnsTarget(worker, target)) isWorking = false;
    }
    private void OnDisable() { isWorking = false; previousTarget = null; clock.Reset(); }
}
