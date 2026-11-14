using _Game.Scripts.UI;
using UnityEngine;

public class GettingResources : MonoBehaviour
{
    [Header("Текущий рюкзак юнита")]
    public int Wood;
    public int Stone;
    public int Wheat;
    public int Fish;
    public int Iron;
    public int Gold;
    public int Flour;
    public int Bread;
    [UnityEngine.Serialization.FormerlySerializedAs("Food")]
    public int PreparedFood;
    public int Food => PreparedFood + Bread + Berries;
    public int Meat;
    public int Berries;
    public int Sword;
    public int Spear;
    public int Bow;
    public int Shield;
    public int SiegeLadder;

    // Метод перекладывания ресурсов из объекта в рюкзак рабочего
    public void TakeResource(Unit targetUnit)
    {
        if (targetUnit == null) return;
        GettingResources cargo = targetUnit.GetComponent<GettingResources>();
        if (cargo == this) return;
        Wood += targetUnit.woodReward + (cargo != null ? cargo.Wood : 0);
        Stone += targetUnit.stoneReward + (cargo != null ? cargo.Stone : 0);
        Wheat += targetUnit.wheatReward + (cargo != null ? cargo.Wheat : 0);
        Fish += targetUnit.fishReward + (cargo != null ? cargo.Fish : 0);
        if (cargo != null)
        {
            Iron += cargo.Iron;
            Gold += cargo.Gold;
            Flour += cargo.Flour;
            Bread += cargo.Bread;
            PreparedFood += cargo.PreparedFood;
            Meat += cargo.Meat;
            Berries += cargo.Berries;
            Sword += cargo.Sword;
            Spear += cargo.Spear;
            Bow += cargo.Bow;
            Shield += cargo.Shield;
            SiegeLadder += cargo.SiegeLadder;
            cargo.Clear();
        }
    }

    public int GetAmount(ResourceType type) => type == ResourceType.Food ? Food : GetStoredAmount(type);

    public int GetStoredAmount(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return Wood;
            case ResourceType.Stone: return Stone;
            case ResourceType.Iron: return Iron;
            case ResourceType.Gold: return Gold;
            case ResourceType.Wheat: return Wheat;
            case ResourceType.Flour: return Flour;
            case ResourceType.Bread: return Bread;
            case ResourceType.Food: return PreparedFood;
            case ResourceType.Fish: return Fish;
            case ResourceType.Meat: return Meat;
            case ResourceType.Berries: return Berries;
            case ResourceType.Sword: return Sword;
            case ResourceType.Spear: return Spear;
            case ResourceType.Bow: return Bow;
            case ResourceType.Shield: return Shield;
            case ResourceType.SiegeLadder: return SiegeLadder;
            default: throw new System.ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public void RemoveStoredAmount(ResourceType type, int amount)
    {
        if (amount < 0 || amount > GetStoredAmount(type)) throw new System.ArgumentOutOfRangeException(nameof(amount));
        switch (type)
        {
            case ResourceType.Wood: Wood -= amount; break;
            case ResourceType.Stone: Stone -= amount; break;
            case ResourceType.Iron: Iron -= amount; break;
            case ResourceType.Gold: Gold -= amount; break;
            case ResourceType.Wheat: Wheat -= amount; break;
            case ResourceType.Flour: Flour -= amount; break;
            case ResourceType.Bread: Bread -= amount; break;
            case ResourceType.Food: PreparedFood -= amount; break;
            case ResourceType.Fish: Fish -= amount; break;
            case ResourceType.Meat: Meat -= amount; break;
            case ResourceType.Berries: Berries -= amount; break;
            case ResourceType.Sword: Sword -= amount; break;
            case ResourceType.Spear: Spear -= amount; break;
            case ResourceType.Bow: Bow -= amount; break;
            case ResourceType.Shield: Shield -= amount; break;
            case ResourceType.SiegeLadder: SiegeLadder -= amount; break;
            default: throw new System.ArgumentOutOfRangeException(nameof(type));
        }
    }

    public void AddStoredAmount(ResourceType type, int amount)
    {
        if (amount < 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
        switch (type)
        {
            case ResourceType.Wood: Wood += amount; break;
            case ResourceType.Stone: Stone += amount; break;
            case ResourceType.Iron: Iron += amount; break;
            case ResourceType.Gold: Gold += amount; break;
            case ResourceType.Wheat: Wheat += amount; break;
            case ResourceType.Flour: Flour += amount; break;
            case ResourceType.Bread: Bread += amount; break;
            case ResourceType.Food: PreparedFood += amount; break;
            case ResourceType.Fish: Fish += amount; break;
            case ResourceType.Meat: Meat += amount; break;
            case ResourceType.Berries: Berries += amount; break;
            case ResourceType.Sword: Sword += amount; break;
            case ResourceType.Spear: Spear += amount; break;
            case ResourceType.Bow: Bow += amount; break;
            case ResourceType.Shield: Shield += amount; break;
            case ResourceType.SiegeLadder: SiegeLadder += amount; break;
            default: throw new System.ArgumentOutOfRangeException(nameof(type));
        }
    }

    public void Clear()
    {
        Wood = Stone = Iron = Gold = Wheat = Flour = Bread = PreparedFood = Fish = Meat = Berries = Sword = Spear = Bow = Shield = SiegeLadder = 0;
    }

    public bool HasCargo => Wood > 0 || Stone > 0 || Iron > 0 || Gold > 0 || Wheat > 0 || Flour > 0 || Bread > 0 || PreparedFood > 0 || Fish > 0 || Meat > 0 || Berries > 0 || Sword > 0 || Spear > 0 || Bow > 0 || Shield > 0 || SiegeLadder > 0;
}
