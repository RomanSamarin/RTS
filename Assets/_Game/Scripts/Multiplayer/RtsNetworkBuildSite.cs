using System.Collections.Generic;
using _Game.Scripts.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
public sealed class RtsNetworkBuildSite : NetworkBehaviour
{
    [SerializeField] private GameObject foundation;
    [SerializeField] private GameObject finishedBuilding;
    private readonly NetworkVariable<int> owner = new NetworkVariable<int>();
    private readonly NetworkVariable<int> wood = new NetworkVariable<int>();
    private readonly NetworkVariable<int> stone = new NetworkVariable<int>();
    private readonly NetworkVariable<int> gold = new NetworkVariable<int>();
    private readonly NetworkVariable<int> wheat = new NetworkVariable<int>();
    private readonly NetworkVariable<Vector2> footprintSize = new NetworkVariable<Vector2>();
    private readonly NetworkVariable<float> progress = new NetworkVariable<float>();
    private readonly NetworkVariable<bool> completed = new NetworkVariable<bool>();
    private readonly NetworkVariable<Vector2> health = new NetworkVariable<Vector2>();
    private BuildingHealth buildingHealth;
    private float preparedHealth = 500f;
    private GameObject serverBuilding;
    private ConstructionSite site;
    private int preparedSlot;
    private int preparedBuildingId;
    private int housingCapacity;
    private int storageCapacity;
    private readonly NetworkVariable<int> buildingId = new NetworkVariable<int>(-1);
    private readonly NetworkVariable<int> housing = new NetworkVariable<int>();
    private readonly NetworkVariable<int> granaryCapacity = new NetworkVariable<int>();
    private readonly NetworkVariable<int> sawmillWood = new NetworkVariable<int>();
    private readonly NetworkVariable<bool> roadConnected = new NetworkVariable<bool>();
    private readonly NetworkVariable<int> stoneHouseStock = new NetworkVariable<int>();
    private RtsNetworkConstruction supplyStation;
    private Sawmill sawmill;
    private StoneHouse stoneHouse;
    private MiningHouse mine;
    private RtsNetworkIronVein ironVein;
    private readonly NetworkVariable<int> mineIron = new NetworkVariable<int>();
    private BuildingRoadAccess supplyAccess;
    private RecipeBuilding recipeDefinition;
    private RecipeBuilding production;
    private readonly NetworkVariable<int> equipmentQueue = new NetworkVariable<int>();
    private readonly NetworkVariable<int> horseQueue = new NetworkVariable<int>();
    private readonly NetworkVariable<int> stableFood = new NetworkVariable<int>();
    private readonly NetworkVariable<int> stableGold = new NetworkVariable<int>();
    private FarmHouse farm;
    private RtsNetworkFarmField field;
    private readonly NetworkVariable<int> farmWheat = new NetworkVariable<int>();
    private readonly NetworkVariable<int> workers = new NetworkVariable<int>();
    private RtsNetworkUnit assignedWorker;
    private readonly NetworkVariable<int> recipeInput = new NetworkVariable<int>();
    private readonly NetworkVariable<int> recipeOutput = new NetworkVariable<int>();
    private readonly NetworkVariable<float> recipeProgress = new NetworkVariable<float>();

    public ulong EntityId => NetworkObjectId;
    public int PlayerId => owner.Value;
    public int DeliveredWood => wood.Value;
    public int DeliveredStone => stone.Value;
    public int DeliveredGold => gold.Value;
    public int DeliveredWheat => wheat.Value;
    public float Progress => progress.Value;
    public bool IsComplete => completed.Value;
    public float Health => health.Value.x;
    public float MaxHealth => health.Value.y;
    public int BuildingId => buildingId.Value;
    public int HousingCapacity => housing.Value;
    public int GranaryCapacity => granaryCapacity.Value;
    public int SawmillWood => sawmillWood.Value;
    public int StoneHouseStock => stoneHouseStock.Value;
    public int MineIron => mineIron.Value;
    public bool HasRoadAccess => roadConnected.Value;
    public int RecipeInput => recipeInput.Value;
    public int RecipeOutput => recipeOutput.Value;
    public float RecipeProgress => recipeProgress.Value;
    public int EquipmentQueueCount => equipmentQueue.Value;
    public int HorseQueueCount => horseQueue.Value;
    public int StableFood => stableFood.Value;
    public int StableGold => stableGold.Value;
    public int FarmWheat => farmWheat.Value;
    public int WorkerCount => workers.Value;
    internal GameObject Workplace => IsServer && IsComplete ? serverBuilding : null;
    internal FarmField AssignedField => field != null ? field.Field : null;

    internal bool TryQueueHorse() => IsServer && IsComplete && production is Stable stable && stable.TryQueueHorse();

    internal bool TryQueueEquipment(ResourceType output)
    {
        if (!IsServer || !IsComplete || !(production is EquipmentProductionBuilding equipment)
            || equipment.QueueCount >= 10 || !BuildingRoadAccess.Allows(equipment)) return false;
        for (int index = 0; index < equipment.Recipes.Count; index++)
        {
            var recipe = equipment.Recipes[index];
            if (recipe == null || recipe.producesUnit || recipe.output != output) continue;
            if (!equipment.Enqueue(index)) return false;
            equipmentQueue.Value = equipment.QueueCount;
            return true;
        }
        return false;
    }

    internal void AssignWorker(RtsNetworkUnit unit)
    {
        assignedWorker = unit;
        workers.Value = 1;
    }
    internal void SetSupplyStation(RtsNetworkConstruction station) => supplyStation = station;
    private Vector2 preparedFootprint;
    internal Bounds Footprint
    {
        get
        {
            Vector2 size = IsServer ? preparedFootprint : footprintSize.Value;
            return new Bounds(transform.position, new Vector3(size.x, 2, size.y));
        }
    }

    internal bool Prepare(int slot, int id, Dictionary<ResourceType, int> cost, Vector2 footprint,
        Vector2Int localSize, float seconds, GameObject sourcePrefab)
    {
        preparedSlot = slot;
        preparedBuildingId = id;
        preparedFootprint = footprint;
        if (sourcePrefab != null)
        {
            if (sourcePrefab.TryGetComponent<HousesScript>(out var residence)) housingCapacity = residence.MaxResidents;
            if (sourcePrefab.TryGetComponent<WarehouseStorage>(out var originalStore)) storageCapacity = originalStore.Capacity;
            sourcePrefab.TryGetComponent(out recipeDefinition);
            if (sourcePrefab.TryGetComponent<BuildingHealth>(out var originalHealth)) preparedHealth = originalHealth.MaxHealth;
        }
        if (TryGetComponent<FactionMember>(out var faction)) faction.SetFaction(NetworkPlayerSlots.Side(slot));
        serverBuilding = new GameObject("Server construction");
        serverBuilding.transform.SetPositionAndRotation(transform.position, transform.rotation);
        FactionMember.Set(serverBuilding, NetworkPlayerSlots.Side(slot));
        var obstacle = serverBuilding.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.up;
        obstacle.size = new Vector3(localSize.x, 2f, localSize.y);
        obstacle.carving = true;
        var door = new GameObject("Entrance");
        door.transform.SetParent(serverBuilding.transform, false);
        door.transform.localPosition = Vector3.back * (localSize.y * .5f + .7f);
        site = ConstructionSite.Begin(serverBuilding, seconds, localSize, null, createVisual: false);
        site.Materials = site.gameObject.AddComponent<ConstructionMaterials>();
        if (!site.Materials.Initialize(cost)) return false;
        site.Completed += Complete;
        return true;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            owner.Value = preparedSlot;
            buildingId.Value = preparedBuildingId;
            footprintSize.Value = preparedFootprint;
        }
        completed.OnValueChanged += SetVisual;
        FactionMember.Set(gameObject, NetworkPlayerSlots.Side(PlayerId));
        SetVisual(false, completed.Value);
    }

    private void LateUpdate()
    {
        if (!IsSpawned || !IsServer) return;
        if (workers.Value != 0 && (assignedWorker == null || !assignedWorker.IsSpawned)) workers.Value = 0;
        if (supplyAccess != null) roadConnected.Value = supplyAccess.IsConnected;
        if (sawmill != null) sawmillWood.Value = sawmill.storedWood;
        if (stoneHouse != null) stoneHouseStock.Value = stoneHouse.storedStone;
        if (mine != null) mineIron.Value = mine.StoredIron;
        if (farm != null) farmWheat.Value = farm.StoredWheat;
        if (production != null)
        {
            recipeInput.Value = production.StoredInput;
            recipeOutput.Value = production.StoredOutput;
            float duration = production.secondsPerBatch;
            if (production is EquipmentProductionBuilding equipment)
            {
                equipmentQueue.Value = equipment.QueueCount;
                duration = equipment.ActiveRecipe?.seconds ?? 1f;
            }
            else if (production is Stable stable)
            {
                horseQueue.Value = stable.QueueCount;
                stableFood.Value = stable.StoredFood;
                stableGold.Value = stable.StoredGold;
                duration = stable.horseSeconds;
            }
            recipeProgress.Value = Mathf.Clamp01(Mathf.Round(100f * production.WorkProgress / Mathf.Max(.1f, duration)) / 100f);
        }
        if (IsComplete || site == null) return;
        SyncDelivery(site);
        progress.Value = Mathf.Round(site.Progress01 * 100f) / 100f;
    }

    private void Complete(ConstructionSite completedSite)
    {
        SyncDelivery(completedSite);
        progress.Value = 1f;
        if (housingCapacity > 0)
        {
            var house = serverBuilding.AddComponent<HousesScript>();
            house.UnitHouse = housingCapacity;
            housing.Value = housingCapacity;
        }
        if (storageCapacity > 0)
        {
            var storage = serverBuilding.AddComponent<WarehouseStorage>();
            storage.SetCapacity(storageCapacity);
            if (preparedBuildingId == RtsNetworkConstruction.GranaryId) storage.LimitToFood();
            granaryCapacity.Value = storageCapacity;
        }
        if (TryGetComponent<RtsNetworkBarracks>(out var barracks)) barracks.Activate();
        if (supplyStation != null)
        {
            supplyAccess = serverBuilding.AddComponent<BuildingRoadAccess>();
            supplyAccess.connectionPoint = serverBuilding.transform.Find("Entrance");
            roadConnected.Value = supplyAccess.IsConnected;
            if (preparedBuildingId == RtsNetworkConstruction.FarmId)
            {
                farm = serverBuilding.AddComponent<FarmHouse>();
                field = supplyStation.CreateFarmField(farm);
            }
            else if (recipeDefinition is EquipmentProductionBuilding equipmentRecipe)
            {
                var equipment = (EquipmentProductionBuilding)serverBuilding.AddComponent(equipmentRecipe.GetType());
                equipment.ConfigureNetworkProduction(equipmentRecipe);
                production = equipment;
            }
            else if (recipeDefinition is Stable stableDefinition)
            {
                var stable = serverBuilding.AddComponent<Stable>();
                var exit = new GameObject("Horse Exit");
                exit.transform.SetParent(serverBuilding.transform, false);
                exit.transform.localPosition = Vector3.forward * 2.4f;
                stable.ConfigureNetworkStable(stableDefinition, supplyStation.HorsePrefab, exit.transform,
                    horse => horse.GetComponent<NetworkObject>().Spawn(destroyWithScene: true));
                production = stable;
            }
            else if (recipeDefinition != null)
            {
                var recipe = serverBuilding.AddComponent<RtsProductionRecipe>();
                recipe.Configure(recipeDefinition);
                production = recipe;
            }
            else if (preparedBuildingId == RtsNetworkConstruction.MineId)
            {
                mine = serverBuilding.AddComponent<MiningHouse>();
                ironVein = supplyStation.CreateIronVein();
                mine.AssignedNetworkDeposit = ironVein.Vein;
            }
            else
            {
                bool stoneSupply = preparedBuildingId == RtsNetworkConstruction.StoneQuarryId;
                serverBuilding.tag = stoneSupply ? "StoneHouse" : "Sawmill";
                if (stoneSupply) stoneHouse = serverBuilding.AddComponent<StoneHouse>();
                else sawmill = serverBuilding.AddComponent<Sawmill>();
                supplyStation.CreateSupplyDeposits(serverBuilding, stoneSupply);
            }
        }
        buildingHealth = gameObject.AddComponent<BuildingHealth>();
        buildingHealth.destroyOnDeath = false;
        buildingHealth.maxHealth = Mathf.Max(1f, preparedHealth);
        buildingHealth.RestoreFullHealth();
        SyncHealth(buildingHealth);
        buildingHealth.HealthChanged += SyncHealth;
        buildingHealth.Died += DespawnDestroyedBuilding;
        completed.Value = true;
    }

    private void SyncHealth(BuildingHealth current) => health.Value = new Vector2(current.CurrentHealth, current.MaxHealth);

    private void DespawnDestroyedBuilding(BuildingHealth current)
    {
        if (IsServer && IsSpawned) NetworkObject.Despawn();
    }

    internal void ReceiveMeleeHit(GameObject attacker, int damage)
    {
        if (IsServer && IsSpawned && IsComplete) buildingHealth?.TakeDamage(damage, attacker);
    }

    internal Vector3 MeleeContact(Vector3 from)
    {
        var point = Footprint.ClosestPoint(from);
        point.y = from.y;
        return point;
    }

    internal bool CanBeStruckFrom(Vector3 point, NavMeshAgent agent, float reach)
    {
        var contact = MeleeContact(point);
        var outward = point - contact;
        float distance = outward.magnitude;
        if (distance < .01f || distance > reach) return false;
        var probe = contact + outward / distance * Mathf.Min(distance, agent.radius + .25f);
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        return NavMesh.SamplePosition(probe, out var hit, .6f, filter)
            && !NavMesh.Raycast(point, hit.position, out _, filter);
    }

    internal bool TryApproachForMelee(NavMeshAgent agent, float reach, out NavMeshPath path)
    {
        path = null;
        float shortest = float.PositiveInfinity;
        var bounds = Footprint;
        var feet = agent.transform.position - Vector3.up * agent.baseOffset;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        float clearance = Mathf.Min(reach * .8f, agent.radius + .45f);
        var candidate = new NavMeshPath();
        for (int side = 0; side < 4; side++)
        {
            bool alongX = side < 2;
            bool positive = side % 2 == 0;
            for (int anchor = 0; anchor < 2; anchor++)
            {
                var point = MeleeContact(anchor == 0 ? feet : bounds.center);
                point.y = feet.y;
                if (alongX) point.x = positive ? bounds.max.x + clearance : bounds.min.x - clearance;
                else point.z = positive ? bounds.max.z + clearance : bounds.min.z - clearance;
                if (!NavMesh.SamplePosition(point, out var hit, .6f, filter) || !CanBeStruckFrom(hit.position, agent, reach)
                    || !agent.CalculatePath(hit.position, candidate) || candidate.status != NavMeshPathStatus.PathComplete) continue;
                float length = SquadMovementRules.Length(candidate.corners);
                if (length >= shortest) continue;
                path = candidate;
                shortest = length;
                candidate = new NavMeshPath();
            }
        }
        return path != null;
    }

    private void SyncDelivery(ConstructionSite current)
    {
        wood.Value = current.Materials.Delivered(ResourceType.Wood);
        stone.Value = current.Materials.Delivered(ResourceType.Stone);
        gold.Value = current.Materials.Delivered(ResourceType.Gold);
        wheat.Value = current.Materials.Delivered(ResourceType.Wheat);
    }

    private void SetVisual(bool previous, bool current)
    {
        foundation.SetActive(!current);
        finishedBuilding.SetActive(current);
    }

    public override void OnNetworkDespawn()
    {
        completed.OnValueChanged -= SetVisual;
        if (buildingHealth != null)
        {
            buildingHealth.HealthChanged -= SyncHealth;
            buildingHealth.Died -= DespawnDestroyedBuilding;
        }
        if (IsServer && field != null && field.IsSpawned && !NetworkManager.ShutdownInProgress)
            field.NetworkObject.Despawn();
        if (IsServer && ironVein != null && ironVein.IsSpawned && !NetworkManager.ShutdownInProgress)
            ironVein.NetworkObject.Despawn();
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        if (site != null) { site.Completed -= Complete; Destroy(site.gameObject); }
        if (serverBuilding != null) Destroy(serverBuilding);
        base.OnDestroy();
    }
}
