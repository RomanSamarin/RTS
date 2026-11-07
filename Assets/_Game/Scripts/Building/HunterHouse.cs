using UnityEngine;
using _Game.Scripts.UI;

[RequireComponent(typeof(WorkplaceHiring))]
public class HunterHouse : GatheringHouse
{
    public override ResourceType Resource => ResourceType.Meat;
    public override ResourcesMove.UnitRole Profession => ResourcesMove.UnitRole.Hunter;
}
