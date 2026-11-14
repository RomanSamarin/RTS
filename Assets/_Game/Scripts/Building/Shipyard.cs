using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

[RequireComponent(typeof(WorkplaceHiring))]
public class Shipyard : EquipmentProductionBuilding
{
    [Header("Спуск на воду")]
    public ShipLaunchArea waterArea;
    public Transform launchPoint;
    [Header("Префабы судов (необязательно)")]
    public GameObject tradeShipPrefab, transportShipPrefab, warshipPrefab;
    public Sprite tradeShipIcon, transportShipIcon, warshipIcon;
    private string launchFailure;
    private float nextLaunchAt;
    private ShipLaunchArea resolvedArea;
    private ShipLaunchArea LaunchWater
    {
        get
        {
            if (waterArea != null) return waterArea;
            if (launchPoint == null) return null;
            if (resolvedArea == null || !resolvedArea.isActiveAndEnabled || !resolvedArea.ContainsPoint(launchPoint.position))
                resolvedArea = ShipLaunchArea.FindAt(launchPoint.position);
            return resolvedArea;
        }
    }
    public bool WaitingForLaunch => ActiveRecipe != null && WorkProgress >= ActiveRecipe.seconds && !string.IsNullOrEmpty(launchFailure);
    public override ResourcesMove.UnitRole Profession => ResourcesMove.UnitRole.Shipwright;
    protected override void ConfigureDefaults()
    { inputCapacity = Mathf.Max(100, inputCapacity); repeatSelectedRecipe = false; }
    private static Recipe Vessel(ShipType type, string title, int wood, int iron, float seconds) => new Recipe
    {
        title = title, producesUnit = true, shipType = type, seconds = seconds,
        ingredients = new List<Stock> { new Stock(ResourceType.Wood, wood), new Stock(ResourceType.Iron, iron) }
    };
    protected override List<Recipe> DefaultRecipes() => new List<Recipe>
    {
        Vessel(ShipType.Trade, "Торговый корабль", 40, 4, 90),
        Vessel(ShipType.Transport, "Транспортный корабль", 35, 4, 80),
        Vessel(ShipType.Warship, "Боевой корабль", 60, 12, 120)
    };
    protected override void PrepareRecipes(List<Recipe> values)
    {
        foreach (ShipType type in System.Enum.GetValues(typeof(ShipType)))
        {
            if (values.Exists(recipe => recipe != null && recipe.producesUnit && recipe.shipType == type)) continue;
            values.Add(type == ShipType.Trade ? Vessel(type, "Торговый корабль", 40, 4, 90)
                : type == ShipType.Transport ? Vessel(type, "Транспортный корабль", 35, 4, 80)
                : Vessel(type, "Боевой корабль", 60, 12, 120));
        }
    }
    public GameObject PrefabFor(ShipType type) => type == ShipType.Trade ? tradeShipPrefab
        : type == ShipType.Transport ? transportShipPrefab : warshipPrefab;
    public Sprite IconFor(ShipType type) => type == ShipType.Trade ? tradeShipIcon
        : type == ShipType.Transport ? transportShipIcon : warshipIcon;
    private string ConfigurationError(Recipe recipe)
    {
        if (recipe == null) return "Нет заказа";
        if (!recipe.producesUnit || recipe.quantity != 1 || !System.Enum.IsDefined(typeof(ShipType), recipe.shipType))
            return "Рецепт верфи должен создавать один корабль";
        var totals = new Dictionary<ResourceType, int>();
        if (recipe.ingredients != null) foreach (var stock in recipe.ingredients)
        {
            if (stock == null || stock.amount <= 0) continue;
            totals.TryGetValue(stock.type, out int amount); totals[stock.type] = amount + stock.amount;
        }
        foreach (var stock in totals) if (stock.Value > inputCapacity) return "Увеличьте Input Capacity верфи: не помещается сырьё рецепта";
        return ShipFactory.ConfigurationError(PrefabFor(recipe.shipType), recipe.shipType, LaunchWater, launchPoint);
    }
    protected override bool CanAcceptOutput(Recipe recipe) => string.IsNullOrEmpty(ConfigurationError(recipe));
    protected override bool TryFinishOutput(Recipe recipe)
    {
        if (Time.time < nextLaunchAt) return false;
        nextLaunchAt = Time.time + 1;
        bool launched = ShipFactory.TrySpawn(PrefabFor(recipe.shipType), recipe.shipType, gameObject,
            LaunchWater, launchPoint, out launchFailure);
        return launched;
    }
    public override string WaitingTask
    {
        get
        {
            if (!BuildingRoadAccess.Allows(this)) return "Нет дороги к зданию";
            string error = ConfigurationError(ActiveRecipe);
            if (!string.IsNullOrEmpty(error)) return error;
            if (WaitingForLaunch) return launchFailure;
            return base.WaitingTask;
        }
    }
    private void QueueShip(ShipType type)
    { for (int i = 0; i < Recipes.Count; i++) if (Recipes[i].producesUnit && Recipes[i].shipType == type) { QueueRecipe(i); return; } }
    public void QueueTradeShip() { QueueShip(ShipType.Trade); }
    public void QueueTransportShip() { QueueShip(ShipType.Transport); }
    public void QueueWarship() { QueueShip(ShipType.Warship); }
    private void Reset()
    {
        inputCapacity = 100; repeatSelectedRecipe = false;
        var hiring = GetComponent<WorkplaceHiring>();
        if (hiring != null) { hiring.profession = ResourcesMove.UnitRole.Shipwright; hiring.maxWorkers = 4; }
    }
}
