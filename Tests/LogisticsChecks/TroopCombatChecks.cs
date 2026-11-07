using System;
using _Game.Scripts.UI;

public static class TroopCombatChecks
{
    public static void Run()
    {
        // Duel damage budget: equal attack frequency, no movement or first-strike advantage.
        Check(100f / (22 * TroopBalance.Multiplier(TroopType.Cavalry, TroopType.Spearman))
            > 160f / (12 * TroopBalance.Multiplier(TroopType.Spearman, TroopType.Cavalry)), "spearman must win stationary cavalry duel");
        Check(140f / (12 * TroopBalance.Multiplier(TroopType.Spearman, TroopType.Swordsman))
            > 100f / (18 * TroopBalance.Multiplier(TroopType.Swordsman, TroopType.Spearman)), "swordsman counters spearman");
        Check(TroopBalance.Multiplier(TroopType.Archer, TroopType.Swordsman)
            < TroopBalance.Multiplier(TroopType.Archer, TroopType.Spearman), "shield protects from arrows");
        foreach (TroopType kind in Enum.GetValues(typeof(TroopType)))
            Check(TroopBalance.Multiplier(kind, TroopType.Auto) == 1, "workers and unclassified targets keep base damage");
        var kit = TrainingLoadout.Get(TroopType.Cavalry);
        Check(kit.Count == 1 && kit[ResourceType.Sword] == 1, "cavalry uses one sword, horse is a reserved world unit");
        Console.WriteLine("PASS: military counter matchups, civilian damage and cavalry equipment");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
