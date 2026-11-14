using UnityEngine;
using _Game.Scripts.UI;

/// <summary>Physical cargo transactions shared by the wagon and isolated checks.</summary>
public static class SiegeLadderCargo
{
    public const int Capacity = 2;
    public static int FreeSpace(GettingResources cargo)
    {
        if (cargo == null) return 0;
        int used = 0;
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType))) used += cargo.GetStoredAmount(type);
        return Mathf.Max(0, Capacity - used);
    }
    public static int Pickup(WarehouseStorage warehouse, GettingResources cargo, Object owner)
    {
        if (warehouse == null || cargo == null || owner == null) return 0;
        return warehouse.PickupMaterial(owner, ResourceType.SiegeLadder,
            Mathf.Min(FreeSpace(cargo), warehouse.MaterialAmount(owner, ResourceType.SiegeLadder)), cargo);
    }
    public static int Unload(WarehouseStorage warehouse, GettingResources cargo)
    {
        if (warehouse == null || cargo == null) return 0;
        int amount = warehouse.AddResource(ResourceType.SiegeLadder, cargo.GetStoredAmount(ResourceType.SiegeLadder));
        cargo.RemoveStoredAmount(ResourceType.SiegeLadder, amount);
        return amount;
    }
}
