using System;
using _Game.Scripts.UI;

/// <summary>Перекладывает реальный груз, сохраняя виды еды и остаток.</summary>
public static class LootTransfer
{
    public static int Count(GettingResources stock)
    { int total = 0; if (stock != null) foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) total += stock.GetStoredAmount(type); return total; }
    public static int Move(GettingResources from, GettingResources to, int limit)
    {
        if (from == null || to == null || from == to) return 0;
        int moved = 0;
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            int amount = Math.Min(from.GetStoredAmount(type), Math.Max(0, limit - moved));
            from.RemoveStoredAmount(type, amount); to.AddStoredAmount(type, amount); moved += amount;
        }
        return moved;
    }
}
