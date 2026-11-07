using System;
using System.Reflection;
using _Game.Scripts.UI;

public static class TrainingEquipmentChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Register(WarehouseStorage warehouse, bool enable)
        => typeof(WarehouseStorage).GetMethod(enable ? "OnEnable" : "OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(warehouse, null);
    private static void Ship(TrainingEquipmentStore store, WarehouseStorage warehouse, ResourceType type, int amount)
    {
        var courier = new UnityEngine.Object(); var cargo = new GettingResources();
        Check(store.ReserveSupply(courier, warehouse, type, amount) && store.ReserveDelivery(courier, type, amount), "reserve actual training delivery");
        Check(store.PickupSupply(courier, warehouse, type, amount, cargo) == amount, "pickup original kit reservation");
        store.ReleaseSupply(courier, warehouse);
        Check(store.Receive(courier, cargo) == amount && cargo.GetStoredAmount(type) == 0, "dropoff transfers gear locally");
        Check(store.Receive(courier, cargo) == 0, "dropoff cannot repeat");
    }
    public static void Run()
    {
        var a = new WarehouseStorage(); var b = new WarehouseStorage(); Register(a, true); Register(b, true);
        try
        {
            a.AddResource(ResourceType.Sword, 2); b.AddResource(ResourceType.Shield, 1);
            var kit = TrainingLoadout.Get(TroopType.Swordsman);
            var first = new TrainingEquipmentStore(); var second = new TrainingEquipmentStore();
            Check(first.TryReserveKit(kit), "reserve sword and shield across warehouses");
            Check(!first.HasKit(kit) && first.LocalAmount(ResourceType.Sword) == 0, "warehouse reservation is not local gear");
            Check(!second.TryReserveKit(kit) && a.AvailableMaterial(ResourceType.Sword) == 1, "competing barracks fail atomically and release partial sword claim");
            var carrier = new UnityEngine.Object(); var rival = new UnityEngine.Object(); var cargo = new GettingResources();
            Check(first.ReserveSupply(carrier, a, ResourceType.Sword, 1) && first.ReserveDelivery(carrier, ResourceType.Sword, 1), "assign reserved sword");
            Check(!first.ReserveSupply(rival, a, ResourceType.Sword, 1), "no double pickup for same kit");
            Check(first.PickupSupply(carrier, a, ResourceType.Sword, 1, cargo) == 1, "real sword enters backpack");
            first.ReleaseSupply(carrier, a); first.RefreshReservations();
            Check(a.MaterialAmount(first, ResourceType.Sword) == 0 && !first.HasKit(kit), "in-transit sword not re-reserved or usable");
            first.Receive(carrier, cargo);
            Check(!first.HasKit(kit), "cannot train without delivered shield");
            Ship(first, b, ResourceType.Shield, 1);
            Check(first.HasKit(kit) && first.ConsumeKit(kit) && !first.HasKit(kit), "training consumes complete local kit once");

            a.AddResource(ResourceType.Spear, 1);
            var spear = TrainingLoadout.Get(TroopType.Spearman);
            Check(second.TryReserveKit(spear), "spearman needs spear only");
            second.ReleaseKit(spear);
            Check(a.AvailableMaterial(ResourceType.Spear) == 1, "cancel before pickup releases warehouse gear");
            Check(second.TryReserveKit(spear), "rebook spear"); Ship(second, a, ResourceType.Spear, 1); second.ReleaseKit(spear);
            Check(second.StoredOutput == 1, "cancel after delivery exposes surplus for return");
            Check(second.Collect(cargo, 1) == 1 && cargo.GetStoredAmount(ResourceType.Spear) == 1, "surplus gear returns physically");
            a.Deposit(cargo);

            Check(second.TryReserveKit(spear), "loss setup");
            Check(second.ReserveSupply(carrier, a, ResourceType.Spear, 1) && second.ReserveDelivery(carrier, ResourceType.Spear, 1), "loss shipment");
            second.PickupSupply(carrier, a, ResourceType.Spear, 1, cargo); second.ReleaseSupply(carrier, a);
            carrier.destroyed = true; second.RefreshReservations();
            Check(!second.HasKit(spear) && second.LocalAmount(ResourceType.Spear) == 0, "carrier death does not create gear");
            a.AddResource(ResourceType.Spear, 1); second.RefreshReservations();
            Check(a.MaterialAmount(second, ResourceType.Spear) == 1, "lost delivery is reserved again only from real new stock");
            second.ReleaseKit(spear);
            var returnStore = new TrainingEquipmentStore();
            var dismissedCargo = new GettingResources();
            int warehouseBefore = a.GetStoredAmount(ResourceType.Shield);
            int goldBefore = a.GetStoredAmount(ResourceType.Gold);
            dismissedCargo.Set(ResourceType.Sword, 1); dismissedCargo.Set(ResourceType.Shield, 1);
            dismissedCargo.Set(ResourceType.Gold, 4);
            Check(returnStore.AcceptReturnedCargo(dismissedCargo), "disband deposits actual gear and loot locally");
            Check(returnStore.StoredOutput == 6 && dismissedCargo.GetStoredAmount(ResourceType.Sword) == 0
                && dismissedCargo.GetStoredAmount(ResourceType.Gold) == 0, "return drains cargo without losing loot");
            returnStore.AcceptReturnedCargo(dismissedCargo);
            Check(returnStore.StoredOutput == 6, "repeated return cannot duplicate equipment");
            Check(!returnStore.HasKit(kit), "returned gear is not reused before physical warehouse return");
            Check(a.GetStoredAmount(ResourceType.Shield) == warehouseBefore && returnStore.ReturnedAmount(ResourceType.Shield) == 1,
                "disband does not remotely credit warehouse");
            var returnCarrier = new GettingResources();
            Check(returnStore.Collect(returnCarrier, 2) == 2 && returnStore.StoredOutput == 4, "return pickup respects carrier capacity");
            int firstShield = returnCarrier.GetStoredAmount(ResourceType.Shield);
            a.Deposit(returnCarrier);
            Check(a.GetStoredAmount(ResourceType.Shield) == warehouseBefore + firstShield, "warehouse credited only delivered kinds");
            Check(returnStore.Collect(returnCarrier, 10) == 4 && returnStore.StoredOutput == 0, "remaining loot returns in following trip");
            a.Deposit(returnCarrier);
            Check(a.GetStoredAmount(ResourceType.Shield) == warehouseBefore + 1 && a.GetStoredAmount(ResourceType.Gold) == goldBefore + 4,
                "all actual equipment and loot reach warehouse without duplication");

            var transitCancel = new TrainingEquipmentStore();
            Check(transitCancel.TryReserveKit(spear), "cancel in transit setup");
            var enRoute = new UnityEngine.Object(); var enRouteCargo = new GettingResources();
            transitCancel.ReserveSupply(enRoute, a, ResourceType.Spear, 1);
            transitCancel.ReserveDelivery(enRoute, ResourceType.Spear, 1);
            transitCancel.PickupSupply(enRoute, a, ResourceType.Spear, 1, enRouteCargo);
            transitCancel.ReleaseSupply(enRoute, a); transitCancel.ReleaseKit(spear);
            Check(enRouteCargo.GetStoredAmount(ResourceType.Spear) == 1 && transitCancel.StoredOutput == 0,
                "cancelling training cannot teleport cargo out of carrier");
            transitCancel.Receive(enRoute, enRouteCargo);
            Check(transitCancel.StoredOutput == 1 && !transitCancel.ConsumeKit(kit), "cancelled delivery becomes returnable surplus");
            Check(TrainingLoadout.Get(TroopType.Archer).Count == 1 && TrainingLoadout.Get(TroopType.Archer)[ResourceType.Bow] == 1,
                "archer requires bow only");
        }
        finally { Register(a, false); Register(b, false); }
        Console.WriteLine("PASS: training weapon checks, atomic reservations, physical gear delivery, cancellation and losses");
    }
}
