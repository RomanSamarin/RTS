using System;
using System.Collections.Generic;
using _Game.Scripts.UI;
using UnityEngine;

// Navigation is an arrival adapter; Port, CargoHold, ShipCargo, ShipProvisions and the route execute production code.
public class ShipNavigation : MonoBehaviour
{
    public enum Motion{Idle,Sailing,Mooring,Moored,Waiting}
    public Motion State=Motion.Idle;
    public ShipSupplyDock DestinationDock;
    public int CommandRevision;
    public string Status="",LastOrderError="Нет пути";
    public bool blocked;
    public bool deferArrival;
    public int MoorCalls,SailCalls;
    public event Action ManualOrderIssued;
    public bool IsMooredTo(ShipSupplyDock dock)=>dock!=null&&State==Motion.Moored&&DestinationDock==dock&&dock.Ship==GetComponent<ShipProvisions>();
    public bool MoorForRoute(ShipSupplyDock dock)
    {
        MoorCalls++;if(blocked||!dock.CanReserve(this))return false;
        var food=GetComponent<ShipProvisions>();if(food.Dock!=null)food.Dock.ReleaseShip(food);
        if(deferArrival){DestinationDock=dock;State=Motion.Sailing;CommandRevision++;return true;}
        dock.ship=food;food.SetDock(dock);DestinationDock=dock;State=Motion.Moored;CommandRevision++;transform.position=dock.BerthPoint;return true;
    }
    public bool MoorAt(ShipSupplyDock dock){bool result=MoorForRoute(dock);if(result)ManualOrderIssued?.Invoke();return result;}
    public bool SailForRoute(Vector3 point)
    {
        if(blocked)return false;SailCalls++;var food=GetComponent<ShipProvisions>();if(food.Dock!=null)food.Dock.ReleaseShip(food);
        DestinationDock=null;State=Motion.Idle;CommandRevision++;transform.position=point;return true;
    }
    public void StopForRoute(){State=Motion.Idle;DestinationDock=null;CommandRevision++;}
    public void ManualMove(){SailForRoute(new Vector3(50,0,50));State=Motion.Sailing;ManualOrderIssued?.Invoke();}
}
public class ShipCrew : MonoBehaviour{public bool Ready=true;}
public class BuildingHealth{public bool IsDead;}
namespace UnityEngine{public class ContextMenu:Attribute{public ContextMenu(string name){}}}

static class ShipTradeRouteChecks
{
    static void Check(bool condition,string message){if(!condition)throw new Exception("Freight route: "+message);}
    sealed class World
    {
        public Port a,b;public ShipTradeRoute route;public ShipNavigation nav;public ShipCargo cargo;public ShipProvisions food;public ShipCrew crew;
        public GettingResources bag,aBag,bBag;
        public World()
        {
            var root=new GameObject();var pose=new Transform();bag=new GettingResources();
            nav=new ShipNavigation{gameObject=root,transform=pose};cargo=new ShipCargo{gameObject=root};food=new ShipProvisions{gameObject=root};crew=new ShipCrew{gameObject=root};
            route=new ShipTradeRoute{gameObject=root,transform=pose,departureMeals=0,repeat=false};
            root.With(bag).With(new ShipHull{gameObject=root}).With(nav).With(cargo).With(food).With(crew).With(route);
            a=MakePort(0,out aBag);b=MakePort(100,out bBag);
            route.sourcePort=a;route.destinationPort=b;route.outbound.Add(new ShipTradeRoute.Freight{type=ResourceType.Wood,amount=40});
            nav.MoorForRoute(a.Dock);
        }
        Port MakePort(float x,out GettingResources inventory)
        {
            var root=new GameObject();inventory=new GettingResources();
            var dock=new ShipSupplyDock{gameObject=root,transform=new Transform{position=new Vector3(x,0,0)}};
            var port=new Port{gameObject=root};root.With(inventory).With(dock).With(port).With(new BuildingHealth());return port;
        }
        public void Step(bool work=true)
        {Time.time+=2;route.Tick();if(work){a.Work(2);b.Work(2);}route.Tick();}
        public void Finish(int maximum=150)
        {for(int i=0;i<maximum&&route.Current!=ShipTradeRoute.Phase.Complete&&route.Current!=ShipTradeRoute.Phase.Paused;i++)Step();}
    }
    public static void Run()
    {
        var w=new World();w.aBag.Set(ResourceType.Wood,80);w.aBag.Set(ResourceType.Iron,20);w.bBag.Set(ResourceType.Stone,30);
        w.route.outbound.Add(new ShipTradeRoute.Freight{type=ResourceType.Iron,amount=20});
        w.route.returnCargo.Add(new ShipTradeRoute.Freight{type=ResourceType.Stone,amount=30});
        Check(w.route.TryStartRoute(),"valid two-port route starts");w.Finish();
        Check(w.route.Current==ShipTradeRoute.Phase.Complete&&w.route.CompletedCycles==1&&w.nav.IsMooredTo(w.a.Dock),"one cycle returns and stops at origin");
        Check(w.aBag.GetStoredAmount(ResourceType.Wood)==40&&w.bBag.GetStoredAmount(ResourceType.Wood)==40
            &&w.bBag.GetStoredAmount(ResourceType.Iron)==20&&w.aBag.GetStoredAmount(ResourceType.Stone)==30&&w.cargo.Stored==0,"multi-commodity outbound and return use real inventory exactly once");
        Check(w.a.StoredOutput==70,"completed route releases its stock requests");

        w=new World();w.route.repeat=true;w.aBag.Set(ResourceType.Wood,80);Check(w.route.TryStartRoute(),"repeat starts");
        for(int i=0;i<150&&w.route.CompletedCycles<2;i++)w.Step();
        Check(w.route.CompletedCycles==2&&w.bBag.GetStoredAmount(ResourceType.Wood)==80&&w.aBag.GetStoredAmount(ResourceType.Wood)==0,"two repeat cycles, no duplication");
        w.route.CancelRoute();

        w=new World();Check(w.route.TryStartRoute(),"empty source may wait");w.Step(false);w.Step(false);
        Check(w.route.Current==ShipTradeRoute.Phase.LoadingOutbound&&w.a.RequestedInputFor(ResourceType.Wood)==40&&w.cargo.Stored==0,"waits for physical carrier supply");
        w.aBag.Set(ResourceType.Iron,20);w.route.CancelRoute();
        Check(w.a.RequestedInputFor(ResourceType.Wood)==0&&w.a.TransferOwner==null,"cancel removes requests and operation ownership");

        w=new World();w.aBag.Set(ResourceType.Wood,40);w.b.storageCapacity=10;Check(w.route.TryStartRoute(),"full destination route starts");
        for(int i=0;i<35;i++)w.Step();
        Check(w.bBag.GetStoredAmount(ResourceType.Wood)==10&&w.b.OrderCount==1&&w.b.Remaining==30&&w.cargo.Stored==30,"full receiving port retains remainder aboard");
        w.route.PauseRoute();Check(w.cargo.Stored==30&&w.cargo.Hold.Available(ResourceType.Wood)==30&&w.b.TransferOwner==null,"pause keeps cargo and releases both reservations");
        w.bBag.Set(ResourceType.Wood,0);w.b.storageCapacity=300;w.route.ResumeRoute();w.Finish();
        Check(w.bBag.GetStoredAmount(ResourceType.Wood)==30&&w.route.CompletedCycles==1,"resume delivers only remaining cargo");

        w=new World();w.bag.Set(ResourceType.Wood,80);Check(w.route.TryStartRoute(),"pre-existing cargo accepted");w.Finish();
        Check(w.bBag.GetStoredAmount(ResourceType.Wood)==40&&w.bag.GetStoredAmount(ResourceType.Wood)==40,"manifest amount does not unload excess cargo");

        w=new World();w.bag.Set(ResourceType.Wood,80);w.route.TryStartRoute();
        for(int i=0;i<20&&w.bBag.GetStoredAmount(ResourceType.Wood)==0;i++)w.Step();
        Check(w.bBag.GetStoredAmount(ResourceType.Wood)==10,"first owned unload batch completed");
        w.b.BeginUnloading(ResourceType.Wood,20);w.b.Work(2);w.b.Work(2);w.route.ResumeRoute();w.Finish();
        Check(w.bBag.GetStoredAmount(ResourceType.Wood)==40&&w.bag.GetStoredAmount(ResourceType.Wood)==40,
            "manual unload while paused reduces remaining delivery even with excess goods aboard");

        w=new World();w.aBag.Set(ResourceType.Wood,40);w.route.TryStartRoute();w.Step(false);
        w.a.BeginLoading(ResourceType.Iron,3);
        Check(w.route.Current==ShipTradeRoute.Phase.Paused&&w.a.Remaining==3&&w.a.TransferOwner==null,"manual cargo order pauses automation without being cancelled");
        w.route.CancelRoute();Check(w.a.OrderCount==1&&w.a.Remaining==3,"cancelling paused route never cancels unrelated manual order");

        w=new World();w.aBag.Set(ResourceType.Wood,40);w.route.TryStartRoute();w.Step(false);w.nav.ManualMove();
        Check(w.route.Current==ShipTradeRoute.Phase.Paused&&w.nav.State==ShipNavigation.Motion.Sailing,"manual movement wins");
        int revision=w.nav.CommandRevision;w.route.Tick();Check(w.nav.CommandRevision==revision,"paused route does not overwrite manual movement");

        w=new World();w.aBag.Set(ResourceType.Wood,40);w.route.departureMeals=4;w.route.TryStartRoute();
        for(int i=0;i<12;i++)w.Step();
        Check(w.nav.IsMooredTo(w.a.Dock)&&w.cargo.Stored==40,"loaded ship waits for real provisions");
        var courier=new UnityEngine.Object();var mealPack=new GettingResources();mealPack.Set(ResourceType.Bread,4);
        Check(w.food.ReserveDelivery(courier,w.a.Dock,4)&&w.food.Receive(courier,w.a.Dock,mealPack)==4,"food is physically delivered");
        w.Finish();Check(w.route.CompletedCycles==1,"sails once departure food threshold reached");

        w=new World();w.aBag.Set(ResourceType.Wood,40);w.crew.Ready=false;w.route.TryStartRoute();for(int i=0;i<12;i++)w.Step();
        Check(w.nav.IsMooredTo(w.a.Dock)&&w.cargo.Stored==40,"incomplete crew cannot depart");
        w.crew.Ready=true;w.Finish();Check(w.route.CompletedCycles==1,"crew availability resumes route");

        w=new World();w.aBag.Set(ResourceType.Wood,40);w.b.Dock.busy=true;w.route.TryStartRoute();for(int i=0;i<15;i++)w.Step();
        Check(w.nav.SailCalls==1&&w.a.Dock.Ship==null&&w.cargo.Stored==40,"busy destination frees origin berth and waits without deleting cargo");
        w.b.Dock.busy=false;w.Finish();Check(w.route.CompletedCycles==1,"free berth restarts travel");

        w=new World();w.aBag.Set(ResourceType.Wood,40);w.route.departureMeals=4;w.b.Dock.busy=true;
        var foodCarrier=new UnityEngine.Object();var foodPack=new GettingResources();foodPack.Set(ResourceType.Bread,4);
        w.food.ReserveDelivery(foodCarrier,w.a.Dock,4);w.food.Receive(foodCarrier,w.a.Dock,foodPack);w.route.TryStartRoute();
        for(int i=0;i<15;i++)w.Step();
        Check(w.a.Dock.Ship==null&&w.food.TryConsume(1),"food may be consumed while waiting outside a busy port");
        w.b.Dock.busy=false;for(int i=0;i<10&&!w.nav.IsMooredTo(w.b.Dock);i++)w.Step();
        Check(w.nav.IsMooredTo(w.b.Dock)&&w.food.StoredMeals==3,"departure threshold never strands a hungry ship outside the resupply port");

        // Save the real route, port order and both reservation holders halfway through a work batch.
        w=new World();w.aBag.Set(ResourceType.Wood,40);w.route.TryStartRoute();w.Step(false);w.a.Work(1);
        var refs=new Dictionary<string,UnityEngine.Object>{{"route",w.route},{"a",w.a},{"b",w.b},{"cargo",w.cargo}};
        var codec=new SaveStateCodec{Reference=obj=>{foreach(var pair in refs)if(pair.Value==obj)return pair.Key;return null;},Resolve=key=>refs.TryGetValue(key,out var obj)?obj:null};
        var routeData=codec.CaptureFields(w.route);var portData=codec.CaptureFields(w.a);var holdData=codec.CaptureFields(w.cargo.Hold);
        var restored=new ShipTradeRoute{gameObject=w.route.gameObject,transform=w.nav.transform};
        w.route.CancelRoute();w.route.gameObject.With(restored);refs["route"]=restored;
        codec.timeOffset=1000;codec.RestoreFields(w.a,portData);codec.RestoreFields(w.cargo.Hold,holdData);codec.RestoreFields(restored,routeData);
        w.route=restored;w.route.RestoreSavedHooks();w.a.Work(1);w.route.Tick();
        Check(w.cargo.Stored==10&&w.a.Remaining==30,"saved half batch completes once with original reservations");
        w.Finish();Check(w.route.CompletedCycles==1&&w.bBag.GetStoredAmount(ResourceType.Wood)==40&&w.cargo.Stored==0,"saved route completes without restarting manifest or duplicating goods");

        w=new World();w.route.outbound[0].amount=201;Check(!w.route.TryStartRoute(),"oversized manifest rejected before changing orders");
        w.route.outbound[0].amount=40;FactionMember.Set(w.b.gameObject,Faction.Bandits);Check(!w.route.TryStartRoute(),"foreign port rejected");
        FactionMember.Set(w.b.gameObject,Faction.Player);w.aBag.Set(ResourceType.Wood,40);w.route.TryStartRoute();w.Step(false);
        w.b.GetComponent<BuildingHealth>().IsDead=true;w.route.Tick();
        Check(w.route.Current==ShipTradeRoute.Phase.Paused&&w.a.TransferOwner==null,"destroyed destination releases operation");
        Console.WriteLine("PASS: actual freight cycles, repeats, return cargo, carrier demand, capacity waits, manual priority, food/crew, busy berths and saved half-batch reservations");
    }
}
