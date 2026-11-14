using System;
using _Game.Scripts.UI;
using UnityEngine;

// Dock geometry/ownership is checked in Unity; the actual provisions inventory executes here.
public class ShipHull : MonoBehaviour { public ShipType type; public float length=8; public Port RepairPort; public void SetRepairPort(Port yard){RepairPort=yard;} public int CargoCapacity => type == ShipType.Trade ? 200 : 0; }
public class ShipSupplyDock : MonoBehaviour
{
    public bool moored = true;
    public ShipProvisions ship;
    public bool busy;
    public float mooringRadius=3;
    public Vector3 BerthPoint=>transform.position;
    public bool CanReserve(ShipNavigation nav)=>isActiveAndEnabled&&!busy&&(ship==null||ship==nav.GetComponent<ShipProvisions>());
    public ShipProvisions Ship => ship;
    public bool CanSupply(ShipProvisions value) => moored && isActiveAndEnabled && ship == value && value.Dock == this;
    public void ReleaseShip(ShipProvisions value) { if (ship == value) { ship = null; value.SetDock(null); GetComponent<Port>()?.CancelTransfer(); } }
}
public static class ShipProvisionsChecks
{
    private static void Check(bool value, string reason) { if (!value) throw new Exception("Ship supplies: " + reason); }
    private static ShipProvisions Moored(out ShipSupplyDock dock)
    {
        var ship = new ShipProvisions(); dock = new ShipSupplyDock { ship = ship }; ship.SetDock(dock); return ship;
    }
    public static void Run()
    {
        var ship = Moored(out var dock); var warehouse = new WarehouseStorage(); warehouse.AddResource(ResourceType.Bread, 40);
        var a = new UnityEngine.Object(); var b = new UnityEngine.Object(); var c = new UnityEngine.Object();
        Check(ship.StoredMeals == 0 && ship.RequestedMeals == 30, "new ship requests real stock and starts empty");
        Check(warehouse.TryReserveFood(a, 20) && ship.ReserveDelivery(a, dock, 20), "reserve warehouse food and dedicated ship space");
        Check(!ship.ReserveDelivery(a, dock, 1) && !ship.ReserveDelivery(b, dock, 11)
            && ship.ReserveDelivery(b, dock, 10) && ship.ReservedMeals == 30, "duplicate and competing reservations cannot overbook");
        Check(ship.StoredMeals == 0 && ship.RequestedMeals == 0, "reserved incoming meals are not already aboard");
        var cargo = new GettingResources(); warehouse.PickupReservedFood(a, cargo);
        Check(ship.StoredMeals == 0 && cargo.Bread == 20 && warehouse.Food == 20, "physical cargo remains in transit");
        Check(ship.Receive(a, dock, cargo) == 20 && cargo.Bread == 0 && ship.StoredMeals == 20
            && ship.Receive(a, dock, cargo) == 0, "single delivery credits ship exactly once");
        ship.CancelDelivery(b); Check(ship.ReservedMeals == 0 && ship.RequestedMeals == 10, "cancellation releases space without inventing meals");
        cargo.Set(ResourceType.Fish, 5); cargo.Set(ResourceType.Meat, 5);
        ship.ReserveDelivery(c, dock, 5);
        Check(ship.Receive(c, dock, cargo) == 0 && cargo.Fish == 5 && cargo.Meat == 5 && ship.StoredMeals == 20,
            "raw fish and meat cannot become provisions");
        cargo.Set(ResourceType.Food, 4); cargo.Set(ResourceType.Berries, 6); ship.ReserveDelivery(c, dock, 10);
        Check(ship.Receive(c, dock, cargo) == 10 && ship.StoredMeals == 30 && cargo.Food == 0
            && cargo.Fish == 5 && cargo.Meat == 5, "mixed edible kinds fill only provisions, leaving raw cargo alone");
        Check(ship.TryConsume(20) && ship.RequestedMeals == 0, "threshold is strict: ten remaining does not start refill");
        Check(ship.TryConsume(1) && ship.RequestedMeals == 21, "below threshold starts refill to full capacity");
        ship.ReserveDelivery(a, dock, 10); cargo.Set(ResourceType.Bread, 10); dock.moored = false;
        Check(ship.Receive(a, dock, cargo) == 0 && cargo.Bread == 10 && ship.StoredMeals == 9 && ship.ReservedMeals == 0,
            "ship leaving berth cannot receive remote delivery and carrier retains food");
        dock.moored = true; ship.ReserveDelivery(a, dock, 10); ship.StopRestocking();
        Check(ship.RequestedMeals == 0 && ship.ReservedMeals == 0 && ship.Receive(a, dock, cargo) == 0,
            "stop loading cancels claims and never deletes transit food");
        ship.RequestRestock(); ship.ReserveDelivery(a, dock, 10); a.destroyed = true;
        Check(ship.ReservedMeals == 0 && ship.RequestedMeals == 21, "dead carrier releases reserved space");

        var changed = Moored(out var oldDock); var courier = new UnityEngine.Object();
        changed.ReserveDelivery(courier, oldDock, 30);
        var replacementDock = new ShipSupplyDock { ship = changed }; changed.SetDock(replacementDock);
        Check(changed.ReservedMeals == 0 && changed.Receive(courier, oldDock, cargo) == 0 && cargo.Bread == 10,
            "changing dock invalidates old delivery without redirecting cargo to another ship");
        changed.ReserveDelivery(courier, replacementDock, 30); changed.capacity = 5; cargo.Set(ResourceType.Bread, 30);
        Check(changed.Receive(courier, replacementDock, cargo) == 5 && cargo.Bread == 25 && changed.StoredMeals == 5,
            "capacity change during transit accepts only actual room and preserves remainder");
        Check(!changed.TryConsume(6) && !changed.TryConsume(0) && changed.StoredMeals == 5, "failed consumption cannot underflow provisions");

        var saved = Moored(out var savedDock); var savedOwner = new UnityEngine.Object(); var newOwner = new UnityEngine.Object();
        saved.ReserveDelivery(savedOwner, savedDock, 7);
        var restored = new ShipProvisions(); var restoredDock = new ShipSupplyDock { ship = restored };
        var codec = new SaveStateCodec
        {
            Reference = obj => ReferenceEquals(obj, savedDock) ? "dock" : ReferenceEquals(obj, savedOwner) ? "carrier" : null,
            Resolve = key => key == "dock" ? restoredDock : key == "carrier" ? newOwner : null
        };
        codec.RestoreFields(restored, codec.CaptureFields(saved));
        Check(restored.Dock == restoredDock && restored.ReservedFor(newOwner) == 7 && restored.ReservedMeals == 7
            && restored.RequestedMeals == 23, "save snapshot restores dedicated food and reservation references");
        Console.WriteLine("PASS: ship provisions, physical food, competing reservations, raw exclusion, thresholds, undocking, cancellation and saved references");
    }
}
