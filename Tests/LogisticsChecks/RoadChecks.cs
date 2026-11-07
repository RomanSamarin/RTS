using System;
using _Game.Scripts.UI;

public static class RoadChecks
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Road: "+message);}
    public static void Run()
    {
        for(int mask=0;mask<16;mask++)
        {
            RoadTopology.Resolve(mask,out var shape,out int rotation);
            Check(RoadTopology.Ports(shape,rotation)==mask,"all 16 neighbour masks resolve exactly");
            Check(RoadTopology.Rotate(mask,4)==mask && RoadTopology.Rotate(RoadTopology.Rotate(mask,1),-1)==mask,"rotation roundtrip");
        }
        Check(RoadTopology.Ports(RoadShape.Corner,0)==3 && RoadTopology.Ports(RoadShape.Corner,1)==6,"corner canonical ports and clockwise turn");
        var line=RoadTopology.Line(0,0,3,2,false,128,out bool complete);
        Check(complete&&line.Count==6&&line[3]==(3,0)&&line[5]==(3,2),"L path includes endpoints without duplicate corner");
        var alternate=RoadTopology.Line(0,0,3,2,true,128,out complete);
        Check(complete&&alternate[2]==(0,2),"rotation changes bend direction");
        for(int i=1;i<line.Count;i++)Check(Math.Abs(line[i].x-line[i-1].x)+Math.Abs(line[i].z-line[i-1].z)==1,"no diagonal gaps");
        var limited=RoadTopology.Line(int.MinValue,0,int.MaxValue,0,false,128,out complete);
        Check(!complete&&limited.Count==128,"long drafts limited without integer subtraction overflow");
        Check(RoadTopology.DistanceSquared(5,0,1.25f,2.5f,1.8f)==0,"straight road reaches cell edge");
        Check(RoadTopology.DistanceSquared(3,0,-1.25f,2.5f,1f)>.5f,"missing corner branch not considered road");
        Check(RoadTopology.DistanceSquared(0,0,0,2.5f,1f)==0,"isolated road has central paved patch");
        // Проверяем реальное производство: потеря дороги не обнуляет накопленный прогресс.
        var mill=new Mill();var carrier=new UnityEngine.Object();var cargo=new GettingResources();cargo.Set(ResourceType.Wheat,1);
        mill.ReserveDelivery(carrier,1);mill.Receive(carrier,cargo);mill.Work(2);
        float before=mill.WorkProgress;
        BuildingRoadAccess.Blocked.Add(mill);mill.Work(100);
        Check(mill.WorkProgress==before&&mill.StoredInput==1&&mill.StoredOutput==0,"road outage pauses without consuming or resetting input/progress");
        BuildingRoadAccess.Blocked.Remove(mill);mill.Work(20);
        Check(mill.StoredOutput==1,"production resumes on reconnect");
        var store=new WarehouseStorage();store.SetCapacity(30);store.AddResource(ResourceType.Gold,10);
        var owner=new UnityEngine.Object();var backpack=new GettingResources();
        Check(store.ReserveMaterial(owner,ResourceType.Gold,5),"connected warehouse reserves outgoing cargo");
        BuildingRoadAccess.Blocked.Add(store);
        Check(store.PickupMaterial(owner,ResourceType.Gold,5,backpack)==0&&store.MaterialAmount(owner,ResourceType.Gold)==5,"road loss retains outgoing material reservation without transfer");
        Check(!store.TryReserveSpace(new UnityEngine.Object(),1)&&!store.ReserveMaterial(new UnityEngine.Object(),ResourceType.Gold,1),"offline warehouse rejects new jobs");
        backpack.Set(ResourceType.Wood,3);
        Check(store.Deposit(backpack)==0&&backpack.GetStoredAmount(ResourceType.Wood)==3,"offline unloading does not destroy cargo");
        BuildingRoadAccess.Blocked.Remove(store);
        Check(store.PickupMaterial(owner,ResourceType.Gold,5,backpack)==5&&store.GetStoredAmount(ResourceType.Gold)==5,"reconnection continues original reservation once");
        Console.WriteLine("PASS: road shapes/rotation, gapless L drafts, limits, paved footprint and production pause/resume");
    }
}
// Адаптер проверки доступа: геометрия/реестр дорог требуют Unity Play Mode.
public static class BuildingRoadAccess
{
    public static readonly System.Collections.Generic.HashSet<object> Blocked=new System.Collections.Generic.HashSet<object>();
    public static bool Allows(object building)=>!Blocked.Contains(building);
}
