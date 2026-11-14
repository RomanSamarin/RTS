using UnityEngine;
using UnityEngine.AI;
using _Game.Scripts.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(GettingResources), typeof(NavMeshAgent), typeof(GOScript))]
[RequireComponent(typeof(Unit), typeof(AttackController))]
public class SiegeLadderCart : MonoBehaviour
{
    [Min(.5f)] public float loadingDistance = 3f;
    [Min(.1f)] public float moveSpeed = 2f;
    public Transform horseHitchPoint;
    [Tooltip("Two optional child objects showing the loaded ladders.")]
    public GameObject[] ladderVisuals = new GameObject[2];
    [SerializeField] private CartHorse attachedHorse;
    private WarehouseStorage loadingWarehouse;
    private bool loading;
    private int routeToken;
    private float nextMove;
    private float lastProgressAt;
    private Vector3 lastProgressPosition;
    private GettingResources cargo;
    private NavMeshAgent agent;
    private GOScript command;
    private Unit unit;
    private CartHorse initialHorse;
    private readonly LadderReservations<SiegeLadderInstaller> ladderReservations = new LadderReservations<SiegeLadderInstaller>();
    public int AvailableLadders => ShipPassenger.Controls(gameObject)?0:ladderReservations.Available(LadderCount);
    public bool ReserveLadder(SiegeLadderInstaller soldier) => !ShipPassenger.Controls(gameObject)&&isActiveAndEnabled && unit != null && unit.unitHealth > 0
        && soldier != null && FactionMember.AreAllies(gameObject, soldier.gameObject)
        && ladderReservations.Reserve(soldier, LadderCount);
    public void ReleaseLadder(SiegeLadderInstaller soldier) => ladderReservations.Release(soldier);
    public bool PickupLadder(SiegeLadderInstaller soldier, GettingResources destination)
    {
        if(ShipPassenger.Controls(gameObject))return false;
        if (!isActiveAndEnabled || unit == null || unit.unitHealth <= 0 || destination == null || soldier == null
            || !FactionMember.AreAllies(gameObject, soldier.gameObject) || !ladderReservations.Contains(soldier)
            || LadderCount <= 0 || Vector3.Distance(transform.position, soldier.transform.position) > loadingDistance) return false;
        ladderReservations.Release(soldier);
        Cargo.RemoveStoredAmount(ResourceType.SiegeLadder, 1);
        destination.AddStoredAmount(ResourceType.SiegeLadder, 1);
        RefreshVisuals(); return true;
    }

    public int Capacity => SiegeLadderCargo.Capacity;
    public int LadderCount => Cargo != null ? Cargo.GetStoredAmount(ResourceType.SiegeLadder) : 0;
    public int FreeSpace => SiegeLadderCargo.FreeSpace(Cargo);
    public CartHorse AttachedHorse => attachedHorse;
    public bool CanMove => isActiveAndEnabled && (unit == null || unit.unitHealth > 0)
        && attachedHorse != null && attachedHorse.IsAlive && attachedHorse.SiegeCart == this
        && FactionMember.AreAllies(gameObject, attachedHorse.gameObject);
    public bool IsMoving => agent != null && agent.enabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > .01f;
    public Vector3 HorsePosition => horseHitchPoint != null && horseHitchPoint.IsChildOf(transform)
        ? horseHitchPoint.position : transform.position + transform.forward * 2.6f;
    public string Status { get; private set; } = "Нужна лошадь";
    private GettingResources Cargo => cargo != null ? cargo : cargo = GetComponent<GettingResources>();

    private void Awake()
    {
        cargo = GetComponent<GettingResources>(); agent = GetComponent<NavMeshAgent>();
        command = GetComponent<GOScript>(); unit = GetComponent<Unit>();
        var worker = GetComponent<ResourcesMove>();
        if (worker == null) worker = gameObject.AddComponent<ResourcesMove>();
        worker.role = ResourcesMove.UnitRole.Transport; worker.ClearWorkplace();
        var attack = GetComponent<AttackController>();
        attack.autoAcquireTargets = false; attack.damageHandledExternally = true; attack.unitDamage = 0;
        if (GetComponent<CaravanTarget>() == null) gameObject.AddComponent<CaravanTarget>();
        var population = GetComponent<UnitAdd>(); if (population != null) population.enabled = false;
        var eater = GetComponent<UnitEat>(); if (eater != null) eater.enabled = false;
        FactionMember.Ensure(gameObject);
        command.ManualOrderIssued += CancelLoading;
    }
    private void Start()
    {
        command.SetBaseSpeed(moveSpeed); agent.areaMask &= ~WallNavigation.AreaMask;
        var house = HousesScript.FindHouseOf(gameObject); if (house != null) house.Evict(gameObject);
        if (attachedHorse != null && attachedHorse.SiegeCart != this)
        { initialHorse = attachedHorse; attachedHorse = null; }
        RefreshVisuals();
    }
    internal void SetHorse(CartHorse horse) { attachedHorse = horse; Status = "Лошадь привязана"; }
    internal void ClearHorse(CartHorse horse)
    { if (attachedHorse != horse) return; attachedHorse = null; CancelLoading(); ForceStop(); Status = "Нужна лошадь"; }
    public bool AttachHorse(CartHorse horse) => !ShipPassenger.Controls(gameObject)&&horse != null && horse.TryHitch(this);
    public void AttachNearestHorse()
    {
        CartHorse best = null; float nearest = float.PositiveInfinity;
        foreach (var horse in CartHorse.All)
        {
            if (horse == null || !horse.IsAlive || horse.IsHitched || horse.TrainingOwner != null
                || !FactionMember.AreAllies(gameObject, horse.gameObject)) continue;
            float distance = Vector3.Distance(transform.position, horse.transform.position);
            if (distance <= horse.hitchDistance && distance < nearest) { best = horse; nearest = distance; }
        }
        if (!AttachHorse(best)) Status = "Подведите свободную лошадь к повозке";
    }
    public void DetachHorse() { if (!ShipPassenger.Controls(gameObject)&&attachedHorse != null) attachedHorse.Unhitch(); }
    private static Vector3 WarehousePoint(WarehouseStorage warehouse)
        => BuildingEntrance.TryGetWorldPoint(warehouse.gameObject, out var door) ? door : warehouse.transform.position;
    private static float Distance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
    private bool Usable(WarehouseStorage warehouse) => warehouse != null && warehouse.isActiveAndEnabled
        && FactionMember.CanUse(gameObject, warehouse.gameObject) && BuildingRoadAccess.Allows(warehouse);
    private bool AtWarehouse(WarehouseStorage warehouse) => Usable(warehouse)
        && Distance(transform.position, WarehousePoint(warehouse)) <= loadingDistance;

    // Loading is physical: only a wagon standing at the warehouse can take its reserved stock.
    public int LoadFromWarehouse(WarehouseStorage warehouse)
    {
        if (!AtWarehouse(warehouse) || FreeSpace == 0) return 0;
        CancelLoading();
        int count = Mathf.Min(FreeSpace, warehouse.AvailableMaterial(ResourceType.SiegeLadder));
        if (count <= 0 || !warehouse.ReserveMaterial(this, ResourceType.SiegeLadder, count)) return 0;
        int loaded = SiegeLadderCargo.Pickup(warehouse, Cargo, this);
        warehouse.ReleaseMaterials(this); RefreshVisuals();
        Status = loaded > 0 ? "Лестницы загружены" : "Не удалось загрузить лестницы";
        return loaded;
    }
    public void LoadNearestWarehouse()
    {
        WarehouseStorage best = null; float nearest = float.PositiveInfinity;
        foreach (var warehouse in WarehouseStorage.All)
        {
            if (!AtWarehouse(warehouse) || warehouse.AvailableMaterial(ResourceType.SiegeLadder) <= 0) continue;
            float distance = Distance(transform.position, WarehousePoint(warehouse));
            if (distance < nearest) { best = warehouse; nearest = distance; }
        }
        if (LoadFromWarehouse(best) == 0) Status = FreeSpace == 0 ? "Повозка заполнена" : "Рядом нет склада с лестницами";
    }
    public void ReturnForLadders()
    {
        CancelLoading();
        if (!CanMove) { Status = "Для рейса нужна лошадь"; return; }
        if (FreeSpace == 0) { Status = "Повозка заполнена"; return; }
        WarehouseStorage best = null; float nearest = float.PositiveInfinity;
        foreach (var warehouse in WarehouseStorage.All)
        {
            if (!Usable(warehouse) || warehouse.AvailableMaterial(ResourceType.SiegeLadder) <= 0) continue;
            float distance = (WarehousePoint(warehouse) - transform.position).sqrMagnitude;
            if (distance < nearest && TryRoute(warehouse, out _)) { best = warehouse; nearest = distance; }
        }
        if (best == null) { Status = "Нет доступного склада с лестницами"; return; }
        int amount = Mathf.Min(FreeSpace, best.AvailableMaterial(ResourceType.SiegeLadder));
        if (!best.ReserveMaterial(this, ResourceType.SiegeLadder, amount)) { Status = "Лестницы уже зарезервированы"; return; }
        command.StopMoving(); loadingWarehouse = best; loading = true; nextMove = 0;
        lastProgressAt = Time.time; lastProgressPosition = transform.position;
        Status = "Едет за лестницами";
    }
    public int UnloadToWarehouse(WarehouseStorage warehouse)
    {
        if (!AtWarehouse(warehouse)) return 0;
        CancelLoading();
        int unloaded = warehouse.AddResource(ResourceType.SiegeLadder, AvailableLadders);
        Cargo.RemoveStoredAmount(ResourceType.SiegeLadder, unloaded); RefreshVisuals();
        Status = unloaded > 0 ? "Лестницы выгружены на склад" : "Нет места на складе";
        return unloaded;
    }
    public void UnloadNearestWarehouse()
    {
        WarehouseStorage best = null; float nearest = float.PositiveInfinity;
        foreach (var warehouse in WarehouseStorage.All)
        {
            if (!AtWarehouse(warehouse) || warehouse.FreeCapacity == 0) continue;
            float distance = Distance(transform.position, WarehousePoint(warehouse));
            if (distance < nearest) { best = warehouse; nearest = distance; }
        }
        if (best == null) { Status = "Подведите повозку к складу со свободным местом"; return; }
        UnloadToWarehouse(best);
    }
    public void DropLadders()
    {
        if (AvailableLadders == 0) { Status = "Нет свободных лестниц"; return; }
        CancelLoading(); command.StopMoving();
        Vector3 point = transform.position - transform.right * 2;
        if (agent == null || !NavMesh.SamplePosition(point, out var hit, 2,
            new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }))
        { Status = "Нет места для выгрузки"; return; }
        // Separate ground stock; unload never turns into an already installed siege ladder.
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = "Осадные лестницы"; obj.transform.position = hit.position + Vector3.up * .2f;
        obj.transform.localScale = new Vector3(.5f, .4f, 2.5f);
        var stock = obj.AddComponent<GettingResources>();
        int amount = AvailableLadders; stock.AddStoredAmount(ResourceType.SiegeLadder, amount);
        obj.AddComponent<LootCache>(); Cargo.RemoveStoredAmount(ResourceType.SiegeLadder, amount);
        RefreshVisuals(); Status = "Лестницы выгружены на землю";
    }
    private bool TryRoute(WarehouseStorage warehouse, out Vector3 point)
    {
        point = default;
        if (!Usable(warehouse) || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(WarehousePoint(warehouse), out var hit, 3, filter)
            || Distance(hit.position, WarehousePoint(warehouse)) > loadingDistance) return false;
        var path = new NavMeshPath();
        if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        point = hit.position; return true;
    }
    private void Update()
    {
        if (SaveGameManager.IsLoading) return;
        if(ShipPassenger.Controls(gameObject))return;
        if (initialHorse != null) { var horse = initialHorse; initialHorse = null; AttachHorse(horse); }
        RefreshVisuals();
        if (!CanMove) { CancelLoading(); ForceStop(); Status = "Нужна живая привязанная лошадь"; return; }
        if (!loading)
        {
            if (command.isCommandToMove) Status = "Перевозит лестницы";
            else if (Status == "Перевозит лестницы") Status = "Ожидает приказ";
            return;
        }
        if (!Usable(loadingWarehouse) || loadingWarehouse.MaterialAmount(this, ResourceType.SiegeLadder) <= 0 || FreeSpace == 0)
        { CancelLoading(); Status = "Загрузка отменена: склад или резерв недоступен"; return; }
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        { CancelLoading(); Status = "Повозка вне NavMesh"; return; }
        if (AtWarehouse(loadingWarehouse))
        {
            int amount = SiegeLadderCargo.Pickup(loadingWarehouse, Cargo, this);
            CancelLoading(); RefreshVisuals(); Status = amount > 0 ? "Лестницы загружены" : "Загрузка не выполнена"; return;
        }
        if (Distance(transform.position, lastProgressPosition) > .25f)
        { lastProgressAt = Time.time; lastProgressPosition = transform.position; }
        if (Time.time - lastProgressAt > 30)
        { CancelLoading(); Status = "Проезд к складу заблокирован"; return; }
        if (Time.time < nextMove) return;
        nextMove = Time.time + .75f;
        if (!TryRoute(loadingWarehouse, out var point)
            || (!command.OwnsSupplyOrder(routeToken) && !command.TryBeginSupplyMove(out routeToken))
            || !command.MoveForSupply(routeToken, point))
        { CancelLoading(); Status = "Нет пути к складу"; }
    }
    public void CancelLoading()
    {
        if (command != null) command.EndSupplyMove(routeToken);
        routeToken = 0; loading = false;
        if (loadingWarehouse != null) loadingWarehouse.ReleaseMaterials(this);
        loadingWarehouse = null;
        Status = "Ожидает приказ";
    }
    private void ForceStop()
    {
        if (command != null && command.isCommandToMove) command.StopMoving();
        if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
    }
    private void RefreshVisuals()
    {
        if (ladderVisuals == null) return;
        for (int i = 0; i < ladderVisuals.Length; i++)
            if (ladderVisuals[i] != null && ladderVisuals[i] != gameObject && ladderVisuals[i].transform.IsChildOf(transform))
                ladderVisuals[i].SetActive(i < LadderCount);
    }
    private void OnDisable() { CancelLoading(); DetachHorse(); ladderReservations.Clear(); }
    private void OnDestroy() { if (command != null) command.ManualOrderIssued -= CancelLoading; }
}
