using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using _Game.Scripts.UI;

[RequireComponent(typeof(Barracks), typeof(NetworkObject), typeof(FactionMember))]
public sealed class RtsNetworkBarracks : NetworkBehaviour
{
    [SerializeField, Min(0)] private int startingKits = 1;
    [SerializeField] private RtsNetworkUnit villagerPrefab;
    [SerializeField] private bool spawnStarter = true;
    public UnityEvent<string> statusChanged = new UnityEvent<string>();
    public string Status { get; private set; } = "";

    private readonly NetworkVariable<int> owner = new NetworkVariable<int>();
    private readonly NetworkVariable<int> queue = new NetworkVariable<int>();
    private readonly NetworkVariable<float> progress = new NetworkVariable<float>();
    private readonly NetworkVariable<int> swords = new NetworkVariable<int>();
    private readonly NetworkVariable<int> spears = new NetworkVariable<int>();
    private readonly NetworkVariable<int> bows = new NetworkVariable<int>();
    private readonly NetworkVariable<int> shields = new NetworkVariable<int>();
    private Barracks barracks;
    private TrainingEquipmentStore store;
    private float nextRequest;
    private bool awaitingStarter;

    public int PlayerId => owner.Value;
    public int QueueCount => queue.Value;
    public float Progress => progress.Value;
    public int SwordCount => swords.Value;
    public int SpearCount => spears.Value;
    public int BowCount => bows.Value;
    public int ShieldCount => shields.Value;

    private void Awake()
    {
        barracks = GetComponent<Barracks>();
        store = GetComponent<TrainingEquipmentStore>();
        barracks.enabled = false;
        store.enabled = false;
    }

    public bool RegisterPrefab(NetworkManager manager, out string error)
    {
        error = name + ": проверьте владельца и сетевые префабы обучения";
        var config = GetComponent<Barracks>();
        if (!FactionRules.IsPlayerSide(GetComponent<FactionMember>().Side) || config.unitTypes.Count == 0) return false;
        var configured = new HashSet<TroopType>();
        foreach (var recipe in config.unitTypes)
        {
            if (recipe == null || recipe.unitPrefab == null || recipe.unitPrefab.scene.IsValid()
                || !recipe.unitPrefab.TryGetComponent<RtsNetworkUnit>(out var unit) || !unit.IsCombatUnit
                || recipe.troopType != unit.MilitaryType || !configured.Add(recipe.troopType) || !unit.ValidatePrototype(out _)
                || recipe.unitPrefab.GetComponent<NetworkObject>().PrefabIdHash == 0) return false;
            if (!manager.NetworkConfig.Prefabs.Contains(recipe.unitPrefab)) manager.AddNetworkPrefab(recipe.unitPrefab);
            if (unit.IsArcher)
            {
                var projectile = unit.GetComponent<Archer>()?.arrowPrefab;
                if (projectile == null || projectile.gameObject.scene.IsValid()
                    || !projectile.TryGetComponent<NetworkObject>(out var arrow) || arrow.PrefabIdHash == 0) return false;
                if (!manager.NetworkConfig.Prefabs.Contains(arrow.gameObject)) manager.AddNetworkPrefab(arrow.gameObject);
            }
        }
        if (!(configured.SetEquals(new[] { TroopType.Archer })
            || configured.SetEquals(new[] { TroopType.Swordsman, TroopType.Spearman, TroopType.Cavalry }))) return false;
        if (villagerPrefab == null || !villagerPrefab.IsVillager || villagerPrefab.gameObject.scene.IsValid()
            || !villagerPrefab.ValidatePrototype(out _) || villagerPrefab.GetComponent<NetworkObject>().PrefabIdHash == 0) return false;
        if (!manager.NetworkConfig.Prefabs.Contains(villagerPrefab.gameObject)) manager.AddNetworkPrefab(villagerPrefab.gameObject);
        error = null;
        return true;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        if (TryGetComponent<RtsNetworkBuildSite>(out _)) return;
        Activate();
    }

    internal void Activate()
    {
        owner.Value = GetComponent<FactionMember>().PlayerId;
        store.enabled = true;
        barracks.enabled = true;
        if (RtsNetworkSession.Instance?.SmokeTrainingKits == true
            && barracks.unitTypes.Exists(recipe => recipe.troopType == TroopType.Swordsman))
        {
            int kits = startingKits + RtsNetworkSession.Instance.SmokeExtraTrainingKits;
            store.AddStartingStock(ResourceType.Sword, kits);
            store.AddStartingStock(ResourceType.Shield, kits);
        }
        awaitingStarter = spawnStarter;
        SyncState();
    }

    [ContextMenu("RTS/Train swordsman (Play Mode)")]
    public void TrainSwordsman() => RequestTraining(TroopType.Swordsman);

    [ContextMenu("RTS/Train spearman (Play Mode)")]
    public void TrainSpearman() => RequestTraining(TroopType.Spearman);

    [ContextMenu("RTS/Train cavalry (Play Mode)")]
    public void TrainCavalry() => RequestTraining(TroopType.Cavalry);

    [ContextMenu("RTS/Train archer (Play Mode)")]
    public void TrainArcher() => RequestTraining(TroopType.Archer);

    internal void RequestTraining(TroopType kind)
    {
        if (IsSpawned && RtsNetworkSession.Instance != null && RtsNetworkSession.Instance.Ready)
            TrainServerRpc(kind);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TrainServerRpc(TroopType kind, ServerRpcParams parameters = default)
    {
        ulong sender = parameters.Receive.SenderClientId;
        var session = RtsNetworkSession.Instance;
        if (!barracks.isActiveAndEnabled || session == null || !session.CanCommand(sender, NetworkPlayerSlots.Side(PlayerId))) return;
        if (Time.unscaledTime < nextRequest) return;
        int recipe = barracks.unitTypes.FindIndex(entry => entry != null && entry.troopType == kind);
        if (recipe < 0) return;
        nextRequest = Time.unscaledTime + .25f;
        string message = barracks.TryTrainUnit(recipe) ? $"{barracks.unitTypes[recipe].unitName} поставлен в очередь" : barracks.LastError;
        SyncState();
        StatusClientRpc(message, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } } });
    }

    [ClientRpc]
    private void StatusClientRpc(string message, ClientRpcParams parameters = default)
    {
        Status = message;
        statusChanged.Invoke(message);
    }

    private void LateUpdate()
    {
        if (!IsSpawned || !IsServer || !barracks.isActiveAndEnabled) return;
        if (awaitingStarter)
        {
            awaitingStarter = false;
            var agent = villagerPrefab.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (NavMesh.SamplePosition(transform.position + Vector3.forward * 7f, out var hit, 1f, filter))
            {
                var citizen = Instantiate(villagerPrefab, hit.position + Vector3.up * agent.baseOffset, Quaternion.identity);
                FactionMember.Set(citizen.gameObject, NetworkPlayerSlots.Side(PlayerId));
                citizen.NetworkObject.Spawn(destroyWithScene: true);
            }
            else Debug.LogError("Нет NavMesh у стартового жителя " + name, this);
        }
        SyncState();
    }

    private void SyncState()
    {
        queue.Value = barracks.QueueCount;
        progress.Value = Mathf.Round(barracks.TrainingProgress * 100) / 100;
        swords.Value = store.LocalAmount(ResourceType.Sword);
        spears.Value = store.LocalAmount(ResourceType.Spear);
        bows.Value = store.LocalAmount(ResourceType.Bow);
        shields.Value = store.LocalAmount(ResourceType.Shield);
    }

    internal bool TryComplete(TrainingRecruit recruit, GameObject prefab, Dictionary<ResourceType, int> kit, Vector3 exit, CartHorse horse)
    {
        if (!IsSpawned || !IsServer || !recruit.TryGetComponent<RtsNetworkUnit>(out var citizen)
            || !citizen.IsSpawned || !citizen.IsVillager || citizen.PlayerId != PlayerId) return false;
        RtsNetworkUnit mount = null;
        if (prefab.GetComponent<RtsNetworkUnit>().IsCavalry
            && (horse == null || !horse.IsAlive || horse.IsHitched || horse.TrainingOwner != barracks
                || !horse.TryGetComponent(out mount) || !mount.IsHorse || !mount.IsSpawned || mount.PlayerId != PlayerId))
            return false;

        var agent = prefab.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(exit, out var hit, 1f, filter)) return false;
        foreach (var entity in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            if (entity == citizen.NetworkObject || (mount != null && entity == mount.NetworkObject)
                || !entity.TryGetComponent<NavMeshAgent>(out var other)) continue;
            var offset = entity.transform.position - hit.position;
            if (new Vector2(offset.x, offset.z).sqrMagnitude < Mathf.Pow(agent.radius + other.radius + .2f, 2)) return false;
        }

        var soldier = Instantiate(prefab, hit.position + Vector3.up * agent.baseOffset, citizen.transform.rotation);
        FactionMember.Set(soldier, NetworkPlayerSlots.Side(PlayerId));
        var residentHealth = citizen.GetComponent<Unit>();
        soldier.GetComponent<Unit>().SetInitialHealthRatio(residentHealth.unitHealth / Mathf.Max(1, residentHealth.unitMaxHealth));
        if (!store.ConsumeKit(kit)) { Destroy(soldier); return false; }
        var gear = soldier.AddComponent<GettingResources>();
        foreach (var item in kit) gear.AddStoredAmount(item.Key, item.Value);
        // Keep the original horse on the host, while the free-horse replica disappears on clients.
        if (mount != null) mount.NetworkObject.Despawn(destroy: false);
        soldier.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
        if (mount != null)
        {
            FactionMember.Set(horse.gameObject, NetworkPlayerSlots.Side(PlayerId));
            soldier.GetComponent<TroopCombat>().Mount(horse);
        }
        recruit.Release();
        citizen.NetworkObject.Despawn();
        return true;
    }

    internal void CancelOnDisconnect()
    {
        if (!IsSpawned || !IsServer) return;
        barracks.CancelAllTraining();
        SyncState();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer) barracks.CancelAllTraining();
        barracks.enabled = false;
        store.enabled = false;
        base.OnNetworkDespawn();
    }
}
