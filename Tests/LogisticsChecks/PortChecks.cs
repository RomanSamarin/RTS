using System;
using _Game.Scripts.UI;
using UnityEngine;

public static class PortChecks
{
    private static void Check(bool value, string reason) { if (!value) throw new Exception("Port: " + reason); }
    private static Port World(out ShipSupplyDock dock, out ShipCargo ship, out GettingResources portBag, out GettingResources shipBag)
    {
        var shipRoot = new GameObject(); shipBag = new GettingResources();
        var hull = new ShipHull { gameObject = shipRoot, type = ShipType.Trade };
        ship = new ShipCargo { gameObject = shipRoot };
        var meals = new ShipProvisions { gameObject = shipRoot };
        shipRoot.With(shipBag).With(hull).With(ship).With(meals);
        dock = new ShipSupplyDock { ship = meals }; meals.SetDock(dock);
        var portRoot = new GameObject(); portBag = new GettingResources(); portRoot.With(portBag).With(dock);
        return new Port { gameObject = portRoot };
    }
    private static void Deliver(Port port, WarehouseStorage source, ResourceType type, int amount)
    {
        var owner = new UnityEngine.Object(); var cargo = new GettingResources();
        Check(source.ReserveMaterial(owner, type, amount) && port.ReserveDelivery(owner, type, amount), "reserve warehouse stock and port space");
        Check(source.PickupMaterial(owner, type, amount, cargo) == amount && port.Receive(owner, cargo) == amount
            && cargo.GetStoredAmount(type) == 0, "real supply delivered to port");
    }
    public static void Run()
    {
        var port = World(out var dock, out var ship, out var portBag, out var shipBag);
        var warehouse = new WarehouseStorage(); warehouse.AddResource(ResourceType.Wood, 100); warehouse.AddResource(ResourceType.Iron, 10);
        Check(port.Profession == ResourcesMove.UnitRole.PortWorker && port.BeginLoading(ResourceType.Wood, 30), "port worker and explicit commodity load");
        Check(!port.CanProduce && port.RequestedInputFor(ResourceType.Wood) == 30 && ship.Stored == 0, "load waits for physical supply");
        Deliver(port, warehouse, ResourceType.Wood, 10); Deliver(port, warehouse, ResourceType.Wood, 20);
        Check(port.StoredOutput == 0, "goods needed for current export are not returned by carriers");
        port.Work(1); Check(ship.Stored == 0 && port.WorkProgress == 1 && ship.FreeSpace == 190, "batch reserves hold space and needs work time");
        port.Work(1); Check(ship.Stored == 10 && port.Remaining == 20 && port.Storage.Count == 20, "physical ten-unit batch once");
        port.Work(2); port.Work(2);
        Check(port.OrderCount == 0 && port.Storage.Count == 0 && shipBag.GetStoredAmount(ResourceType.Wood) == 30
            && warehouse.GetStoredAmount(ResourceType.Wood) == 70, "finite loading order preserves warehouse plus ship total");
        port.BeginLoading(ResourceType.Iron, 5); Deliver(port, warehouse, ResourceType.Iron, 5); port.Work(2);
        Check(ship.Stored == 35 && shipBag.GetStoredAmount(ResourceType.Iron) == 5, "mixed resource types remain exact");
        Check(port.UnloadAll(), "unload all queues each actual commodity");
        for (int i = 0; i < 4; i++) port.Work(2);
        Check(ship.Stored == 0 && port.Storage.Count == 35 && port.OrderCount == 0, "unloading fills local port only");
        Check(warehouse.GetStoredAmount(ResourceType.Wood) == 70, "local port stock is not instantly counted at ordinary warehouse");
        while (port.StoredOutput > 0)
        {
            var owner = new UnityEngine.Object(); var pack = new GettingResources();
            int amount = port.ReserveCollection(owner, 10);
            Check(amount > 0 && warehouse.TryReserveSpace(owner, amount), "reserve imported goods and receiving warehouse");
            Check(port.CollectReserved(owner, pack, 10) == amount && warehouse.DepositReserved(owner, pack) == amount, "carrier returns real import to warehouse");
        }
        Check(warehouse.GetStoredAmount(ResourceType.Wood) == 100 && warehouse.GetStoredAmount(ResourceType.Iron) == 10
            && port.Storage.Count == 0, "complete warehouse-port-ship-port-warehouse cycle has no loss or duplication");

        portBag.Set(ResourceType.Wood, 150); port.SetStockTarget(ResourceType.Wood, 100);
        var a = new UnityEngine.Object(); var b = new UnityEngine.Object();
        Check(port.ReserveCollection(a, 50) == 50 && port.ReserveCollection(b, 50) == 0, "carrier reserves only surplus, keeping requested local stock");
        port.CancelCollection(a); port.SetStockTarget(ResourceType.Wood, 0); portBag.Set(ResourceType.Wood, 20);
        port.BeginLoading(ResourceType.Wood, 20); port.Work(1); port.CancelTransfer();
        Check(ship.FreeSpace == 200 && port.Storage.Available(ResourceType.Wood) == 20 && port.StoredOutput == 20,
            "cancelling partial work releases both endpoints without moving goods");
        port.Work(100); Check(ship.Stored == 0, "cancelled operation cannot finish through later worker tick");

        shipBag.Set(ResourceType.Wood, 200); portBag.Set(ResourceType.Wood, 0); portBag.Set(ResourceType.Iron, 10);
        port.BeginLoading(ResourceType.Iron, 10); port.Work(100);
        Check(ship.Stored == 200 && port.Storage.Count == 10 && port.Remaining == 10, "full hold keeps goods at port and order waiting");
        port.CancelTransfer(); shipBag.Set(ResourceType.Wood, 0); portBag.Set(ResourceType.Iron, 0);
        portBag.Set(ResourceType.Wood, 300); shipBag.Set(ResourceType.Iron, 20);
        port.BeginUnloading(ResourceType.Iron, 20); port.Work(100);
        Check(port.Storage.Count == 300 && ship.Stored == 20, "full port leaves goods aboard");
        var courier = new UnityEngine.Object(); var carried = new GettingResources();
        Check(port.ReserveCollection(courier, 10) == 10 && port.CollectReserved(courier, carried, 10) == 10, "carrier frees ten slots in full port");
        port.Work(2); Check(port.Storage.Count == 300 && ship.Stored == 10, "unloading resumes only into newly freed space");
        port.CancelTransfer(); portBag.Clear(); shipBag.Clear(); portBag.Set(ResourceType.Wood, 10);
        port.BeginLoading(ResourceType.Wood, 10); port.Work(1); dock.moored = false; port.Work(2);
        Check(port.OrderCount == 0 && port.Storage.Count == 10 && ship.Stored == 0, "undocking cancels old batch without remote delivery");

        var stockA = new GettingResources(); stockA.Set(ResourceType.Bread, 5); stockA.Set(ResourceType.Food, 2);
        var stockB = new GettingResources(); var holdA = new CargoHold().Initialize(stockA, 10); var holdB = new CargoHold().Initialize(stockB, 10);
        var operation = new UnityEngine.Object(); holdA.ReserveOutgoing(operation, ResourceType.Food, 2); holdB.ReserveIncoming(operation, ResourceType.Food, 2);
        Check(holdA.Transfer(operation, holdB) == 2 && stockA.Bread == 5 && stockB.PreparedFood == 2 && stockB.Bread == 0,
            "trading prepared food does not consume aggregate bread twice");
        Check(holdA.Transfer(operation, holdB) == 0, "completed transfer cannot execute twice");
        var other = new UnityEngine.Object(); holdB.ReserveIncoming(other, ResourceType.Wood, 8); other.destroyed = true;
        Check(holdB.FreeSpace == 8, "dead owner releases incoming freight capacity");
        var reservedPort = World(out var busyDock, out var busyShip, out var busyPortBag, out var busyShipBag);
        busyShipBag.Set(ResourceType.Iron, 10);
        var olderOperation = new UnityEngine.Object(); busyShip.Hold.ReserveOutgoing(olderOperation, ResourceType.Iron, 10);
        Check(reservedPort.BeginUnloading(ResourceType.Iron, 10) && reservedPort.OrderCount == 1 && !reservedPort.CanProduce,
            "temporarily reserved ship goods do not silently discard unload order");
        busyShip.Hold.CancelOutgoing(olderOperation); reservedPort.Work(2);
        Check(busyShip.Stored == 0 && reservedPort.Storage.Amount(ResourceType.Iron) == 10, "unload resumes after old reservation is released");
        reservedPort.SetStockTarget(ResourceType.Wood, 300); reservedPort.SetStockTarget(ResourceType.Stone, 300);
        Check(reservedPort.ReserveDelivery(a, ResourceType.Wood, 200) && !reservedPort.ReserveDelivery(b, ResourceType.Stone, 100),
            "different commodities share one 300-unit port capacity including imported stock");
        reservedPort.CancelDelivery(a);
        port=World(out dock,out ship,out portBag,out shipBag);
        portBag.Set(ResourceType.Wood,20);portBag.Set(ResourceType.Iron,5);
        var manifest=new System.Collections.Generic.Dictionary<ResourceType,int>{{ResourceType.Wood,20},{ResourceType.Iron,5}};
        Check(port.BeginManifest(ship,manifest,false)&&port.OrderCount==2,"ship UI submits one multi-resource order");
        Check(port.Collectable(ResourceType.Iron)==0,"queued later commodity is protected from carrier export");
        port.Work(2);port.Work(2);port.Work(2);
        Check(shipBag.GetStoredAmount(ResourceType.Wood)==20&&shipBag.GetStoredAmount(ResourceType.Iron)==5&&port.Storage.Count==0,"all manifest lines move physically with worker time");
        Check(port.BeginManifest(ship,manifest,true),"unload manifest accepted");
        int revision=port.TransferRevision;
        Check(!port.BeginManifest(ship,new System.Collections.Generic.Dictionary<ResourceType,int>{{ResourceType.Wood,201}},false)
            &&port.TransferRevision==revision&&port.OrderCount==2,"oversized manifest leaves existing valid order intact");
        var unrelated=new ShipCargo();
        Check(!port.BeginManifest(unrelated,manifest,false)&&port.TransferRevision==revision,"cannot use a port for a different selected ship");
        dock.moored=false;
        Check(!port.BeginManifest(ship,manifest,false),"no manifest submission outside completed mooring");
        Console.WriteLine("PASS: port warehouse/ship loading cycle, local stock, typed reservations, surplus protection, full destinations, cancellation and undocking");
    }
}
