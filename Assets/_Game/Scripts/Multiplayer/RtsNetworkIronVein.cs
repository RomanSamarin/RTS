using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class RtsNetworkIronVein : NetworkBehaviour
{
    [SerializeField] private GameObject ore;
    private readonly NetworkVariable<int> settlement = new NetworkVariable<int>();
    private readonly NetworkVariable<int> remaining = new NetworkVariable<int>();
    private int preparedSettlement;
    internal IronVein Vein { get; private set; }

    public ulong EntityId => NetworkObjectId;
    public int PlayerId => settlement.Value;
    public int Remaining => remaining.Value;

    internal void Prepare(int playerId)
    {
        preparedSettlement = playerId;
        Vein = gameObject.AddComponent<IronVein>();
    }

    public override void OnNetworkSpawn()
    {
        remaining.OnValueChanged += SetOreVisible;
        if (IsServer)
        {
            settlement.Value = preparedSettlement;
            remaining.Value = Vein.Remaining;
        }
        SetOreVisible(0, Remaining);
    }

    private void LateUpdate()
    {
        if (IsSpawned && IsServer) remaining.Value = Vein.Remaining;
    }

    private void SetOreVisible(int previous, int current) => ore.SetActive(current > 0);

    public override void OnNetworkDespawn()
    {
        remaining.OnValueChanged -= SetOreVisible;
        if (Vein != null) Vein.enabled = false;
        base.OnNetworkDespawn();
    }
}
