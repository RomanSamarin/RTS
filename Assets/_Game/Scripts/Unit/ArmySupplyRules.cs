using System;

/// <summary>Общие правила радиусов, вместимости и тяги без зависимости от сцены.</summary>
public static class ArmySupplyRules
{
    public static bool UseExpedition(bool alreadyExpedition, float nearestTavern, float settlementRadius, float hysteresis)
    {
        float radius = Math.Max(1f, settlementRadius);
        float threshold = alreadyExpedition ? Math.Max(1f, radius - Math.Max(0f, hysteresis)) : radius;
        return nearestTavern > threshold;
    }
    public static bool WithinSupply(bool sameArmy, float distance, float radius)
        => sameArmy && distance <= Math.Max(0f, radius);
    public static bool CanPull(bool cartAlive, bool attached, bool horseAlive, bool correctLink)
        => cartAlive && attached && horseAlive && correctLink;
    public static int FreeCargo(int capacity, int used) => Math.Max(0, capacity - Math.Max(0, used));
}
