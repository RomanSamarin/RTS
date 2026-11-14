using System;

public static class FormationCombatChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.0001f;
    public static void Run()
    {
        var loose = BattleRules.Charge(TroopType.Swordsman, false, false, true, false);
        var wall = BattleRules.Charge(TroopType.Swordsman, true, true, true, false);
        var flank = BattleRules.Charge(TroopType.Swordsman, true, true, false, false);
        var rear = BattleRules.Charge(TroopType.Swordsman, true, true, false, true);
        Check(wall.damage < loose.damage && wall.push < loose.push && wall.disorder < loose.disorder, "cohesive shield wall resists frontal charge better than loose infantry");
        Check(flank.damage > wall.damage && rear.damage > flank.damage && rear.push == 1, "flank and rear bypass frontal formation protection");
        var spears = BattleRules.Charge(TroopType.Spearman, true, true, true, false);
        Check(spears.stopped && spears.counter > 0 && spears.push == 0 && spears.stun == 0, "prepared frontal spears stop cavalry without losing their row");
        var spearRear = BattleRules.Charge(TroopType.Spearman, true, true, false, true);
        Check(!spearRear.stopped && spearRear.counter == 0 && spearRear.disorder > 0, "spears cannot brace against an attack from the rear");
        var isolatedSpear = BattleRules.Charge(TroopType.Spearman, true, false, true, false);
        Check(!isolatedSpear.stopped && isolatedSpear.counter < spears.counter && isolatedSpear.damage > spears.damage,
            "isolated spear does not replace a cohesive braced formation");
        Check(16 * spears.counter * TroopBalance.Multiplier(TroopType.Spearman, TroopType.Cavalry)
            > 32 * spears.damage * TroopBalance.Multiplier(TroopType.Cavalry, TroopType.Spearman), "braced spear counter outweighs cavalry shock damage");
        float frontArrow = BattleRules.Incoming(CombatHitKind.Ranged, TroopType.Swordsman, true, true, false, true);
        float flankArrow = BattleRules.Incoming(CombatHitKind.Ranged, TroopType.Swordsman, true, true, false, false);
        Check(frontArrow < flankArrow, "shield formation provides directional arrow protection");
        Check(Near(BattleRules.Incoming(CombatHitKind.Ranged, TroopType.Spearman, true, true, false, true, true), 0.75f)
            && Near(BattleRules.Incoming(CombatHitKind.Ranged, TroopType.Spearman, true, false, false, true, true), 0.9f),
            "an equipped spear shield protects while holding and improves in formation");
        Check(Near(BattleRules.Incoming(CombatHitKind.Ranged, TroopType.Spearman, true, true, false, false, true), 1f)
            && Near(BattleRules.Incoming(CombatHitKind.Ranged, TroopType.Spearman, false, false, false, true, true), 1f)
            && Near(BattleRules.Incoming(CombatHitKind.Ranged, TroopType.Spearman, true, true, false, true), 0.85f),
            "spear shield bonus requires frontal defense and legacy spears retain formation protection");
        Check(BattleRules.Incoming(CombatHitKind.Charge, TroopType.Swordsman, true, true, false, true) == 1,
            "formation charge mitigation is applied once by the impact resolver");

        var run = new ChargeRun();
        run.Tick(0, 0, 0, 1, 4.5f, 6, false); Check(!run.Ready(0), "standing horse cannot charge");
        run.Tick(1, 6, 3, 1, 4.5f, 6, false); Check(!run.Ready(1), "short run does not arm charge");
        run.Tick(2, 6, 3, 1, 4.5f, 6, false); Check(run.Ready(2), "six meters at speed arms charge");
        run.Tick(2.3f, 0, 0, 1, 4.5f, 6, false); Check(run.Ready(2.3f), "momentum survives brief braking at contact");
        run.Consume(2.3f, 8); Check(!run.Ready(2.3f) && !run.Ready(10), "one contact consumes charge and enforces cooldown");
        run.Tick(4, 6, 100, 1, 4.5f, 6, false); Check(!run.Ready(4) && run.Distance == 0, "cooldown cannot be bypassed by accumulated movement");
        run.Tick(11, 6, 6, 1, 4.5f, 6, false); Check(run.Ready(11), "new run after recovery permits another charge");
        run.Tick(11.1f, 6, 1, 0, 4.5f, 6, false); Check(!run.Ready(11.1f), "sharp turn loses charge momentum");
        run.Tick(12, 6, 6, 1, 4.5f, 6, true); Check(!run.Ready(12), "stun, food or off-mesh traversal cannot build charge");

        var shock = new BattleShock(); shock.Impact(10, 1, 6); shock.Fatigue(10, 4);
        Check(shock.Stunned(10.5f) && shock.SpeedFactor(10.5f) == 0, "stunned unit cannot move");
        Check(!shock.Stunned(11) && Near(shock.SpeedFactor(11), 0.56f) && Near(shock.DamageFactor(11), 0.68f), "fatigue and disorder affect movement and damage after stun ends");
        Check(Near(shock.AttackRateFactor(11), 0.75f), "disorder slows attacks");
        shock.Impact(10.5f, 0, 0);
        Check(shock.StunnedUntil == 11 && shock.DisorderedUntil == 16, "weaker repeat impact does not erase active effects");
        Check(Near(shock.SpeedFactor(14), 0.8f) && shock.SpeedFactor(16) == 1 && shock.DamageFactor(16) == 1,
            "temporary debuffs expire without permanently modifying base stats");
        Console.WriteLine("PASS: charge momentum/cooldown, shield direction, braced spears, flanks and timed battle effects");
    }
}
