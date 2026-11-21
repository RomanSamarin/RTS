using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

/// <summary>Снаряжение казармы: общий резерв заказов, реальные рейсы носильщиков, локальный запас.</summary>
[DisallowMultipleComponent]
public class TrainingEquipmentStore : RecipeBuilding
{
    private readonly Dictionary<ResourceType, int> required = new Dictionary<ResourceType, int>();
    private readonly Dictionary<ResourceType, int> stock = new Dictionary<ResourceType, int>();
    private readonly Dictionary<ResourceType, int> returned = new Dictionary<ResourceType, int>();
    public int ReturnedAmount(ResourceType type) => Count(returned, type);
    public bool AcceptReturnedCargo(GettingResources cargo)
    {
        if (!isActiveAndEnabled || cargo == null) return false;
        foreach (ResourceType type in WarehouseStorage.Types)
        {
            int amount = Mathf.Max(0, cargo.GetStoredAmount(type));
            returned[type] = ReturnedAmount(type) + amount; cargo.RemoveStoredAmount(type, amount);
        }
        return true;
    }
    private class Shipment { public WarehouseStorage warehouse; public ResourceType type; public int amount; public bool picked; }
    private readonly Dictionary<Object, Shipment> shipments = new Dictionary<Object, Shipment>();
    private readonly Dictionary<Object, Shipment> incoming = new Dictionary<Object, Shipment>();
    private float nextReserve;
    public override ResourceType InputType => ResourceType.Sword;
    public override ResourceType OutputType => ResourceType.Sword;
    public override ResourcesMove.UnitRole Profession => ResourcesMove.UnitRole.Villager;
    public override bool CanProduce => false;
    public override IReadOnlyList<ResourceType> InputTypes => new List<ResourceType>(required.Keys);
    private static int Count(Dictionary<ResourceType, int> items, ResourceType type) => items.TryGetValue(type, out int amount) ? amount : 0;
    public int LocalAmount(ResourceType type) => Count(stock, type);
    internal void AddStartingStock(ResourceType type, int amount) =>
        stock[type] = Mathf.Clamp(LocalAmount(type) + Mathf.Max(0, amount), 0, inputCapacity);
    public int RequiredAmount(ResourceType type) => Count(required, type);
    public override int StoredInput { get { int total = 0; foreach (var amount in stock.Values) total += amount; return total; } }
    public override int StoredOutput
    { get { int total = 0; foreach (var item in stock) total += Mathf.Max(0, item.Value - RequiredAmount(item.Key)); foreach (var amount in returned.Values) total += amount; return total; } }
    public override int RequestedInput
    { get { int total = 0; foreach (var type in InputTypes) total += RequestedInputFor(type); return total; } }
    private void Prune()
    {
        var expired = new List<Object>();
        foreach (var item in incoming) if (item.Key == null) expired.Add(item.Key);
        foreach (var owner in expired) incoming.Remove(owner);
        expired.Clear();
        foreach (var item in shipments) if (item.Key == null) expired.Add(item.Key);
        foreach (var owner in expired) shipments.Remove(owner);
    }
    private int Pending(ResourceType type, bool pickedOnly)
    {
        Prune(); int amount = 0;
        foreach (var item in incoming.Values) if (item.type == type && (!pickedOnly || item.picked)) amount += item.amount;
        return amount;
    }
    public override int RequestedInputFor(ResourceType type)
        => Mathf.Max(0, Mathf.Min(RequiredAmount(type), inputCapacity) - LocalAmount(type) - Pending(type, false));
    private IEnumerable<WarehouseStorage> Warehouses()
    {
        foreach (var warehouse in WarehouseStorage.All)
            if (warehouse != null && FactionMember.CanUse(gameObject,warehouse.gameObject)&&warehouse.isActiveAndEnabled && (supplyWarehouse == null || supplyWarehouse == warehouse)) yield return warehouse;
    }
    public bool TryReserveKit(Dictionary<ResourceType, int> kit)
    {
        if (!isActiveAndEnabled || kit == null || kit.Count == 0) return false;
        foreach (var item in kit) if (item.Value <= 0) return false;
        foreach (var item in kit) required[item.Key] = RequiredAmount(item.Key) + item.Value;
        if (ReserveMissing(true)) return true;
        foreach (var item in kit) required[item.Key] -= item.Value;
        return false;
    }
    public void ReleaseKit(Dictionary<ResourceType, int> kit)
    {
        foreach (var item in kit) required[item.Key] = Mathf.Max(0, RequiredAmount(item.Key) - item.Value);
        TrimClaims();
    }
    public bool HasKit(Dictionary<ResourceType, int> kit)
    { foreach (var item in kit) if (LocalAmount(item.Key) < item.Value) return false; return true; }
    public bool ConsumeKit(Dictionary<ResourceType, int> kit)
    {
        if (!HasKit(kit)) return false;
        foreach (var item in kit) stock[item.Key] -= item.Value;
        ReleaseKit(kit); return true;
    }
    private bool ReserveMissing(bool transactional)
    {
        var added = new List<Shipment>();
        foreach (var item in required)
        {
            int claimed = 0; foreach (var warehouse in Warehouses()) claimed += warehouse.MaterialAmount(this, item.Key);
            int missing = Mathf.Max(0, item.Value - LocalAmount(item.Key) - claimed - Pending(item.Key, true));
            foreach (var warehouse in Warehouses())
            {
                int amount = Mathf.Min(missing, warehouse.AvailableMaterial(item.Key));
                if (amount <= 0 || !warehouse.ReserveMaterial(this, item.Key, amount)) continue;
                added.Add(new Shipment { warehouse = warehouse, type = item.Key, amount = amount }); missing -= amount;
            }
            if (missing > 0 && transactional)
            {
                foreach (var entry in added) entry.warehouse.ReleaseMaterial(this, entry.type, entry.amount);
                return false;
            }
        }
        return true;
    }
    private int Assigned(WarehouseStorage warehouse, ResourceType type)
    {
        Prune(); int amount = 0;
        foreach (var item in shipments.Values) if (item.warehouse == warehouse && item.type == type) amount += item.amount;
        return amount;
    }
    private void TrimClaims()
    {
        foreach (var type in InputTypes)
        {
            int claimed = 0; foreach (var warehouse in Warehouses()) claimed += warehouse.MaterialAmount(this, type);
            int excess = Mathf.Max(0, LocalAmount(type) + Pending(type, true) + claimed - RequiredAmount(type));
            foreach (var warehouse in Warehouses())
            {
                int free = Mathf.Max(0, warehouse.MaterialAmount(this, type) - Assigned(warehouse, type));
                int released = warehouse.ReleaseMaterial(this, type, Mathf.Min(free, excess)); excess -= released;
            }
        }
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return; if (Time.time >= nextReserve) { nextReserve = Time.time + 1f; RefreshReservations(); } }
    public void RefreshReservations() { TrimClaims(); ReserveMissing(false); }
    public override int AvailableSupply(WarehouseStorage warehouse, ResourceType type)
        =>warehouse!=null&&FactionMember.CanUse(gameObject,warehouse.gameObject)? Mathf.Max(0, warehouse.MaterialAmount(this, type) - Assigned(warehouse, type)):0;
    public override bool ReserveSupply(Object carrier, WarehouseStorage warehouse, ResourceType type, int amount)
    {
        if (carrier == null || amount <= 0 || shipments.ContainsKey(carrier) || AvailableSupply(warehouse, type) < amount) return false;
        shipments.Add(carrier, new Shipment { warehouse = warehouse, type = type, amount = amount }); return true;
    }
    public override void ReleaseSupply(Object carrier, WarehouseStorage warehouse) { shipments.Remove(carrier); }
    public override int PickupSupply(Object carrier, WarehouseStorage warehouse, ResourceType type, int amount, GettingResources cargo)
    {
        if (!shipments.TryGetValue(carrier, out var job) || job.warehouse != warehouse || job.type != type) return 0;
        int taken = warehouse.PickupMaterial(this, type, Mathf.Min(amount, job.amount), cargo);
        if (incoming.TryGetValue(carrier, out var delivery)) { delivery.picked = true; delivery.amount = taken; }
        return taken;
    }
    public override bool ReserveDelivery(Object owner, int amount) => ReserveDelivery(owner, InputType, amount);
    public override bool ReserveDelivery(Object owner, ResourceType type, int amount)
    {
        if (owner == null || amount <= 0 || incoming.ContainsKey(owner) || RequestedInputFor(type) < amount) return false;
        incoming.Add(owner, new Shipment { type = type, amount = amount, picked = !shipments.ContainsKey(owner) }); return true;
    }
    public override void CancelDelivery(Object owner) { incoming.Remove(owner); }
    public override int Receive(Object owner, GettingResources cargo)
    {
        if (cargo == null || !incoming.TryGetValue(owner, out var job)) return 0;
        incoming.Remove(owner);
        int amount = Mathf.Min(job.amount, cargo.GetStoredAmount(job.type));
        cargo.RemoveStoredAmount(job.type, amount); stock[job.type] = LocalAmount(job.type) + amount;
        TrimClaims(); return amount;
    }
    public override int CollectableOutput(WarehouseStorage destination)
    {
        int amount = 0;
        foreach (var item in returned)
            if (destination == null || destination.Accepts(item.Key)) amount += item.Value;
        foreach (var item in stock)
            if (destination == null || destination.Accepts(item.Key))
                amount += Mathf.Max(0, item.Value - RequiredAmount(item.Key));
        return amount;
    }

    public override int Collect(GettingResources cargo, int capacity, WarehouseStorage destination = null)
    {
        if (cargo == null) return 0;
        int taken = 0;
        foreach (var type in new List<ResourceType>(returned.Keys))
        {
            if (destination != null && !destination.Accepts(type)) continue;
            int amount = Mathf.Min(Mathf.Max(0, capacity - taken), returned[type]);
            returned[type] -= amount; cargo.AddStoredAmount(type, amount); taken += amount;
        }
        foreach (var type in new List<ResourceType>(stock.Keys))
        {
            if (destination != null && !destination.Accepts(type)) continue;
            int amount = Mathf.Min(Mathf.Max(0, capacity - taken), Mathf.Max(0, LocalAmount(type) - RequiredAmount(type)));
            stock[type] -= amount; cargo.AddStoredAmount(type, amount); taken += amount;
        }
        return taken;
    }
    public override void Work(float seconds) { }
    protected override void OnDisable()
    {
        base.OnDisable();
        foreach (var warehouse in WarehouseStorage.All) if (warehouse != null) warehouse.ReleaseMaterials(this);
        incoming.Clear(); shipments.Clear();
    }
}
