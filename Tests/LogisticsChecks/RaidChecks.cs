using System;
using _Game.Scripts.UI;

public static class RaidChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        var camp = new RaidSchedule(600);
        Check(camp.Tick(0, 45, 240, true) == RaidSchedule.Signal.None, "No immediate opening raid");
        Check(camp.Tick(554, 45, 240, true) == RaidSchedule.Signal.None, "Grace period protected");
        Check(camp.Tick(555, 45, 240, true) == RaidSchedule.Signal.Warning, "Warning before wave");
        Check(camp.Tick(599, 45, 240, true) == RaidSchedule.Signal.None, "No early launch");
        Check(camp.Tick(600, 45, 240, true) == RaidSchedule.Signal.Launch, "Launch at end of grace");
        Check(camp.Tick(600, 45, 240, true) == RaidSchedule.Signal.None, "No duplicate launch");
        Check(camp.Tick(795, 45, 240, true) == RaidSchedule.Signal.Warning, "Next wave warned");
        camp.Cancel();
        Check(camp.Tick(840, 45, 240, true) == RaidSchedule.Signal.None, "Destroyed camp cancels announced wave");
        Check(camp.Tick(99999, 45, 240, true) == RaidSchedule.Signal.None, "Destroyed camp never respawns");
        var second = new RaidSchedule(600);
        Check(second.Tick(1000, 45, 240, false) == RaidSchedule.Signal.None, "Full camp/no settlement cannot raid");
        Check(second.Tick(1000, 45, 240, true) == RaidSchedule.Signal.Warning && second.NextAt == 1045, "Late enable still gives warning time");
        Check(second.Tick(1045, 45, 240, false) == RaidSchedule.Signal.None, "Cap blocks prepared wave");
        Check(second.Tick(1100, 45, 240, true) == RaidSchedule.Signal.Warning, "Unblocked wave warns again");
        Check(second.Tick(1145, 45, 240, true) == RaidSchedule.Signal.Launch, "Other camp survives first camp destruction");

        var loot = new GettingResources(); loot.Set(ResourceType.Gold, 20); loot.Set(ResourceType.Sword, 3); loot.Bread = 2;
        var first = new GettingResources(); var rival = new GettingResources();
        Check(LootTransfer.Move(loot, first, 10) == 10 && LootTransfer.Count(first) == 10, "Carry capacity limited");
        Check(LootTransfer.Count(loot) == 15, "Pickup preserves remainder");
        Check(LootTransfer.Move(loot, rival, 30) == 15, "Second carrier takes only remainder");
        Check(LootTransfer.Move(loot, rival, 30) == 0, "Replaying pickup cannot duplicate loot");
        Check(LootTransfer.Move(first, first, 10) == 0, "Self transfer does not erase loot");
        Check(LootTransfer.Move(first, rival, -1) == 0, "Negative limit leaves cargo intact");
        var warehouse = new WarehouseStorage(); warehouse.SetCapacity(5);
        Check(warehouse.UsedCapacity == 0, "Loot in transit not credited to settlement");
        var reservation = new UnityEngine.Object();
        Check(warehouse.TryReserveSpace(reservation, 5), "Reserve destination for loot");
        Check(warehouse.DepositReserved(reservation, first) == 5 && LootTransfer.Count(first) == 5, "Arrival deposits only accepted loot");
        Check(warehouse.UsedCapacity + LootTransfer.Count(first) + LootTransfer.Count(rival) == 25, "All resources conserved across physical route");
        var wreckStock = new GettingResources();
        var passengerCargo = new GettingResources();
        wreckStock.AddStoredAmount(ResourceType.Wood, 3);
        passengerCargo.AddStoredAmount(ResourceType.Wood, 1);
        passengerCargo.AddStoredAmount(ResourceType.Bread, 5);
        passengerCargo.AddStoredAmount(ResourceType.Food, 1);
        LootTransfer.Move(passengerCargo, wreckStock, int.MaxValue);
        LootTransfer.KeepHalfOfEachResource(wreckStock);
        Check(wreckStock.GetStoredAmount(ResourceType.Wood) == 2
            && wreckStock.GetStoredAmount(ResourceType.Bread) == 2
            && wreckStock.GetStoredAmount(ResourceType.Food) == 0
            && LootTransfer.Count(passengerCargo) == 0, "Wreck keeps half of each combined commodity without duplicating food");
        Check(rival.Bread == 2 && rival.GetStoredAmount(ResourceType.Sword) == 3, "Actual bread and weapon types preserved");
        Console.WriteLine("Raid and loot checks: PASS");
    }
}
