using UnityEngine;
using UnityEngine.AI;
using _Game.Scripts.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(ResourcesMove), typeof(NavMeshAgent), typeof(GettingResources))]
public class Farmer : MonoBehaviour
{
    [Min(0.5f)] public float workDistance = 2f;
    [Min(1f)] public float searchRadius = 200f;
    private ResourcesMove worker;
    private NavMeshAgent agent;
    private GettingResources cargo;
    private GOScript command;
    private AttackController attack;
    private Animator animator;
    private FarmField field;
    private bool delivering;
    public string CurrentTask => delivering ? "Доставить" : field != null ? "Собирать урожай" : "Искать работу";
    private float nextSearch;

    private void Awake()
    {
        worker = GetComponent<ResourcesMove>();
        agent = GetComponent<NavMeshAgent>();
        cargo = GetComponent<GettingResources>();
        command = GetComponent<GOScript>();
        attack = GetComponent<AttackController>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (BuildingRoadAccess.PauseWorker(worker)) { SetWorking(false); return; }
        if (UnitEat.ControlsMovement(this)) { SetWorking(false); return; }
        if (worker.role != ResourcesMove.UnitRole.Farmer) return;
        if (worker.IsReturningToWorkplace || (command != null && command.isCommandToMove))
        { SetWorking(false); return; }
        if (!agent.enabled || !agent.isOnNavMesh) { SetWorking(false); return; }
        if (attack != null) attack.targetToAttack = null;
        GameObject home = worker.AssignedWorkplace;
        FarmHouse storage = home != null ? home.GetComponent<FarmHouse>() : null;
        if (storage == null || !storage.isActiveAndEnabled)
        { agent.ResetPath(); SetWorking(false); return; }

        int carried = 0;
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            carried += cargo.GetStoredAmount(type);
        int capacity = Mathf.Max(1, worker.MaxUnitResources);
        if (carried >= capacity || (cargo.Wheat > 0 && (field == null || !field.isActiveAndEnabled || !field.IsRipe)))
            delivering = true;
        if (delivering)
        {
            SetWorking(false);
            if (worker.IsAtMiningHome(home))
            {
                agent.ResetPath();
                storage.Deposit(cargo);
                delivering = false;
                field = null;
            }
            else MoveTo(worker.GetApproachPoint(home.transform));
            return;
        }
        if (field == null || !field.isActiveAndEnabled || !field.IsRipe)
        {
            field = null;
            if (Time.time >= nextSearch)
            {
                nextSearch = Time.time + 1f;
                float best = searchRadius * searchRadius;
                foreach (FarmField candidate in FarmField.All)
                {
                    if (candidate == null || !candidate.IsRipe) continue;
                    float distance = (candidate.WorkPosition - transform.position).sqrMagnitude;
                    if (distance >= best) continue;
                    var path = new NavMeshPath();
                    if (!agent.CalculatePath(candidate.WorkPosition, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    best = distance;
                    field = candidate;
                }
            }
        }
        if (field == null) { agent.ResetPath(); SetWorking(false); return; }
        if (Vector3.Distance(transform.position, field.WorkPosition) > workDistance)
        { SetWorking(false); MoveTo(field.WorkPosition); return; }
        agent.ResetPath();
        SetWorking(true);
        cargo.Wheat += field.Harvest(Time.deltaTime, capacity - carried);
    }

    private void MoveTo(Vector3 point) { agent.isStopped = false; agent.SetDestination(point); }
    private void SetWorking(bool working)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var parameter in animator.parameters)
        {
            if (parameter.name == "isAttacking" && parameter.type == AnimatorControllerParameterType.Bool)
                animator.SetBool(parameter.name, working);
            if (working && parameter.name == "ActionType" && parameter.type == AnimatorControllerParameterType.Int)
                animator.SetInteger(parameter.name, 1);
        }
    }
    private void OnDisable() { SetWorking(false); }
}
