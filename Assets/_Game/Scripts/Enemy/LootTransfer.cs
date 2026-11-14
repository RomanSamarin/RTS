using System;
using _Game.Scripts.UI;

/// <summary>Перекладывает реальный груз, сохраняя виды еды и остаток.</summary>
public static class LootTransfer
{
    public static int Count(GettingResources stock, Predicate<ResourceType> accepts = null)
    { int total = 0; if (stock != null) foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) if (accepts == null || accepts(type)) total += stock.GetStoredAmount(type); return total; }

    public static void KeepHalfOfEachResource(GettingResources stock)
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            int amount = stock.GetStoredAmount(type);
            if (amount > 0) stock.RemoveStoredAmount(type, amount - amount / 2);
        }
    }
    public static int Move(GettingResources from, GettingResources to, int limit, Predicate<ResourceType> accepts = null)
    {
        if (from == null || to == null || from == to) return 0;
        int moved = 0;
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (accepts != null && !accepts(type)) continue;
            int amount = Math.Min(from.GetStoredAmount(type), Math.Max(0, limit - moved));
            from.RemoveStoredAmount(type, amount); to.AddStoredAmount(type, amount); moved += amount;
        }
        return moved;
    }
}
