using System;
using System.Net;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>Place on a scene object separate from NetworkManager. UI is wired manually.</summary>
[RequireComponent(typeof(NetworkObject))]
public sealed partial class RtsNetworkSession : NetworkBehaviour
{
    public static RtsNetworkSession Instance { get; private set; }
    public static bool BlocksOfflineCommands => Instance != null;
    public NetworkManager manager;
    public UnityTransport transport;
    [SerializeField] private RtsNetworkUnit reserveUnitPrefab;
    public string address = "127.0.0.1";
    public ushort port = 7777;
    public UnityEvent<string> statusChanged = new UnityEvent<string>();
    public UnityEvent<int> playerAssigned = new UnityEvent<int>();
    public string Status { get; private set; } = "Не подключено";
    public int LocalPlayerId { get; private set; }
    internal bool SmokeTrainingKits { get; set; }
    internal int SmokeExtraTrainingKits { get; set; }
    public bool Ready => IsSpawned && LocalPlayerId != 0 && manager != null && manager.IsConnectedClient;
    private readonly NetworkPlayerSlots slots = new NetworkPlayerSlots();
    private readonly Dictionary<int, RtsNetworkUnit> reserves = new Dictionary<int, RtsNetworkUnit>();
    private readonly Dictionary<ulong, float> nextSpawnRequest = new Dictionary<ulong, float>();
    private readonly Dictionary<ulong, float> nextFormationRequest = new Dictionary<ulong, float>();
    private bool used;
    private RoadStroke testRoad;
    private readonly List<MonoBehaviour> suspendedServices = new List<MonoBehaviour>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        networkRoads = new NetworkList<RtsNetworkRoadData>();
        Application.runInBackground = true;
    }
    private void Start() => SuspendOfflineServices();
    private void SuspendOfflineServices()
    {
        foreach (var script in FindObjectsOfType<MonoBehaviour>())
        {
            if (!script.enabled || !(script is SaveGameManager || script is RoadBuilder || script is RoadNavigation
                || script is BuildingSelectionManager || script is ShipTraffic)) continue;
            suspendedServices.Add(script);
            script.enabled = false;
            script.StopAllCoroutines();
        }
    }
    public void SetAddress(string value) => address = value.Trim();
    public void SetPort(string value)
    {
        if (ushort.TryParse(value, out var parsed) && parsed > 0) port = parsed;
        else Report("Порт должен быть от 1 до 65535");
    }
    [ContextMenu("RTS/Start host (Play Mode)")]
    public void StartHost() => Begin(true);
    [ContextMenu("RTS/Start client (Play Mode)")]
    public void StartClient() => Begin(false);
    private void Begin(bool host)
    {
        if (!Application.isPlaying) return;
        SuspendOfflineServices();
        if (used) { Report("Для новой сессии перезагрузите тестовую сцену"); return; }
        if (manager == null || transport == null || manager.gameObject == gameObject)
        { Report("Назначьте NetworkManager и UnityTransport на отдельном объекте"); return; }
        if (manager.IsListening) { Report("NetworkManager уже запущен"); return; }
        if (port == 0 || !IPAddress.TryParse(address, out _)) { Report("Укажите IP-адрес и порт 1–65535"); return; }
        if (FindObjectsOfType<RtsNetworkUnit>().Length == 0) { Report("В сцене нет RtsNetworkUnit"); return; }
        // Fail closed: this slice must not silently run an unsynchronised settlement.
        foreach (var script in FindObjectsOfType<MonoBehaviour>())
        {
            if (!script.isActiveAndEnabled || script.GetType().Assembly != typeof(Unit).Assembly) continue;
            if (script is RtsNetworkSession || script is RtsNetworkUnit || script is Unit || script is FactionMember
                || script is ManagerGO || script is UnitSelectionBox || script is CameraController
                || script is HealthTracker || script is FaceCamera || script is RtsNetworkSmokeProbe
                || script is RtsNetworkBarracks || script is RtsNetworkConstruction || script is SoldiersFormation
                || script is SelectedMilitaryCommandPanel) continue;
            Report("Используйте тестовую сцену движения: не синхронизирован " + script.GetType().Name); return;
        }
        foreach (var unit in FindObjectsOfType<RtsNetworkUnit>())
            if (!unit.ValidatePrototype(out var error)) { Report(error); return; }
        foreach (var unit in FindObjectsOfType<Unit>())
            if (unit.GetComponent<RtsNetworkUnit>() == null) { Report("Нет сетевого компонента на " + unit.name); return; }

        manager.NetworkConfig.NetworkTransport = transport;
        manager.NetworkConfig.ConnectionApproval = true;
        manager.NetworkConfig.EnableSceneManagement = true;
        manager.NetworkConfig.PlayerPrefab = null;
        manager.NetworkConfig.ProtocolVersion = 27;
        if (reserveUnitPrefab == null || reserveUnitPrefab.gameObject.scene.IsValid()
            || reserveUnitPrefab.GetComponent<NetworkObject>().PrefabIdHash == 0
            || !reserveUnitPrefab.ValidatePrototype(out _))
        { Report("Назначьте корректный сетевой префаб резервного юнита"); return; }
        if (!manager.NetworkConfig.Prefabs.Contains(reserveUnitPrefab.gameObject))
            manager.AddNetworkPrefab(reserveUnitPrefab.gameObject);
        foreach (var building in FindObjectsOfType<RtsNetworkBarracks>())
        {
            if (!building.RegisterPrefab(manager, out var error)) { Report(error); return; }
        }
        foreach (var construction in FindObjectsOfType<RtsNetworkConstruction>())
            if (!construction.RegisterPrefabs(manager))
            { Report("Проверьте сетевые префабы строительства: " + construction.name); return; }
        manager.ConnectionApprovalCallback += Approve;
        manager.OnClientConnectedCallback += Connected;
        manager.OnClientDisconnectCallback += Disconnected;
        transport.SetConnectionData(address, port, "0.0.0.0");
        slots.Clear();
        if (host) slots.TryJoin(Unity.Netcode.NetworkManager.ServerClientId, out _);
        used = true;
        Report(host ? "Запуск хоста…" : "Подключение…");
        try
        {
            if (!(host ? manager.StartHost() : manager.StartClient())) Report("Не удалось запустить сеть; перезагрузите сцену");
        }
        catch (Exception exception) { Report("Ошибка запуска: " + exception.Message); manager.Shutdown(); }
    }
    private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = slots.TryJoin(request.ClientNetworkId, out _);
        response.CreatePlayerObject = false;
        response.Pending = false;
        if (!response.Approved) response.Reason = "Все четыре места заняты";
    }
    public override void OnNetworkSpawn()
    {
        ConfigureRoadGround();
        if (RoadBuilder.Instance != null)
        {
            var roads = RoadBuilder.Instance;
            roads.FixedNetworkRoads = true;
            roads.enabled = true;
            var road = new GameObject("Starting Timber Road");
            road.transform.SetParent(transform, false);
            testRoad = road.AddComponent<RoadStroke>();
            roads.RestoreSavedRoad(testRoad, new[] { new Vector3(-12, .08f, 17), new Vector3(12, .08f, 17) }, 1.4f, false, false);
            var north = new GameObject("Farm Road");
            north.transform.SetParent(testRoad.transform, false);
            roads.RestoreSavedRoad(north.AddComponent<RoadStroke>(),
                new[] { new Vector3(-12, .08f, 29), new Vector3(12, .08f, 29) }, 1.4f, false, false);
        }
        networkRoads.OnListChanged += NetworkRoadChanged;
        RefreshNetworkRoads();
        if (IsHost) AssignLocal(1);
    }

    public override void OnNetworkDespawn()
    {
        TearDownNetworkRoads();
        if (testRoad != null) { testRoad.gameObject.SetActive(false); Destroy(testRoad.gameObject); }
        if (RoadBuilder.Instance != null)
        {
            RoadBuilder.Instance.enabled = false;
            RoadBuilder.Instance.FixedNetworkRoads = false;
        }
        base.OnNetworkDespawn();
    }
    private void Connected(ulong client)
    {
        if (!IsServer || !IsSpawned) return;
        if (client == Unity.Netcode.NetworkManager.ServerClientId) { AssignLocal(1); return; }
        int slot = slots.Get(client);
        if (slot == 0) { manager.DisconnectClient(client); return; }
        AssignPlayerClientRpc(slot, new ClientRpcParams
        { Send = new ClientRpcSendParams { TargetClientIds = new[] { client } } });
    }
    [ClientRpc]
    private void AssignPlayerClientRpc(int slot, ClientRpcParams parameters = default) => AssignLocal(slot);
    private void AssignLocal(int slot)
    {
        if (slot < 1 || slot > 4) return;
        LocalPlayerId = slot;
        FactionMember.SetLocalPlayer(NetworkPlayerSlots.Side(slot));
        playerAssigned.Invoke(slot);
        Report($"Подключено: игрок {slot}" + (IsHost ? " (хост)" : ""));
    }
    public bool CanCommand(ulong client, Faction owner) => IsServer && NetworkPlayerSlots.CanCommand(slots.Get(client), owner);

    internal const int MaxFormationUnits = 32;
    internal void RequestFormation(ulong[] units, Vector3[] positions, Vector3 facing)
    {
        if (Ready && units.Length > 0 && units.Length <= MaxFormationUnits)
            FormationServerRpc(units, positions, facing);
    }

    internal void RequestFormationMarch(ulong[] units, Vector3 center)
    {
        if (Ready && units.Length >= 2 && units.Length <= MaxFormationUnits)
            FormationMarchServerRpc(units, center);
    }

    internal void RequestDefenseFacing(ulong[] units, Vector3 point)
    {
        if (Ready && units.Length > 0 && units.Length <= MaxFormationUnits)
            DefenseFacingServerRpc(units, point);
    }

    [ServerRpc(RequireOwnership = false)]
    private void DefenseFacingServerRpc(ulong[] ids, Vector3 point, ServerRpcParams parameters = default)
    {
        ulong sender = parameters.Receive.SenderClientId;
        if (ids == null || ids.Length == 0 || ids.Length > MaxFormationUnits
            || !NetworkPlayerSlots.ValidPoint(point.x, point.y, point.z) || !TryReceiveFormationOrder(sender, out int slot)) return;

        var units = new RtsNetworkUnit[ids.Length];
        var selected = new HashSet<ulong>();
        var center = Vector3.zero;
        for (int i = 0; i < ids.Length; i++)
        {
            if (!selected.Add(ids[i]) || !TryGetFormationUnit(ids[i], slot, out units[i]) || !units[i].CanUseFormation)
            {
                Reply(sender, "Поворот обороны отклонён. Выберите своих бойцов ближнего боя.");
                return;
            }
            center += units[i].transform.position;
        }
        var facing = point - center / units.Length;
        facing.y = 0;
        if (facing.sqrMagnitude < .01f)
        {
            Reply(sender, "Выберите направление дальше от центра отряда.");
            return;
        }
        facing.Normalize();
        int group = FormationHold.BeginGroup();
        foreach (var unit in units) unit.SetDefenseFacing(facing, group);
    }

    private bool TryGetFormationUnit(ulong id, int slot, out RtsNetworkUnit unit)
    {
        unit = null;
        return manager.SpawnManager.SpawnedObjects.TryGetValue(id, out var entity)
            && entity.TryGetComponent(out unit) && unit.PlayerId == slot;
    }

    [ServerRpc(RequireOwnership = false)]
    private void FormationMarchServerRpc(ulong[] ids, Vector3 center, ServerRpcParams parameters = default)
    {
        ulong sender = parameters.Receive.SenderClientId;
        if (ids == null || ids.Length < 2 || ids.Length > MaxFormationUnits
            || !NetworkPlayerSlots.ValidPoint(center.x, center.y, center.z) || !TryReceiveFormationOrder(sender, out int slot)) return;
        var units = new List<GameObject>(ids.Length);
        var selected = new HashSet<ulong>();
        foreach (ulong id in ids)
        {
            if (!selected.Add(id) || !TryGetFormationUnit(id, slot, out var unit) || !unit.IsHoldingPosition)
            {
                Reply(sender, "Марш отклонён. Выберите бойцов одного строя.");
                return;
            }
            units.Add(unit.gameObject);
        }
        if (!SquadMovement.MoveNetworkFormation(units, center))
            Reply(sender, "Марш отклонён. Проверьте выделение и доступность пути.");
    }

    private bool TryReceiveFormationOrder(ulong sender, out int slot)
    {
        slot = slots.Get(sender);
        if (slot == 0 || (nextFormationRequest.TryGetValue(sender, out var next) && Time.unscaledTime < next)) return false;
        nextFormationRequest[sender] = Time.unscaledTime + .25f;
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void FormationServerRpc(ulong[] ids, Vector3[] positions, Vector3 facing, ServerRpcParams parameters = default)
    {
        ulong sender = parameters.Receive.SenderClientId;
        if (ids == null || positions == null || ids.Length == 0
            || ids.Length > MaxFormationUnits || ids.Length != positions.Length
            || !TryReceiveFormationOrder(sender, out int slot)) return;
        facing.y = 0;
        if (!NetworkPlayerSlots.ValidPoint(facing.x, 0, facing.z) || facing.sqrMagnitude < .001f) return;

        var units = new RtsNetworkUnit[ids.Length];
        var destinations = new Vector3[ids.Length];
        var paths = new NavMeshPath[ids.Length];
        var selected = new HashSet<ulong>();
        for (int i = 0; i < ids.Length; i++)
        {
            if (!selected.Add(ids[i]) || !TryGetFormationUnit(ids[i], slot, out units[i])
                || !units[i].TryPlanFormation(positions[i], out destinations[i], out paths[i]))
            {
                Reply(sender, "Построение отклонено. Проверьте выделение и доступность точек.");
                return;
            }
            for (int previous = 0; previous < i; previous++)
            {
                var gap = destinations[i] - destinations[previous];
                gap.y = 0;
                float spacing = Mathf.Max(1.4f, units[i].NavigationRadius + units[previous].NavigationRadius + .15f);
                if (gap.sqrMagnitude >= spacing * spacing) continue;
                Reply(sender, "Места построения слишком близко друг к другу.");
                return;
            }
        }
        int group = FormationHold.BeginGroup();
        facing.Normalize();
        for (int i = 0; i < units.Length; i++) units[i].BeginFormation(destinations[i], facing, group, paths[i]);
        Reply(sender, "Приказ построиться принят.");
    }

    [ContextMenu("RTS/Spawn reserve (Play Mode)")]
    public void SpawnReserveUnit()
    {
        if (Ready) SpawnReserveServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnReserveServerRpc(ServerRpcParams parameters = default)
    {
        ulong sender = parameters.Receive.SenderClientId;
        int slot = slots.Get(sender);
        if (slot == 0 || (nextSpawnRequest.TryGetValue(sender, out var next) && Time.unscaledTime < next)) return;
        nextSpawnRequest[sender] = Time.unscaledTime + .25f;
        if (reserves.TryGetValue(slot, out var existing) && existing != null && existing.IsSpawned)
        {
            Reply(sender, "Резервный юнит уже на карте");
            return;
        }

        var agent = reserveUnitPrefab.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        var entrance = new Vector3((slot - 2.5f) * 6f, 0, -5f);
        if (!NavMesh.SamplePosition(entrance, out var hit, 1f, filter))
        {
            Reply(sender, "На точке появления нет NavMesh");
            return;
        }
        foreach (var unit in manager.SpawnManager.SpawnedObjectsList)
        {
            if (!unit.TryGetComponent<NavMeshAgent>(out var other)) continue;
            var offset = unit.transform.position - hit.position;
            if (new Vector2(offset.x, offset.z).sqrMagnitude >= Mathf.Pow(agent.radius + other.radius + .2f, 2)) continue;
            Reply(sender, "Точка появления занята");
            return;
        }

        var reserve = Instantiate(reserveUnitPrefab, hit.position + Vector3.up * agent.baseOffset, Quaternion.identity);
        reserve.GetComponent<FactionMember>().SetFaction(NetworkPlayerSlots.Side(slot));
        reserve.NetworkObject.Spawn(destroyWithScene: true);
        reserves[slot] = reserve;
        Reply(sender, "Резервный юнит появился");
    }

    [ContextMenu("RTS/Remove selected reserve (Play Mode)")]
    public void RemoveSelectedReserveUnit()
    {
        if (!Ready || ManagerGO.Instance == null) return;
        foreach (var selected in ManagerGO.Instance.unitsSelected)
        {
            if (selected == null || !selected.TryGetComponent<RtsNetworkUnit>(out var unit)
                || !unit.IsSpawned || unit.NetworkObject.IsSceneObject != false || unit.PlayerId != LocalPlayerId) continue;
            RemoveReserveServerRpc(unit.EntityId);
            return;
        }
        Report("Выберите своего резервного юнита");
    }

    [ServerRpc(RequireOwnership = false)]
    private void RemoveReserveServerRpc(ulong entity, ServerRpcParams parameters = default)
    {
        ulong sender = parameters.Receive.SenderClientId;
        int slot = slots.Get(sender);
        if (!reserves.TryGetValue(slot, out var unit) || unit == null || !unit.IsSpawned
            || unit.EntityId != entity || !CanCommand(sender, NetworkPlayerSlots.Side(unit.PlayerId))) return;
        reserves.Remove(slot);
        unit.NetworkObject.Despawn();
        Reply(sender, "Резервный юнит удалён");
    }

    private void Reply(ulong client, string message) => ReserveStatusClientRpc(message, new ClientRpcParams
    { Send = new ClientRpcSendParams { TargetClientIds = new[] { client } } });

    [ClientRpc]
    private void ReserveStatusClientRpc(string message, ClientRpcParams parameters = default) => Report(message);

    private void Disconnected(ulong client)
    {
        if (IsServer)
        {
            int slot = slots.Get(client);
            if (reserves.TryGetValue(slot, out var unit))
            {
                reserves.Remove(slot);
                if (!manager.ShutdownInProgress && unit != null && unit.IsSpawned) unit.NetworkObject.Despawn();
            }
            nextSpawnRequest.Remove(client);
            nextFormationRequest.Remove(client);
            foreach (var building in FindObjectsOfType<RtsNetworkBarracks>())
                if (building.PlayerId == slot) building.CancelOnDisconnect();
            if (slot != 0 && !manager.ShutdownInProgress)
                foreach (var fighter in FindObjectsOfType<RtsNetworkUnit>())
                    if (fighter.PlayerId == slot) fighter.EndCombatOrders();
            slots.Leave(client);
        }
        if (manager != null && client == manager.LocalClientId)
        {
            LocalPlayerId = 0;
            ManagerGO.Instance?.DeselectAll();
            Report("Соединение закрыто. " + manager.DisconnectReason + " Перезагрузите тестовую сцену.");
        }
    }
    public void Disconnect()
    {
        LocalPlayerId = 0;
        ManagerGO.Instance?.DeselectAll();
        if (manager != null) manager.Shutdown();
        Report("Отключено; для новой сессии перезагрузите тестовую сцену");
    }
    private void Report(string message) { Status = message; Debug.Log("[RTS Network] " + message, this); statusChanged.Invoke(message); }
    public override void OnDestroy()
    {
        if (manager != null)
        {
            manager.ConnectionApprovalCallback -= Approve;
            manager.OnClientConnectedCallback -= Connected;
            manager.OnClientDisconnectCallback -= Disconnected;
        }
        if (Instance == this) { Instance = null; FactionMember.SetLocalPlayer(Faction.Player); }
        foreach (var script in suspendedServices) if (script != null) script.enabled = true;
        base.OnDestroy();
    }
}
