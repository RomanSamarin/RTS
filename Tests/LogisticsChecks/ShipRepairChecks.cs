using System;
using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

static class ShipRepairChecks
{
    static void Check(bool value,string reason){if(!value)throw new Exception("Ship repair: "+reason);}
    sealed class World
    {
        public Port port;public ShipHull ship;public Unit health;public ShipNavigation nav;public ShipSupplyDock dock;
        public World(float hp=250)
        {
            var shore=new GameObject();dock=new ShipSupplyDock{gameObject=shore};port=new Port{gameObject=shore};
            shore.With(dock).With(port).With(new BuildingHealth()).With(new GettingResources());
            var root=new GameObject();health=new Unit{unitHealth=hp,unitMaxHealth=300};
            ship=new ShipHull{gameObject=root};nav=new ShipNavigation{gameObject=root};var meals=new ShipProvisions{gameObject=root};
            root.With(health).With(ship).With(nav).With(meals).With(new ShipCargo{gameObject=root}).With(new GettingResources());nav.MoorForRoute(dock);
        }
        public void Deliver(ResourceType type,int amount)
        {
            var carrier=new UnityEngine.Object();var cargo=new GettingResources();cargo.Set(type,amount);
            Check(port.ReserveDelivery(carrier,type,amount)&&port.Receive(carrier,cargo)==amount&&cargo.GetStoredAmount(type)==0,"physical repair delivery");
        }
    }
    public static void Run()
    {
        var w=new World();Check(w.port.RequestRepair(w.ship),"request moored damaged ship");
        Check(w.port.RequestedInputFor(ResourceType.Wood)==2&&w.port.RequestedInputFor(ResourceType.Iron)==0,"request only cost of missing health");
        w.port.Work(100);Check(w.health.unitHealth==250&&w.port.WorkProgress==0,"no healing or progress without delivered wood");
        w.Deliver(ResourceType.Wood,2);w.port.TickRepair();Check(w.health.unitHealth==250,"scheduler never grants worker time");
        w.port.Work(4);Check(w.health.unitHealth==250&&w.port.Storage.Amount(ResourceType.Wood)==2,"materials remain until full work cycle");
        w.port.Work(1);Check(w.health.unitHealth==300&&!w.port.IsRepairing&&w.ship.RepairPort==null&&w.port.Storage.Amount(ResourceType.Wood)==0,"one paid cycle heals, completes and releases ship");
        w.port.Work(100);Check(w.health.unitHealth==300,"completed repair never repeats");

        w=new World(249);w.port.RequestRepair(w.ship);w.Deliver(ResourceType.Wood,4);w.port.Work(5);
        Check(w.health.unitHealth==299&&w.port.IsRepairing,"first of two cycles");
        w.port.Work(5);Check(w.health.unitHealth==300&&w.port.Storage.Amount(ResourceType.Wood)==0,"partial last cycle clamps health and has explicit batch cost");

        w=new World(200);w.port.repairIronPerCycle=1;w.port.RequestRepair(w.ship);w.Deliver(ResourceType.Wood,4);w.port.Work(100);
        Check(w.health.unitHealth==200&&w.port.Storage.Amount(ResourceType.Wood)==4,"mixed costs are atomic when iron is absent");
        w.Deliver(ResourceType.Iron,2);w.port.Work(10);Check(w.health.unitHealth==300&&w.port.Storage.Amount(ResourceType.Wood)==0&&w.port.Storage.Amount(ResourceType.Iron)==0,"both paid batches consume exact wood and iron");

        w=new World();w.port.RequestRepair(w.ship);w.Deliver(ResourceType.Wood,2);w.port.Work(2);
        BuildingRoadAccess.Blocked.Add(w.port);w.port.Work(100);w.port.TickRepair();
        Check(w.health.unitHealth==250&&w.port.WorkProgress==2,"road loss pauses work and preserves progress");
        BuildingRoadAccess.Blocked.Remove(w.port);w.port.CancelRepair();
        Check(w.health.unitHealth==250&&w.port.Storage.Amount(ResourceType.Wood)==2&&w.ship.RepairPort==null,"cancel retains delivered raw materials");
        w.port.RequestRepair(w.ship);w.port.Work(3);Check(w.health.unitHealth==250,"new repair cannot reuse cancelled progress");
        w.port.Work(2);Check(w.health.unitHealth==300,"new full cycle succeeds");

        w=new World();w.port.RequestRepair(w.ship);w.Deliver(ResourceType.Wood,2);w.port.Work(2);w.nav.ManualMove();
        int revision=w.nav.CommandRevision;
        Check(!w.port.IsRepairing&&w.ship.RepairPort==null&&w.nav.State==ShipNavigation.Motion.Sailing,"manual departure cancels repair and preserves new order");
        w.port.Work(100);Check(w.health.unitHealth==250&&w.nav.CommandRevision==revision&&w.port.Storage.Amount(ResourceType.Wood)==2,"no remote healing or resource consumption after departure");

        w=new World();w.nav.ManualMove();w.nav.deferArrival=true;w.port.RequestRepair(w.ship);w.Deliver(ResourceType.Wood,2);w.port.Work(100);
        Check(w.port.IsRepairing&&w.port.WorkProgress==0&&w.health.unitHealth==250,"approaching vessel can request materials but cannot be healed remotely");
        w.port.CancelRepair();Check(w.nav.State==ShipNavigation.Motion.Idle&&w.ship.RepairPort==null,"cancel stops only owned approach and releases claim");

        w=new World();w.port.RequestRepair(w.ship);w.Deliver(ResourceType.Wood,2);w.dock.ReleaseShip(w.ship.GetComponent<ShipProvisions>());w.port.TickRepair();
        Check(!w.port.IsRepairing&&w.ship.RepairPort==null&&w.port.Storage.Amount(ResourceType.Wood)==2,"lost berth cancels repair without spending materials");

        w=new World();w.port.RequestRepair(w.ship);
        var other=new World();Check(!other.port.RequestRepair(w.ship),"two ports cannot claim one ship");
        FactionMember.Set(w.ship.gameObject,Faction.Bandits);w.port.TickRepair();Check(!w.port.IsRepairing&&w.ship.RepairPort==null,"ownership change releases claim");

        w=new World();w.port.RequestRepair(w.ship);w.health.unitHealth=0;w.port.TickRepair();
        Check(!w.port.IsRepairing&&!w.port.RequestRepair(w.ship),"repair cannot resurrect sunk ship");

        w=new World();w.port.RequestRepair(w.ship);w.Deliver(ResourceType.Wood,2);w.port.Work(2);
        var refs=new Dictionary<string,UnityEngine.Object>{{"ship",w.ship}};
        var codec=new SaveStateCodec{Reference=obj=>{foreach(var p in refs)if(p.Value==obj)return p.Key;return null;},Resolve=key=>refs.TryGetValue(key,out var obj)?obj:null};
        var data=codec.CaptureFields(w.port);w.port.CancelRepair();codec.RestoreFields(w.port,data);w.port.RestoreSavedRepairHooks();
        Check(w.port.IsRepairing&&w.port.RepairTarget==w.ship&&w.ship.RepairPort==w.port&&w.port.WorkProgress==2,"save restores target, claim and contributed worker time");
        w.port.Work(3);Check(w.health.unitHealth==300&&w.port.Storage.Amount(ResourceType.Wood)==0,"saved repair finishes once without free materials");
        w=new World(200);w.port.RequestRepair(w.ship);w.Deliver(ResourceType.Wood,2);w.port.Work(100);
        Check(w.health.unitHealth==250&&w.port.WorkProgress==0,"worker cannot bank future work without materials");
        w.Deliver(ResourceType.Wood,2);w.port.Work(1);Check(w.health.unitHealth==250,"newly supplied cycle still requires work");
        Check(w.port.Collectable(ResourceType.Wood)==0,"repair stock protected from carrier surplus collection");
        w.port.CancelRepair();var owner=new UnityEngine.Object();
        Check(w.port.Storage.ReserveOutgoing(owner,ResourceType.Wood,2),"older carrier reserves delivered materials");
        w.port.RequestRepair(w.ship);w.port.Work(100);
        Check(w.health.unitHealth==250&&w.port.Storage.Amount(ResourceType.Wood)==2,"repair cannot steal carrier-reserved stock");
        w.port.Storage.CancelOutgoing(owner);w.port.Work(5);Check(w.health.unitHealth==300,"repair resumes after reservation releases");
        w=new World();w.port.storageCapacity=2;w.port.repairIronPerCycle=1;
        Check(!w.port.RequestRepair(w.ship),"whole mixed repair cycle must fit shared port capacity");
        w=new World();w.port.GetComponent<GettingResources>().Set(ResourceType.Wood,10);
        Check(w.port.BeginLoading(ResourceType.Wood,10),"cargo operation before repair");w.port.Work(1);
        var hold=w.ship.GetComponent<ShipCargo>().Hold;
        Check(hold.FreeSpace==190&&w.port.RequestRepair(w.ship),"repair takes priority over unfinished cargo batch");
        Check(w.port.OrderCount==0&&hold.FreeSpace==200&&!w.port.BeginLoading(ResourceType.Wood,1),"repair releases both cargo claims and blocks new loading");
        w.port.Work(5);
        Check(w.health.unitHealth==300&&hold.Count==0&&w.port.Storage.Amount(ResourceType.Wood)==8,"port workers repair from local stock without moving cancelled cargo");
        var oldYard=new Shipyard();
        codec.RestoreFields(oldYard,new List<SaveField>{new SaveField{name="Shipyard/repairTarget",value=new SaveValue{kind="null"}},
            new SaveField{name="Shipyard/<RepairStatus>k__BackingField",value=new SaveValue{kind="null"}}});
        codec.RestoreFields(w.ship,new List<SaveField>{new SaveField{name="ShipHull/<RepairYard>k__BackingField",value=new SaveValue{kind="null"}}});
        Console.WriteLine("PASS: actual port repair, physical costs, worker time, partial healing, cancellation, road/owner/death handling, shared stock reservations and saved progress");
    }
}
