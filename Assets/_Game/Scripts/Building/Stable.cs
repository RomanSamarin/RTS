using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using _Game.Scripts.UI;

/// <summary>Конюшня: доставленные еда и золото → отдельная управляемая лошадь.</summary>
public class Stable : RecipeBuilding
{
    public GameObject horsePrefab;
    public Transform spawnPoint;
    [Min(1)] public int foodCost = 10;
    [Min(1)] public int goldCost = 5;
    [Min(0.1f)] public float horseSeconds = 45f;
    [Min(1)] public int maxQueue = 10;
    [Min(1)] public int maxNearbyHorses = 6;
    [Min(1f)] public float paddockRadius = 12f;
    [SerializeField] private int queuedHorses;
    [SerializeField] private float elapsed;
    [SerializeField] private List<EquipmentProductionBuilding.Stock> stock = new List<EquipmentProductionBuilding.Stock>();
    private struct Delivery { public ResourceType type; public int amount; }
    private readonly Dictionary<Object, Delivery> incoming = new Dictionary<Object, Delivery>();
    private static readonly ResourceType[] inputs = { ResourceType.Food, ResourceType.Gold };
    private static readonly ResourceType[] foods = { ResourceType.Food, ResourceType.Bread, ResourceType.Berries };
    private float nextSpawnAttempt;
    private System.Action<GameObject> spawnNetworkHorse;
    public string Status { get; private set; } = "Нет заказа";
    public int QueueCount => queuedHorses;
    public float Progress => queuedHorses > 0 ? Mathf.Clamp01(elapsed / Mathf.Max(0.1f, horseSeconds)) : 0f;
    public int StoredFood { get { int total = 0; foreach (var type in foods) total += Amount(type); return total; } }
    public int StoredGold => Amount(ResourceType.Gold);
    public override ResourceType InputType => ResourceType.Food;
    // Лошадь — объект мира, а не предмет в рюкзаке. Collect выдаёт только лишнее сырьё.
    public override ResourceType OutputType => ResourceType.Gold;
    public override ResourcesMove.UnitRole Profession => ResourcesMove.UnitRole.Horse;
    public override IReadOnlyList<ResourceType> InputTypes => inputs;
    public override int StoredInput => StoredFood + StoredGold;
    public override float WorkProgress => elapsed;
    private int Needed(ResourceType type) => queuedHorses <= 0 ? 0 : type == ResourceType.Food
        ? Mathf.Max(1, foodCost) : type == ResourceType.Gold ? Mathf.Max(1, goldCost) : 0;
    public override int StoredOutput => Mathf.Max(0, StoredFood - Needed(ResourceType.Food)) + Mathf.Max(0, StoredGold - Needed(ResourceType.Gold));
    public override int RequestedInput => RequestedInputFor(ResourceType.Food) + RequestedInputFor(ResourceType.Gold);
    public override bool CanProduce => isActiveAndEnabled && queuedHorses > 0 && ValidPrefab()
        && StoredFood >= Needed(ResourceType.Food) && StoredGold >= Needed(ResourceType.Gold);
    public override string WaitingTask => Status;

    private int Amount(ResourceType type)
    { int total = 0; foreach (var item in stock) if (item.type == type) total += item.amount; return total; }
    private void Add(ResourceType type, int amount)
    {
        foreach (var item in stock) if (item.type == type) { item.amount += amount; return; }
        stock.Add(new EquipmentProductionBuilding.Stock(type, amount));
    }
    private bool ValidPrefab() => horsePrefab != null && horsePrefab.GetComponent<CartHorse>() != null
        && horsePrefab.GetComponent<NavMeshAgent>() != null && horsePrefab.GetComponent<Unit>() != null
        && horsePrefab.GetComponent<WildlifeAnimal>() == null && horsePrefab.GetComponent<EnemyAi>() == null;
    public bool TryQueueHorse()
    {
        if(!BuildingRoadAccess.Allows(this)){Status="Нет дороги к зданию";return false;}
        if (!isActiveAndEnabled || !ValidPrefab()) { Status = "Назначьте префаб лошади с CartHorse, Unit и NavMeshAgent"; return false; }
        if (queuedHorses >= Mathf.Max(1, maxQueue)) { Status = "Очередь заполнена"; return false; }
        queuedHorses++;
        Status = "Ждёт доставки еды и золота";
        return true;
    }
    public void QueueHorse() { TryQueueHorse(); }
    internal void ConfigureNetworkStable(Stable definition, GameObject networkHorse, Transform exit,
        System.Action<GameObject> spawn)
    {
        horsePrefab = networkHorse;
        spawnPoint = exit;
        spawnNetworkHorse = spawn;
        foodCost = definition.foodCost;
        goldCost = definition.goldCost;
        horseSeconds = definition.horseSeconds;
        maxQueue = definition.maxQueue;
        maxNearbyHorses = definition.maxNearbyHorses;
        paddockRadius = definition.paddockRadius;
    }
    public void CancelCurrentHorse()
    {
        if (queuedHorses <= 0) return;
        queuedHorses--; elapsed = 0f; nextSpawnAttempt = 0f;
        Status = queuedHorses > 0 ? "Ждёт доставки еды и золота" : "Нет заказа";
    }
    public void CancelAllHorses() { queuedHorses = 0; elapsed = 0; nextSpawnAttempt = 0; Status = "Нет заказа"; }

    private int Pending(ResourceType type)
    {
        var expired = new List<Object>(); int total = 0;
        foreach (var pair in incoming)
            if (pair.Key == null) expired.Add(pair.Key);
            else if (pair.Value.type == type) total += pair.Value.amount;
        foreach (var owner in expired) incoming.Remove(owner);
        return total;
    }
    public override int RequestedInputFor(ResourceType type)
        => !ValidPrefab() ? 0 : Mathf.Max(0, Needed(type) - (type == ResourceType.Food ? StoredFood : Amount(type)) - Pending(type));
    public override int AvailableSupply(WarehouseStorage warehouse, ResourceType type)
        =>warehouse==null||!FactionMember.CanUse(gameObject,warehouse.gameObject)?0:type == ResourceType.Food ? warehouse.AvailableFood : base.AvailableSupply(warehouse, type);
    public override bool ReserveSupply(Object owner, WarehouseStorage warehouse, ResourceType type, int amount)
        =>warehouse!=null&&FactionMember.CanUse(gameObject,warehouse.gameObject)&&(type == ResourceType.Food ? warehouse.TryReserveFood(owner, amount) : base.ReserveSupply(owner, warehouse, type, amount));
    public override int PickupSupply(Object owner, WarehouseStorage warehouse, ResourceType type, int amount, GettingResources cargo)
        => type == ResourceType.Food ? warehouse.PickupReservedFood(owner, cargo) : base.PickupSupply(owner, warehouse, type, amount, cargo);
    public override void ReleaseSupply(Object owner, WarehouseStorage warehouse)
    { if (warehouse != null) { warehouse.ReleaseFood(owner); base.ReleaseSupply(owner, warehouse); } }
    public override bool ReserveDelivery(Object owner, int amount) => ReserveDelivery(owner, InputType, amount);
    public override bool ReserveDelivery(Object owner, ResourceType type, int amount)
    {
        if (!isActiveAndEnabled || owner == null || amount <= 0 || incoming.ContainsKey(owner) || RequestedInputFor(type) < amount) return false;
        incoming.Add(owner, new Delivery { type = type, amount = amount }); return true;
    }
    public override void CancelDelivery(Object owner) { incoming.Remove(owner); }
    public override int Receive(Object owner, GettingResources cargo)
    {
        if (cargo == null || !incoming.TryGetValue(owner, out var job)) return 0;
        incoming.Remove(owner);
        // Уже забранная поставка принимается и после отмены заказа, затем возвращается на склад.
        int remaining = Mathf.Min(job.amount, job.type == ResourceType.Food ? cargo.Food : cargo.GetStoredAmount(job.type));
        int taken = remaining;
        foreach (var type in job.type == ResourceType.Food ? foods : new[] { job.type })
        {
            int count = Mathf.Min(remaining, cargo.GetStoredAmount(type));
            cargo.RemoveStoredAmount(type, count); Add(type, count); remaining -= count;
        }
        return taken;
    }
    public override int CollectableOutput(WarehouseStorage destination)
        => destination != null && !destination.Accepts(ResourceType.Gold)
            ? Mathf.Max(0, StoredFood - Needed(ResourceType.Food)) : StoredOutput;

    public override int Collect(GettingResources cargo, int capacity, WarehouseStorage destination = null)
    {
        if (cargo == null) return 0;
        int foodExcess = Mathf.Max(0, StoredFood - Needed(ResourceType.Food));
        int taken = 0;
        foreach (var type in foods)
        {
            int count = Mathf.Min(Amount(type), Mathf.Min(foodExcess, Mathf.Max(0, capacity - taken)));
            Add(type, -count); cargo.AddStoredAmount(type, count); taken += count; foodExcess -= count;
        }
        int gold = destination != null && !destination.Accepts(ResourceType.Gold) ? 0
            : Mathf.Min(Mathf.Max(0, StoredGold - Needed(ResourceType.Gold)), Mathf.Max(0, capacity - taken));
        Add(ResourceType.Gold, -gold); cargo.AddStoredAmount(ResourceType.Gold, gold);
        return taken + gold;
    }
    // Конюшня работает по таймеру, как очередь обучения; профессия и житель не требуются.
    public override void Work(float seconds) { }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if(!BuildingRoadAccess.Allows(this)){Status="Нет дороги к зданию";return;}
        if (queuedHorses <= 0) { Status = "Нет заказа"; return; }
        if (!ValidPrefab()) { Status = "Не назначен корректный префаб лошади"; return; }
        if (!CanProduce) { Status = "Ждёт доставки еды и золота"; return; }
        int nearby = 0;
        foreach (var horse in CartHorse.All)
            if (horse != null && FactionMember.CanUse(gameObject,horse.gameObject)&&horse.IsAlive && !horse.IsHitched && horse.TrainingOwner == null &&
                Vector3.Distance(horse.transform.position, transform.position) <= paddockRadius) nearby++;
        if (nearby >= Mathf.Max(1, maxNearbyHorses)) { Status = "Загон заполнен: уведите свободных лошадей"; return; }
        Status = "Подготовка лошади";
        elapsed = Mathf.Min(Mathf.Max(0.1f, horseSeconds), elapsed + Time.deltaTime);
        if (elapsed < Mathf.Max(0.1f, horseSeconds) || Time.time < nextSpawnAttempt) return;
        nextSpawnAttempt = Time.time + 1f;
        if (!TrySpawn()) { Status = "Выход занят или недоступен на NavMesh"; return; }
        int remaining = Mathf.Max(1, foodCost);
        foreach (var type in foods) { int count = Mathf.Min(remaining, Amount(type)); Add(type, -count); remaining -= count; }
        Add(ResourceType.Gold, -Mathf.Max(1, goldCost));
        queuedHorses--; elapsed = 0f;
        Status = queuedHorses > 0 ? "Ждёт доставки еды и золота" : "Лошадь готова";
    }
    private bool TrySpawn()
    {
        var template = horsePrefab.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter { agentTypeID = template.agentTypeID, areaMask = template.areaMask & ~WallNavigation.AreaMask };
        Vector3 exit = spawnPoint != null ? spawnPoint.position : BuildingEntrance.TryGetWorldPoint(gameObject, out var door) ? door : transform.position + transform.forward * 4f;
        if (!NavMesh.SamplePosition(exit, out var hit, 2f, filter)) return false;
        foreach (var other in FindObjectsOfType<NavMeshAgent>())
            if (other.enabled && Mathf.Abs(other.transform.position.y - hit.position.y) < 2f &&
                Vector3.Distance(other.transform.position, hit.position) < template.radius + other.radius + 0.2f) return false;
        var horse = Instantiate(horsePrefab, hit.position, transform.rotation);
        if (spawnNetworkHorse == null) SaveGameManager.Stamp(horse,horsePrefab);
        FactionMember.Set(horse,FactionMember.Of(gameObject));
        horse.SetActive(true);
        horse.GetComponent<Unit>().SetInitialHealthRatio(1f);
        spawnNetworkHorse?.Invoke(horse);
        return true;
    }
    protected override void OnDisable() { incoming.Clear(); base.OnDisable(); }
}
