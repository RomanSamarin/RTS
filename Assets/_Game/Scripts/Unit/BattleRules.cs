public enum CombatHitKind { Melee, Ranged, Charge, Counter }

public static class BattleRules
{
    public struct Impact
    {
        public float damage, push, stun, disorder, counter;
        public bool stopped;
    }
    public static Impact Charge(TroopType defender, bool holding, bool cohesive, bool front, bool rear)
    {
        var result = new Impact { damage = rear ? 1.6f : front ? 1f : 1.3f, push = 1, stun = 0.8f, disorder = 6 };
        if (defender == TroopType.Spearman && holding && front)
            return new Impact { damage = cohesive ? 0.3f : 0.55f, push = cohesive ? 0 : 0.15f,
                stun = 0, disorder = cohesive ? 0 : 2, counter = cohesive ? 1f : 0.5f, stopped = cohesive };
        if (defender == TroopType.Swordsman && holding && front)
        {
            result.damage *= cohesive ? 0.7f : 0.85f;
            result.push = cohesive ? 0.35f : 0.7f; result.stun = cohesive ? 0.3f : 0.6f;
            result.disorder = cohesive ? 3f : 5f;
        }
        return result;
    }
    public static float Incoming(CombatHitKind hit, TroopType type, bool holding, bool cohesive, bool disordered, bool front, bool hasShield = false)
    {
        float result = disordered ? 1.25f : 1f;
        if (hit == CombatHitKind.Charge || !holding || !front) return result;
        if (type == TroopType.Swordsman) result *= hit == CombatHitKind.Ranged ? (cohesive ? 0.65f : 0.85f) : (cohesive ? 0.8f : 0.9f);
        else if (type == TroopType.Spearman)
        {
            if (hit == CombatHitKind.Ranged && hasShield) result *= cohesive ? 0.75f : 0.9f;
            else if (cohesive) result *= 0.85f;
        }
        return result;
    }
}

public sealed class BattleShock
{
    public float StunnedUntil { get; private set; }
    public float DisorderedUntil { get; private set; }
    public float FatiguedUntil { get; private set; }
    public bool Stunned(float now) => now < StunnedUntil;
    public bool Disordered(float now) => now < DisorderedUntil;
    public bool Fatigued(float now) => now < FatiguedUntil;
    public void Impact(float now, float stun, float disorder)
    {
        StunnedUntil = System.Math.Max(StunnedUntil, now + System.Math.Max(0, stun));
        DisorderedUntil = System.Math.Max(DisorderedUntil, now + System.Math.Max(0, disorder));
    }
    public void Fatigue(float now, float duration) { FatiguedUntil = System.Math.Max(FatiguedUntil, now + duration); }
    public float DamageFactor(float now) => (Disordered(now) ? 0.8f : 1) * (Fatigued(now) ? 0.85f : 1);
    public float AttackRateFactor(float now) => Disordered(now) ? 0.75f : 1;
    public float SpeedFactor(float now) => Stunned(now) ? 0 : (Disordered(now) ? 0.8f : 1) * (Fatigued(now) ? 0.7f : 1);
}

public sealed class ChargeRun
{
    public float Distance { get; private set; }
    public float PeakSpeed { get; private set; }
    public float ReadyUntil { get; private set; } = float.NegativeInfinity;
    public float NextChargeAt { get; private set; }
    public bool Ready(float now) => now >= NextChargeAt && now <= ReadyUntil;
    public void Tick(float now, float speed, float travelled, float turnDot, float minSpeed, float runDistance, bool blocked)
    {
        if (blocked || now < NextChargeAt || turnDot < 0.8f)
        { ResetRun(); return; }
        if (speed < minSpeed)
        { if (!Ready(now)) ResetRun(); return; }
        Distance += System.Math.Max(0, travelled); PeakSpeed = System.Math.Max(PeakSpeed, speed);
        if (Distance >= runDistance) ReadyUntil = now + 0.8f;
    }
    public void Consume(float now, float cooldown) { NextChargeAt = now + System.Math.Max(0, cooldown); ResetRun(); }
    private void ResetRun() { Distance = PeakSpeed = 0; ReadyUntil = float.NegativeInfinity; }
}
