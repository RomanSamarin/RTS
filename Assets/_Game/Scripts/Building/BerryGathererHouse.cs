using UnityEngine;
using _Game.Scripts.UI;

[RequireComponent(typeof(WorkplaceHiring))]
public class BerryGathererHouse : GatheringHouse
{
    public override ResourceType Resource => ResourceType.Berries;
    public override ResourcesMove.UnitRole Profession => ResourcesMove.UnitRole.BerryGatherer;
}
