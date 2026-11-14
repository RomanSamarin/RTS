using System;
using _Game.Scripts.UI;
using UnityEngine;

// Scene creation is adapted; actual Workshop, WarehouseStorage and cargo transactions execute here.
public static class SiegeLadderCartFactory
{
    public static bool SpawnAllowed;
    public static int SpawnCount;
    public static bool Valid(GameObject prefab) => true;
    public static bool TrySpawn(GameObject prefab, GameObject workshop, Transform point)
    { if (!SpawnAllowed) return false; SpawnCount++; return true; }
}
public static class SiegeLadderCartChecks
{
    private static void Check(bool value, string reason) { if (!value) throw new Exception("Ladder wagon: " + reason); }
    private static void Deliver(Workshop shop, ResourceType type, int amount)
    {
        var owner = new UnityEngine.Object(); var cargo = new GettingResources(); cargo.Set(type, amount);
        Check(shop.ReserveDelivery(owner, type, amount) && shop.Receive(owner, cargo) == amount, "physical workshop input");
    }
    public static void Run()
    {
        var reservations = new LadderReservations<object>();
        var soldierA = new object(); var soldierB = new object(); var soldierC = new object();
        Check(reservations.Reserve(soldierA, 2) && reservations.Reserve(soldierA, 2)
            && reservations.Available(2) == 1, "repeat order does not reserve twice");
        Check(reservations.Reserve(soldierB, 2) && !reservations.Reserve(soldierC, 2)
            && reservations.Available(2) == 0, "two soldiers exclude third and unloading");
        Check(reservations.Release(soldierA) && !reservations.Release(soldierA)
            && reservations.Reserve(soldierC, 2), "cancellation releases exactly one ladder");
        Check(reservations.Release(soldierB) && reservations.Available(1) == 0,
            "pickup and stock decrement preserve remaining reservation");
        reservations.Clear();
        Check(reservations.Available(1) == 1 && !reservations.Contains(soldierC)
            && !reservations.Reserve(null, 1) && !reservations.Reserve(soldierA, 0), "disable clears claims; invalid claims rejected");
        var source = new WarehouseStorage(); source.AddResource(ResourceType.SiegeLadder, 3);
        var first = new UnityEngine.Object(); var second = new UnityEngine.Object();
        var cargo = new GettingResources(); var rivalCargo = new GettingResources();
        Check(source.ReserveMaterial(first, ResourceType.SiegeLadder, 2), "first wagon reserves two ladders");
        Check(!source.ReserveMaterial(second, ResourceType.SiegeLadder, 2)
            && source.ReserveMaterial(second, ResourceType.SiegeLadder, 1), "second wagon cannot overbook first cargo");
        Check(SiegeLadderCargo.Pickup(source, cargo, first) == 2 && SiegeLadderCargo.FreeSpace(cargo) == 0,
            "wagon physically loads two and has no free room");
        Check(SiegeLadderCargo.Pickup(source, cargo, second) == 0
            && source.MaterialAmount(second, ResourceType.SiegeLadder) == 1, "full wagon does not consume another reservation");
        Check(SiegeLadderCargo.Pickup(source, rivalCargo, second) == 1
            && source.GetStoredAmount(ResourceType.SiegeLadder) == 0, "other wagon loads only its reserved ladder");
        Check(SiegeLadderCargo.Pickup(source, cargo, first) == 0, "pickup cannot duplicate cargo");

        var target = new WarehouseStorage(); target.SetCapacity(2);
        var carrier = new UnityEngine.Object(); Check(target.TryReserveSpace(carrier, 1), "carrier owns destination space");
        Check(SiegeLadderCargo.Unload(target, cargo) == 1 && cargo.GetStoredAmount(ResourceType.SiegeLadder) == 1,
            "partial unloading preserves remaining ladder and carrier reservation");
        Check(SiegeLadderCargo.Unload(target, cargo) == 0, "full warehouse does not delete wagon cargo");
        target.ReleaseSpace(carrier);
        Check(SiegeLadderCargo.Unload(target, cargo) == 1 && cargo.GetStoredAmount(ResourceType.SiegeLadder) == 0,
            "remaining ladder unloads when space becomes available");
        var mixed = new GettingResources(); mixed.Set(ResourceType.Wood, 2);
        Check(SiegeLadderCargo.FreeSpace(mixed) == 0 && SiegeLadderCargo.Unload(target, mixed) == 0
            && mixed.GetStoredAmount(ResourceType.Wood) == 2, "other cargo is never silently removed or overloaded");
        source.AddResource(ResourceType.SiegeLadder, 2); source.ReserveMaterial(first, ResourceType.SiegeLadder, 2);
        source.ReleaseMaterials(first);
        Check(source.AvailableMaterial(ResourceType.SiegeLadder) == 2 && SiegeLadderCargo.Pickup(source, cargo, first) == 0,
            "cancelled reservation frees ladders and cannot be picked up");

        Time.time = 300; SiegeLadderCartFactory.SpawnAllowed = false; SiegeLadderCartFactory.SpawnCount = 0;
        var shop = new Workshop(); shop.SetRepeat(false); shop.QueueLadderCart();
        Check(shop.ActiveRecipe.producesLadderCart && shop.Recipes.Count == 6 && shop.SiegeLadderRecipeIndex == 3,
            "wagon appended after original recipes");
        shop.Work(100); Check(SiegeLadderCartFactory.SpawnCount == 0, "no wagon without delivered materials");
        Deliver(shop, ResourceType.Wood, 20); Deliver(shop, ResourceType.Iron, 4);
        shop.Work(44); Check(shop.GetInputAmount(ResourceType.Wood) == 20, "materials kept until assembly finishes");
        shop.Work(1); Check(shop.QueueCount == 1 && shop.GetInputAmount(ResourceType.Wood) == 20
            && shop.WorkProgress == 45, "blocked exit retains completed order and materials");
        Time.time += 2; SiegeLadderCartFactory.SpawnAllowed = true; shop.Work(.1f);
        Check(SiegeLadderCartFactory.SpawnCount == 1 && shop.QueueCount == 0 && shop.StoredOutput == 0
            && shop.GetInputAmount(ResourceType.Wood) == 0 && shop.GetInputAmount(ResourceType.Iron) == 0,
            "one wagon spawns with one exact payment, not a resource item");
        shop.Work(100); Check(SiegeLadderCartFactory.SpawnCount == 1, "finished finite order cannot spawn twice");
        shop.QueueLadderCart(); Deliver(shop, ResourceType.Wood, 20); Deliver(shop, ResourceType.Iron, 4); shop.Work(10);
        shop.ClearQueue(); Check(shop.GetInputAmount(ResourceType.Wood) == 20 && shop.GetInputAmount(ResourceType.Iron) == 4,
            "cancel assembly keeps real materials");
        Console.WriteLine("PASS: ladder wagon two-slot cargo, competing reservations, partial unloading, workshop assembly and blocked exit");
    }
}
