public enum Faction { Neutral=0, Player=1, Bandits=2 }

/// <summary>Правила одиночной игры. Нейтральные ресурсы не являются боевыми врагами.</summary>
public static class FactionRules
{
    public static bool AreAllies(Faction a,Faction b)=>a!=Faction.Neutral&&a==b;
    public static bool AreEnemies(Faction a,Faction b)=>a!=Faction.Neutral&&b!=Faction.Neutral&&a!=b;
    public static bool IsControllable(Faction faction)=>faction==Faction.Player;
    public static Faction InferLegacy(bool resource,bool hostile,bool worker,bool legacyEnemy,bool entity)
        =>resource?Faction.Neutral:hostile?Faction.Bandits:worker?Faction.Player:legacyEnemy?Faction.Bandits:entity?Faction.Player:Faction.Neutral;
}
