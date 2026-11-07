using _Game.Scripts.UI;
using UnityEngine;

/// <summary>Рыба ИЛИ мясо → готовые порции. Общий запас сырья; ингредиенты не требуются одновременно.</summary>
[RequireComponent(typeof(WorkplaceHiring))]
public class Smokehouse : RecipeBuilding
{
    private static readonly ResourceType[] rawFoods={ResourceType.Fish,ResourceType.Meat};
    public override ResourceType InputType=>ResourceType.Fish;
    public override System.Collections.Generic.IReadOnlyList<ResourceType> InputTypes=>rawFoods;
    public override ResourceType OutputType=>ResourceType.Food;
    public override ResourcesMove.UnitRole Profession=>ResourcesMove.UnitRole.Smoker;
}
