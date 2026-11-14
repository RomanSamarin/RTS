using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Профессия «Строитель».
///
/// Вешается на юнита-рабочего (можно на копию префаба фермера).
/// Строитель сам ищет ближайшую стройплощадку (ConstructionSite),
/// подходит к ней, проигрывает анимацию работы и ускоряет стройку.
/// Когда площадка достроена — ищет следующую.
///
/// Что делает компонент:
///   • переключает ResourcesMove в роль Builder (чтобы рабочий не бегал за ресурсами);
///   • отключает авто-захват целей у AttackController (строитель не отвлекается на деревья);
///   • останавливает работу, если игрок отдал приказ идти или атаковать.
///
/// Ничего вызывать вручную не нужно. Достаточно добавить компонент
/// на префаб рабочего и назначить этот префаб в казарму (Object To Spawn).
/// </summary>
[DisallowMultipleComponent]
public class Builder : MonoBehaviour
{
    [Header("Работа")]
    [Tooltip("С какой дистанции строитель начинает работать")]
    public float workDistance = 1.6f;

    [Tooltip("Скорость работы. 1 — как один обычный строитель. " +
             "Прогресс площадки суммируется по всем строителям")]
    public float workSpeed = 1f;

    [Tooltip("Максимальная дистанция поиска стройки")]
    public float searchRadius = 250f;

    [Tooltip("Как часто искать новую стройку, если текущей нет, сек")]
    public float retargetInterval = 0.8f;

    [Tooltip("Как часто обновлять путь к площадке, сек")]
    public float repathInterval = 0.5f;

    [Tooltip("Автоматически искать стройку, когда строитель свободен")]
    public bool autoFindWork = true;

    [Header("Ремонт")]
    public bool autoRepair = true;
    [Min(0.1f), Tooltip("Восстановление здоровья в секунду. Материалы пока не расходуются.")]
    public float repairSpeed = 10f;

    [Header("Анимация")]
    [Tooltip("Работа использует Bool isWorking, боевой isAttacking не включается.")]
    public string workParameter = "isWorking";
    [field: SerializeField] public bool isWorking { get; private set; }

    [Tooltip("Скорость поворота к стройке")]
    public float turnSpeed = 8f;

    [Header("Отладка")]
    [Tooltip("Писать в Console, какую стройку нашёл строитель и почему не может идти")]
    public bool logState = true;

    private NavMeshAgent agent;
    private GOScript goScript;
    private AttackController attackController;
    private ResourcesMove resourcesMove;
    private Unit unit;
    private Animator animator;

    private ConstructionSite currentSite;
    private ConstructionSite orderedSite;
    private bool hasBuildOrder;
    public ConstructionSite OrderedSite=>hasBuildOrder?orderedSite:null;
    private BuildingHealth repairTarget;
    private Vector3 repairPoint;
    private int repairMoveToken;
    public BuildingHealth RepairTarget => repairTarget;
    private bool working;

    private float nextSearchTime;
    private float nextRepathTime;
    private float nextProblemLogTime;

    // Все живые строители на сцене — нужны только для подсказки игроку,
    // если он поставил здание, а строить некому.
    private static readonly List<Builder> allBuilders = new List<Builder>();

    /// <summary>Сколько строителей сейчас существует в сцене.</summary>
    public static int ActiveCount
    {
        get
        {
            for (int i = allBuilders.Count - 1; i >= 0; i--)
            {
                if (allBuilders[i] == null)
                    allBuilders.RemoveAt(i);
            }

            return allBuilders.Count;
        }
    }
    // ------------------------------------------------------------------ Свойства

    /// <summary>Скорость этого строителя (используется площадкой).</summary>
    public float WorkSpeed => Mathf.Max(0.05f, workSpeed);
    public bool ControlsWorkAnimation=>isActiveAndEnabled&&isWorking&&(working||repairTarget!=null)
        &&resourcesMove!=null&&resourcesMove.role==ResourcesMove.UnitRole.Builder
        &&!UnitEat.ControlsMovement(this)&&!BattleCombatant.BlocksActions(this)
        &&(goScript==null||!goScript.HasPlayerOrder);
    public static bool ControlsAnimation(Component owner)
    {var builder=owner!=null?owner.GetComponentInParent<Builder>():null;return builder!=null&&builder.ControlsWorkAnimation;}

    /// <summary>Строитель сейчас работает именно на этой площадке.</summary>
    public bool IsWorkingOn(ConstructionSite site)
    {
        return working && currentSite == site && site != null;
    }

    // ------------------------------------------------------------------ Unity

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        goScript = GetComponent<GOScript>();
        if(goScript!=null)goScript.ManualOrderIssued+=CancelBuildOrder;
        attackController = GetComponent<AttackController>();
        resourcesMove = GetComponent<ResourcesMove>();
        unit = GetComponent<Unit>();

        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Рабочий с этим компонентом — строитель, а не добытчик.
        if (resourcesMove != null)
            resourcesMove.role = ResourcesMove.UnitRole.Builder;

        // Строитель не должен сам цепляться к деревьям/врагам, иначе
        // будет уходить со стройки. Приказ игрока всё равно работает:
        // ManagerGO выставляет targetToAttack напрямую.
        if (attackController != null)
        {
            attackController.autoAcquireTargets = false;
            attackController.targetToAttack = null;
        }

        workParameter = "isWorking";


        if (!allBuilders.Contains(this))
            allBuilders.Add(this);
    }

    private void OnDisable()
    {
        CancelBuildOrder();
    }

    private void Start()
    {
        // Строитель может быть добавлен позже Unit.Start (через роль в ResourcesMove),
        // поэтому на всякий случай гарантируем, что он попадёт в список выделения.
        if (ManagerGO.Instance != null && ManagerGO.Instance.allUnitsList != null &&
            !ManagerGO.Instance.allUnitsList.Contains(gameObject))
        {
            ManagerGO.Instance.allUnitsList.Add(gameObject);
        }
    }

    private void OnDestroy()
    {
        if(goScript!=null)goScript.ManualOrderIssued-=CancelBuildOrder;
        ReleaseSite();
        allBuilders.Remove(this);
    }

    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if(hasBuildOrder&&(orderedSite==null||!orderedSite.IsAvailable||!FactionMember.AreAllies(gameObject,orderedSite.gameObject)))CancelBuildOrder();
        if (BuildingRoadAccess.PauseWorker(resourcesMove)) { ReleaseSite(); MaterialTask="Нет дороги к зданию"; return; }
        if (UnitEat.ControlsMovement(this)) { ReleaseSite(); return; }
        // Мёртвый юнит ничего не строит.
        // Проверяем максимум здоровья: у рабочих он может быть не настроен (0),
        // и тогда unitHealth тоже 0 — но юнит при этом живой и должен строить.
        if (unit != null && unit.unitMaxHealth > 0f && unit.unitHealth <= 0f)
        {
            ReleaseSite();
            return;
        }

        // Игрок приказал идти — стройка прерывается.
        if (goScript != null && goScript.HasPlayerOrder)
        {
            ReleaseSite();
            return;
        }

        // Игрок приказал атаковать — стройка прерывается.
        if (attackController != null && attackController.targetToAttack != null)
        {
            ReleaseSite();
            return;
        }

        if (resourcesMove != null && resourcesMove.IsReturningToWorkplace)
        {
            ReleaseSite();
            return;
        }

        if (currentSite != null && !currentSite.IsAvailable)
            ReleaseSite();

        // Перерыв на еду/недоступный подъезд освобождает рабочее место, но сохраняет выбранную стройку.
        if(hasBuildOrder&&currentSite==null)
        {currentSite=orderedSite;nextRepathTime=0;}

        if (repairTarget != null)
        {
            if (autoFindWork && autoRepair && CanRepair(repairTarget))
            {
                UpdateRepair();
                return;
            }
            ReleaseSite();
        }

        // Свободен — ищем ближайшую стройку.
        if (currentSite == null)
        {
            StopWorking();

            if (!autoFindWork)
            {
                if (resourcesMove != null) resourcesMove.ReturnToWorkplace();
                return;
            }

            if (Time.time < nextSearchTime)
                return;

            nextSearchTime = Time.time + Mathf.Max(0.1f, retargetInterval);

            currentSite = ConstructionSite.FindNearest(transform.position, searchRadius,FactionMember.Of(gameObject));
            if (currentSite == null)
            {
                var leftovers = GetComponent<GettingResources>();
                if (leftovers != null && leftovers.HasCargo) { ReturnMaterials(leftovers); return; }
                if (autoRepair && FindRepairTarget(searchRadius))
                {
                    nextRepathTime = 0f;
                    LogState($"нашёл повреждённое здание {repairTarget.name}");
                    UpdateRepair();
                    return;
                }
                LogNoSite();
                if (resourcesMove != null) resourcesMove.ReturnToWorkplace();
                return;
            }

            nextRepathTime = 0f;
            LogState($"нашёл стройку {currentSite.name} — иду строить");
        }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            StopWorking();
            LogProblem(agent == null
                ? "не может идти: на юните нет NavMeshAgent"
                : "не может идти: NavMeshAgent не стоит на NavMesh");
            return;
        }

        var materials = currentSite.Materials;
        var backpack = GetComponent<GettingResources>();
        if (backpack == null) backpack = gameObject.AddComponent<GettingResources>();
        if (materials != null && (!materials.Ready || backpack.HasCargo))
        {
            StopWorking();
            int capacity = resourcesMove != null ? resourcesMove.MaxUnitResources : 10;
            if (materials.Supply(this, currentSite, agent, backpack, capacity, workDistance)) return;
        }
        MaterialTask = "Строит";
        Vector3 workPoint = currentSite.GetWorkPoint(transform.position);

        float distance = Vector3.Distance(transform.position, workPoint);
        if (distance > workDistance + (working ? .25f : 0f))
        {
            StopWorking();

            if (Time.time >= nextRepathTime)
            {
                nextRepathTime = Time.time + Mathf.Max(0.1f, repathInterval);
                MoveTowards(workPoint);
            }

            return;
        }

        // Дошёл — встаём и работаем.
        StopAgent();
        FaceTarget(currentSite.transform.position);

        if (!working)
        {
            if (!currentSite.RegisterBuilder(this))
            {
                if(hasBuildOrder)
                {
                    MaterialTask="Ждёт свободное место";
                    return;
                }
                // На площадке уже максимум строителей — ищем другую.
                ReleaseSite();
                nextSearchTime = Time.time + Mathf.Max(0.1f, retargetInterval);
                return;
            }

            working = true;
            StartWorkAnimation();

            LogState($"начал строить {currentSite.name} " +
                     $"({currentSite.BuilderCount}/{currentSite.MaxBuilders} строителей)");
        }

        EnsureWorkAnimation();
    }

    // ------------------------------------------------------------------ Стройка

    public bool AssignConstruction(ConstructionSite site)
    {
        if(!isActiveAndEnabled||resourcesMove==null||resourcesMove.role!=ResourcesMove.UnitRole.Builder
            ||site==null||!site.IsAvailable||!FactionMember.IsPlayer(gameObject)||!FactionMember.AreAllies(gameObject,site.gameObject)
            ||(unit!=null&&unit.unitMaxHealth>0&&unit.unitHealth<=0))return false;
        if(agent==null||!agent.enabled||!agent.isOnNavMesh)
        {LogProblem("не может принять приказ: нет активного NavMeshAgent на поверхности");return false;}
        var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
        var path=new NavMeshPath();
        if(!NavMesh.SamplePosition(site.GetWorkPoint(transform.position),out var hit,2,filter)
            ||!site.IsNearWorkArea(hit.position)
            ||!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)
        {LogProblem("нет полного пути к выбранной стройке");return false;}
        // Только после проверки заменяем предыдущую работу. Груз остаётся в рюкзаке.
        CancelBuildOrder();
        if(goScript!=null){goScript.StopMoving();goScript.NotifyMilitaryOrder();}
        else GetComponent<UnitEat>()?.CancelForCommand(false);
        if(attackController!=null)attackController.targetToAttack=null;
        FormationHold.Clear(gameObject);
        resourcesMove.CompleteWorkplaceReturn();
        orderedSite=site;hasBuildOrder=true;currentSite=site;
        nextRepathTime=0;nextSearchTime=0;MaterialTask="Идёт строить";
        LogState($"получил приказ строить {site.name}");return true;
    }
    public void CancelBuildOrder()
    {
        hasBuildOrder=false;orderedSite=null;ReleaseSite();
    }

    /// <summary>Вызывается площадкой, когда стройка завершена.</summary>
    public void OnSiteCompleted(ConstructionSite site)
    {
        if(orderedSite==site){hasBuildOrder=false;orderedSite=null;}
        if (currentSite != site)
            return;

        working = false;
        currentSite = null;
        StopWorkAnimation();

        nextSearchTime = Time.time + Mathf.Max(0.1f, retargetInterval);
    }

    /// <summary>Вызывается площадкой, если её снесли до окончания стройки.</summary>
    public void OnSiteRemoved(ConstructionSite site)
    {
        if(orderedSite==site){hasBuildOrder=false;orderedSite=null;}
        if (currentSite != site)
            return;

        working = false;
        currentSite = null;
        StopWorkAnimation();

        nextSearchTime = Time.time + Mathf.Max(0.1f, retargetInterval);
    }

    private void ReleaseSite()
    {
        MaterialTask = "Ищет стройку";
        if (currentSite != null && currentSite.Materials != null) currentSite.Materials.ReleaseBuilder(this);
        StopWorking();
        currentSite = null;
        repairTarget = null;
        if (goScript != null && repairMoveToken != 0) goScript.EndSupplyMove(repairMoveToken);
        repairMoveToken = 0;
    }

    // Один строитель резервирует здание до завершения или прерывания ремонта.
    private bool CanRepair(BuildingHealth building)
    {
        if (building == null || !building.isActiveAndEnabled || building.IsDead ||
            building.CurrentHealth >= building.MaxHealth ||
            !FactionMember.AreAllies(gameObject,building.gameObject) ||
            building.GetComponentInParent<ConstructionSite>() != null)
            return false;

        foreach (var builder in allBuilders)
            if (builder != null && builder != this && builder.isActiveAndEnabled &&
                builder.repairTarget == building) return false;
        return true;
    }

    private bool FindRepairTarget(float radius)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        float best = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
        foreach (var building in BuildingManager.AllBuildings)
        {
            if (!CanRepair(building)) continue;
            float distance = (building.transform.position - transform.position).sqrMagnitude;
            if (distance > best || !TryRepairPoint(building, out Vector3 point)) continue;
            best = distance;
            repairTarget = building;
            repairPoint = point;
        }
        if (repairTarget == null) return false;
        if (goScript != null && !goScript.TryBeginSupplyMove(out repairMoveToken))
        {
            repairTarget = null;
            return false;
        }
        return true;
    }

    private bool TryRepairPoint(BuildingHealth building, out Vector3 point)
    {
        point = default;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        var path = new NavMeshPath();
        Collider body = building.GetComponent<Collider>();
        if (body == null) body = building.GetComponentInChildren<Collider>();
        Bounds bounds = body != null ? body.bounds : new Bounds(building.transform.position, Vector3.zero);
        float best = float.PositiveInfinity;
        // Пробуем ближайшую стену, дверь и восемь сторон: центр здания обычно вырезан из NavMesh.
        for (int i = 0; i < 10; i++)
        {
            Vector3 candidate;
            if (i == 0) candidate = bounds.ClosestPoint(transform.position);
            else if (i == 1)
            {
                if (!BuildingEntrance.TryGetWorldPoint(building.gameObject, out candidate)) continue;
            }
            else
            {
                float angle = (i - 2) * Mathf.PI / 4f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                candidate = bounds.ClosestPoint(bounds.center + direction * (bounds.extents.magnitude + 2f));
                candidate += direction * (agent.radius + 0.15f);
            }
            candidate.y = transform.position.y;
            if (!NavMesh.SamplePosition(candidate, out var hit, Mathf.Max(1f, workDistance), filter)) continue;
            if (Vector3.Distance(hit.position, bounds.ClosestPoint(hit.position)) > Mathf.Max(0.2f, workDistance)) continue;
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float distance = (hit.position - transform.position).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            point = hit.position;
        }
        return best < float.PositiveInfinity;
    }

    private void UpdateRepair()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) { ReleaseSite(); return; }
        if (goScript != null && !goScript.OwnsSupplyOrder(repairMoveToken)) { ReleaseSite(); return; }
        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + Mathf.Max(0.1f, repathInterval);
            if (!TryRepairPoint(repairTarget, out repairPoint))
            {
                ReleaseSite();
                nextSearchTime = Time.time + Mathf.Max(0.1f, retargetInterval);
                return;
            }
        }
        // Проверяем фактическое расстояние: здоровье не восстанавливается издалека.
        Collider body = repairTarget.GetComponent<Collider>();
        if (body == null) body = repairTarget.GetComponentInChildren<Collider>();
        Vector3 surface = body != null ? body.ClosestPoint(transform.position) : repairTarget.transform.position;
        bool atBuilding = Vector3.Distance(transform.position, surface) <= Mathf.Max(0.2f, workDistance);
        if (!atBuilding || Vector3.Distance(transform.position, repairPoint) > Mathf.Max(0.25f, workDistance))
        {
            StopWorkAnimation();
            MaterialTask = "Идёт ремонтировать";
            if (goScript != null)
            {
                if (!goScript.MoveForSupply(repairMoveToken, repairPoint, 0.1f)) ReleaseSite();
            }
            else MoveTowards(repairPoint);
            return;
        }
        MaterialTask = "Ремонтирует";
        StopAgent();
        FaceTarget(repairTarget.transform.position);
        StartWorkAnimation();
        EnsureWorkAnimation();
        repairTarget.Heal(Mathf.Max(0f, repairSpeed) * Time.deltaTime);
        if (repairTarget == null || repairTarget.CurrentHealth >= repairTarget.MaxHealth)
        {
            ReleaseSite();
            nextSearchTime = 0f;
        }
    }

    public string MaterialTask { get; set; } = "Ищет стройку";
    public void ReturnMaterials(GettingResources cargo)
    {
        MaterialTask = "Возвращает материалы";
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        _Game.Scripts.UI.WarehouseStorage closest = null; float best = float.PositiveInfinity;
        foreach (var store in _Game.Scripts.UI.WarehouseStorage.All)
        {
            if (store.FreeCapacity <= 0||!FactionMember.CanUse(gameObject,store.gameObject)) continue;
            float distance = (store.transform.position - transform.position).sqrMagnitude;
            Vector3 point = resourcesMove != null ? resourcesMove.GetApproachPoint(store.transform) : store.transform.position;
            var path = new NavMeshPath();
            if (distance >= best || !agent.CalculatePath(point, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            closest = store; best = distance;
        }
        if (closest == null) { StopAgent(); return; }
        if (resourcesMove != null && resourcesMove.IsAtMiningHome(closest.gameObject))
        { StopAgent(); closest.Deposit(cargo); }
        else MoveTowards(resourcesMove != null ? resourcesMove.GetApproachPoint(closest.transform) : closest.transform.position);
    }

    private void StopWorking()
    {
        if (working)
        {
            working = false;

            if (currentSite != null)
                currentSite.UnregisterBuilder(this);
        }

        StopWorkAnimation();
    }

    // ------------------------------------------------------------------ Движение

    private void MoveTowards(Vector3 point)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.isStopped = false;
        agent.SetDestination(point);
    }

    private void StopAgent()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (agent.hasPath)
            agent.ResetPath();
    }

    // ------------------------------------------------------------------ Логи

    private void LogState(string message)
    {
        if (logState)
            Debug.Log($"[Builder] {name}: {message}", this);
    }

    private void LogProblem(string message)
    {
        if (!logState || Time.time < nextProblemLogTime)
            return;

        nextProblemLogTime = Time.time + 5f;
        Debug.LogWarning($"[Builder] {name}: {message}", this);
    }

    private void LogNoSite()
    {
        if (!logState || Time.time < nextProblemLogTime)
            return;

        int siteCount = ConstructionSite.AllSites.Count;
        if (siteCount == 0)
            return; // строек нет — это норма, не засоряем Console

        nextProblemLogTime = Time.time + 5f;
        Debug.LogWarning($"[Builder] {name}: на сцене есть стройки ({siteCount}), но ни одна не подходит — " +
                         "все заняты максимумом строителей или находятся дальше searchRadius.", this);
    }

    private void FaceTarget(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * Mathf.Max(0.1f, turnSpeed));
    }

    // Work animation is driven by its own bool, never by combat states.
    private void StartWorkAnimation()
    { isWorking = true; WorkerAnimation.Apply(animator, true); }
    private void StopWorkAnimation()
    { isWorking = false; WorkerAnimation.Apply(animator, false); }
    private void EnsureWorkAnimation()
    { WorkerAnimation.Apply(animator, isWorking); }
}
