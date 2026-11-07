using UnityEngine;
using _Game.Scripts.UI;
using System.Collections.Generic;

/// <summary>Башня заменяет секцию; верхняя площадка соединена с землёй внутренним подъёмом.</summary>
public class WallTower : WallSegment
{
    private void Reset() { walkwayWidth = 2.4f; thickness = 2.4f; }
    [Header("Цена башни вместо секции стены")]
    [Min(0)] public int woodCost = 10;
    [Min(0)] public int stoneCost = 25;
    [Min(0.5f)] public float towerBuildTime = 30f;
    [Tooltip("Вход вне коллайдера башни. Если не задан, выбирается с ближайшей доступной стороны.")]
    public Transform stairEntrance;
    [Tooltip("Точка выхода лестницы на уровне площадки. Если не задана — центр площадки.")]
    public Transform stairExit;
    public Dictionary<ResourceType, int> GetCost() => new Dictionary<ResourceType, int>
    { [ResourceType.Wood] = Mathf.Max(0, woodCost), [ResourceType.Stone] = Mathf.Max(0, stoneCost) };
}
