using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

[RequireComponent(typeof(WorkplaceHiring))]
public class Blacksmith : EquipmentProductionBuilding
{
    public override ResourcesMove.UnitRole Profession => ResourcesMove.UnitRole.Blacksmith;
    protected override List<Recipe> DefaultRecipes() => new List<Recipe>
    {
        new Recipe { title = "Меч", output = ResourceType.Sword, seconds = 30,
            ingredients = new List<Stock> { new Stock(ResourceType.Iron, 3) } },
        new Recipe { title = "Копьё", output = ResourceType.Spear, seconds = 25,
            ingredients = new List<Stock> { new Stock(ResourceType.Wood, 2), new Stock(ResourceType.Iron, 1) } }
    };
    public void SelectSword() { SetRecipe(0); }
    public void SelectSpear() { SetRecipe(1); }
    public void QueueSword() { QueueRecipe(0); }
    public void QueueSpear() { QueueRecipe(1); }
}
