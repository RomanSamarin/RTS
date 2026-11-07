using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using _Game.Scripts.UI;
using UnityEngine.EventSystems;

/// <summary>Житель + доставленное снаряжение → обученный солдат. Кнопки прежние.</summary>
[RequireComponent(typeof(TrainingEquipmentStore))]
[DefaultExecutionOrder(-50)]
public class Barracks : MonoBehaviour
{
    [System.Serializable]
    public class UnitType
    {
        public string unitName = "Солдат";
        public GameObject unitPrefab;
        [Min(0.1f)] public float spawnDelay = 35f;
        public TroopType troopType;
        // Старые сериализованные цены сохранены для совместимости и больше не используются.
        [HideInInspector] public int woodCost, stoneCost, wheatCost, fishCost;
    }
    public GameObject objectToSpawn;
    public GameObject SpawnPoint;
    public LayerMask Ground;
    public GameObject CollectedPoint;
    public bool IsCollectedPoint;
    [Header("Сборный пункт")]
    [Min(0.1f)] public float rallySnapRadius = 2f;
    public Vector3 markerOffset = new Vector3(0f, 0.1f, 0f);
    [SerializeField] private bool hasRallyPoint;
    [SerializeField] private Vector3 rallyPoint;
    private GameObject rallyMarker;
    private float markerBottomOffset;
    private bool cursorWasVisible;
    private int rallyStartedFrame;
    private static Barracks pickingRally;
    private static int rallyEndedFrame = -1;
    public bool HasRallyPoint => hasRallyPoint;
    public Vector3 RallyPoint => rallyPoint;
    public static bool BlocksWorldInput => pickingRally != null || rallyEndedFrame == Time.frameCount;
    public static bool IsChoosingRallyPoint => pickingRally != null;
    public static void CancelActiveRallySelection() { if(pickingRally!=null)pickingRally.CancelRallySelection(); }
    [Min(0.1f)] public float spawnDelay = 35f;
    public TroopType troopType;
    [Min(1f)] public float horseRecruitRadius = 12f;
    [Min(0f)] public float recruitRadius;
    [Header("Роспуск")]
    public GameObject villagerPrefab;
    [Min(1f)] public float disbandRadius = 15f;
    [HideInInspector] public int woodCost, stoneCost, wheatCost, fishCost;
    public List<UnitType> unitTypes = new List<UnitType>();
    private class Order
    {
        public int id;
        public GameObject prefab;
        public TrainingRecruit recruit;
        public TroopType kind;
        public CartHorse horse;
        public Dictionary<ResourceType, int> kit;
        public float duration, elapsed;
    }
    private readonly List<Order> orders = new List<Order>();
    private TrainingEquipmentStore equipment;
    private int nextId;
    public bool IsTraining { get; private set; }
    public int QueueCount => orders.Count;
    public float TrainingProgress => orders.Count > 0 ? Mathf.Clamp01(orders[0].elapsed / orders[0].duration) : 0f;
    public string Status { get; private set; } = "Нет заказа";
    public string LastError { get; private set; } = "";
    public TrainingEquipmentStore Equipment => equipment;
    public struct TrainingOrderView
    {
        public int id;
        public string title;
        public TrainingRecruit recruit;
        public float elapsed, duration;
        public bool active;
    }
    public List<TrainingOrderView> GetTrainingQueue()
    {
        var result = new List<TrainingOrderView>();
        for (int i = 0; i < orders.Count; i++)
        {
            var order = orders[i];
            result.Add(new TrainingOrderView { id = order.id, title = order.prefab != null ? order.prefab.name : "Солдат",
                recruit = order.recruit, elapsed = order.elapsed, duration = order.duration, active = i == 0 });
        }
        return result;
    }
    public GameObject GetVillagerPrefab()
    {
        if (villagerPrefab != null) return villagerPrefab;
        foreach (var spawner in FindObjectsOfType<UnitSpawningVillage>())
            if (spawner.UnitToSpawn != null && spawner.UnitToSpawn.GetComponent<Unit>() != null
                && spawner.UnitToSpawn.GetComponent<NavMeshAgent>() != null) return spawner.UnitToSpawn;
        return null;
    }
    public bool TryDisband(GameObject soldier)
    {
        if (soldier == null || soldier.GetComponent<ResourcesMove>() == null
            || soldier.GetComponent<ResourcesMove>().role != ResourcesMove.UnitRole.Soldier) return Fail("Выберите солдата.");
        var service = soldier.GetComponent<SoldierService>();
        if (service == null) service = soldier.AddComponent<SoldierService>();
        if (!service.TryDisband(this)) return Fail(service.LastError);
        LastError = ""; return true;
    }
    public void DisbandSelectedSoldier()
    {
        var selected = ManagerGO.Instance != null ? ManagerGO.Instance.unitsSelected : null;
        if (selected == null || selected.Count != 1) { Fail("Выберите одного солдата для роспуска."); return; }
        TryDisband(selected[0]);
    }
    private void Awake()
    {
        equipment = GetComponent<TrainingEquipmentStore>();
        if (equipment == null) equipment = gameObject.AddComponent<TrainingEquipmentStore>();
    }
    public void OnButtonSpawnUnit() { TryEnqueue(objectToSpawn, spawnDelay, troopType); }
    public void TrainUnit(int index)
    {
        if (unitTypes == null || index < 0 || index >= unitTypes.Count || unitTypes[index] == null)
        { Fail("Неверный тип солдата."); return; }
        var type = unitTypes[index]; TryEnqueue(type.unitPrefab, type.spawnDelay, type.troopType);
    }
    public bool TryTrainUnit(int index)
    {
        if (unitTypes == null || index < 0 || index >= unitTypes.Count || unitTypes[index] == null) return Fail("Неверный тип солдата.");
        var type = unitTypes[index]; return TryEnqueue(type.unitPrefab, type.spawnDelay, type.troopType);
    }
    private bool TryEnqueue(GameObject prefab, float duration, TroopType kind)
    {
        if(!BuildingRoadAccess.Allows(this))return Fail("Нет дороги к зданию.");
        if (!isActiveAndEnabled || equipment == null || !equipment.isActiveAndEnabled) return Fail("Казарма недоступна.");
        if (orders.Count >= 100) return Fail("Очередь заполнена.");
        if (prefab == null || prefab.GetComponent<Unit>() == null || prefab.GetComponent<NavMeshAgent>() == null)
            return Fail("Префаб солдата должен содержать Unit и NavMeshAgent на корневом объекте.");
        var citizen = FindRecruit();
        if (citizen == null) return Fail("Нет свободного жителя с доступным путём в казарму.");
        if (kind == TroopType.Auto)
        {
            var profile = prefab.GetComponent<TroopCombat>();
            kind = profile != null && profile.troopType != TroopType.Auto ? profile.troopType : prefab.GetComponent<Archer>() != null ? TroopType.Archer : TroopType.Swordsman;
        }
        if ((kind == TroopType.Archer) != (prefab.GetComponent<Archer>() != null))
            return Fail("Компонент Archer должен быть только на префабе лучника.");
        if (prefab.GetComponent<CartHorse>() != null) return Fail("Назначьте префаб всадника, а не отдельной лошади CartHorse.");
        CartHorse horse = null;
        if (kind == TroopType.Cavalry)
        {
            foreach (var candidate in CartHorse.All)
                if (candidate.IsAlive && FactionMember.CanUse(candidate.gameObject,gameObject) && !candidate.IsHitched && candidate.TrainingOwner == null
                    && Vector3.Distance(candidate.transform.position, transform.position) <= horseRecruitRadius)
                { horse = candidate; break; }
            if (horse == null) return Fail("Приведите свободную лошадь к казарме.");
        }
        var kit = TrainingLoadout.Get(kind);
        if (!equipment.TryReserveKit(kit)) return Fail("Нет доступного снаряжения: мечнику нужны меч и щит, копейщику — копьё, лучнику — лук.");
        var recruit = citizen.GetComponent<TrainingRecruit>();
        if (recruit == null) recruit = citizen.gameObject.AddComponent<TrainingRecruit>();
        if (!recruit.Reserve(this)) { equipment.ReleaseKit(kit); return Fail("Житель уже занят другим заказом."); }
        if (horse != null) horse.TrainingOwner = this;
        orders.Add(new Order { id = ++nextId, prefab = prefab, recruit = recruit, kit = kit, kind = kind, horse = horse, duration = Mathf.Max(0.1f, duration) });
        LastError = ""; Status = "Ждёт жителя и снаряжение"; return true;
    }
    private ResourcesMove FindRecruit()
    {
        ResourcesMove best = null; float distance = float.PositiveInfinity;
        foreach (var candidate in FindObjectsOfType<ResourcesMove>())
        {
            if(!FactionMember.CanUse(candidate.gameObject,gameObject))continue;
            if (!candidate.isActiveAndEnabled || !candidate.IsUnemployed || !candidate.CanChangeRole || candidate.AssignedWorkplace != null) continue;
            var unit = candidate.GetComponent<Unit>();
            var agent = candidate.GetComponent<NavMeshAgent>();
            var command = candidate.GetComponent<GOScript>();
            if (unit == null || !unit.enabled || unit.unitHealth <= 0 || agent == null || !agent.enabled || !agent.isOnNavMesh) continue;
            if (command != null && (command.isCommandToMove || command.CurrentOrder == MovementOrderState.Order.Combat)) continue;
            float d = Vector3.Distance(transform.position, candidate.transform.position);
            if (d >= distance || (recruitRadius > 0 && d > recruitRadius) || !Approach(agent, out var point)) continue;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(point, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            best = candidate; distance = d;
        }
        return best;
    }
    public bool Approach(NavMeshAgent agent, out Vector3 point)
    {
        point = BuildingEntrance.TryGetWorldPoint(gameObject, out var door) ? door : transform.position;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(point, out var hit, 3f, filter)) return false;
        point = hit.position; return true;
    }
    public bool IsCurrentRecruit(TrainingRecruit recruit) => orders.Count > 0 && orders[0].recruit == recruit;
    // UI обрабатывается в Update; выбор точки проверяем после него.
    private void LateUpdate() {
        if(SaveGameManager.IsLoading)return; UpdateRallySelection(); }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        IsTraining = false;
        for (int i = orders.Count - 1; i >= 0; i--)
        {
            var recruit = orders[i].recruit;
            var unit = recruit != null ? recruit.GetComponent<Unit>() : null;
            var worker = recruit != null ? recruit.GetComponent<ResourcesMove>() : null;
            var cargo = recruit != null ? recruit.GetComponent<GettingResources>() : null;
            bool lostHorse = orders[i].kind == TroopType.Cavalry && (orders[i].horse == null || !orders[i].horse.IsAlive || orders[i].horse.TrainingOwner != this);
            if (lostHorse || recruit == null || unit == null || unit.unitHealth <= 0 || recruit.Owner != this
                || worker == null || worker.role != ResourcesMove.UnitRole.Villager || (cargo != null && cargo.HasCargo)) CancelAt(i);
        }
        if(!BuildingRoadAccess.Allows(this)){Status="Нет дороги к зданию";return;}
        if (orders.Count == 0) { Status = "Нет заказа"; return; }
        var order = orders[0];
        if (order.prefab == null) { CancelAt(0); return; }
        if (equipment == null || !equipment.isActiveAndEnabled) { Status = "Нет доступа к снаряжению"; return; }
        if (!order.recruit.gameObject.activeInHierarchy || !order.recruit.enabled || !order.recruit.Arrived)
        { Status = "Ждёт прибытия жителя"; return; }
        if (!equipment.HasKit(order.kit)) { Status = "Ждёт доставки снаряжения"; return; }
        if (order.horse != null && Vector3.Distance(order.horse.transform.position, transform.position) > horseRecruitRadius)
        { Status = "Ждёт возвращения лошади"; return; }
        IsTraining = true; Status = "Обучается";
        order.elapsed += Time.deltaTime;
        if (order.elapsed >= order.duration && Complete(order)) orders.RemoveAt(0);
    }
    private bool Complete(Order order)
    {
        var citizen = order.recruit.gameObject;
        Vector3 position = SpawnPoint != null ? SpawnPoint.transform.position : citizen.transform.position;
        // Это замена модели существующего человека, а не создание нового населения.
        if (!UnitModelTransfer.TryPrepare(citizen, order.prefab, ResourcesMove.UnitRole.Soldier, position, out var soldier))
        { Status = "Нет доступного выхода"; IsTraining = false; return false; }
        var cargo = soldier.GetComponent<GettingResources>();
        if (!equipment.ConsumeKit(order.kit)) { Destroy(soldier); return false; }
        foreach (var item in order.kit) cargo.AddStoredAmount(item.Key, item.Value);
        var service = soldier.GetComponent<SoldierService>();
        if (service == null) service = soldier.AddComponent<SoldierService>();
        service.villagerPrefab = GetVillagerPrefab();
        var combat = soldier.GetComponent<TroopCombat>();
        if (combat == null) combat = soldier.AddComponent<TroopCombat>();
        combat.Configure(order.kind);
        if (order.horse != null) combat.Mount(order.horse);
        order.recruit.Release(); UnitModelTransfer.Commit(citizen, soldier, true);
        SendToRallyPoint(soldier);
        return true;
    }
    public void CancelCurrentTraining() { if (orders.Count > 0) CancelAt(0); }
    public bool CancelOrder(int id)
    {
        int index = orders.FindIndex(order => order.id == id);
        if (index < 0) return false;
        CancelAt(index); return true;
    }
    public bool CancelRecruit(TrainingRecruit recruit)
    {
        int index = orders.FindIndex(order => order.recruit == recruit);
        if (index < 0) return false;
        CancelAt(index); return true;
    }
    public void CancelTraining(int index) { if (index >= 0 && index < orders.Count) CancelAt(index); }
    public void CancelAllTraining() { for (int i = orders.Count - 1; i >= 0; i--) CancelAt(i); }
    private void CancelAt(int index)
    {
        var order = orders[index];
        if (order.horse != null && order.horse.TrainingOwner == this) order.horse.TrainingOwner = null;
        if (equipment != null) equipment.ReleaseKit(order.kit);
        if (order.recruit != null) order.recruit.Release();
        orders.RemoveAt(index);
        if (index == 0) { IsTraining = false; Status = orders.Count > 0 ? "Ждёт жителя и снаряжение" : "Нет заказа"; }
    }
    private bool Fail(string message) { LastError = message; Debug.LogWarning("[Barracks] " + name + ": " + message, this); return false; }
    private void OnDisable()
    {
        IsTraining = false;
        CancelRallySelection();
        if (rallyMarker != null) rallyMarker.SetActive(false);
    }
    private void OnEnable() { if (rallyMarker != null) ShowRallyMarker(rallyPoint, hasRallyPoint); }
    private void OnDestroy()
    {
        CancelAllTraining();
        CancelRallySelection();
        if (rallyMarker != null) Destroy(rallyMarker);
    }
    // Старое имя сохранено: кнопка пользователя уже вызывает этот метод.
    public void CollectedPoints()
    {
        RoadBuilder.CancelActivePlacement();
        if (!isActiveAndEnabled) return;
        if (PlacementSystem.Instance != null && PlacementSystem.Instance.IsPlacing)
        { Fail("Сначала завершите размещение здания."); return; }
        if (SoldiersFormation.IsPlacing)
        { Fail("Сначала завершите размещение построения."); return; }
        if (pickingRally != null) pickingRally.CancelRallySelection();
        cursorWasVisible = Cursor.visible;
        pickingRally = this;
        Cursor.visible = false;
        IsCollectedPoint = true;
        rallyStartedFrame = Time.frameCount;
        LastError = "";
    }

    public void CancelRallySelection()
    {
        IsCollectedPoint = false;
        if (pickingRally != this) return;
        pickingRally = null;
        Cursor.visible = cursorWasVisible;
        rallyEndedFrame = Time.frameCount;
        ShowRallyMarker(rallyPoint, hasRallyPoint);
    }

    public void ClearRallyPoint()
    {
        hasRallyPoint = false;
        CancelRallySelection();
        if (rallyMarker != null) rallyMarker.SetActive(false);
    }

    private void UpdateRallySelection()
    {
        if (pickingRally != this) return;
        Cursor.visible = false;
        if ((PlacementSystem.Instance != null && PlacementSystem.Instance.IsPlacing) ||
            Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        { CancelRallySelection(); return; }
        if (Time.frameCount == rallyStartedFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        var camera = Camera.main;
        int mask = Ground.value;
        if (mask == 0 && ManagerGO.Instance != null) mask = ManagerGO.Instance.Ground.value;
        if (mask == 0) mask = LayerMask.GetMask("Ground");
        bool valid = false;
        Vector3 point = default;
        if (camera != null && Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition), out var hit,
            1000f, mask, QueryTriggerInteraction.Ignore))
        {
            var prefabAgent = objectToSpawn != null ? objectToSpawn.GetComponent<NavMeshAgent>() : null;
            var filter = new NavMeshQueryFilter { agentTypeID = prefabAgent != null ? prefabAgent.agentTypeID : 0,
                areaMask = (prefabAgent != null ? prefabAgent.areaMask : NavMesh.AllAreas) & ~WallNavigation.AreaMask };
            valid = NavMesh.SamplePosition(hit.point, out var navHit, Mathf.Max(0.1f, rallySnapRadius), filter);
            if (valid) point = navHit.position;
        }
        ShowRallyMarker(point, valid);
        if (!Input.GetMouseButtonDown(0)) return;
        if (!valid) { Fail("Выберите точку на доступной поверхности NavMesh."); return; }
        rallyPoint = point;
        hasRallyPoint = true;
        LastError = "";
        CancelRallySelection();
    }

    private void ShowRallyMarker(Vector3 point, bool visible)
    {
        if (rallyMarker == null && visible && CollectedPoint != null)
        {
            // Каждый экземпляр казармы получает собственный визуальный маркер.
            rallyMarker = Instantiate(CollectedPoint, point + markerOffset, CollectedPoint.transform.rotation);
            rallyMarker.name = name + " — сборный пункт";
            foreach (var child in rallyMarker.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
            foreach (var collider in rallyMarker.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var obstacle in rallyMarker.GetComponentsInChildren<NavMeshObstacle>(true)) obstacle.enabled = false;
            foreach (var body in rallyMarker.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.detectCollisions = false; }
            // Pivot куба находится в центре: совмещаем с землёй низ модели,
            // а не её центр. Для флажков с pivot у основания это тоже работает.
            rallyMarker.SetActive(true);
            float bottom = float.PositiveInfinity;
            foreach (var renderer in rallyMarker.GetComponentsInChildren<Renderer>())
                if (renderer.enabled) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
            markerBottomOffset = bottom < float.PositiveInfinity ? rallyMarker.transform.position.y - bottom : 0f;
        }
        if (rallyMarker == null) return;
        if (visible) rallyMarker.transform.position = point + markerOffset + Vector3.up * markerBottomOffset;
        rallyMarker.SetActive(visible);
    }

    private void SendToRallyPoint(GameObject soldier)
    {
        if (!hasRallyPoint) return;
        var agent = soldier.GetComponent<NavMeshAgent>();
        var command = soldier.GetComponent<GOScript>();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || command == null) return;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask & ~WallNavigation.AreaMask };
        var path = new NavMeshPath();
        if (!NavMesh.SamplePosition(rallyPoint, out var hit, Mathf.Max(0.1f, rallySnapRadius), filter) ||
            !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
        { Fail("Солдат обучен, но путь к сборному пункту недоступен."); return; }
        command.MoveTo(hit.position);
    }
}
