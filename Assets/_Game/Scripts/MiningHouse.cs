using System.Collections.Generic;
using UnityEngine;

/// <summary>Промежуточный запас здания шахтёров, не общий склад поселения.</summary>
public class MiningHouse : MonoBehaviour
{
    private static readonly List<MiningHouse> houses = new List<MiningHouse>();
    public static IReadOnlyList<MiningHouse> All => houses;
    [SerializeField, Min(0)] private int storedIron;
    [SerializeField, Min(0)] private int storedGold;
    public int StoredIron => storedIron;
    public int StoredGold => storedGold;
    public bool HasOre => storedIron > 0 || storedGold > 0;
    private void OnEnable() { if (!houses.Contains(this)) houses.Add(this); }
    private void OnDisable() { houses.Remove(this); }

    internal OreDeposit AssignedNetworkDeposit { private get; set; }
    internal bool AllowsDeposit(OreDeposit deposit)
        => ReferenceEquals(AssignedNetworkDeposit, null) || AssignedNetworkDeposit == deposit;

    public void Deposit(GettingResources cargo)
    {
        storedIron += cargo.Iron;
        storedGold += cargo.Gold;
        cargo.Iron = cargo.Gold = 0;
    }

    public int Collect(GettingResources cargo, int capacity)
    {
        int iron = Mathf.Min(storedIron, Mathf.Max(0, capacity));
        int gold = Mathf.Min(storedGold, Mathf.Max(0, capacity - iron));
        storedIron -= iron;
        storedGold -= gold;
        cargo.Iron += iron;
        cargo.Gold += gold;
        return iron + gold;
    }
}
