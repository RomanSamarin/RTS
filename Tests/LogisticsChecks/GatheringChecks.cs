using System;
using _Game.Scripts.UI;

public static class GatheringChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        var bush = new BerryBush { secondsPerUnit = 2 };
        int initial = bush.Remaining;
        Check(bush.Work(100, 0) == 0 && bush.Remaining == initial, "full backpack does not consume berries");
        Check(bush.Work(1, 10) == 0 && bush.Work(1, 10) == 1, "gathering requires work time");
        int taken = 1 + bush.Work(1000, 1000);
        Check(taken == initial && bush.Remaining == 0 && bush.Work(1000, 10) == 0, "finite stock cannot be gathered twice");
        var shared = new BerryBush { secondsPerUnit = 1 };
        int a = shared.Work(1000, 15), b = shared.Work(1000, 15);
        Check(a + b == 20 && shared.Remaining == 0, "two gatherers cannot exceed shared stock");
        var house = new BerryGathererHouse(); var backpack = new GettingResources();
        backpack.Berries = taken; backpack.Fish = 2;
        house.Deposit(backpack);
        Check(house.Stored == taken && backpack.Berries == 0 && backpack.Fish == 2, "home only accepts its resource");
        var courier = new GettingResources(); var warehouse = new WarehouseStorage();
        Check(warehouse.Food == 0, "home stock is not remotely available at warehouse");
        int first = house.Collect(courier, 7);
        Check(first == 7 && house.Stored == taken - 7 && courier.Berries == 7, "carrier capacity limits pickup");
        warehouse.Deposit(courier);
        Check(warehouse.Food == 7 && courier.Berries == 0, "berries become warehouse food only after delivery");
        var hunting = new HunterHouse(); backpack.Meat = 5;
        hunting.Deposit(backpack); hunting.Collect(courier, 3); warehouse.Deposit(courier);
        Check(hunting.Stored == 2 && warehouse.Food == 7 && warehouse.GetStoredAmount(ResourceType.Meat) == 3 && backpack.Meat == 0, "meat uses same physical logistics");
        Console.WriteLine("PASS: finite berries, shared stock, local homes and food delivery");
    }
}
