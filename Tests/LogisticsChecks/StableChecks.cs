using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using _Game.Scripts.UI;

public static class StableChecks
{
    private static void Check(bool ok, string label) { if (!ok) throw new Exception("Stable: " + label); }
    private static void Tick(Stable stable, float seconds)
    { Time.deltaTime = seconds; Time.time += seconds; typeof(Stable).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stable,null); }
    private static Stable Create() => new Stable { horsePrefab = new GameObject().With(new Unit()).With(new CartHorse()).With(new NavMeshAgent()), horseSeconds=45 };
    public static void Run()
    {
        var stable = Create(); var food = new ObjectOwner(); var gold = new ObjectOwner();
        Check(stable.TryQueueHorse(),"queue valid horse");
        Tick(stable,100);
        Check(stable.Progress == 0 && stable.QueueCount == 1,"no progress without delivered inputs");
        var warehouse = new WarehouseStorage(); warehouse.SetCapacity(100); warehouse.AddResource(ResourceType.Bread,10); warehouse.AddResource(ResourceType.Gold,5);
        Check(stable.ReserveSupply(food,warehouse,ResourceType.Food,10),"reserve aggregate edible food");
        Check(stable.ReserveDelivery(food,ResourceType.Food,10),"reserve destination");
        Check(!stable.ReserveDelivery(new ObjectOwner(),ResourceType.Food,1),"no competing overbooking");
        var pack = new GettingResources();
        Check(stable.PickupSupply(food,warehouse,ResourceType.Food,10,pack)==10 && pack.Bread==10,"physical typed food pickup");
        Check(stable.Receive(food,pack)==10 && pack.Food==0 && stable.StoredFood==10,"delivery drains food once");
        Check(stable.ReserveSupply(gold,warehouse,ResourceType.Gold,5) && stable.ReserveDelivery(gold,ResourceType.Gold,5),"gold reservations");
        Check(stable.PickupSupply(gold,warehouse,ResourceType.Gold,5,pack)==5 && stable.Receive(gold,pack)==5,"physical gold delivery");
        int before=GameObject.SpawnCount;
        NavMesh.CanSample=false; Tick(stable,45);
        Check(stable.QueueCount==1 && stable.StoredFood==10 && stable.StoredGold==5 && stable.Progress==1,"blocked exit preserves paid inputs and completed progress");
        NavMesh.CanSample=true; Tick(stable,1);
        Check(stable.QueueCount==0 && stable.StoredInput==0 && GameObject.SpawnCount==before+1,"one horse consumes one batch once");
        Tick(stable,100); Check(GameObject.SpawnCount==before+1,"empty queue cannot spawn");

        var cancelled=Create(); cancelled.TryQueueHorse();
        var carrier=new ObjectOwner(); var mixed=new GettingResources(); mixed.Bread=10;
        cancelled.ReserveDelivery(carrier,ResourceType.Food,10); cancelled.CancelAllHorses();
        Check(cancelled.Receive(carrier,mixed)==10,"in-flight delivery accepted after cancellation");
        var returned=new GettingResources();
        Check(cancelled.Collect(returned,10)==10 && returned.Bread==10 && cancelled.StoredOutput==0,"cancelled food returns without changing type");
        cancelled.TryQueueHorse();
        CartHorse.All.Add(new CartHorse()); cancelled.maxNearbyHorses=1;
        var a=new ObjectOwner(); var b=new ObjectOwner(); pack.Set(ResourceType.Food,10); pack.Set(ResourceType.Gold,5);
        cancelled.ReserveDelivery(a,ResourceType.Food,10); cancelled.Receive(a,pack);
        cancelled.ReserveDelivery(b,ResourceType.Gold,5); cancelled.Receive(b,pack);
        Tick(cancelled,60); Check(cancelled.Progress==0,"full paddock pauses production");
        CartHorse.All.Clear();
        Console.WriteLine("PASS: stable physical food/gold delivery, competing reservations, blocked spawn, cancellation and paddock limit");
    }
    private class ObjectOwner : UnityEngine.Object { }
}

// Только адаптер окружения. Навигация Unity и визуальные префабы проверяются в Play Mode.
namespace UnityEngine.AI
{
    public struct NavMeshQueryFilter { public int agentTypeID,areaMask; }
    public struct NavMeshHit { public Vector3 position; }
    public class NavMeshAgent { public int agentTypeID,areaMask=-1; public float radius=.5f; public bool enabled=true; public Transform transform=new Transform(); public static NavMeshAgent[] TestAgents=Array.Empty<NavMeshAgent>(); }
    public static class NavMesh
    {
        public static bool CanSample=true;
        public static bool SamplePosition(Vector3 point,out NavMeshHit hit,float radius,NavMeshQueryFilter filter) { hit=new NavMeshHit { position=point }; return CanSample; }
    }
}
public class Unit { public float unitHealth,unitMaxHealth=300;public void Heal(float value){if(unitHealth>0)unitHealth=Math.Min(unitMaxHealth,unitHealth+value);} public void SetInitialHealthRatio(float value) { } }
public class WildlifeAnimal { }
public class EnemyAi { }
public static class WallNavigation { public const int AreaMask=1<<30; }
public class CartHorse
{
    public GameObject gameObject=new GameObject();
    public static readonly List<CartHorse> All=new List<CartHorse>();
    public bool IsAlive=true,IsHitched; public object TrainingOwner; public Transform transform=new Transform();
}
