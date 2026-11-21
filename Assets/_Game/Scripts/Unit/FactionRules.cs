public enum Faction { Neutral=0, Player=1, Bandits=2, Player2=3, Player3=4, Player4=5 }

/// <summary>Правила одиночной игры. Нейтральные ресурсы не являются боевыми врагами.</summary>
public static class FactionRules
{
    public static bool AreAllies(Faction a,Faction b)=>a!=Faction.Neutral&&a==b;
    public static bool AreEnemies(Faction a,Faction b)=>a!=Faction.Neutral&&b!=Faction.Neutral&&a!=b;
    public static int PlayerId(Faction faction) => faction switch
    {
        Faction.Player => 1,
        Faction.Player2 => 2,
        Faction.Player3 => 3,
        Faction.Player4 => 4,
        _ => 0
    };
    public static bool IsPlayerSide(Faction faction) => PlayerId(faction) != 0;
    public static bool IsControllable(Faction faction, Faction local = Faction.Player)
        => IsPlayerSide(local) && faction == local;
    public static Faction InferLegacy(bool resource,bool hostile,bool worker,bool legacyEnemy,bool entity)
        =>resource?Faction.Neutral:hostile?Faction.Bandits:worker?Faction.Player:legacyEnemy?Faction.Bandits:entity?Faction.Player:Faction.Neutral;
}
