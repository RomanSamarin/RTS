using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(ResourcesMove), typeof(NavMeshAgent))]
public class ProductionWorker : MonoBehaviour
{
    public string CurrentTask { get; private set; } = "Ожидает работу";
    private bool working;
    public bool IsWorking => working && isActiveAndEnabled && worker != null && worker.IsProductionWorker
        && !UnitEat.ControlsMovement(this);
    public bool isWorking => IsWorking && worker.CanPerformWork;
    private ResourcesMove worker;
    private NavMeshAgent agent;
    private GOScript command;
    private AttackController attack;
    private float nextRepath;
    private float savedStoppingDistance;
    private bool ownsStoppingDistance;
    private void Awake()
    {
        worker = GetComponent<ResourcesMove>(); agent = GetComponent<NavMeshAgent>();
        command = GetComponent<GOScript>(); attack = GetComponent<AttackController>();
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        working = false;
        if (BattleCombatant.BlocksActions(this)) { RestoreStoppingDistance(); return; }
        if (BuildingRoadAccess.PauseWorker(worker)) { RestoreStoppingDistance(); CurrentTask="Нет дороги к зданию"; return; }
        if (!worker.IsProductionWorker) { RestoreStoppingDistance(); return; }
        if (UnitEat.ControlsMovement(this)) { CurrentTask = "Перерыв на еду"; return; }
        if (command != null && command.HasPlayerOrder)
        { RestoreStoppingDistance(); CurrentTask = "Выполняет приказ"; return; }
        var home = worker.AssignedWorkplace;
        var building = home != null ? home.GetComponent<RecipeBuilding>() : null;
        if (building == null || !building.isActiveAndEnabled || building.Profession != worker.role)
        { CurrentTask = "Нет места работы"; return; }
        if (!agent.enabled || !agent.isOnNavMesh) { CurrentTask = "Нет доступного пути"; return; }
        if (!ownsStoppingDistance) { savedStoppingDistance = agent.stoppingDistance; ownsStoppingDistance = true; }
        agent.stoppingDistance = Mathf.Min(savedStoppingDistance, 0.25f);
        if (attack != null) attack.targetToAttack = null;
        Vector3 point = worker.GetApproachPoint(home.transform);
        if (!NavMesh.SamplePosition(point, out var hit, 2f, agent.areaMask))
        { CurrentTask = "Нет доступного пути"; return; }
        if (!worker.IsAtMiningHome(home) && Vector3.Distance(transform.position, hit.position) > 1.2f)
        {
            CurrentTask = "Перемещается";
            if (Time.time < nextRepath || agent.pathPending) return;
            nextRepath = Time.time + 0.75f;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
            { CurrentTask = "Нет доступного пути"; return; }
            agent.isStopped = false; agent.SetPath(path); return;
        }
        agent.ResetPath();
        if (worker.IsReturningToWorkplace) worker.CompleteWorkplaceReturn();
        if (!building.CanProduce)
        {
            CurrentTask = building.WaitingTask;
            return;
        }
        CurrentTask = worker.role == ResourcesMove.UnitRole.Miller ? "Перемалывает"
            : worker.role == ResourcesMove.UnitRole.Baker ? "Выпекает"
            : worker.role == ResourcesMove.UnitRole.Smoker ? "Готовит"
            : worker.role == ResourcesMove.UnitRole.Shipwright ? "Строит корабль"
            : worker.role == ResourcesMove.UnitRole.PortWorker ? (building is Port port&&port.IsRepairing?"Ремонтирует корабль":"Перемещает груз")
            : worker.role == ResourcesMove.UnitRole.Blacksmith ? "Куёт" : "Изготавливает";
        working = true;
        building.Work(Time.deltaTime);
    }
    private void RestoreStoppingDistance()
    {
        // Новый приказ уже мог выставить свою дистанцию: не перезаписываем её.
        if (ownsStoppingDistance && agent != null && Mathf.Approximately(agent.stoppingDistance, Mathf.Min(savedStoppingDistance, 0.25f)))
            agent.stoppingDistance = savedStoppingDistance;
        ownsStoppingDistance = false;
    }
    private void OnDisable() { working = false; RestoreStoppingDistance(); }
}
