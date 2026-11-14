using System;
using _Game.Scripts.UI;
using UnityEngine;

// Water/scene creation are adapted; actual queue, materials and capacity execute in Shipyard.
public class ShipLaunchArea : MonoBehaviour
{
    public bool ContainsPoint(Vector3 point) => true;
    public static ShipLaunchArea FindAt(Vector3 point) => null;
}
public static class ShipFactory
{
    public static bool Valid = true, ExitFree = true;
    public static int SpawnCount;
    public static ShipType LastType;
    public static string ConfigurationError(GameObject prefab, ShipType type, ShipLaunchArea water, Transform launch) => Valid ? null : "Invalid shipyard configuration";
    public static bool TrySpawn(GameObject prefab, ShipType type, GameObject owner, ShipLaunchArea water, Transform point, out string reason)
    {
        reason = ExitFree ? null : "Launch occupied";
        if (!Valid || !ExitFree) return false;
        SpawnCount++; LastType = type; return true;
    }
}
public static class ShipyardChecks
{
    private static void Check(bool value, string reason) { if (!value) throw new Exception("Shipyard: " + reason); }
    private static void Deliver(Shipyard yard, ResourceType type, int amount)
    {
        var carrier = new UnityEngine.Object(); var cargo = new GettingResources(); cargo.Set(type, amount);
        Check(yard.ReserveDelivery(carrier, type, amount) && yard.Receive(carrier, cargo) == amount
            && cargo.GetStoredAmount(type) == 0, "physical delivery to shipyard");
    }
    public static void Run()
    {
        Time.time = 1000; ShipFactory.Valid = true; ShipFactory.ExitFree = true; ShipFactory.SpawnCount = 0;
        var yard = new Shipyard();
        Check(yard.Recipes.Count == 3 && yard.Recipes.Count == 3 && !yard.repeatSelectedRecipe
            && yard.inputCapacity >= 60 && yard.Profession == ResourcesMove.UnitRole.Shipwright,
            "three unique recipes, finite orders and enough raw capacity for largest hull");
        yard.QueueTradeShip(); yard.Work(200);
        Check(ShipFactory.SpawnCount == 0 && yard.WorkProgress == 0, "no ship without actual materials");
        Deliver(yard, ResourceType.Wood, 40); yard.Work(90);
        Check(ShipFactory.SpawnCount == 0 && yard.GetInputAmount(ResourceType.Wood) == 40, "wood alone does not produce hull");
        Deliver(yard, ResourceType.Iron, 4); yard.Work(89);
        Check(yard.WorkProgress == 89 && ShipFactory.SpawnCount == 0, "full work time required");
        ShipFactory.ExitFree = false; yard.Work(1);
        Check(yard.WaitingForLaunch && yard.QueueCount == 1 && yard.WorkProgress == 90
            && yard.GetInputAmount(ResourceType.Wood) == 40, "blocked slip keeps finished order and unspent material");
        Time.time += 2; yard.Work(10); Check(ShipFactory.SpawnCount == 0 && yard.WorkProgress == 90, "retry cannot duplicate blocked hull");
        ShipFactory.ExitFree = true; Time.time += 2; yard.Work(.1f);
        Check(ShipFactory.SpawnCount == 1 && ShipFactory.LastType == ShipType.Trade && yard.QueueCount == 0
            && yard.GetInputAmount(ResourceType.Wood) == 0 && yard.GetInputAmount(ResourceType.Iron) == 0
            && yard.StoredOutput == 0, "one launch pays exact cost and creates a hull instead of warehouse item");
        yard.Work(500); Check(ShipFactory.SpawnCount == 1, "empty finite queue does not spawn additional ships");
        yard.QueueTransportShip(); Deliver(yard, ResourceType.Wood, 35); Deliver(yard, ResourceType.Iron, 4);
        Time.time += 80;
        yard.Work(80); Check(ShipFactory.SpawnCount == 2 && ShipFactory.LastType == ShipType.Transport, "transport ship uses own recipe and type");
        yard.QueueWarship(); Deliver(yard, ResourceType.Wood, 60); Deliver(yard, ResourceType.Iron, 12);
        yard.Work(119); yard.CancelOrder(yard.GetQueueSnapshot()[0].orderId);
        Check(yard.QueueCount == 0 && yard.GetInputAmount(ResourceType.Wood) == 60 && yard.GetInputAmount(ResourceType.Iron) == 12,
            "cancel unfinished warship preserves delivered raw stock");
        Time.time += 120; yard.QueueWarship(); yard.Work(120);
        Check(ShipFactory.SpawnCount == 3 && ShipFactory.LastType == ShipType.Warship, "warship completes only after its full new timer");
        yard.QueueTradeShip(); Deliver(yard, ResourceType.Wood, 40); Deliver(yard, ResourceType.Iron, 4);
        ShipFactory.Valid = false; yard.Work(100);
        Check(ShipFactory.SpawnCount == 3 && yard.WorkProgress == 0 && !yard.CanProduce, "bad configuration pauses before assembly");
        ShipFactory.Valid = true; yard.inputCapacity = 30;
        Check(!yard.CanProduce && yard.WaitingTask.Contains("Input Capacity"), "misconfigured storage cannot silently deadlock a 40-wood recipe");

        Check(ShipLaunchRules.Contains(5, 10, 10, 20) && !ShipLaunchRules.Contains(5.01f, 0, 10, 20)
            && !ShipLaunchRules.Contains(0, 0, 0, 20), "water rectangle bounds reject shore and invalid areas");
        Check(ShipLaunchRules.EnoughDepth(4, 2, 2) && !ShipLaunchRules.EnoughDepth(4, 3, 2)
            && !ShipLaunchRules.EnoughDepth(4, 5, 1), "minimum draft rejects shallows and land above surface");
        Console.WriteLine("PASS: shipyard recipes, physical input, finite queue, blocked slip, exact payment, cancellation and water/depth rules");
    }
}
