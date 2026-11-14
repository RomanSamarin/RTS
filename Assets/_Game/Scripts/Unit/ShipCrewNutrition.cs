using System;

/// <summary>One resident's existing hunger episode; only a real portion ends it.</summary>
public static class ShipCrewNutrition
{
    public static bool Tick(float now, bool hungry, float nextMeal, int portions, float grace, float damageInterval,
        HungerTimer hunger, Func<int, bool> consume, out bool fed, out bool damage)
    {
        fed = damage = false;
        if (!hungry && now < nextMeal) return false;
        hunger.Begin(now, grace);
        if (consume != null && consume(Math.Max(1, portions)))
        { fed = true; hunger.Reset(); return false; }
        damage = hunger.TryDamage(now, damageInterval);
        return true;
    }
}
