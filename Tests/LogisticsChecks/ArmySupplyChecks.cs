using System;
using _Game.Scripts.UI;

public static class ArmySupplyChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        Check(!ArmySupplyRules.UseExpedition(false, 60, 60, 10), "at town boundary still uses tavern");
        Check(ArmySupplyRules.UseExpedition(false, 61, 60, 10), "far soldier needs wagon");
        Check(ArmySupplyRules.UseExpedition(true, 55, 60, 10), "hysteresis avoids boundary oscillation");
        Check(!ArmySupplyRules.UseExpedition(true, 50, 60, 10), "returning soldier resumes tavern within return radius");
        Check(ArmySupplyRules.UseExpedition(false, float.PositiveInfinity, 60, 10), "no tavern means field supply");
        Check(ArmySupplyRules.WithinSupply(true, 25, 25) && !ArmySupplyRules.WithinSupply(true, 25.1f, 25), "supply radius enforced");
        Check(!ArmySupplyRules.WithinSupply(false, 0, 25), "foreign or unassigned army cannot use wagon");
        Check(!ArmySupplyRules.CanPull(true, false, true, false), "no attached horse cannot move wagon");
        Check(!ArmySupplyRules.CanPull(true, true, false, true), "dead horse cannot move wagon");
        Check(!ArmySupplyRules.CanPull(true, true, true, false), "a horse reference is not enough without reciprocal hitch");
        Check(!ArmySupplyRules.CanPull(false, true, true, true), "destroyed wagon cannot move");
        Check(ArmySupplyRules.CanPull(true, true, true, true), "live hitched horse enables movement");
        Check(ArmySupplyRules.FreeCargo(50, 45) == 5 && ArmySupplyRules.FreeCargo(50, 60) == 0, "loot and food share cart capacity");

        var orders = new MovementOrderState();
        Check(orders.TryBeginSupply(out int escort), "idle escort can follow wagon");
        Check(orders.TryBeginFood(out int meal) && !orders.End(escort), "food preempts escort without stale path cancellation");
        Check(!orders.TryBeginSupply(out _), "escort cannot overwrite meal");
        int manual = orders.Begin(MovementOrderState.Order.PlayerMove);
        Check(!orders.End(meal) && !orders.TryBeginSupply(out _), "manual command preempts food and escort");
        orders.End(manual);
        Check(orders.TryBeginSupply(out int wagon), "automatic escort resumes after manual movement");
        int combat = orders.Begin(MovementOrderState.Order.Combat);
        Check(!orders.End(wagon) && !orders.TryBeginSupply(out _) && !orders.TryBeginFood(out _), "combat protects movement from supply and food");

        var warehouse = new WarehouseStorage(); warehouse.AddResource(ResourceType.Bread, 20); warehouse.AddResource(ResourceType.Food, 10);
        var cart = new UnityEngine.Object(); var carrier = new UnityEngine.Object();
        Check(warehouse.TryReserveFood(cart, 25) && warehouse.Food == 30, "planned refill reserves real food without teleporting");
        Check(!warehouse.TryReserveFood(carrier, 6), "tavern carrier cannot claim wagon food");
        warehouse.ReleaseFood(cart);
        Check(warehouse.AvailableFood == 30, "interrupted refill releases all uncollected stock");
        var pack = new GettingResources();
        Check(warehouse.TryReserveFood(cart, 25) && warehouse.PickupReservedFood(cart, pack) == 25, "loading transfers physical food");
        Check(pack.Bread == 15 && pack.PreparedFood == 10 && warehouse.Food == 5, "food kinds preserved in wagon");
        Check(warehouse.PickupReservedFood(cart, pack) == 0, "repeated loading cannot duplicate shipment");
        Console.WriteLine("PASS: tavern/field switching, supply radius, horse requirement, escort priorities and physical refills");
    }
}
