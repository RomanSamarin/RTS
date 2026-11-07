public static class TroopBalance
{
    public static float Multiplier(TroopType attacker, TroopType defender)
    {
        if (attacker == TroopType.Spearman && defender == TroopType.Cavalry) return 2.5f;
        if (attacker == TroopType.Cavalry && defender == TroopType.Spearman) return 0.6f;
        if (attacker == TroopType.Cavalry && defender == TroopType.Archer) return 1.5f;
        if (attacker == TroopType.Swordsman && defender == TroopType.Spearman) return 1.35f;
        if (attacker == TroopType.Archer && defender == TroopType.Swordsman) return 0.5f;
        if (attacker == TroopType.Archer && defender == TroopType.Spearman) return 1.25f;
        return 1f;
    }
}
