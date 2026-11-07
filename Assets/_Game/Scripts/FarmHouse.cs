using System.Collections.Generic;
using UnityEngine;

/// <summary>Место найма фермера и промежуточное хранилище урожая.</summary>
[DisallowMultipleComponent]
public class FarmHouse : MonoBehaviour
{
    private static readonly List<FarmHouse> houses = new List<FarmHouse>();
    public static IReadOnlyList<FarmHouse> All => houses;
    [SerializeField, Min(0)] private int storedWheat;
    public int StoredWheat => storedWheat;
    private void OnEnable() { if (!houses.Contains(this)) houses.Add(this); }
    private void OnDisable() { houses.Remove(this); }
    public void Deposit(GettingResources cargo)
    {
        if (cargo == null) return;
        storedWheat += Mathf.Max(0, cargo.Wheat);
        cargo.Wheat = 0;
    }
    public int Collect(GettingResources cargo, int freeCapacity)
    {
        if (cargo == null) return 0;
        int amount = Mathf.Min(storedWheat, Mathf.Max(0, freeCapacity));
        storedWheat -= amount;
        cargo.Wheat += amount;
        return amount;
    }
}
