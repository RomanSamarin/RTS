using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

/// <summary>Real inventory plus typed reservations. Food is an exact commodity, not a meal aggregate.</summary>
public sealed class CargoHold
{
    private GettingResources inventory;
    private int capacity;
    private struct Reservation { public ResourceType type; public int amount; }
    private readonly Dictionary<Object, Reservation> incoming = new Dictionary<Object, Reservation>();
    private readonly Dictionary<Object, Reservation> outgoing = new Dictionary<Object, Reservation>();
    public CargoHold Initialize(GettingResources stock, int limit) { inventory = stock; capacity = Mathf.Max(0, limit); return this; }
    public int Capacity => capacity;
    public int Amount(ResourceType type) => inventory != null ? inventory.GetStoredAmount(type) : 0;
    public int Count { get { int total = 0; foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType))) total += Amount(type); return total; } }
    private static int Reserved(Dictionary<Object, Reservation> values, ResourceType? type)
    {
        int amount = 0; var stale = new List<Object>();
        foreach (var item in values)
            if (item.Key == null) stale.Add(item.Key);
            else if (!type.HasValue || item.Value.type == type.Value) amount += item.Value.amount;
        foreach (var owner in stale) values.Remove(owner);
        return amount;
    }
    public int Incoming(ResourceType type) => Reserved(incoming, type);
    public int Outgoing(ResourceType type) => Reserved(outgoing, type);
    public int Available(ResourceType type) => Mathf.Max(0, Amount(type) - Outgoing(type));
    public int FreeSpace => Mathf.Max(0, capacity - Count - Reserved(incoming, null));
    public bool ReserveIncoming(Object owner, ResourceType type, int amount)
    {
        if (inventory == null || owner == null || amount <= 0 || incoming.ContainsKey(owner) || amount > FreeSpace) return false;
        incoming.Add(owner, new Reservation { type = type, amount = amount }); return true;
    }
    public bool ReserveOutgoing(Object owner, ResourceType type, int amount)
    {
        if (inventory == null || owner == null || amount <= 0 || outgoing.ContainsKey(owner) || amount > Available(type)) return false;
        outgoing.Add(owner, new Reservation { type = type, amount = amount }); return true;
    }
    public void CancelIncoming(Object owner) { incoming.Remove(owner); }
    public void CancelOutgoing(Object owner) { outgoing.Remove(owner); }
    public void ClearReservations() { incoming.Clear(); outgoing.Clear(); }
    public int Receive(Object owner, GettingResources cargo)
    {
        if (cargo == null || ReferenceEquals(cargo, inventory) || !incoming.TryGetValue(owner, out var order)) return 0;
        incoming.Remove(owner);
        int amount = Mathf.Min(order.amount, Mathf.Min(FreeSpace, cargo.GetStoredAmount(order.type)));
        cargo.RemoveStoredAmount(order.type, amount); inventory.AddStoredAmount(order.type, amount); return amount;
    }
    public int Pickup(Object owner, GettingResources cargo, int limit)
    {
        if (cargo == null || ReferenceEquals(cargo, inventory) || !outgoing.TryGetValue(owner, out var order)) return 0;
        outgoing.Remove(owner);
        int amount = Mathf.Min(Mathf.Max(0, limit), Mathf.Min(order.amount, Available(order.type)));
        inventory.RemoveStoredAmount(order.type, amount); cargo.AddStoredAmount(order.type, amount); return amount;
    }
    public int Transfer(Object owner, CargoHold target)
    {
        if (owner == null || target == null || ReferenceEquals(this, target) || ReferenceEquals(inventory, target.inventory)
            || !outgoing.TryGetValue(owner, out var from) || !target.incoming.TryGetValue(owner, out var to) || from.type != to.type) return 0;
        outgoing.Remove(owner); target.incoming.Remove(owner);
        int amount = Mathf.Min(Mathf.Min(from.amount, to.amount), Mathf.Min(Available(from.type), target.FreeSpace));
        inventory.RemoveStoredAmount(from.type, amount); target.inventory.AddStoredAmount(from.type, amount); return amount;
    }
}
