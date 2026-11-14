using UnityEngine;

/// <summary>A produced hull. Crew, sailing, trade and boarding are separate systems.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Unit), typeof(GettingResources))]
public class ShipHull : MonoBehaviour
{
    public ShipType type;
    [Min(1)] public float length = 8;
    [Min(.5f)] public float width = 3;
    [Min(.1f)] public float minimumDepth = 1.2f;
    [Min(.5f)] public float clearanceHeight = 6;
    public int CargoCapacity => type == ShipType.Trade ? 200 : 0;
    public int TransportCapacity => type == ShipType.Transport ? 16 : 0;
    public int ArcherPositions => type == ShipType.Warship ? 6 : 0;
    public int RequiredCrew => 2;
    public Port RepairPort{get;private set;}
    internal void SetRepairPort(Port yard){RepairPort=yard;}
    public string Status => sinking ? "Тонет" : GetComponent<ShipNavigation>()?.Status ?? "Ожидает приказ";
    public ShipProvisions Provisions => GetComponent<ShipProvisions>();
    public ShipCargo Cargo => GetComponent<ShipCargo>();
    [Header("Потопление")]
    [Min(.1f)] public float sinkingSeconds = 5f;
    private bool sinking;
    private float sinkingElapsed;
    private Vector3 sinkingOrigin;
    private Quaternion sinkingRotation;
    private float nextSalvageAt;
    private void Awake()
    {
        if (GetComponent<ShipProvisions>() == null) gameObject.AddComponent<ShipProvisions>();
        if (GetComponent<ShipCargo>() == null) gameObject.AddComponent<ShipCargo>();
        if (GetComponent<ShipNavigation>() == null) gameObject.AddComponent<ShipNavigation>();
        if (GetComponent<ShipCrew>() == null) gameObject.AddComponent<ShipCrew>();
        if (GetComponent<ShipTradeRoute>() == null) gameObject.AddComponent<ShipTradeRoute>();
        if (GetComponent<ShipTransport>() == null) gameObject.AddComponent<ShipTransport>();
        if (type == ShipType.Warship && GetComponent<ShipCombat>() == null) gameObject.AddComponent<ShipCombat>();
    }
    internal void BeginSinking()
    {
        if (sinking) return;
        sinking = true;
        sinkingOrigin = transform.position;
        sinkingRotation = transform.rotation;

        if (TryGetComponent<ShipTradeRoute>(out var route)) route.enabled = false;
        if (TryGetComponent<ShipNavigation>(out var navigation)) navigation.enabled = false;
        RepairPort?.CancelRepair();
        if (TryGetComponent<ShipProvisions>(out var provisions)) provisions.enabled = false;
        if (TryGetComponent<ShipTraffic>(out var traffic)) traffic.enabled = false;
        if (TryGetComponent<ShipCargo>(out var cargo)) cargo.enabled = false;
        if (TryGetComponent<AttackController>(out var attack)) attack.enabled = false;
        if (TryGetComponent<ShipCombat>(out var combat)) combat.enabled = false;
        FactionMember.Ensure(gameObject).RefreshControlRegistration();

        var wreck = ShipWreck.CreateAt(sinkingOrigin, sinkingRotation);
        var salvage = wreck.GetComponent<GettingResources>();
        LootTransfer.Move(GetComponent<GettingResources>(), salvage, int.MaxValue);
        if (TryGetComponent<ShipCrew>(out var crew))
        {
            crew.LoseCrew(salvage);
            crew.enabled = false;
        }
        if (TryGetComponent<ShipTransport>(out var transport))
        {
            transport.LosePassengers(salvage);
            transport.enabled = false;
        }
        wreck.KeepHalf();

        foreach (var animator in GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var body in GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var agent in GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) agent.enabled = false;
        foreach (var obstacle in GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true)) obstacle.enabled = false;
    }
    private void Update()
    {
        if (SaveGameManager.IsLoading) return;
        if (sinking)
        {
            sinkingElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(sinkingElapsed / Mathf.Max(.1f, sinkingSeconds));
            transform.SetPositionAndRotation(
                sinkingOrigin - Vector3.up * (Mathf.Max(2f, clearanceHeight) * progress * progress),
                sinkingRotation * Quaternion.Euler(12f * progress, 0f, 60f * Mathf.SmoothStep(0, 1, progress)));
            if (progress >= 1f) Destroy(gameObject);
            return;
        }

        var unit = GetComponent<Unit>();
        if (!unit.HasStarted) return;
        if (unit.unitHealth <= 0)
        {
            unit.Destroy();
            return;
        }
        if (type != ShipType.Trade || Time.time < nextSalvageAt || !ShipWreck.HasWrecks) return;
        nextSalvageAt = Time.time + .5f;
        // Leave cargo space available for orders at the berth and on automatic routes.
        if (GetComponent<ShipNavigation>()?.DestinationDock != null ||
            (GetComponent<ShipTradeRoute>()?.IsRunning ?? false)) return;
        ShipWreck.CollectNearby(this);
    }
    [ContextMenu("Потопить корабль (проверка)")]
    private void TestSinking()
    {
        if (Application.isPlaying) GetComponent<Unit>().Destroy();
    }
    public static void DefaultDimensions(ShipType type, out float width, out float length, out float depth)
    {
        width = type == ShipType.Trade ? 3 : 3.5f;
        length = type == ShipType.Trade ? 8 : type == ShipType.Transport ? 9 : 10;
        depth = type == ShipType.Warship ? 1.5f : 1.2f;
    }
}
