using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

/// <summary>Separate edible provisions hold; not a FoodService for people on shore.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ShipHull))]
public class ShipProvisions : MonoBehaviour
{
    private static readonly List<ShipProvisions> ships = new List<ShipProvisions>();
    public static IReadOnlyList<ShipProvisions> All => ships;
    [Min(1)] public int capacity = 30;
    [Min(0)] public int restockThreshold = 10;
    public bool autoResupply = true;
    [SerializeField, Min(0)] private int storedMeals;
    [SerializeField] private bool restocking;
    [SerializeField] private ShipSupplyDock dock;
    private struct Delivery { public ShipSupplyDock dock; public int amount; }
    private readonly Dictionary<Object, Delivery> incoming = new Dictionary<Object, Delivery>();
    public ShipSupplyDock Dock => dock;
    public int StoredMeals => storedMeals;
    public int ReservedMeals
    {
        get
        {
            int count = 0; var expired = new List<Object>();
            foreach (var pair in incoming)
                if (pair.Key == null || pair.Value.dock == null || !pair.Value.dock.CanSupply(this)) expired.Add(pair.Key);
                else count += pair.Value.amount;
            foreach (var owner in expired) incoming.Remove(owner);
            return count;
        }
    }
    public int FreeSpace => Mathf.Max(0, capacity - storedMeals - ReservedMeals);
    public int RequestedMeals
    {
        get
        {
            if (!isActiveAndEnabled || !autoResupply || dock == null || !dock.CanSupply(this)) return 0;
            if (storedMeals >= capacity) restocking = false;
            else if (storedMeals < Mathf.Clamp(restockThreshold, 0, capacity)) restocking = true;
            return restocking ? FreeSpace : 0;
        }
    }
    public int ReservedFor(Object owner) => incoming.TryGetValue(owner, out var delivery) ? delivery.amount : 0;
    internal void SetDock(ShipSupplyDock value)
    { if (dock == value) return; incoming.Clear(); dock = value; }
    public bool ReserveDelivery(Object owner, ShipSupplyDock atDock, int amount)
    {
        if (owner == null || atDock == null || atDock != dock || amount <= 0 || incoming.ContainsKey(owner)
            || !atDock.CanSupply(this) || RequestedMeals < amount) return false;
        incoming.Add(owner, new Delivery { dock = atDock, amount = amount }); return true;
    }
    public void CancelDelivery(Object owner) { incoming.Remove(owner); }
    public int Receive(Object owner, ShipSupplyDock atDock, GettingResources cargo)
    {
        if (cargo == null || !incoming.TryGetValue(owner, out var delivery)) return 0;
        incoming.Remove(owner);
        if (atDock == null || delivery.dock != atDock || atDock != dock || !atDock.CanSupply(this) || !autoResupply) return 0;
        int taken = Mathf.Min(delivery.amount, Mathf.Min(cargo.Food, FreeSpace));
        int remaining = taken;
        foreach (var type in new[] { ResourceType.Food, ResourceType.Bread, ResourceType.Berries })
        {
            int amount = Mathf.Min(remaining, cargo.GetStoredAmount(type));
            cargo.RemoveStoredAmount(type, amount); remaining -= amount;
        }
        storedMeals += taken;
        if (storedMeals >= capacity) restocking = false;
        return taken;
    }
    public void RequestRestock() { autoResupply = true; restocking = true; }
    public void StopRestocking() { autoResupply = false; restocking = false; incoming.Clear(); }
    // For a future crew/passenger feeding system; land units never consume this stock automatically.
    public bool TryConsume(int portions)
    { if (!isActiveAndEnabled || portions <= 0 || portions > storedMeals) return false; storedMeals -= portions; return true; }
    private void OnEnable() { if (!ships.Contains(this)) ships.Add(this); }
    private void OnDisable()
    { ships.Remove(this); incoming.Clear(); if (dock != null) dock.ReleaseShip(this); dock = null; }
}
