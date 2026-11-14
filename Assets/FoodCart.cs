using UnityEngine;
using UnityEngine.AI;
using _Game.Scripts.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(GettingResources), typeof(NavMeshAgent), typeof(GOScript))]
[RequireComponent(typeof(Unit), typeof(AttackController))]
public class FoodCart : FoodService
{
    private static readonly ResourceType[] MealTypes = { ResourceType.Food, ResourceType.Bread, ResourceType.Berries };
    [Min(1)] public int capacity = 50;
    [Min(0.5f)] public float loadingDistance = 3f;
    [Min(1f)] public float supplyRadius = 25f;
    [Min(1f)] public float followDistance = 7f;
    public bool followArmy = true;
    public bool autoRefill = true;
    [Min(0)] public int restockThreshold = 10;
    [Min(1f)] public float refillCooldown = 20f;
    public Transform horseHitchPoint;
    [SerializeField] private ArmyGroup assignedArmy;
    [SerializeField] private CartHorse attachedHorse;
    public ArmyGroup Army => assignedArmy;
    public CartHorse AttachedHorse => attachedHorse;
    public string Status { get; private set; } = "Нужна лошадь";
    public Vector3 HorsePosition => horseHitchPoint != null && horseHitchPoint.IsChildOf(transform) ? horseHitchPoint.position : transform.position + transform.forward * 2f;
    public override Vector3 ServicePoint => servingPoint != null && servingPoint.IsChildOf(transform)
        ? servingPoint.position : transform.position - transform.right * (agent != null ? agent.radius + 0.75f : 1.5f);
    public bool IsMoving => agent != null && agent.enabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > 0.01f;
    public bool CanMove => ArmySupplyRules.CanPull(unit == null || unit.unitHealth > 0,
        attachedHorse != null, attachedHorse != null && attachedHorse.IsAlive, attachedHorse != null && attachedHorse.Cart == this);
    private GettingResources cargo;
    private GettingResources Cargo => cargo != null ? cargo : cargo = GetComponent<GettingResources>();
    private NavMeshAgent agent;
    private GOScript command;
    private Unit unit;
    private WarehouseStorage refillWarehouse;
    private bool refilling;
    private bool loading;
    private int remainingLoad;
    private CarrierTask loadingCarrier;
    private int reservedLoad;
    private int routeToken;
    private float nextMove, nextRefill, nextWarehouseSearch;
    private CartHorse initialHorse;
    public override int AvailableMeals => Cargo != null ? Cargo.Food : 0;
    public int FreeCargoSpace
    {
        get { int used = 0; foreach (var type in WarehouseStorage.Types) used += Cargo.GetStoredAmount(type); return ArmySupplyRules.FreeCargo(capacity, used); }
    }
    internal WarehouseStorage LoadingWarehouse => refilling && loading && isActiveAndEnabled && CanMove &&
        !ShipPassenger.Controls(gameObject) && (command == null || !command.isCommandToMove) &&
        CanUseWarehouse(refillWarehouse) && HorizontalDistance(transform.position, WarehousePoint(refillWarehouse)) <= loadingDistance
        ? refillWarehouse : null;
    internal int RequestedMeals => LoadingWarehouse != null && loadingCarrier == null
        ? Mathf.Min(remainingLoad, FreeCargoSpace) : 0;
    private void Awake()
    {
        if (GetComponent<CaravanTarget>() == null) gameObject.AddComponent<CaravanTarget>();
        cargo = GetComponent<GettingResources>(); agent = GetComponent<NavMeshAgent>(); command = GetComponent<GOScript>(); unit = GetComponent<Unit>();
        if (unit == null) unit = gameObject.AddComponent<Unit>();
        var attack = GetComponent<AttackController>(); if (attack == null) attack = gameObject.AddComponent<AttackController>();
        attack.autoAcquireTargets = false; attack.unitDamage = 0;
        FactionMember.Ensure(gameObject);
        var population = GetComponent<UnitAdd>(); if (population != null) population.enabled = false;
        command.ManualOrderIssued += ManualOrder;
        var move = GetComponent<ResourcesMove>(); if (move != null) { move.role = ResourcesMove.UnitRole.Transport; move.ClearWorkplace(); }
        var house = HousesScript.FindHouseOf(gameObject); if (house != null) house.Evict(gameObject);
    }
    private void Start()
    {
        if (assignedArmy != null) assignedArmy.AssignCart(this);
        if (attachedHorse != null && attachedHorse.Cart != this) { initialHorse = attachedHorse; attachedHorse = null; }
        if (!CanMove) ForceStop();
    }
    internal void SetArmy(ArmyGroup army)
    { StopRoute(); CancelRefill(); assignedArmy = army; }
    public void AssignArmy(ArmyGroup army)
    { if (army != null) army.AssignCart(this); else if (assignedArmy != null) assignedArmy.RemoveCart(this); }
    internal void SetHorse(CartHorse horse) { attachedHorse = horse; Status = "Лошадь привязана"; }
    internal void ClearHorse(CartHorse horse)
    { if (attachedHorse == horse) attachedHorse = null; ForceStop(); CancelRefill(); Status = "Нет привязанной лошади"; }
    public bool AttachHorse(CartHorse horse) => !ShipPassenger.Controls(gameObject)&&horse != null && horse.TryHitch(this);
    public void AttachNearestHorse()
    {
        CartHorse nearest = null; float distance = float.PositiveInfinity;
        foreach (var horse in CartHorse.All)
        {
            if (horse == null || !horse.IsAlive || horse.IsHitched) continue;
            float d = Vector3.Distance(transform.position, horse.transform.position);
            if (d <= horse.hitchDistance && d < distance) { nearest = horse; distance = d; }
        }
        if (nearest == null || !AttachHorse(nearest)) Status = "Подведите свободную лошадь к повозке";
    }
    public void DetachHorse() { if (!ShipPassenger.Controls(gameObject)&&attachedHorse != null) attachedHorse.Unhitch(); }
    public bool CanSupply(UnitEat eater)
    {
        if(ShipPassenger.Aboard(gameObject))return false;
        if(eater==null||!FactionMember.CanUse(eater.gameObject,gameObject))return false;
        var member = eater != null ? eater.GetComponent<ArmyMember>() : null;
        bool sameArmy = assignedArmy != null && assignedArmy.isActiveAndEnabled && member != null && member.Army == assignedArmy;
        return isActiveAndEnabled && ArmySupplyRules.WithinSupply(sameArmy,
            eater != null ? HorizontalDistance(eater.transform.position, ServicePoint) : float.PositiveInfinity, supplyRadius);
    }
    public override bool TryServe(int portions)
    {
        if(ShipPassenger.Aboard(gameObject))return false;
        if (portions <= 0 || portions > AvailableMeals) return false;
        int remaining = portions;
        foreach (var type in MealTypes)
        {
            int take = Mathf.Min(remaining, Cargo.GetStoredAmount(type)); Cargo.RemoveStoredAmount(type, take); remaining -= take;
        }
        return true;
    }
    internal bool ReserveLoading(CarrierTask carrier, int amount)
    {
        if (carrier == null || amount <= 0 || amount > RequestedMeals ||
            !FactionMember.CanUse(carrier.gameObject, gameObject)) return false;
        loadingCarrier = carrier;
        reservedLoad = amount;
        return true;
    }

    internal bool HasLoadingReservation(CarrierTask carrier)
        => carrier != null && carrier == loadingCarrier && reservedLoad > 0 && LoadingWarehouse != null &&
           FactionMember.CanUse(carrier.gameObject, gameObject);

    internal void CancelLoading(CarrierTask carrier)
    {
        if (loadingCarrier != carrier) return;
        loadingCarrier = null;
        reservedLoad = 0;
    }

    internal int ReceiveLoading(CarrierTask carrier, GettingResources delivered)
    {
        if (delivered == null || !HasLoadingReservation(carrier) ||
            HorizontalDistance(carrier.transform.position, ServicePoint) > 2.5f) return 0;
        int amount = Mathf.Min(reservedLoad, Mathf.Min(remainingLoad, Mathf.Min(FreeCargoSpace, delivered.Food)));
        int remaining = amount;
        foreach (var type in MealTypes)
        {
            int count = Mathf.Min(remaining, delivered.GetStoredAmount(type));
            delivered.RemoveStoredAmount(type, count);
            Cargo.AddStoredAmount(type, count);
            remaining -= count;
        }
        remainingLoad -= amount;
        CancelLoading(carrier);
        return amount;
    }

    public void LoadNearestWarehouse()
    {
        if (refilling || ShipPassenger.Controls(gameObject)) return;
        if (!CanMove) { Status = "Для загрузки нужна лошадь"; return; }
        WarehouseStorage nearest = null; float distance = float.PositiveInfinity;
        foreach (var warehouse in WarehouseStorage.All)
        {
            if (!CanUseWarehouse(warehouse) || warehouse.AvailableFood <= 0) continue;
            float d = HorizontalDistance(transform.position, WarehousePoint(warehouse));
            if (d <= loadingDistance && d < distance) { nearest = warehouse; distance = d; }
        }
        if (nearest == null) { Status = "Нет доступной еды у склада рядом"; return; }
        ReturnForFood();
        if (refilling) refillWarehouse = nearest;
    }
    public void ReturnForFood()
    {
        if (refilling || ShipPassenger.Controls(gameObject)) return;
        if (!CanMove) { Status = "Для рейса за едой нужна лошадь"; return; }
        if (FreeCargoSpace <= 0) { Status = "Повозка заполнена"; return; }
        command.StopMoving(); followArmy = true; refilling = true; nextWarehouseSearch = 0f;
    }
    public void ResumeFollowing()
    {
        if (refilling)
        {
            StopRoute();
            CancelRefill();
            nextRefill = Time.time + Mathf.Max(1f, refillCooldown);
        }
        command.StopMoving();
        followArmy = true;
        nextMove = 0f;
    }
    public void StopFollowing() { followArmy = false; StopRoute(); CancelRefill(); }
    private void ManualOrder() { followArmy = false; routeToken = 0; CancelRefill(); }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (ShipPassenger.Controls(gameObject)) { CancelRefill(); return; }
        // Unit.Start у лошади уже выполнился: начальное нулевое здоровье не мешает привязке.
        if (initialHorse != null) { var horse = initialHorse; initialHorse = null; if (!horse.TryHitch(this)) Status = "Подведите лошадь к повозке"; }
        if (!CanMove) { ForceStop(); CancelRefill(); Status = "Нет живой привязанной лошади"; return; }
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) { StopRoute(); CancelRefill(); Status = "Повозка вне NavMesh"; return; }
        var attack = GetComponent<AttackController>(); if (attack != null) attack.targetToAttack = null;
        if (command.isCommandToMove) { Status = "Выполняет приказ движения"; return; }
        if (FreeCargoSpace > 0 && followArmy && autoRefill && assignedArmy != null && assignedArmy.MemberCount > 0
            && AvailableMeals <= Mathf.Max(0, restockThreshold) && Time.time >= nextRefill) refilling = true;
        if (refilling) { TickRefill(); return; }
        if (!followArmy) { StopRoute(); Status = "Сопровождение выключено"; return; }
        if (assignedArmy == null || !assignedArmy.isActiveAndEnabled || assignedArmy.MemberCount == 0)
        { StopRoute(); Status = "Не назначен отряд"; return; }
        if (!assignedArmy.TryGetAnchor(out var anchor, out var forward)) { StopRoute(); Status = "Ждёт отряд"; return; }
        Vector3 goal = anchor - forward * Mathf.Max(1f, followDistance);
        if (HorizontalDistance(transform.position, goal) <= 2f) { StopRoute(); Status = "Снабжает отряд"; return; }
        Status = "Следует за отрядом"; MoveRoute(goal, anchor);
    }

    private void TickRefill()
    {
        if (!CanUseWarehouse(refillWarehouse))
        {
            if (loading) { FinishRefill("Склад недоступен"); return; }
            if (Time.time < nextWarehouseSearch) return;
            nextWarehouseSearch = Time.time + 3f;
            if (!PlanRefill()) { FinishRefill("Нет доступной еды на складах", 5f); return; }
        }

        // Saves made before carrier loading may still have a reservation owned by the cart.
        if (refillWarehouse.ReservedFoodFor(this) > 0) refillWarehouse.ReleaseFood(this);
        Vector3 point = WarehousePoint(refillWarehouse);
        if (HorizontalDistance(transform.position, point) > loadingDistance)
        {
            if (loading) { FinishRefill("Загрузка прервана"); return; }
            Status = "Едет за едой";
            if (!MoveRoute(point)) FinishRefill("Нет доступного пути к складу", 5f);
            return;
        }

        StopRoute();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        if (!loading)
        {
            loading = true;
            remainingLoad = Mathf.Min(FreeCargoSpace, refillWarehouse.AvailableFood);
        }
        if (FreeCargoSpace <= 0)
            FinishRefill("Еда загружена");
        else if (remainingLoad <= 0)
            FinishRefill(AvailableMeals > 0 ? "Повозка загружена частично" : "Нет доступной еды на складе");
        else if (loadingCarrier != null)
            Status = "Носильщик загружает еду";
        else if (refillWarehouse.AvailableFood <= 0)
            FinishRefill(AvailableMeals > 0 ? "Повозка загружена частично" : "Нет доступной еды на складе");
        else
            Status = "Ждёт носильщика";
    }

    private void FinishRefill(string status, float retryDelay = 0f)
    {
        StopRoute();
        CancelRefill();
        nextRefill = Time.time + (retryDelay > 0 ? retryDelay : Mathf.Max(1f, refillCooldown));
        Status = status;
    }

    private bool PlanRefill()
    {
        WarehouseStorage best = null; float distance = float.PositiveInfinity;
        foreach (var warehouse in WarehouseStorage.All)
        {
            if (!CanUseWarehouse(warehouse) || warehouse.AvailableFood <= 0) continue;
            float d = (warehouse.transform.position - transform.position).sqrMagnitude;
            if (d >= distance || !RoutePoint(WarehousePoint(warehouse), out _)) continue;
            best = warehouse; distance = d;
        }
        if (best == null) return false;
        refillWarehouse = best; return true;
    }
    private bool CanUseWarehouse(WarehouseStorage warehouse)
        => warehouse != null && warehouse.isActiveAndEnabled && BuildingRoadAccess.Allows(warehouse) &&
           FactionMember.CanUse(gameObject, warehouse.gameObject);
    private bool RoutePoint(Vector3 point, out Vector3 result)
    {
        result = point;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(point, out var hit, 3f, filter)) return false;
        var path = new NavMeshPath();
        if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        result = hit.position; return true;
    }
    private bool MoveRoute(Vector3 point, Vector3? fallback = null)
    {
        if (Time.time < nextMove || agent.pathPending) return true; nextMove = Time.time + 0.75f;
        if (!command.OwnsSupplyOrder(routeToken) && !command.TryBeginSupplyMove(out routeToken)) return false;
        bool found = RoutePoint(point, out var destination);
        if (!found && fallback.HasValue) found = RoutePoint(fallback.Value, out destination);
        if (!found || !command.MoveForSupply(routeToken, destination))
        { StopRoute(); Status = "Нет доступного пути"; return false; }
        return true;
    }
    private static Vector3 WarehousePoint(WarehouseStorage warehouse)
        => BuildingEntrance.TryGetWorldPoint(warehouse.gameObject, out var door) ? door : warehouse.transform.position;
    private static float HorizontalDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
    private void StopRoute() { if (command != null) command.EndSupplyMove(routeToken); routeToken = 0; }
    private void ForceStop()
    {
        StopRoute();
        if (command != null && command.isCommandToMove) command.StopMoving();
        if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
    }
    private void CancelRefill()
    {
        var carrier = loadingCarrier;
        loadingCarrier = null;
        reservedLoad = remainingLoad = 0;
        refilling = loading = false;
        if (refillWarehouse != null && refillWarehouse.ReservedFoodFor(this) > 0)
            refillWarehouse.ReleaseFood(this);
        refillWarehouse = null;
        if (carrier != null) carrier.CancelCartDelivery(this);
    }
    protected override void OnDisable()
    { base.OnDisable(); StopRoute(); CancelRefill(); DetachHorse(); }
    private void OnDestroy()
    { if (command != null) command.ManualOrderIssued -= ManualOrder; if (assignedArmy != null) assignedArmy.RemoveCart(this); }
}
