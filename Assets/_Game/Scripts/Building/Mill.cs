using _Game.Scripts.UI;
using UnityEngine;

[RequireComponent(typeof(WorkplaceHiring))]
public class Mill : RecipeBuilding
{
    public override ResourceType InputType => ResourceType.Wheat;
    public override ResourceType OutputType => ResourceType.Flour;
    public override ResourcesMove.UnitRole Profession => ResourcesMove.UnitRole.Miller;
}
