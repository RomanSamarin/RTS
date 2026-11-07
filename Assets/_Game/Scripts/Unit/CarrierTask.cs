using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using _Game.Scripts.UI;

/// <summary>Один источник принадлежит одному заказу до забора; место на складе резервируется заранее.</summary>
[RequireComponent(typeof(ResourcesMove), typeof(GettingResources), typeof(NavMeshAgent))]
public class CarrierTask : MonoBehaviour
{
    public enum TaskState { Waiting, Collect, Deliver, Paused }
    private static readonly Dictionary<GameObject, CarrierTask> sources = new Dictionary<GameObject, CarrierTask>();
    public static void RebuildSavedReservations()
    {
        sources.Clear();
        foreach(var task in FindObjectsOfType<CarrierTask>(true))
            if(task.State==TaskState.Collect&&task.source!=null&&task.ingredientSource==null&&task.foodSource==null)sources[task.source]=task;
    }
    public TaskState State { get; private set; }
    public string Status { get; private set; } = "Ожидает работу";
    public GameObject Source => source;
    public WarehouseStorage Destination => destination;
    public GameObject DestinationObject => production != null ? production.gameObject : tavern != null ? tavern.gameObject : destination != null ? destination.gameObject : null;
    public int ReservedCargo { get; private set; }
    private GameObject source;
    private WarehouseStorage destination;
    private Tavern tavern;
    private WarehouseStorage foodSource;
    private RecipeBuilding production;
    private WarehouseStorage ingredientSource;
    private ResourceType productionInput;
    private GettingResources cargo;
    private ResourcesMove worker;
    private NavMeshAgent agent;
    private GOScript command;
    private AttackController attack;
    private float nextSearch;
    private GameObject routeTarget;
    private float nextRepath;
    private Vector3 lastProgressPosition;
    private float lastProgressTime;
    private bool carryingLoot;

    public int CargoCount
    {
        get { int sum = 0; foreach (ResourceType type in WarehouseStorage.Types) sum += cargo.GetStoredAmount(type); return sum; }
    }
    private void Awake()
    {
        cargo = GetComponent<GettingResources>(); worker = GetComponent<ResourcesMove>();
        agent = GetComponent<NavMeshAgent>(); command = GetComponent<GOScript>(); attack = GetComponent<AttackController>();
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (BuildingRoadAccess.PauseWorker(worker)) { Cancel(); Status="Нет дороги к зданию"; return; }
        if (UnitEat.ControlsMovement(this)) { Cancel(); State = TaskState.Paused; Status = "Перерыв на еду"; return; }
        if (worker.role != ResourcesMove.UnitRole.Carrier) { Cancel(); return; }
        if ((command != null && command.isCommandToMove) || worker.IsReturningToWorkplace)
        { Cancel(); State = TaskState.Paused; Status = "Выполняет приказ / идёт к месту работы"; return; }
        if (!agent.enabled || !agent.isOnNavMesh)
        { Cancel(); Status = "Нет доступного пути"; return; }
        if (attack != null) attack.targetToAttack = null;
        if (!ReferenceEquals(destination, null) && (destination == null || !destination.isActiveAndEnabled)) Cancel();
        if (!ReferenceEquals(tavern, null) && (tavern == null || !tavern.isActiveAndEnabled)) Cancel();
        if (!ReferenceEquals(production, null) && (production == null || !production.isActiveAndEnabled)) Cancel();
        if ((source!=null&&!BuildingRoadAccess.Allows(source)) || (DestinationObject!=null&&!BuildingRoadAccess.Allows(DestinationObject))) Cancel();
        if (State == TaskState.Collect && CargoCount > 0) Cancel();
        if (State == TaskState.Collect && (source == null || !source.activeInHierarchy)) Cancel();
        if (State == TaskState.Collect && source != null && source.TryGetComponent<LootCache>(out var cache) && !cache.IsSafeFor(gameObject)) Cancel();
        if (destination == null && tavern == null && production == null)
        {
            if (Time.time < nextSearch) return;
            nextSearch = Time.time + 1f;
            if (!Plan()) { State = TaskState.Waiting; Status = CargoCount > 0 ? "Ждёт места на складе" : "Нет доступных грузов или места на складе"; return; }
        }
        GameObject target = State == TaskState.Collect ? source : DestinationObject;
        if (!TryRoutePoint(target, out Vector3 point))
        { FailRoute(); return; }
        if (routeTarget != target)
        {
            routeTarget = target; nextRepath = 0;
            lastProgressTime = Time.time; lastProgressPosition = transform.position;
        }
        bool reachedPoint = Vector3.Distance(transform.position, point) <= 1.2f;
        if (!worker.IsAtMiningHome(target) && !reachedPoint)
        {
            if ((transform.position - lastProgressPosition).sqrMagnitude > 0.25f)
            { lastProgressPosition = transform.position; lastProgressTime = Time.time; }
            if (Time.time - lastProgressTime > 15f) { FailRoute(); return; }
            if (Time.time >= nextRepath && !agent.pathPending)
            {
                var path = new NavMeshPath();
                if (!agent.CalculatePath(point, path) || path.status != NavMeshPathStatus.PathComplete)
                { FailRoute(); return; }
                agent.isStopped = false;
                agent.SetPath(path);
                nextRepath = Time.time + 0.75f;
            }
            return;
        }
        agent.ResetPath();
        if (State == TaskState.Collect)
        {
            Pickup();
            ReleaseSource();
            if (CargoCount == 0) { Cancel(); return; }
            State = TaskState.Deliver;
            Status = "Доставить";
        }
        else
        {
            if (production != null) { production.Receive(this, cargo); production = null; }
            else if (tavern != null) { tavern.Receive(this, cargo); tavern = null; }
            else destination.DepositReserved(this, cargo);
            destination = null;
            ReservedCargo = 0;
            State = TaskState.Waiting;
            Status = CargoCount == 0 ? "Доставка завершена" : "Ожидает места для остатка груза";
        }
    }

    private bool Plan()
    {
        int carried = CargoCount;
        if (carried == 0) carryingLoot = false;
        // Трофеи сначала учитываются на складе, даже после паузы на еду/ручного приказа.
        var candidates = new List<GameObject>();
        if (carried == 0)
        {
            foreach (var item in FindObjectsOfType<Sawmill>()) if (item.storedWood > 0) candidates.Add(item.gameObject);
            foreach (var item in FindObjectsOfType<StoneHouse>()) if (item.storedStone > 0) candidates.Add(item.gameObject);
            foreach (var item in FindObjectsOfType<Fishing>()) if (item.storeFish > 0) candidates.Add(item.gameObject);
            foreach (var item in FarmHouse.All) if (item != null && item.StoredWheat > 0) candidates.Add(item.gameObject);
            foreach (var item in MiningHouse.All) if (item != null && item.HasOre) candidates.Add(item.gameObject);
            foreach (var item in RecipeBuilding.All) if (item != null && item.StoredOutput > 0) candidates.Add(item.gameObject);
            foreach (var item in GatheringHouse.All) if (item != null && item.Stored > 0) candidates.Add(item.gameObject);
            foreach (var item in LootCache.All) if (item != null && item.Amount > 0 && item.IsSafeFor(gameObject)) candidates.Add(item.gameObject);
            candidates.Sort((a,b) => (a.transform.position-transform.position).sqrMagnitude.CompareTo((b.transform.position-transform.position).sqrMagnitude));
        }
        var warehouses = new List<WarehouseStorage>(WarehouseStorage.All);
        warehouses.Sort((a,b) => (a.transform.position-transform.position).sqrMagnitude.CompareTo((b.transform.position-transform.position).sqrMagnitude));
        return DeliveryPriorityRules.TryPlan(level =>
        {
            if (!carryingLoot && (carried == 0 || cargo.Food > 0) && PlanTavern(level)) return true;
            if (!carryingLoot && PlanProduction(level)) return true;
            return PlanWarehouse(carried,candidates,warehouses,level);
        });
    }
    private bool PlanWarehouse(int carried,List<GameObject> candidates,List<WarehouseStorage> warehouses,DeliveryPriorityLevel level)
    {
        foreach (var warehouse in warehouses)
        {
            if (warehouse == null || !warehouse.isActiveAndEnabled) continue;
            if (worker.AssignedWorkplace != null && warehouse.gameObject != worker.AssignedWorkplace) continue;
            if (!Reachable(warehouse.gameObject)) continue;
            if (carried > 0)
            {
                if(DeliveryPriority.Of(warehouse.gameObject)!=level)continue;
                int amount = Mathf.Min(carried, warehouse.FreeCapacity);
                if (amount <= 0 || !warehouse.TryReserveSpace(this, amount)) continue;
                destination = warehouse; ReservedCargo = amount; State = TaskState.Deliver;
                Status = "Доставить"; return true;
            }
            foreach (var candidate in candidates)
            {
                if(DeliveryPriority.Of(candidate)!=level)continue;
                if (!BuildingRoadAccess.Allows(candidate)) continue;
                if (sources.TryGetValue(candidate, out var other) && other != null && other != this) continue;
                int amount = Mathf.Min(Mathf.Max(1, worker.MaxUnitResources), Mathf.Min(Available(candidate), warehouse.FreeCapacity));
                if (amount <= 0 || !Reachable(candidate) || !warehouse.TryReserveSpace(this, amount)) continue;
                sources[candidate] = this; source = candidate; destination = warehouse;
                ReservedCargo = amount; State = TaskState.Collect;
                Status = "Забрать"; return true;
            }
        }
        return false;
    }
    private bool PlanProduction(DeliveryPriorityLevel level)
    {
        var targets = new List<RecipeBuilding>(RecipeBuilding.All);
        // Сначала обслуживаем простаивающие здания, затем пополняем остальные.
        targets.Sort((a, b) => a.StoredInput.CompareTo(b.StoredInput));
        foreach (var target in targets)
        {
            if (target == null || DeliveryPriority.Of(target.gameObject)!=level || !target.isActiveAndEnabled || !BuildingRoadAccess.Allows(target) || target.RequestedInput <= 0 || !Reachable(target.gameObject)) continue;
            foreach (var input in target.InputTypes)
            {
                int requested = target.RequestedInputFor(input);
                if (requested <= 0) continue;
                int carried = cargo.GetStoredAmount(input);
                if (carried > 0)
                {
                    int amount = Mathf.Min(carried, requested);
                    if (!target.ReserveDelivery(this, input, amount)) continue;
                    production = target; productionInput = input; ReservedCargo = amount;
                    State = TaskState.Deliver; Status = "Доставить"; return true;
                }
                if (CargoCount > 0) continue;
                foreach (var warehouse in WarehouseStorage.All)
                {
                    if (warehouse == null || !warehouse.isActiveAndEnabled) continue;
                    if (target.supplyWarehouse != null && target.supplyWarehouse != warehouse) continue;
                    if (worker.AssignedWorkplace != null && worker.AssignedWorkplace != warehouse.gameObject) continue;
                    int amount = Mathf.Min(Mathf.Max(1, worker.MaxUnitResources),
                        Mathf.Min(requested, target.AvailableSupply(warehouse, input)));
                    if (amount <= 0 || !Reachable(warehouse.gameObject)) continue;
                    if (!target.ReserveSupply(this, warehouse, input, amount)) continue;
                    if (!target.ReserveDelivery(this, input, amount)) { target.ReleaseSupply(this, warehouse); continue; }
                    ingredientSource = warehouse; source = warehouse.gameObject; production = target;
                    productionInput = input;
                    ReservedCargo = amount; State = TaskState.Collect; Status = "Забрать"; return true;
                }
            }
        }
        return false;
    }
    private bool PlanTavern(DeliveryPriorityLevel level)
    {
        var taverns = new List<Tavern>();
        foreach (var service in FoodService.All)
            if (service is Tavern candidate && candidate.RequestedMeals > 0 && DeliveryPriority.Of(candidate.gameObject)==level) taverns.Add(candidate);
        taverns.Sort((a,b) => a.AvailableMeals.CompareTo(b.AvailableMeals));
        foreach (var target in taverns)
        {
            if (!Reachable(target.gameObject)) continue;
            // После паузы/еды/ручного приказа груз уже в рюкзаке:
            // резервируем новую доставку без повторного списания на складе.
            if (cargo.Food > 0)
            {
                int amount = Mathf.Min(cargo.Food, target.RequestedMeals);
                if (target.ReserveDelivery(this, amount))
                {
                    tavern = target; ReservedCargo = amount;
                    State = TaskState.Deliver; Status = "Доставить"; return true;
                }
                continue;
            }
            foreach (var warehouse in WarehouseStorage.All)
            {
                if (warehouse == null || !warehouse.isActiveAndEnabled) continue;
                if (target.foodStorage != null && warehouse != target.foodStorage) continue;
                if (worker.AssignedWorkplace != null && worker.AssignedWorkplace != warehouse.gameObject) continue;
                if (!Reachable(warehouse.gameObject)) continue;
                int amount = Mathf.Min(Mathf.Max(1, worker.MaxUnitResources), Mathf.Min(target.RequestedMeals, warehouse.AvailableFood));
                if (amount <= 0 || !warehouse.TryReserveFood(this, amount)) continue;
                if (!target.ReserveDelivery(this, amount)) { warehouse.ReleaseFood(this); continue; }
                source = warehouse.gameObject; foodSource = warehouse; tavern = target;
                ReservedCargo = amount; State = TaskState.Collect; Status = "Забрать";
                return true;
            }
        }
        return false;
    }
    private bool Reachable(GameObject target)
    {
        if (!TryRoutePoint(target, out Vector3 point)) return false;
        var path = new NavMeshPath();
        return agent.CalculatePath(point, path) && path.status == NavMeshPathStatus.PathComplete;
    }
    private bool TryRoutePoint(GameObject target, out Vector3 point)
    {
        point=default;
        if(target==null||(target.GetComponent<LootCache>()==null&&!FactionMember.CanUse(gameObject,target)))return false;
        point = worker.GetApproachPoint(target.transform);
        var targetTavern = target.GetComponent<Tavern>();
        if (targetTavern != null && !BuildingEntrance.TryGetWorldPoint(target, out _))
            point = targetTavern.ServicePoint;
        if (!NavMesh.SamplePosition(point, out var hit, 2f, agent.areaMask)) return false;
        point = hit.position; return true;
    }
    private void FailRoute()
    {
        Cancel();
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        nextSearch = Time.time + 5f;
        Status = "Маршрут заблокирован, повторит поиск";
    }
    private static int Available(GameObject obj)
    {
        var loot = obj.GetComponent<LootCache>(); if (loot != null) return loot.Amount;
        var gathering = obj.GetComponent<GatheringHouse>(); if (gathering != null) return gathering.Stored;
        var recipe = obj.GetComponent<RecipeBuilding>(); if (recipe != null) return recipe.StoredOutput;
        var farm = obj.GetComponent<FarmHouse>(); if (farm != null) return farm.StoredWheat;
        var mine = obj.GetComponent<MiningHouse>(); if (mine != null) return mine.StoredIron + mine.StoredGold;
        var wood = obj.GetComponent<Sawmill>(); if (wood != null) return wood.storedWood;
        var stone = obj.GetComponent<StoneHouse>(); if (stone != null) return stone.storedStone;
        var fish = obj.GetComponent<Fishing>(); return fish != null ? fish.storeFish : 0;
    }
    private void Pickup()
    {
        var loot = source.GetComponent<LootCache>(); if (loot != null) { carryingLoot = loot.Collect(cargo, ReservedCargo) > 0; return; }
        if (ingredientSource != null)
        {
            production.PickupSupply(this, ingredientSource, productionInput, ReservedCargo, cargo);
            production.ReleaseSupply(this, ingredientSource); ingredientSource = null; return;
        }
        if (foodSource != null) { foodSource.PickupReservedFood(this, cargo); foodSource = null; return; }
        var gathering = source.GetComponent<GatheringHouse>(); if (gathering != null) { gathering.Collect(cargo, ReservedCargo); return; }
        var recipe = source.GetComponent<RecipeBuilding>(); if (recipe != null) { recipe.Collect(cargo, ReservedCargo); return; }
        var farm = source.GetComponent<FarmHouse>(); if (farm != null) { farm.Collect(cargo, ReservedCargo); return; }
        var mine = source.GetComponent<MiningHouse>(); if (mine != null) { mine.Collect(cargo, ReservedCargo); return; }
        var wood = source.GetComponent<Sawmill>(); if (wood != null) { cargo.Wood += wood.ExtractWood(ReservedCargo); return; }
        var stone = source.GetComponent<StoneHouse>(); if (stone != null) { cargo.Stone += stone.ExtractStone(ReservedCargo); return; }
        var fish = source.GetComponent<Fishing>(); if (fish != null) cargo.Fish += fish.ExtractFish(ReservedCargo);
    }
    private void ReleaseSource()
    {
        // ReferenceEquals allows removing the key even after Unity destroyed the source.
        if (!ReferenceEquals(source, null) && sources.TryGetValue(source, out var owner) && owner == this) sources.Remove(source);
        source = null;
    }
    private void Cancel()
    {
        routeTarget = null;
        ReleaseSource();
        if (foodSource != null) foodSource.ReleaseFood(this);
        if (tavern != null) tavern.CancelDelivery(this);
        if (production != null) production.CancelDelivery(this);
        if (ingredientSource != null)
        {
            if (production != null) production.ReleaseSupply(this, ingredientSource);
            else
            {
                // Здание могло быть уничтожено до забора груза; его override уже недоступен.
                ingredientSource.ReleaseMaterials(this);
                ingredientSource.ReleaseFood(this);
            }
        }
        production = null; ingredientSource = null;
        foodSource = null; tavern = null;
        if (destination != null) destination.ReleaseSpace(this);
        destination = null; ReservedCargo = 0; State = TaskState.Waiting;
    }
    private void OnDisable() { Cancel(); }
}
