using System;
using UnityEngine;
using _Game.Scripts.UI;

public static class TradeYardChecks
{
    private static void Check(bool result, string reason)
    {
        if (!result) throw new Exception("Trade yard: " + reason);
    }

    public static void Run()
    {
        var root = new GameObject();
        var yardStock = new GettingResources();
        root.With(yardStock);
        var yard = new TradeYard { gameObject = root };
        var warehouse = new WarehouseStorage();
        warehouse.AddResource(ResourceType.Gold, 30);
        warehouse.AddResource(ResourceType.Wood, 10);
        var first = new UnityEngine.Object();
        var second = new UnityEngine.Object();
        var pack = new GettingResources();

        Check(!yard.BeginTrade(ResourceType.Gold, 1, true), "gold is tender only");
        Check(!yard.BeginTrade(ResourceType.Wood, int.MaxValue, true), "overflow or oversize order rejected");
        Check(yard.BeginTrade(ResourceType.Wood, 5, true) && yard.RequiredAmount == 10, "buy quote");
        Check(!yard.BeginTrade(ResourceType.Wood, 1, false), "cannot overwrite an active order");
        Check(yard.RequestedInputFor(ResourceType.Wood) == 0 && yard.RequestedInputFor(ResourceType.Gold) == 10,
            "purchase requests only gold");
        yard.Work(10);
        Check(yardStock.GetStoredAmount(ResourceType.Wood) == 0, "no purchase without delivered gold");
        Check(yard.ReserveDelivery(first, ResourceType.Gold, 10) && !yard.ReserveDelivery(second, ResourceType.Gold, 1),
            "competing carriers cannot overbook");
        Check(warehouse.ReserveMaterial(first, ResourceType.Gold, 10) &&
              warehouse.PickupMaterial(first, ResourceType.Gold, 10, pack) == 10 &&
              yard.Receive(first, pack) == 10, "carrier transports gold from warehouse");
        Check(yard.Collect(pack, 30) == 0, "tender is protected from pickup");
        BuildingRoadAccess.Blocked.Add(yard);
        yard.Work(10);
        BuildingRoadAccess.Blocked.Remove(yard);
        Check(yard.HasOrder, "road loss suspends trade");
        yard.Work(10);
        yard.Work(10);
        Check(!yard.HasOrder && yardStock.GetStoredAmount(ResourceType.Gold) == 0 &&
              yardStock.GetStoredAmount(ResourceType.Wood) == 5, "purchase exchanges once");
        Check(warehouse.TryReserveSpace(first, 5) && yard.Collect(pack, 5, warehouse) == 5 &&
              warehouse.DepositReserved(first, pack) == 5 && warehouse.GetAmount(ResourceType.Wood) == 15,
            "purchase only adds spendable warehouse stock after delivery");

        Check(yard.BeginTrade(ResourceType.Wood, 4, false), "sale accepted");
        pack.Set(ResourceType.Wood, 4);
        Check(yard.ReserveDelivery(first, ResourceType.Wood, 4) && yard.Receive(first, pack) == 4,
            "seller physically delivers goods");
        yard.Work(10);
        Check(yard.Collect(pack, 10, warehouse) == 4 && pack.GetStoredAmount(ResourceType.Gold) == 4,
            "sale returns configured gold to a carrier");
        pack.Clear();

        Check(yard.BeginTrade(ResourceType.Wood, 5, false) && yard.ReserveDelivery(first, ResourceType.Wood, 2) &&
              yard.ReserveDelivery(second, ResourceType.Wood, 3), "partial and in-flight delivery");
        pack.Set(ResourceType.Wood, 5);
        Check(yard.Receive(first, pack) == 2, "first batch received");
        yard.CancelTrade();
        Check(yard.Receive(second, pack) == 0 && yard.Collect(pack, 5, warehouse) == 2 &&
              pack.GetStoredAmount(ResourceType.Wood) == 5, "cancel returns delivered cargo and releases unfinished reservations");
        Console.WriteLine("PASS: trade yard physical buy/sell, price, road, competing delivery and cancellation");
    }
}
