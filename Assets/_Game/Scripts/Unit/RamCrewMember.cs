using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class RamCrewMember : MonoBehaviour
{
    private BatteringRam ram;
    private int slot, token;
    private bool inPosition;
    private bool previousRootMotion;
    private float startedAt;
    private float nextRepath;
    private NavMeshAgent agent;
    private Animator animator;
    private GOScript command;
    public BatteringRam Ram => ram;
    public bool IsAssigned => ram != null;
    public string Status => inPosition ? (ram != null && ram.IsMoving ? "Толкает таран" : "У тарана") : "Идёт к тарану";
    public bool InPosition => IsAssigned && inPosition && isActiveAndEnabled &&
        TryGetComponent<Unit>(out var unit) && unit.unitHealth > 0 &&
        FactionMember.CanUse(gameObject, ram.gameObject);
    public static bool Assigned(GameObject obj) => obj != null && (obj.GetComponent<RamCrewMember>()?.IsAssigned ?? false);
    public static bool Seated(GameObject obj) => obj.GetComponent<RamCrewMember>()?.inPosition ?? false;
    public static bool Controls(Component obj) => obj.GetComponentInParent<RamCrewMember>()?.IsAssigned ?? false;

    internal static bool CanAssign(GameObject soldier, BatteringRam target)
    {
        if (soldier == null || !soldier.activeInHierarchy || !FactionMember.CanUse(soldier, target.gameObject) ||
            soldier.GetComponent<ResourcesMove>()?.role != ResourcesMove.UnitRole.Soldier ||
            !WallStation.AllowsWalkway(soldier) || HospitalPatient.Controls(soldier.transform) ||
            ShipPassenger.Controls(soldier) || (soldier.GetComponent<ShipCrewMember>()?.IsAssigned ?? false) ||
            (soldier.GetComponent<SiegeLadderClimber>()?.IsBusy ?? false) ||
            (soldier.GetComponent<SiegeLadderInstaller>()?.IsBusy ?? false)) return false;
        return soldier.TryGetComponent<Unit>(out var unit) && unit.unitHealth > 0 &&
            soldier.TryGetComponent<NavMeshAgent>(out var nav) && nav.enabled && nav.isOnNavMesh &&
            soldier.GetComponent<GOScript>() != null;
    }

    private void Awake() => Cache();

    private void Cache()
    {
        agent = GetComponent<NavMeshAgent>();
        command = GetComponent<GOScript>();
        animator = GetComponent<Unit>()?.animator ?? GetComponentInChildren<Animator>(true);
    }

    internal bool TryAssign(BatteringRam target, int position)
    {
        if (!isActiveAndEnabled || IsAssigned || !TryApproach(target, position, out var point)) return false;
        command.NotifyMilitaryOrder();
        command.StopMoving();
        GetComponent<MilitaryOrders>()?.Cancel();
        GetComponent<ArmyRejoin>()?.Cancel("");
        GetComponent<WallStation>()?.Release();
        GetComponent<BattleCombatant>()?.PrepareForOrder();
        FormationHold.Clear(gameObject);
        FormationAnchor.Release(gameObject);
        if (TryGetComponent<AttackController>(out var attack)) attack.targetToAttack = null;
        if (!command.TryBeginSupplyMove(out token) || !command.MoveForSupply(token, point, .1f))
        {
            command.EndSupplyMove(token);
            token = 0;
            return false;
        }
        ram = target;
        slot = position;
        startedAt = Time.time;
        nextRepath = Time.time + .5f;
        command.ManualOrderIssued += Release;
        return true;
    }

    private bool TryApproach(BatteringRam target, int position, out Vector3 point)
    {
        point = default;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask & ~WallNavigation.AreaMask };
        if (!NavMesh.SamplePosition(target.CrewPosition(position), out var hit, 1f, filter)) return false;
        var path = new NavMeshPath();
        if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        point = hit.position;
        return Vector3.Distance(point, target.CrewPosition(position)) <= .35f;
    }

    private void Update()
    {
        if (SaveGameManager.IsLoading || ReferenceEquals(ram, null)) return;
        if (!IsAssigned || !ram.isActiveAndEnabled || !FactionMember.CanUse(gameObject, ram.gameObject) ||
            GetComponent<Unit>().unitHealth <= 0)
        {
            Release();
            return;
        }
        if (inPosition) return;
        if (!agent.enabled || !agent.isOnNavMesh || !command.OwnsSupplyOrder(token) || Time.time - startedAt > 35f)
        {
            Release();
            return;
        }

        if (Vector3.Distance(transform.position, ram.CrewPosition(slot)) <= .5f)
        {
            command.EndSupplyMove(token);
            token = 0;
            agent.enabled = false;
            inPosition = true;
            if (animator != null)
            {
                previousRootMotion = animator.applyRootMotion;
                animator.applyRootMotion = false;
            }
            return;
        }
        if (Time.time < nextRepath) return;
        nextRepath = Time.time + .5f;
        if (agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete &&
            (agent.destination - ram.CrewPosition(slot)).sqrMagnitude <= .35f * .35f) return;
        if (!TryApproach(ram, slot, out var point)) { Release(); return; }
        if (!command.MoveForSupply(token, point, .1f)) Release();
    }

    private void LateUpdate()
    {
        if (SaveGameManager.IsLoading || !InPosition) return;
        transform.SetPositionAndRotation(ram.CrewPosition(slot), ram.CrewRotation(slot));
        WorkerAnimation.Bool(animator, "IsFollowing", false);
        WorkerAnimation.Bool(animator, "isMoving", false);
        WorkerAnimation.Bool(animator, "isAttacking", false);
        WorkerAnimation.Bool(animator, "isPushingRam", ram.IsMoving);
    }

    public void Release()
    {
        if (ReferenceEquals(ram, null) && !inPosition) return;
        var previous = ram;
        ram = null;
        if (command != null)
        {
            command.ManualOrderIssued -= Release;
            command.EndSupplyMove(token);
        }
        token = 0;
        WorkerAnimation.Bool(animator, "IsFollowing", false);
        if (previous != null) previous.ReleaseCrew(this);
        if (!inPosition) return;
        inPosition = false;
        if (animator != null) animator.applyRootMotion = previousRootMotion;
        WorkerAnimation.Bool(animator, "isPushingRam", false);
        if (agent == null || !gameObject.activeInHierarchy || GetComponent<Unit>().unitHealth <= 0) return;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask & ~WallNavigation.AreaMask };
        if (NavMesh.SamplePosition(transform.position, out var ground, 2f, filter))
            transform.position = ground.position;
        agent.enabled = true;
    }

    internal void RestoreSavedHooks()
    {
        Cache();
        if (command == null || agent == null) return;
        command.ManualOrderIssued -= Release;
        if (ram == null)
        {
            Release();
            return;
        }
        command.ManualOrderIssued += Release;
        if (!inPosition) return;
        agent.enabled = false;
        if (animator != null) animator.applyRootMotion = false;
    }

    private void OnDisable()
    {
        if (!SaveGameManager.IsLoading) Release();
    }
}
