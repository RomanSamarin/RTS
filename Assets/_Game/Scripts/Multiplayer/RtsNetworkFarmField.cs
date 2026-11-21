using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class RtsNetworkFarmField : NetworkBehaviour
{
    [SerializeField] private GameObject crop;
    private readonly NetworkVariable<int> wheat = new NetworkVariable<int>();
    private readonly NetworkVariable<float> growth = new NetworkVariable<float>();
    internal FarmField Field { get; private set; }
    public int WheatRemaining => wheat.Value;
    public float Growth => growth.Value;
    public Bounds Footprint => new Bounds(transform.position, new Vector3(1.7f, 1f, 2f));

    internal void Prepare(bool smoke)
    {
        Field = gameObject.AddComponent<FarmField>();
        if (smoke) Field.ConfigureHarvest(6, 600);
    }

    public override void OnNetworkSpawn()
    {
        wheat.OnValueChanged += UpdateCrop;
        if (IsServer) SyncField();
        UpdateCrop(0, wheat.Value);
    }

    private void LateUpdate()
    {
        if (IsSpawned && IsServer) SyncField();
    }

    private void SyncField()
    {
        wheat.Value = Field.Remaining;
        growth.Value = Mathf.Round(Field.Growth01 * 100f) / 100f;
    }

    private void UpdateCrop(int previous, int current) => crop.SetActive(current > 0);

    public override void OnNetworkDespawn()
    {
        wheat.OnValueChanged -= UpdateCrop;
        if (Field != null) Field.enabled = false;
        base.OnNetworkDespawn();
    }
}
