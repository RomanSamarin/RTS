using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

[DisallowMultipleComponent]
public abstract class GatheringHouse : MonoBehaviour
{
    private static readonly List<GatheringHouse> houses = new List<GatheringHouse>();
    public static IReadOnlyList<GatheringHouse> All => houses;
    public abstract ResourceType Resource { get; }
    public abstract ResourcesMove.UnitRole Profession { get; }
    [SerializeField, Min(0)] private int stored;
    public int Stored => stored;
    protected virtual void OnEnable() { if (!houses.Contains(this)) houses.Add(this); }
    protected virtual void OnDisable() { houses.Remove(this); }
    public void Deposit(GettingResources cargo)
    {
        if (cargo == null) return;
        int amount = Mathf.Max(0, cargo.GetStoredAmount(Resource));
        cargo.RemoveStoredAmount(Resource, amount); stored += amount;
    }
    public int Collect(GettingResources cargo, int capacity)
    {
        if (cargo == null) return 0;
        int amount = Mathf.Min(stored, Mathf.Max(0, capacity));
        stored -= amount; cargo.AddStoredAmount(Resource, amount); return amount;
    }
}
