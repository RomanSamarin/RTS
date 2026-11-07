using UnityEngine;
using UnityEngine.AI;
using _Game.Scripts.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(ResourcesMove), typeof(GettingResources), typeof(NavMeshAgent))]
public class FoodGatheringWorker : MonoBehaviour
{
    [Min(1f)] public float searchRadius = 200f;
    [Min(0.5f)] public float workDistance = 2f;
    public string CurrentTask { get; private set; } = "Ищет работу";
    [SerializeField, TextArea] private string searchDiagnostics;
    public string SearchDiagnostics => searchDiagnostics;
    private ResourcesMove worker;
    private GettingResources cargo;
    private NavMeshAgent agent;
    private GOScript command;
    private AttackController attack;
    private Animator animator;
    private float originalStoppingDistance;
    private bool ownsStoppingDistance;
    public bool IsHunting => worker != null && worker.IsFoodGatherer && isActiveAndEnabled
        && target is WildlifeAnimal animal && !animal.IsDead && !delivering
        && HorizontalDistance(transform.position, target.WorkPosition) <= TargetReach();
    private FoodResourceSource target;
    private bool delivering;
    private float nextSearch, nextRepath;
    private string routeFailure;
    private void Awake()
    {
        worker = GetComponent<ResourcesMove>(); cargo = GetComponent<GettingResources>();
        agent = GetComponent<NavMeshAgent>(); command = GetComponent<GOScript>(); attack = GetComponent<AttackController>();
        animator = GetComponentInChildren<Animator>();
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (BuildingRoadAccess.PauseWorker(worker)) { SetWorking(false); RestoreStoppingDistance(); CurrentTask="Нет дороги к зданию"; return; }
        if (!worker.IsFoodGatherer) { SetWorking(false); RestoreStoppingDistance(); return; }
        if (UnitEat.ControlsMovement(this)) { SetWorking(false); CurrentTask = "Перерыв на еду"; return; }
        if (command != null && command.isCommandToMove)
        { SetWorking(false); RestoreStoppingDistance(); CurrentTask = "Перемещается"; return; }
        if (!agent.enabled || !agent.isOnNavMesh) { CurrentTask = "Нет доступного пути"; return; }
        if (!ownsStoppingDistance) { originalStoppingDistance = agent.stoppingDistance; ownsStoppingDistance = true; }
        agent.stoppingDistance = Mathf.Min(originalStoppingDistance, 0.25f);
        SetWorking(false);
        if (attack != null) attack.targetToAttack = null;
        var home = worker.AssignedWorkplace;
        var storage = home != null ? home.GetComponent<GatheringHouse>() : null;
        if (storage == null || !storage.isActiveAndEnabled || storage.Profession != worker.role)
        { agent.ResetPath(); CurrentTask = "Нет места работы"; return; }
        if (worker.IsReturningToWorkplace)
        {
            CurrentTask = "Перемещается";
            Vector3 point = worker.GetApproachPoint(home.transform);
            if (worker.IsAtMiningHome(home) || (NavMesh.SamplePosition(point, out var hit, 2f, agent.areaMask)
                && HorizontalDistance(transform.position, hit.position) <= 1.2f))
            { agent.ResetPath(); worker.CompleteWorkplaceReturn(); }
            else { Move(point); return; }
        }
        int carried = 0;
        foreach (ResourceType type in WarehouseStorage.Types) carried += cargo.GetStoredAmount(type);
        int capacity = Mathf.Max(1, worker.MaxUnitResources);
        bool targetValid = target != null && target.isActiveAndEnabled && target.Remaining > 0 && target.Resource == storage.Resource;
        if (carried >= capacity || (!targetValid && cargo.GetStoredAmount(storage.Resource) > 0)) delivering = true;
        if (delivering)
        {
            CurrentTask = "Доставить";
            Vector3 homePoint = worker.GetApproachPoint(home.transform);
            if (worker.IsAtMiningHome(home) || (NavMesh.SamplePosition(homePoint, out var homeHit, 2f, agent.areaMask)
                && HorizontalDistance(transform.position, homeHit.position) <= 1.2f))
            { agent.ResetPath(); storage.Deposit(cargo); delivering = false; target = null; }
            else Move(worker.GetApproachPoint(home.transform));
            return;
        }
        if (!targetValid)
        {
            target = null;
            if (Time.time >= nextSearch)
            {
                nextSearch = Time.time + 1f;
                float best = Mathf.Max(1f, searchRadius) * Mathf.Max(1f, searchRadius);
                int matching = 0, stocked = 0, inRange = 0;
                // Поиск в сцене также восстанавливает выбор целей после перезагрузки скриптов.
                var candidates = FindObjectsOfType<FoodResourceSource>();
                foreach (var candidate in candidates)
                {
                    if (candidate == null || !candidate.isActiveAndEnabled || candidate.Resource != storage.Resource) continue;
                    matching++;
                    if (candidate.Remaining <= 0) continue;
                    stocked++;
                    float distance = HorizontalDistance(candidate.WorkPosition, transform.position);
                    distance *= distance;
                    if (distance > Mathf.Max(1f, searchRadius) * Mathf.Max(1f, searchRadius)) continue;
                    inRange++;
                    if (distance >= best || !SourceRoute(candidate, out _)) continue;
                    best = distance; target = candidate;
                }
                string details = target != null ? "Добыча найдена" : matching == 0
                    ? "Нет активных компонентов " + (worker.role == ResourcesMove.UnitRole.Hunter ? "WildlifeAnimal" : "BerryBush") + " в сцене. Имя или тег Animal не заменяют компонент."
                    : stocked == 0 ? "У всех источников Remaining = 0."
                    : inRange == 0 ? "Все источники за пределами Search Radius."
                    : "Не удалось построить подход к добыче. " + routeFailure;
                if (target == null && searchDiagnostics != details) Debug.LogWarning("[Сбор добычи] " + name + ": " + details, this);
                searchDiagnostics = details;
            }
        }
        if (target == null) { agent.ResetPath(); CurrentTask = "Нет доступной добычи"; return; }
        // Высота pivot модели и прежняя боевая stoppingDistance не мешают подходу.
        float reach = TargetReach();
        bool close = HorizontalDistance(transform.position, target.WorkPosition) <= reach;
        if (!close && SourceRoute(target, out var approach) && !agent.pathPending && approach.corners.Length > 0)
        {
            Vector3 end = approach.corners[approach.corners.Length - 1];
            close = HorizontalDistance(transform.position, end) <= 0.6f
                && HorizontalDistance(end, target.WorkPosition) <= reach + 0.5f;
        }
        if (!close)
        {
            CurrentTask = "Перемещается";
            if (Time.time >= nextRepath && !agent.pathPending)
            {
                nextRepath = Time.time + 0.5f;
                if (!SourceRoute(target, out var path)) { target = null; return; }
                agent.isStopped = false;
                if (!agent.SetPath(path))
                { searchDiagnostics = "NavMeshAgent отклонил рассчитанный путь: " + path.status; target = null; }
            }
            return;
        }
        agent.ResetPath();
        SetWorking(true);
        CurrentTask = target is WildlifeAnimal animal && !animal.IsDead ? "Охотится" : "Собирает";
        cargo.AddStoredAmount(storage.Resource, target.Work(Time.deltaTime, capacity - carried));
    }
    private static float HorizontalDistance(Vector3 a, Vector3 b)
    { a.y = b.y = 0; return Vector3.Distance(a, b); }
    private float TargetReach()
    {
        return SourceReach(target);
    }
    private float SourceReach(FoodResourceSource source)
    {
        var other = source != null ? source.GetComponent<NavMeshAgent>() : null;
        return Mathf.Max(workDistance, agent.radius + (other != null ? other.radius : 0f) + 0.5f);
    }
    private bool SourceRoute(FoodResourceSource source, out NavMeshPath path)
    {
        // Достаточно подойти сбоку: центр животного/куста может быть вырезан препятствием.
        Vector3 center = source.WorkPosition;
        var animalAgent = source.GetComponent<NavMeshAgent>();
        if (source.workPoint == null && animalAgent != null && animalAgent.enabled && animalAgent.isOnNavMesh)
            center = animalAgent.nextPosition;
        if (HorizontalDistance(transform.position, center) <= SourceReach(source))
        { path = new NavMeshPath(); return true; }
        // Статический запрос от позиции симуляции агента не зависит от его
        // текущего pathPending/маршрута и смещения pivot модели.
        var filter = NavigationFilter();
        Vector3 start = agent.nextPosition;
        if (!NavMesh.SamplePosition(start, out var startHit, 1f, filter))
        {
            routeFailure = $"Начало пути не найдено: Agent Type={agent.agentTypeID}, Area Mask={agent.areaMask}, позиция={start}.";
            path = new NavMeshPath(); return false;
        }
        start = startHit.position;
        if (SourcePointRoute(start, center, center, SourceReach(source), filter, out path)) return true;
        float radius = Mathf.Max(0.5f, SourceReach(source) - 0.25f);
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8f;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
            if (SourcePointRoute(start, point, center, SourceReach(source), filter, out path)) return true;
        }
        path = new NavMeshPath(); return false;
    }
    private NavMeshQueryFilter NavigationFilter()
    {
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        for (int i = 0; i < 32; i++) filter.SetAreaCost(i, agent.GetAreaCost(i));
        return filter;
    }
    private bool SourcePointRoute(Vector3 start, Vector3 point, Vector3 source, float reach,
        NavMeshQueryFilter filter, out NavMeshPath path)
    {
        path = new NavMeshPath();
        // Ищем поверхность под высоким pivot и у края коллайдера, а не только в центре модели.
        if (!NavMesh.SamplePosition(point, out var hit, Mathf.Max(3f, reach), filter))
        {
            routeFailure = $"Нет поверхности возле добычи: Agent Type={agent.agentTypeID}, Area Mask={agent.areaMask}, цель={source}.";
            return false;
        }
        bool calculated = NavMesh.CalculatePath(start, hit.position, filter, path);
        var corners = path.corners;
        float distance = corners.Length > 0 ? HorizontalDistance(corners[corners.Length - 1], source) : float.PositiveInfinity;
        // Неполный маршрут тоже подходит, если его конец уже находится в зоне охоты.
        // Далёкие островки NavMesh и непроходимые разрывы по-прежнему отклоняются.
        if (calculated && path.status != NavMeshPathStatus.PathInvalid && corners.Length > 0
            && distance <= reach && Mathf.Abs(corners[corners.Length - 1].y - source.y) <= 3f) return true;
        routeFailure = $"Статус={path.status}, углов={corners.Length}, от конца пути до добычи={distance:F1} м, дистанция охоты={reach:F1} м, Agent Type={agent.agentTypeID}, Area Mask={agent.areaMask}.";
        return false;
    }
    private void SetWorking(bool value)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var parameter in animator.parameters)
        {
            if (parameter.name == "isAttacking" && parameter.type == AnimatorControllerParameterType.Bool)
                animator.SetBool(parameter.name, value);
            if (value && parameter.name == "ActionType" && parameter.type == AnimatorControllerParameterType.Int)
                animator.SetInteger(parameter.name, 1);
        }
    }
    private void RestoreStoppingDistance()
    { if (ownsStoppingDistance && agent != null) agent.stoppingDistance = originalStoppingDistance; ownsStoppingDistance = false; }
    private void OnDisable() { SetWorking(false); RestoreStoppingDistance(); }
    private bool Route(Vector3 point, out NavMeshPath path)
    {
        path = new NavMeshPath();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        return NavMesh.SamplePosition(point, out var hit, 2f, filter)
            && agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete;
    }
    private bool Move(Vector3 point)
    {
        if (Time.time < nextRepath || agent.pathPending) return true;
        nextRepath = Time.time + 0.5f;
        if (!Route(point, out var path)) { agent.ResetPath(); CurrentTask = "Нет доступного пути"; return false; }
        agent.isStopped = false; agent.SetPath(path); return true;
    }
}
