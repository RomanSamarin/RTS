using UnityEngine;
using _Game.Scripts.UI;

/// <summary>Таверна выдаёт только физически доставленные порции из своего запаса.</summary>
[DisallowMultipleComponent]
public class Tavern : FoodService
{
    public WarehouseStorage foodStorage;
    [Min(1)] public int capacity = 50;
    [Tooltip("Начинать пополнение, когда запас строго ниже этого значения. Затем пополнять до вместимости.")]
    [Min(0)] public int restockThreshold = 15;
    [SerializeField] private bool restocking;
    [SerializeField, Min(0)] private int storedMeals;
    private readonly System.Collections.Generic.Dictionary<Object, int> incoming = new System.Collections.Generic.Dictionary<Object, int>();
    public int StoredMeals=>storedMeals;
    public override int AvailableMeals => BuildingRoadAccess.Allows(this)?storedMeals:0;
    public int ReservedMeals
    {
        get
        {
            var expired = new System.Collections.Generic.List<Object>(); int total = 0;
            foreach (var item in incoming) if (item.Key == null) expired.Add(item.Key); else total += item.Value;
            foreach (var owner in expired) incoming.Remove(owner);
            return total;
        }
    }
    public int NeededMeals => Mathf.Max(0, capacity - storedMeals - ReservedMeals);
    public virtual int RequestedMeals
    {
        get
        {
            if(!BuildingRoadAccess.Allows(this))return 0;
            if (storedMeals >= capacity) restocking = false;
            else if (storedMeals < Mathf.Clamp(restockThreshold, 0, capacity)) restocking = true;
            return restocking ? NeededMeals : 0;
        }
    }
    public bool ReserveDelivery(Object owner, int amount)
    {
        if (owner == null || amount <= 0 || incoming.ContainsKey(owner) || RequestedMeals < amount) return false;
        incoming.Add(owner, amount); return true;
    }
    public void CancelDelivery(Object owner) { incoming.Remove(owner); }
    public int Receive(Object owner, GettingResources cargo)
    {
        if (cargo == null || !incoming.TryGetValue(owner, out int amount)) return 0;
        incoming.Remove(owner);
        int remaining = Mathf.Min(amount, Mathf.Min(cargo.Food, NeededMeals));
        int taken = remaining;
        foreach (var type in new[] { ResourceType.Food, ResourceType.Bread, ResourceType.Berries })
        {
            int count = Mathf.Min(remaining, cargo.GetStoredAmount(type));
            cargo.RemoveStoredAmount(type, count); remaining -= count;
        }
        storedMeals += taken;
        if (storedMeals >= capacity) restocking = false;
        return taken;
    }
    public override bool TryServe(int portions)
    {
        if(!BuildingRoadAccess.Allows(this))return false;
        if (portions <= 0 || storedMeals < portions) return false;
        storedMeals -= portions; return true;
    }
    protected override void OnDisable() { incoming.Clear(); base.OnDisable(); }
}
