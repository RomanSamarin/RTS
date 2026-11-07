using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(ResourcesMove), typeof(NavMeshAgent))]
public class Miner : MonoBehaviour
{
    [Min(0.5f)] public float workDistance = 2f;
    [Min(1f)] public float searchRadius = 200f;
    private ResourcesMove worker;
    private NavMeshAgent agent;
    private Animator animator;
    private OreDeposit target;
    private float nextSearch;
    private bool delivering;
    public string CurrentTask => delivering ? "Доставить" : target != null ? "Добывать руду" : "Искать работу";
    private GettingResources cargo;

    private void Awake()
    {
        worker = GetComponent<ResourcesMove>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        cargo = GetComponent<GettingResources>();
        if (cargo == null) cargo = gameObject.AddComponent<GettingResources>();
    }

    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (BuildingRoadAccess.PauseWorker(worker)) { SetWorking(false); return; }
        if (UnitEat.ControlsMovement(this)) { SetWorking(false); return; }
        if (worker.role != ResourcesMove.UnitRole.Miner) return;
        var command = GetComponent<GOScript>();
        if (worker.IsReturningToWorkplace || (command != null && command.isCommandToMove))
        {
            SetWorking(false);
            return;
        }
        if (!agent.enabled || !agent.isOnNavMesh) return;
        var attack = GetComponent<AttackController>();
        if (attack != null) attack.targetToAttack = null;

        // Работник разгружается только в собственном здании найма.
        GameObject home = worker.AssignedWorkplace;
        MiningHouse storage = home != null ? home.GetComponent<MiningHouse>() : null;
        if (home != null && storage == null) storage = home.AddComponent<MiningHouse>();
        if (storage == null || !storage.isActiveAndEnabled)
        {
            agent.ResetPath();
            SetWorking(false);
            return;
        }
        int carried = 0;
        foreach (_Game.Scripts.UI.ResourceType type in System.Enum.GetValues(typeof(_Game.Scripts.UI.ResourceType)))
            carried += cargo.GetStoredAmount(type);
        int capacity = Mathf.Max(1, worker.MaxUnitResources);
        if (carried >= capacity || ((target == null || target.Remaining <= 0) && (cargo.Iron > 0 || cargo.Gold > 0)))
            delivering = true;
        if (delivering)
        {
            SetWorking(false);
            if (worker.IsAtMiningHome(home))
            {
                agent.ResetPath();
                storage.Deposit(cargo);
                delivering = false;
                target = null;
            }
            else
            {
                agent.isStopped = false;
                agent.SetDestination(worker.GetApproachPoint(home.transform));
            }
            return;
        }
        if (target == null || !target.isActiveAndEnabled || target.Remaining <= 0)
        {
            target = null;
            if (Time.time >= nextSearch)
            {
                nextSearch = Time.time + 1f;
                float best = searchRadius * searchRadius;
                foreach (var deposit in OreDeposit.All)
                {
                    if (deposit == null || deposit.Remaining <= 0) continue;
                    float distance = (deposit.WorkPosition - transform.position).sqrMagnitude;
                    if (distance >= best) continue;
                    var path = new NavMeshPath();
                    if (!agent.CalculatePath(deposit.WorkPosition, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    best = distance;
                    target = deposit;
                }
            }
        }
        if (target == null) { agent.ResetPath(); SetWorking(false); return; }
        if (Vector3.Distance(transform.position, target.WorkPosition) > workDistance)
        {
            SetWorking(false);
            agent.isStopped = false;
            agent.SetDestination(target.WorkPosition);
            return;
        }
        agent.ResetPath();
        SetWorking(true);
        int mined = target.Work(Time.deltaTime, capacity - carried);
        if (target.Resource == _Game.Scripts.UI.ResourceType.Iron) cargo.Iron += mined;
        else cargo.Gold += mined;
    }

    private void SetWorking(bool working)
    {
        if (animator == null) return;
        foreach (var parameter in animator.parameters)
        {
            if (parameter.name == "isAttacking" && parameter.type == AnimatorControllerParameterType.Bool)
                animator.SetBool("isAttacking", working);
            if (working && parameter.name == "ActionType" && parameter.type == AnimatorControllerParameterType.Int)
                animator.SetInteger("ActionType", 1);
        }
    }
    private void OnDisable() { SetWorking(false); }
}
