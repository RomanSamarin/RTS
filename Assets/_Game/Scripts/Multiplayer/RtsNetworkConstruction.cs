using System.Collections.Generic;
using _Game.Scripts.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>Owns the warehouse and building request for one plot on the network test map.</summary>
[RequireComponent(typeof(NetworkObject), typeof(FactionMember))]
public sealed class RtsNetworkConstruction : NetworkBehaviour
{
    public const int WoodCost = 6;
    public const int HouseId = 75, GranaryId = 72, BarracksId = 0, SawmillId = 1, StoneQuarryId = 3;
    public const int MillId = 76, BakeryId = 77;
    public const int FarmId = 78;
    public const int BlacksmithId = 79, WorkshopId = 80, MineId = 81, ArcheryRangeId = 82, StableId = 83;
    public const int StartingWood = 160;
    private const int TestBuildingId = -1;
    private const int WarehouseJobId = -2;
    private const float CellSize = 2f;
    [System.Serializable]
    public sealed class CatalogEntry
    {
        public int id;
        public RtsNetworkBuildSite prefab;
    }
    [SerializeField] private ObjectsDatabseSO database;
    [SerializeField] private CatalogEntry[] catalog;
    [SerializeField] private RtsNetworkUnit builderPrefab;
    [SerializeField] private RtsNetworkUnit lumberjackPrefab, carrierPrefab, stoneMasonPrefab, producerPrefab;
    [SerializeField] private RtsNetworkUnit farmerPrefab;
    [SerializeField] private RtsNetworkUnit minerPrefab;
    [SerializeField] private RtsNetworkUnit horsePrefab;
    [SerializeField] private RtsNetworkUnit residentPrefab;
    [SerializeField] private RtsNetworkFarmField fieldPrefab;
    [SerializeField] private RtsNetworkIronVein ironVeinPrefab;
    [SerializeField] private NetworkObject timberTreePrefab, stoneDepositPrefab;
    [SerializeField] private RtsNetworkBuildSite buildingPrefab;
    public UnityEvent<string> statusChanged = new UnityEvent<string>();

    private readonly NetworkVariable<int> owner = new NetworkVariable<int>();
    private readonly NetworkVariable<int> stock = new NetworkVariable<int>();
    private readonly NetworkVariable<int> stoneStock = new NetworkVariable<int>();
    private readonly NetworkVariable<int> wheatStock = new NetworkVariable<int>();
    private readonly NetworkVariable<int> flourStock = new NetworkVariable<int>();
    private readonly NetworkVariable<int> breadStock = new NetworkVariable<int>();
    private readonly NetworkVariable<int> ironStock = new NetworkVariable<int>();
    private readonly NetworkVariable<int> goldStock = new NetworkVariable<int>();
    private readonly NetworkVariable<int> swordStock = new NetworkVariable<int>();
    private readonly NetworkVariable<int> spearStock = new NetworkVariable<int>();
    private readonly NetworkVariable<int> bowStock = new NetworkVariable<int>();
    private readonly NetworkVariable<int> shieldStock = new NetworkVariable<int>();
    private readonly NetworkVariable<int> reserved = new NetworkVariable<int>();
    private readonly NetworkVariable<int> cargo = new NetworkVariable<int>();
    private readonly NetworkVariable<int> availableResidents = new NetworkVariable<int>();
    private readonly NetworkVariable<int> carrierCount = new NetworkVariable<int>();
    private WarehouseStorage warehouse;
    private BuildingEntrance warehouseEntrance;
    private RtsNetworkUnit builder;
    private readonly List<RtsNetworkUnit> carriers = new List<RtsNetworkUnit>();
    private readonly List<RtsNetworkBuildSite> projects = new List<RtsNetworkBuildSite>();
    private static RtsNetworkConstruction activePlacement;
    private static int placementEndedFrame = -1;
    private bool createBuilder;
    private float nextPopulationScan;
    private float nextRequest;
    private int startingWood = StartingWood;
    private int startingStone = 80;
    private int startingWheat = 50;
    private int startingIron;
    private int startingFood;
    private bool testFieldHarvest;
    private int selectedBuildingId;
    private int rotationSteps;
    private GameObject preview;
    public int PlayerId => owner.Value;
    public int WoodStock => stock.Value;
    public int StoneStock => stoneStock.Value;
    public int WheatStock => wheatStock.Value;
    public int FlourStock => flourStock.Value;
    public int BreadStock => breadStock.Value;
    public int IronStock => ironStock.Value;
    public int GoldStock => goldStock.Value;
    internal GameObject HorsePrefab => horsePrefab.gameObject;
    public int SwordStock => swordStock.Value;
    public int SpearStock => spearStock.Value;
    public int BowStock => bowStock.Value;
    public int ShieldStock => shieldStock.Value;
    public int ReservedWood => reserved.Value;
    public int CargoWood => cargo.Value;
    public int AvailableResidents => availableResidents.Value;
    public int CarrierCount => carrierCount.Value;
    public static bool BlocksWorldInput => activePlacement != null || placementEndedFrame == Time.frameCount;
    internal static void CancelPlacement() => activePlacement?.EndPlacement();

    public bool RegisterPrefabs(NetworkManager manager)
    {
        if (!FactionRules.IsPlayerSide(GetComponent<FactionMember>().Side)
            || builderPrefab == null || !builderPrefab.IsBuilder || !builderPrefab.ValidatePrototype(out _)
            || buildingPrefab == null || !RegisterPrefab(manager, builderPrefab.gameObject)) return false;
        if (lumberjackPrefab == null || !lumberjackPrefab.IsLumberjack || !RegisterWorker(manager, lumberjackPrefab)
            || carrierPrefab == null || !carrierPrefab.IsCarrier || !RegisterWorker(manager, carrierPrefab)
            || stoneMasonPrefab == null || !stoneMasonPrefab.IsStoneMason || !RegisterWorker(manager, stoneMasonPrefab)
            || producerPrefab == null || !producerPrefab.IsProducer || !RegisterWorker(manager, producerPrefab)
            || farmerPrefab == null || !farmerPrefab.IsFarmer || !RegisterWorker(manager, farmerPrefab)
            || minerPrefab == null || !minerPrefab.IsMiner || !RegisterWorker(manager, minerPrefab)
            || horsePrefab == null || !horsePrefab.IsHorse || !horsePrefab.TryGetComponent<CartHorse>(out _)
            || !RegisterWorker(manager, horsePrefab)
            || residentPrefab == null || !residentPrefab.IsVillager || !RegisterWorker(manager, residentPrefab)
            || fieldPrefab == null || !RegisterPrefab(manager, fieldPrefab.gameObject)
            || ironVeinPrefab == null || !RegisterPrefab(manager, ironVeinPrefab.gameObject)
            || timberTreePrefab == null || !RegisterPrefab(manager, timberTreePrefab.gameObject)
            || stoneDepositPrefab == null || !RegisterPrefab(manager, stoneDepositPrefab.gameObject)
            || !RegisterPrefab(manager, buildingPrefab.gameObject) || database == null || catalog == null) return false;
        var ids = new HashSet<int>();
        foreach (var entry in catalog)
        {
            if (entry == null || !ids.Add(entry.id) || entry.prefab == null || FindDefinition(entry.id) == null
                || !RegisterPrefab(manager, entry.prefab.gameObject)) return false;
            if (entry.prefab.TryGetComponent<RtsNetworkBarracks>(out var barracks) && !barracks.RegisterPrefab(manager, out _)) return false;
        }
        return ids.Contains(HouseId) && ids.Contains(GranaryId) && ids.Contains(BarracksId)
            && ids.Contains(SawmillId) && ids.Contains(StoneQuarryId) && ids.Contains(MillId) && ids.Contains(BakeryId)
            && ids.Contains(FarmId) && ids.Contains(BlacksmithId) && ids.Contains(WorkshopId) && ids.Contains(MineId)
            && ids.Contains(ArcheryRangeId) && ids.Contains(StableId);
    }

    private static bool RegisterWorker(NetworkManager manager, RtsNetworkUnit prefab)
        => prefab.ValidatePrototype(out _) && RegisterPrefab(manager, prefab.gameObject);

    internal void UseSupplySmokeStock(bool stone)
    {
        // The material being tested is exhausted by the first construction.
        var definition = FindDefinition(stone ? StoneQuarryId : SawmillId);
        if (definition == null) return;
        var cost = BuildCost.GetMaterials(definition);
        if (stone) startingStone = cost[ResourceType.Stone];
        else startingWood = cost[ResourceType.Wood];
    }

    internal void UseBreadSmokeStock()
    {
        startingWheat = 6;
    }

    internal void UseEquipmentSmokeStock(bool spear = false) => startingIron = spear ? 1 : 3;
    internal void UseStableSmokeStock() => startingFood = 10;

    internal void UseFarmSmokeStock()
    {
        startingWheat = 0;
        testFieldHarvest = true;
    }

    private static bool RegisterPrefab(NetworkManager manager, GameObject prefab)
    {
        if (prefab.scene.IsValid() || prefab.GetComponent<NetworkObject>().PrefabIdHash == 0) return false;
        if (!manager.NetworkConfig.Prefabs.Contains(prefab)) manager.AddNetworkPrefab(prefab);
        return manager.NetworkConfig.Prefabs.Contains(prefab);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        var faction = GetComponent<FactionMember>();
        owner.Value = faction.PlayerId;
        warehouse = gameObject.AddComponent<WarehouseStorage>();
        var entrance = new GameObject("Warehouse service entrance");
        entrance.transform.SetParent(transform, false);
        entrance.transform.localPosition = Vector3.right * 1.8f;
        entrance.transform.localRotation = Quaternion.Euler(0, 90, 0);
        warehouseEntrance = gameObject.AddComponent<BuildingEntrance>();
        warehouseEntrance.doorPoint = entrance.transform;
        warehouse.AddResource(ResourceType.Wood, startingWood);
        warehouse.AddResource(ResourceType.Stone, startingStone);
        warehouse.AddResource(ResourceType.Wheat, startingWheat);
        warehouse.AddResource(ResourceType.Iron, startingIron);
        warehouse.AddResource(ResourceType.Gold, 15);
        warehouse.AddResource(ResourceType.Food, startingFood);
        SyncStock();
        createBuilder = true;
    }

    [ContextMenu("RTS/Place building (Play Mode)")]
    public void BeginPlacement() => BeginPlacement(TestBuildingId);

    // Persistent UI can target the station even though completed barracks are spawned at runtime.
    public void TrainAtBuiltBarracks() => TrainAtBuiltBarracks(TroopType.Swordsman);
    public void TrainSpearmanAtBuiltBarracks() => TrainAtBuiltBarracks(TroopType.Spearman);
    public void TrainCavalryAtBuiltBarracks() => TrainAtBuiltBarracks(TroopType.Cavalry);
    public void TrainArcherAtBuiltRange() => TrainAtBuilding(TroopType.Archer, ArcheryRangeId);

    private void TrainAtBuiltBarracks(TroopType kind) => TrainAtBuilding(kind, BarracksId);

    private void TrainAtBuilding(TroopType kind, int buildingId)
    {
        var session = RtsNetworkSession.Instance;
        if (!IsSpawned || session == null || !session.Ready || session.LocalPlayerId != PlayerId) return;
        foreach (var site in FindObjectsOfType<RtsNetworkBuildSite>())
        {
            if (!site.IsSpawned || !site.IsComplete || site.PlayerId != PlayerId || site.BuildingId != buildingId
                || !site.TryGetComponent<RtsNetworkBarracks>(out var barracks)) continue;
            barracks.RequestTraining(kind);
            return;
        }
        statusChanged.Invoke(buildingId == ArcheryRangeId ? "Нет готового стрельбища" : "Нет готовой казармы");
    }

    public void QueueSword() => QueueEquipment(ResourceType.Sword);
    public void QueueSpear() => QueueEquipment(ResourceType.Spear);
    public void QueueBow() => QueueEquipment(ResourceType.Bow);
    public void QueueShield() => QueueEquipment(ResourceType.Shield);

    public void QueueHorse()
    {
        var session = RtsNetworkSession.Instance;
        if (IsSpawned && session?.Ready == true && session.LocalPlayerId == PlayerId) QueueHorseServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void QueueHorseServerRpc(ServerRpcParams parameters = default)
    {
        ulong sender = parameters.Receive.SenderClientId;
        if (RtsNetworkSession.Instance?.CanCommand(sender, NetworkPlayerSlots.Side(PlayerId)) != true) return;
        foreach (var site in projects)
        {
            if (site == null || !site.IsSpawned || !site.TryQueueHorse()) continue;
            Reply(sender, "Лошадь поставлена в очередь конюшни");
            return;
        }
        Reply(sender, "Нет готовой конюшни с подъездом и свободным местом в очереди");
    }

    private void QueueEquipment(ResourceType item)
    {
        var session = RtsNetworkSession.Instance;
        if (IsSpawned && session != null && session.Ready && session.LocalPlayerId == PlayerId)
            QueueEquipmentServerRpc(item);
    }

    [ServerRpc(RequireOwnership = false)]
    private void QueueEquipmentServerRpc(ResourceType item, ServerRpcParams parameters = default)
    {
        ulong sender = parameters.Receive.SenderClientId;
        var session = RtsNetworkSession.Instance;
        if (session == null || !session.CanCommand(sender, NetworkPlayerSlots.Side(PlayerId))) return;
        if (item != ResourceType.Sword && item != ResourceType.Spear && item != ResourceType.Shield && item != ResourceType.Bow) return;
        int buildingId = item == ResourceType.Shield || item == ResourceType.Bow ? WorkshopId : BlacksmithId;
        foreach (var site in projects)
        {
            if (site == null || !site.IsSpawned || site.BuildingId != buildingId || !site.TryQueueEquipment(item)) continue;
            Reply(sender, "Заказ снаряжения добавлен");
            return;
        }
        Reply(sender, "Нет готового здания с подъездом и свободным местом в очереди");
    }

    public void BeginPlacement(int buildingId)
    {
        var session = RtsNetworkSession.Instance;
        if (!IsSpawned || session == null || !session.Ready || session.LocalPlayerId != PlayerId) return;
        if (buildingId != TestBuildingId && FindEntry(buildingId) == null) return;
        if (activePlacement != null) activePlacement.EndPlacement();
        selectedBuildingId = buildingId;
        rotationSteps = 0;
        RoadBuilder.CancelActivePlacement();
        activePlacement = this;
        statusChanged.Invoke("ЛКМ — разместить, R — повернуть, ПКМ — отменить");
    }

    private ObjectData FindDefinition(int id)
    {
        if (database == null || database.objectsData == null) return null;
        ObjectData match = null;
        foreach (var entry in database.objectsData)
        {
            if (entry == null || entry.ID != id) continue;
            if (match != null) return null;
            match = entry;
        }
        return match?.Prefab != null && match.Size.x > 0 && match.Size.y > 0 ? match : null;
    }

    private CatalogEntry FindEntry(int id)
    {
        if (catalog == null) return null;
        foreach (var entry in catalog) if (entry != null && entry.id == id) return entry;
        return null;
    }

    private void Update()
    {
        if (activePlacement != this || !IsSpawned || SettingsMenu.BlocksWorldInput) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { EndPlacement(); return; }
        if (Input.GetKeyDown(KeyCode.R)) rotationSteps = (rotationSteps + 1) % 4;
        if (Camera.main == null || ManagerGO.Instance == null) return;
        if (!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit, Mathf.Infinity,
            ManagerGO.Instance.Ground, QueryTriggerInteraction.Ignore))
        { if (preview != null) preview.SetActive(false); return; }
        var data = selectedBuildingId == TestBuildingId ? null : FindDefinition(selectedBuildingId);
        var center = SnapPoint(hit.point, data, rotationSteps);
        Vector2 size = SizeOf(data, rotationSteps);
        UpdatePreview(center, size, OnPlot(center, size) && !IsOccupied(center, size)
            && (!RequiresRoad(selectedBuildingId, data) || HasSupplyRoad(center, data, rotationSteps)));
        if (!Input.GetMouseButtonDown(0) || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
        EndPlacement();
        RequestBuildAt(selectedBuildingId, center, rotationSteps);
    }

    private void UpdatePreview(Vector3 point, Vector2 size, bool allowed)
    {
        if (preview == null)
        {
            preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            preview.name = "Placement preview";
            preview.GetComponent<Collider>().enabled = false;
        }
        preview.SetActive(true);
        preview.transform.position = point + Vector3.up * .12f;
        preview.transform.localScale = new Vector3(size.x, .08f, size.y);
        var colors = new MaterialPropertyBlock();
        var color = allowed ? Color.green : Color.red;
        colors.SetColor("_Color", color);
        colors.SetColor("_BaseColor", color);
        preview.GetComponent<Renderer>().SetPropertyBlock(colors);
    }

    private void EndPlacement()
    {
        if (activePlacement != this) return;
        activePlacement = null;
        placementEndedFrame = Time.frameCount;
        if (preview != null) { Destroy(preview); preview = null; }
    }

    public void RequestBuildAt(Vector3 point) => RequestBuildAt(TestBuildingId, point, 0);

    public void RequestBuildAt(int buildingId, Vector3 point, int quarterTurns)
    {
        if (IsSpawned && RtsNetworkSession.Instance != null && RtsNetworkSession.Instance.Ready)
            BuildServerRpc(buildingId, point, quarterTurns);
    }

    [ServerRpc(RequireOwnership = false)]
    private void BuildServerRpc(int id, Vector3 point, int turns, ServerRpcParams parameters = default)
    {
        ulong sender = parameters.Receive.SenderClientId;
        var session = RtsNetworkSession.Instance;
        if (session == null || !session.CanCommand(sender, NetworkPlayerSlots.Side(PlayerId))) return;
        if (Time.unscaledTime < nextRequest) return;
        nextRequest = Time.unscaledTime + .25f;

        if (!NetworkPlayerSlots.ValidPoint(point.x, point.y, point.z) || turns < 0 || turns > 3) return;
        bool test = id == TestBuildingId;
        var definition = test ? null : FindDefinition(id);
        var entry = test ? null : FindEntry(id);
        if (!test && (definition == null || entry == null))
        { Reply(sender, "Это здание ещё не подключено к сетевому строительству"); return; }
        var size = SizeOf(definition, turns);
        point = SnapPoint(point, definition, turns);
        if (!OnPlot(point, size))
        { Reply(sender, "Стройку можно поставить только за своим складом на отмеченном участке"); return; }
        if (IsSupplyBuilding(id) || id == FarmId)
        {
            foreach (var existing in projects)
                if (existing != null && existing.BuildingId == id)
                { Reply(sender, "На тестовом участке уже есть такое здание"); return; }
        }
        if (RequiresRoad(id, definition) && !HasSupplyRoad(point, definition, turns))
        { Reply(sender, "Зданию нужен подъезд к входу"); return; }
        if (builder == null || !builder.TryGetComponent<NavMeshAgent>(out var agent) || !agent.isOnNavMesh)
        { Reply(sender, "Нет доступного строителя"); return; }
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        var path = new NavMeshPath();
        if (IsOccupied(point, size) || !NavMesh.SamplePosition(point, out var hit, .6f, filter) || !OnPlot(hit.position, size)
            || !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete
            || new Vector2(hit.position.x - point.x, hit.position.z - point.z).sqrMagnitude > .01f)
        { Reply(sender, "Место занято или строитель не может подойти"); return; }

        Dictionary<ResourceType, int> cost;
        try { cost = test ? new Dictionary<ResourceType, int> { [ResourceType.Wood] = WoodCost } : BuildCost.GetMaterials(definition); }
        catch (System.ArgumentException) { Reply(sender, "Ошибка стоимости в каталоге"); return; }
        catch (System.OverflowException) { Reply(sender, "Ошибка стоимости в каталоге"); return; }
        var building = Instantiate(test ? buildingPrefab : entry.prefab, hit.position, Quaternion.Euler(0, turns * 90f, 0));
        var localSize = test ? new Vector2Int(3, 3) : definition.Size * (int)CellSize;
        if (!building.Prepare(PlayerId, id, cost, size, localSize, test ? 4f : PlacementSystem.StandardBuildTime, definition?.Prefab))
        {
            Destroy(building.gameObject);
            Reply(sender, "На складах не хватает свободных материалов");
            return;
        }
        if (RequiresRoad(id, definition)) building.SetSupplyStation(this);
        projects.Add(building);
        SyncStock();
        building.NetworkObject.Spawn(destroyWithScene: true);
        Reply(sender, "Материалы зарезервированы. Строитель заберёт их со склада");
    }

    internal static bool IsSupplyBuilding(int id) => id == SawmillId || id == StoneQuarryId || id == MineId;
    private static bool RequiresRoad(int id, ObjectData data) => IsSupplyBuilding(id) || id == FarmId
        || (data?.Prefab != null && data.Prefab.TryGetComponent<RecipeBuilding>(out var recipe)
            && !(recipe is TrainingEquipmentStore));

    private static bool HasSupplyRoad(Vector3 point, ObjectData definition, int turns)
    {
        var roads = RoadBuilder.Instance;
        if (roads == null || !roads.isActiveAndEnabled || definition == null) return false;
        var entrance = point + Quaternion.Euler(0, turns * 90f, 0) * Vector3.back * (definition.Size.y * CellSize * .5f + .7f);
        return roads.HasConnection(entrance, 1.25f, 2f);
    }

    internal void CreateSupplyDeposits(GameObject workplace, bool stone)
    {
        if (!IsServer || !BuildingRoadAccess.Allows(workplace)) return;
        if (stone)
        {
            var deposit = Instantiate(stoneDepositPrefab, transform.position + new Vector3(1.6f, .08f, 13.7f), Quaternion.identity);
            deposit.gameObject.tag = "StoneQuarry";
            var rock = deposit.gameObject.AddComponent<StoneQuarry>();
            rock.totalStoneCount = 20;
            FactionMember.Set(deposit.gameObject, Faction.Neutral);
            deposit.Spawn(destroyWithScene: true);
        }
        else
        {
            for (int index = 0; index < 3; index++)
            {
                var point = transform.position + new Vector3((index - 1) * 1.6f, .08f, 14.8f);
                var tree = Instantiate(timberTreePrefab, point, Quaternion.identity);
                tree.gameObject.tag = "Enemy";
                var resource = tree.gameObject.AddComponent<Unit>();
                resource.unitMaxHealth = 30;
                resource.woodReward = 10;
                FactionMember.Set(tree.gameObject, Faction.Neutral);
                tree.Spawn(destroyWithScene: true);
            }
        }
    }

    internal RtsNetworkFarmField CreateFarmField(FarmHouse farm)
    {
        if (!IsServer || !BuildingRoadAccess.Allows(farm)) return null;
        // The test map reserves a field near the settlement, separate from buildable plots and roads.
        var field = Instantiate(fieldPrefab, transform.position + new Vector3(1.6f, .08f, -8.2f), Quaternion.identity);
        field.Prepare(testFieldHarvest);
        field.NetworkObject.Spawn(destroyWithScene: true);
        return field;
    }

    internal RtsNetworkIronVein CreateIronVein()
    {
        // The prototype reserves a vein beyond each settlement's buildable strip.
        var point = transform.position + new Vector3(-1.5f, .08f, 15.2f);
        var deposit = Instantiate(ironVeinPrefab, point, Quaternion.identity);
        deposit.Prepare(PlayerId);
        deposit.NetworkObject.Spawn(destroyWithScene: true);
        return deposit;
    }

    public void HireCarrier() => HireWorker(WarehouseJobId);

    public void HireWorker(int buildingId)
    {
        if (IsSpawned && RtsNetworkSession.Instance != null && RtsNetworkSession.Instance.Ready)
            HireWorkerServerRpc(buildingId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void HireWorkerServerRpc(int buildingId, ServerRpcParams parameters = default)
    {
        ulong sender = parameters.Receive.SenderClientId;
        var side = NetworkPlayerSlots.Side(PlayerId);
        if (RtsNetworkSession.Instance == null || !RtsNetworkSession.Instance.CanCommand(sender, side)) return;
        RtsNetworkBuildSite site = null;
        GameObject workplace;
        RtsNetworkUnit prefab;
        ResourcesMove.UnitRole? role = null;
        if (buildingId == WarehouseJobId)
        {
            carriers.RemoveAll(unit => unit == null || !unit.IsSpawned);
            if (carriers.Count >= 4) { Reply(sender, "На складе нет свободных мест"); return; }
            workplace = warehouse.gameObject;
            prefab = carrierPrefab;
        }
        else
        {
            site = projects.Find(project => project != null && project.IsComplete && project.BuildingId == buildingId && project.WorkerCount == 0);
            workplace = site != null ? site.Workplace : null;
            if (workplace == null) { Reply(sender, "Нет готового здания со свободным местом"); return; }
            switch (buildingId)
            {
                case SawmillId: prefab = lumberjackPrefab; break;
                case StoneQuarryId: prefab = stoneMasonPrefab; break;
                case MineId: prefab = minerPrefab; break;
                case FarmId:
                    if (site.AssignedField == null) return;
                    prefab = farmerPrefab;
                    break;
                default:
                    if (!workplace.TryGetComponent<RecipeBuilding>(out var recipe))
                    { Reply(sender, "Для этого здания найм ещё не подключён"); return; }
                    prefab = producerPrefab;
                    role = recipe.Profession;
                    break;
            }
        }
        if (!FactionMember.AreAllies(gameObject, workplace) || !BuildingRoadAccess.Allows(workplace))
        { Reply(sender, "До рабочего места нет подъезда"); return; }

        var resident = FindResident(side, workplace);
        if (resident == null) { Reply(sender, "Нет свободного жителя, который может дойти до здания"); return; }
        var worker = Instantiate(prefab, resident.transform.position, resident.transform.rotation);
        var workerRole = worker.GetComponent<ResourcesMove>();
        if (role.HasValue) workerRole.role = role.Value;
        FactionMember.Set(worker.gameObject, side);
        var previousHealth = resident.GetComponent<Unit>();
        worker.GetComponent<Unit>().SetInitialHealthRatio(previousHealth.unitHealth / Mathf.Max(1, previousHealth.unitMaxHealth));
        worker.NetworkObject.Spawn(destroyWithScene: true);
        workerRole.AssignWorkplace(workplace);
        if (worker.IsFarmer) worker.gameObject.AddComponent<Farmer>().AssignField(site.AssignedField);
        var home = HousesScript.FindHouseOf(resident.gameObject);
        if (home != null) { home.Evict(resident.gameObject); home.TrySettle(worker.gameObject); }
        resident.NetworkObject.Despawn();
        if (site != null) site.AssignWorker(worker);
        else
        {
            carriers.Add(worker);
            carrierCount.Value = carriers.Count;
        }
        Reply(sender, "Житель назначен и идёт к месту работы");
    }

    private static bool IsAvailableResident(RtsNetworkUnit unit, Faction side)
        => unit != null && unit.IsSpawned && unit.IsVillager && FactionMember.Of(unit.gameObject) == side
            && unit.TryGetComponent<ResourcesMove>(out var role) && role.IsUnemployed && role.CanChangeRole
            && unit.GetComponent<Unit>().unitHealth > 0;

    private static RtsNetworkUnit FindResident(Faction side, GameObject workplace)
    {
        float nearest = float.PositiveInfinity;
        RtsNetworkUnit result = null;
        foreach (var unit in FindObjectsOfType<RtsNetworkUnit>())
        {
            if (!IsAvailableResident(unit, side) || !unit.TryGetComponent<NavMeshAgent>(out var agent)
                || !agent.enabled || !agent.isOnNavMesh || agent.hasPath || agent.pathPending) continue;
            float distance = (unit.transform.position - workplace.transform.position).sqrMagnitude;
            if (distance >= nearest || !BuildingRoadAccess.TryWorkplacePath(workplace, agent, out _, out _)) continue;
            nearest = distance;
            result = unit;
        }
        return result;
    }

    private static Vector2 SizeOf(ObjectData definition, int turns)
    {
        if (definition == null) return new Vector2(2.4f, 2.4f);
        var size = (Vector2)definition.Size * CellSize;
        return turns % 2 == 0 ? size : new Vector2(size.y, size.x);
    }

    private Vector3 SnapPoint(Vector3 point, ObjectData definition, int turns)
    {
        if (definition == null) return point;
        Vector2 size = SizeOf(definition, turns);
        Vector3 corner = transform.position + new Vector3(-2.5f, 0, 4f);
        point.x = corner.x + Mathf.Round((point.x - corner.x - size.x * .5f) / CellSize) * CellSize + size.x * .5f;
        point.z = corner.z + Mathf.Round((point.z - corner.z - size.y * .5f) / CellSize) * CellSize + size.y * .5f;
        return point;
    }

    private bool OnPlot(Vector3 point, Vector2 size)
    {
        var offset = point - transform.position;
        return NetworkPlayerSlots.ValidPoint(point.x, point.y, point.z)
            && Mathf.Abs(offset.x) + size.x * .5f <= 2.5f && offset.z - size.y * .5f >= 4f
            && offset.z + size.y * .5f <= 14f && Mathf.Abs(offset.y) <= .6f;
    }

    private bool IsOccupied(Vector3 point, Vector2 size)
    {
        var footprint = new Bounds(point, new Vector3(size.x, 1f, size.y));
        if (RoadBuilder.Instance != null && RoadBuilder.Instance.OverlapsRoad(footprint)) return true;
        IEnumerable<RtsNetworkBuildSite> visibleProjects = IsServer ? projects : FindObjectsOfType<RtsNetworkBuildSite>();
        foreach (var project in visibleProjects)
        {
            if (project == null) continue;
            var bounds = project.Footprint;
            if (Mathf.Abs(bounds.center.x - point.x) < bounds.extents.x + size.x * .5f
                && Mathf.Abs(bounds.center.z - point.z) < bounds.extents.z + size.y * .5f) return true;
        }
        return Physics.CheckBox(point + Vector3.up, new Vector3(size.x * .5f - .05f, .75f, size.y * .5f - .05f),
            Quaternion.identity, Physics.DefaultRaycastLayers | (1 << 2), QueryTriggerInteraction.Ignore);
    }

    private void Reply(ulong client, string message) => StatusClientRpc(message,
        new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { client } } });

    [ClientRpc]
    private void StatusClientRpc(string message, ClientRpcParams parameters = default)
    {
        statusChanged.Invoke(message);
        Debug.Log("[RTS Construction] " + message, this);
    }

    private void LateUpdate()
    {
        if (!IsSpawned || !IsServer) return;
        if (createBuilder)
        {
            createBuilder = false;
            var agent = builderPrefab.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (NavMesh.SamplePosition(transform.position + Vector3.back * 3f, out var start, 1f, filter))
            {
                builder = Instantiate(builderPrefab, start.position + Vector3.up * agent.baseOffset, Quaternion.identity);
                FactionMember.Set(builder.gameObject, NetworkPlayerSlots.Side(PlayerId));
                builder.NetworkObject.Spawn(destroyWithScene: true);
            }
            SpawnStartingResidents();
        }
        if (Time.unscaledTime >= nextPopulationScan)
        {
            nextPopulationScan = Time.unscaledTime + .25f;
            int total = 0;
            var side = NetworkPlayerSlots.Side(PlayerId);
            foreach (var unit in FindObjectsOfType<RtsNetworkUnit>())
                if (IsAvailableResident(unit, side)) total++;
            availableResidents.Value = total;
            carriers.RemoveAll(unit => unit == null || !unit.IsSpawned);
            carrierCount.Value = carriers.Count;
        }
        SyncStock();
    }

    private void SpawnStartingResidents()
    {
        var agent = residentPrefab.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        for (int i = 0; i < 6; i++)
        {
            var point = transform.position + new Vector3(i % 2 == 0 ? -1.55f : 1.55f, 0, -11f + i / 2 * 2.5f);
            if (!NavMesh.SamplePosition(point, out var hit, 1f, filter)) continue;
            var resident = Instantiate(residentPrefab, hit.position + Vector3.up * agent.baseOffset, Quaternion.identity);
            FactionMember.Set(resident.gameObject, NetworkPlayerSlots.Side(PlayerId));
            resident.NetworkObject.Spawn(destroyWithScene: true);
        }
    }

    private void SyncStock()
    {
        stock.Value = warehouse.GetStoredAmount(ResourceType.Wood);
        stoneStock.Value = warehouse.GetStoredAmount(ResourceType.Stone);
        wheatStock.Value = warehouse.GetStoredAmount(ResourceType.Wheat);
        flourStock.Value = warehouse.GetStoredAmount(ResourceType.Flour);
        breadStock.Value = warehouse.GetStoredAmount(ResourceType.Bread);
        ironStock.Value = warehouse.GetStoredAmount(ResourceType.Iron);
        goldStock.Value = warehouse.GetStoredAmount(ResourceType.Gold);
        swordStock.Value = warehouse.GetStoredAmount(ResourceType.Sword);
        spearStock.Value = warehouse.GetStoredAmount(ResourceType.Spear);
        bowStock.Value = warehouse.GetStoredAmount(ResourceType.Bow);
        shieldStock.Value = warehouse.GetStoredAmount(ResourceType.Shield);
        reserved.Value = warehouse.ReservedMaterial(ResourceType.Wood);
        cargo.Value = builder != null && builder.TryGetComponent<GettingResources>(out var backpack)
            ? backpack.GetStoredAmount(ResourceType.Wood) : 0;
    }

    public override void OnNetworkDespawn()
    {
        EndPlacement();
        if (warehouseEntrance != null)
        {
            Destroy(warehouseEntrance.doorPoint.gameObject);
            Destroy(warehouseEntrance);
        }
        if (warehouse != null) { warehouse.enabled = false; Destroy(warehouse); }
        base.OnNetworkDespawn();
    }
}
