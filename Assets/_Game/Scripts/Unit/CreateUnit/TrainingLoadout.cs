using System.Collections.Generic;
using _Game.Scripts.UI;

public enum TroopType { Auto, Swordsman, Spearman, Archer, Cavalry }

public static class TrainingLoadout
{
    public static Dictionary<ResourceType, int> Get(TroopType type)
    {
        switch (type)
        {
            case TroopType.Cavalry: return new Dictionary<ResourceType, int> { [ResourceType.Sword] = 1 };
            case TroopType.Swordsman: return new Dictionary<ResourceType, int> { [ResourceType.Sword] = 1, [ResourceType.Shield] = 1 };
            case TroopType.Spearman: return new Dictionary<ResourceType, int> { [ResourceType.Spear] = 1, [ResourceType.Shield] = 1 };
            case TroopType.Archer: return new Dictionary<ResourceType, int> { [ResourceType.Bow] = 1 };
            default: throw new System.ArgumentException("Resolve Auto before requesting a loadout.");
        }
    }
}
