using _Game.Scripts.UI;
using UnityEngine;

[RequireComponent(typeof(WorkplaceHiring))]
public class Bakery : RecipeBuilding
{
    public override ResourceType InputType => ResourceType.Flour;
    public override ResourceType OutputType => ResourceType.Bread;
    public override ResourcesMove.UnitRole Profession => ResourcesMove.UnitRole.Baker;
}
