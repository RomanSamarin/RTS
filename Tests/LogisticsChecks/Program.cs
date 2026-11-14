using System;
using System.Collections.Generic;
using _Game.Scripts.UI;

// Изолированная проверка реальной логики WarehouseStorage; Unity lifecycle/NavMesh здесь не моделируются.
class Program
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Main()
    {
        FactionChecks.Run();
        HospitalChecks.Run();
        ArmyRejoinChecks.Run();
        MeleeCombatChecks.Run();
        SiegeChecks.Run();
        SiegeLadderCartChecks.Run();
        LadderClimbChecks.Run();
        WorkerAnimationChecks.Run();
        ShipyardChecks.Run();
        ShipRepairChecks.Run();
        ShipPathChecks.Run();
        ShipCrewChecks.Run();
        ShipTrafficChecks.Run();
        ShipTradeRouteChecks.Run();
        ShipPassengerChecks.Run();
        ShipProvisionsChecks.Run();
        PortChecks.Run();
        TradeYardChecks.Run();
        SquadMovementChecks.Run();
        RecipeChecks.Run();
        SmokehouseChecks.Run();
        GatheringChecks.Run();
        MovementOrderChecks.Run();
        DeliveryPriorityChecks.Run();
        SaveGameChecks.Run();
        PatrolChecks.Run();
        HungerChecks.Run();
        EquipmentProductionChecks.Run();
        TrainingEquipmentChecks.Run();
        StableChecks.Run();
        ArmySupplyChecks.Run();
        TroopCombatChecks.Run();
        FormationCombatChecks.Run();
        ArrowTrajectoryChecks.Run();
        WallLineChecks.Run();
        RoadChecks.Run();
        RoadCurveChecks.Run();
        RoadNavigationChecks.Run();
        RaidChecks.Run();
        var warehouse = new WarehouseStorage();
        Check(warehouse.SetCapacity(10), "capacity");
        var first = new UnityEngine.Object(); var second = new UnityEngine.Object();
        Check(warehouse.TryReserveSpace(first, 7), "first carrier reserves seven slots");
        Check(!warehouse.TryReserveSpace(second, 4), "second carrier cannot overbook destination");
        Check(!warehouse.TryReserveSpace(first, 1), "same owner cannot reserve twice");
        Check(warehouse.AddResource(ResourceType.Stone, 10) == 3, "unreserved deposit cannot consume reserved slots");
        Check(!warehouse.SetCapacity(9), "capacity cannot shrink through reservations");
        var cargo = new GettingResources(); cargo.Set(ResourceType.Wood, 7);
        Check(warehouse.DepositReserved(first, cargo) == 7, "reserved delivery succeeds");
        Check(warehouse.UsedCapacity == 10 && warehouse.ReservedCapacity == 0 && cargo.GetStoredAmount(ResourceType.Wood) == 0, "no duplication or reservation leak");
        Check(warehouse.TrySpendResource(ResourceType.Stone, 3), "free slots");
        Check(warehouse.TryReserveSpace(second, 3), "reserve freed space");
        warehouse.ReleaseSpace(second);
        Check(warehouse.FreeCapacity == 3, "cancellation releases slots");
        Check(warehouse.TryReserveSpace(second, 3), "reserve again");
        second.destroyed = true;
        Check(warehouse.FreeCapacity == 3 && warehouse.ReservedCapacity == 0, "destroyed owner releases slots");
        var overflow = new GettingResources(); overflow.Set(ResourceType.Iron, 8);
        Check(warehouse.Deposit(overflow) == 3 && overflow.GetStoredAmount(ResourceType.Iron) == 5, "partial delivery preserves remainder");
        var granary = new WarehouseStorage();
        typeof(WarehouseStorage).GetField("foodOnly", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(granary, true);
        Check(granary.SetCapacity(8) && granary.AddResource(ResourceType.Wood, 5) == 0, "granary refuses building materials");
        var mixedCargo = new GettingResources();
        mixedCargo.Set(ResourceType.Wheat, 3);
        mixedCargo.Set(ResourceType.Fish, 2);
        mixedCargo.Set(ResourceType.Bread, 3);
        mixedCargo.Set(ResourceType.Iron, 4);
        Check(granary.AcceptedCargo(mixedCargo) == 8 && granary.TryReserveSpace(first, 8)
            && granary.DepositReserved(first, mixedCargo) == 8, "granary reserves and accepts only food from mixed cargo");
        Check(mixedCargo.GetStoredAmount(ResourceType.Iron) == 4 && granary.Food == 3
            && !granary.TryReserveFood(first, 4), "raw fish and wheat are not meals");
        Check(granary.TryReserveFood(first, 3) && granary.PickupReservedFood(first, mixedCargo) == 3
            && mixedCargo.GetStoredAmount(ResourceType.Bread) == 3 && granary.UsedCapacity == 5, "granary dispatches real bread");
        var kitchen = new WarehouseStorage();
        kitchen.AddResource(ResourceType.Wheat, 10);
        kitchen.AddResource(ResourceType.Flour, 10);
        Check(kitchen.Food == 0 && !kitchen.TrySpendResource(ResourceType.Food, 1), "ingredients are not meals");
        kitchen.AddResource(ResourceType.Bread, 2);
        kitchen.AddResource(ResourceType.Food, 1);
        Check(kitchen.TrySpendResource(ResourceType.Food, 1) && kitchen.Food == 2, "serving consumes exactly one meal");
        Check(!kitchen.TrySpendResource(ResourceType.Food, 3) && kitchen.Food == 2, "failed serving does not consume stock");
        Check(kitchen.TrySpendResource(ResourceType.Food, 2) && kitchen.Food == 0, "mixed meals consumed once");
        Check(kitchen.GetAmount(ResourceType.Wheat) == 10 && kitchen.GetAmount(ResourceType.Flour) == 10, "ingredients preserved");
        var supply = new WarehouseStorage(); supply.AddResource(ResourceType.Bread, 10);
        var tavern = new Tavern { foodStorage = supply, capacity = 5 };
        var courier = new UnityEngine.Object(); var rival = new UnityEngine.Object();
        Check(tavern.AvailableMeals == 0 && !tavern.TryServe(1), "warehouse stock does not feed tavern remotely");
        Check(supply.TryReserveFood(courier, 5) && tavern.ReserveDelivery(courier, 5), "reserve food and tavern capacity");
        Check(!tavern.ReserveDelivery(rival, 1), "no destination overbooking");
        Check(!supply.TrySpendResource(ResourceType.Food, 6), "reserved food cannot be spent");
        var shipment = new GettingResources();
        Check(supply.PickupReservedFood(courier, shipment) == 5 && supply.Food == 5 && shipment.Bread == 5, "actual pickup transfers food kind");
        Check(tavern.AvailableMeals == 0, "food unavailable while in transit");
        Check(tavern.Receive(courier, shipment) == 5 && shipment.Bread == 0 && tavern.AvailableMeals == 5, "delivery transfers inventory");
        Check(tavern.TryServe(1) && tavern.AvailableMeals == 4 && supply.Food == 5, "meal consumes local stock only");
        Check(supply.TryReserveFood(courier, 2), "reserve cancellation setup");
        supply.ReleaseFood(courier);
        Check(supply.AvailableFood == 5, "food reservation cancellation");
        var builderStock = new WarehouseStorage(); builderStock.AddResource(ResourceType.Wood, 20);
        builderStock.AddResource(ResourceType.Bread, 5);
        var siteA = new UnityEngine.Object(); var siteB = new UnityEngine.Object();
        Check(builderStock.ReserveMaterial(siteA, ResourceType.Wood, 15), "reserve construction stock");
        Check(builderStock.UsedCapacity == 25 && builderStock.GetStoredAmount(ResourceType.Wood) == 20, "reservation does not remove stock");
        Check(!builderStock.ReserveMaterial(siteB, ResourceType.Wood, 6), "sites cannot reserve same wood");
        Check(!builderStock.TrySpendResource(ResourceType.Wood, 6), "ordinary spending cannot consume material reserve");
        var pack = new GettingResources();
        Check(builderStock.PickupMaterial(siteA, ResourceType.Wood, 10, pack) == 10 && pack.GetStoredAmount(ResourceType.Wood) == 10, "builder takes limited batch");
        Check(builderStock.MaterialAmount(siteA, ResourceType.Wood) == 5 && builderStock.GetStoredAmount(ResourceType.Wood) == 10, "picked reserve and physical stock decrease once");
        builderStock.ReleaseMaterials(siteA);
        Check(builderStock.GetAmount(ResourceType.Wood) == 10 && pack.GetStoredAmount(ResourceType.Wood) == 10, "cancellation releases only uncollected stock");
        Check(builderStock.ReserveMaterial(siteB, ResourceType.Bread, 4), "reserve edible construction requirement");
        Check(!builderStock.TryReserveFood(courier, 2), "tavern cannot claim fish reserved for construction");
        Check(builderStock.TryReserveFood(courier, 1), "last unreserved food available");
        var fishPack = new GettingResources();
        Check(builderStock.PickupReservedFood(courier, fishPack) == 1 && builderStock.GetStoredAmount(ResourceType.Bread) == 4, "food pickup protects construction materials");
        siteB.destroyed = true;
        Check(builderStock.GetAmount(ResourceType.Bread) == 4, "destroyed site releases stock");
        var mixed = MaterialLoadPlan.Create(new Dictionary<ResourceType,int> { [ResourceType.Wood] = 20, [ResourceType.Stone] = 20 }, 10);
        Check(mixed[ResourceType.Wood] == 5 && mixed[ResourceType.Stone] == 5, "builder mixes materials within total capacity");
        mixed = MaterialLoadPlan.Create(new Dictionary<ResourceType,int> { [ResourceType.Wood] = 1, [ResourceType.Stone] = 20 }, 10);
        Check(mixed[ResourceType.Wood] == 1 && mixed[ResourceType.Stone] == 9, "unused share filled by remaining material");
        Check(MaterialLoadPlan.Create(new Dictionary<ResourceType,int> { [ResourceType.Wood] = 10 }, 0).Count == 0, "zero capacity gives empty shipment");
        var thresholdTavern = new Tavern { capacity = 10, restockThreshold = 3 };
        var token = new UnityEngine.Object(); var provisions = new GettingResources(); provisions.PreparedFood = 10;
        Check(thresholdTavern.ReserveDelivery(token, 10) && thresholdTavern.Receive(token, provisions) == 10, "initial restock");
        thresholdTavern.TryServe(7);
        Check(thresholdTavern.RequestedMeals == 0, "no restock at exact threshold");
        thresholdTavern.TryServe(1);
        Check(thresholdTavern.RequestedMeals == 8, "restock starts below threshold");
        provisions.PreparedFood = 3;
        Check(thresholdTavern.ReserveDelivery(token, 3) && thresholdTavern.Receive(token, provisions) == 3, "partial refill");
        Check(thresholdTavern.AvailableMeals == 5 && thresholdTavern.RequestedMeals == 5, "started batch continues to capacity");
        provisions.PreparedFood = 5;
        Check(thresholdTavern.ReserveDelivery(token, 5) && thresholdTavern.Receive(token, provisions) == 5, "finish restock");
        thresholdTavern.TryServe(1);
        Check(thresholdTavern.RequestedMeals == 0, "no refill after every served meal");
        thresholdTavern.TryServe(8);
        Check(thresholdTavern.ReserveDelivery(token, 4), "reserve first shipment after new low stock");
        thresholdTavern.CancelDelivery(token);
        var resumedCargo = new GettingResources(); resumedCargo.PreparedFood = 4;
        Check(thresholdTavern.ReserveDelivery(token, resumedCargo.Food), "loaded courier can rebook cancelled delivery without warehouse pickup");
        Check(thresholdTavern.Receive(token, resumedCargo) == 4 && resumedCargo.Food == 0, "resumed delivery actually unloads");
        Check(thresholdTavern.RequestedMeals == 5, "partial delivery above threshold still requests remainder");
        Check(thresholdTavern.ReserveDelivery(rival, 5) && thresholdTavern.RequestedMeals == 0, "incoming remainder counted once");
        thresholdTavern.CancelDelivery(rival);
        Check(thresholdTavern.RequestedMeals == 5, "cancelled remainder requested again");
        Console.WriteLine("PASS: logistics, construction mixed loads, tavern threshold and restock cycles.");
    }
}

// Минимальный адаптер окружения для исполнения класса склада без запущенного Unity Editor.
namespace UnityEngine
{
    public class Sprite : Object { }
    public class Object
    {
        public bool destroyed;
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b) || (ReferenceEquals(a, null) && !ReferenceEquals(b, null) && b.destroyed) || (ReferenceEquals(b, null) && !ReferenceEquals(a, null) && a.destroyed);
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class GameObject : Object
    {
        private readonly Dictionary<Type, object> components = new Dictionary<Type, object>();
        public static int SpawnCount;
        public void SetActive(bool value) { }
        public GameObject With<T>(T component) { components[typeof(T)] = component; return this; }
        public T GetComponent<T>() => components.TryGetValue(typeof(T), out var value) ? (T)value : default;
    }
    public class Transform { public Vector3 position, forward = new Vector3(0, 0, 1); public Quaternion rotation; }
    public struct Quaternion { }
    public struct Vector3
    {
        public static Vector3 zero=>new Vector3();
        public static Vector3 forward=>new Vector3(0,0,1);
        public static Vector3 right=>new Vector3(1,0,0);
        public float x, y, z;
        public float sqrMagnitude=>x*x+y*y+z*z;
        public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
        public Vector3 normalized=>magnitude>.000001f?this/magnitude:new Vector3();
        public Vector3(float x,float y,float z) { this.x=x; this.y=y; this.z=z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static float Dot(Vector3 a, Vector3 b) => a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 operator *(Vector3 a, float b) => new Vector3(a.x*b,a.y*b,a.z*b);
        public static Vector3 operator *(float b,Vector3 a)=>a*b;
        public static Vector3 operator /(Vector3 a,float b)=>new Vector3(a.x/b,a.y/b,a.z/b);
        public static Vector3 ClampMagnitude(Vector3 v,float max)=>v.magnitude>max?v.normalized*max:v;
        public static float Distance(Vector3 a, Vector3 b) => (float)Math.Sqrt(Math.Pow(a.x-b.x,2)+Math.Pow(a.y-b.y,2)+Math.Pow(a.z-b.z,2));
    }
    public struct Bounds
    {
        public Vector3 center,extents;
        public Bounds(Vector3 center,Vector3 size){this.center=center;extents=size*.5f;}
    }
    public static class Time { public static float time, deltaTime; }
    public class MonoBehaviour : Object
    {
        public GameObject gameObject = new GameObject(); public Transform transform = new Transform(); public bool isActiveAndEnabled = true;
        public T GetComponent<T>() => gameObject.GetComponent<T>();
        protected static T[] FindObjectsOfType<T>() => typeof(T) == typeof(UnityEngine.AI.NavMeshAgent)
            ? (T[])(object)UnityEngine.AI.NavMeshAgent.TestAgents : Array.Empty<T>();
        protected static GameObject Instantiate(GameObject prefab, Vector3 point, Quaternion rotation)
        { GameObject.SpawnCount++; return new GameObject().With(new Unit()); }
    }
    public class DisallowMultipleComponent : Attribute { }
    public class RequireComponent : Attribute { public RequireComponent(params Type[] types) { } }
    public class SerializeField : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string value) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string value) { } }
    public static class Mathf
    {
        public const float PI=(float)Math.PI;
        public static float Sin(float value)=>(float)Math.Sin(value);
        public static float Cos(float value)=>(float)Math.Cos(value);
        public static float Clamp01(float value) => Math.Clamp(value,0,1);
        public static float Abs(float value) => Math.Abs(value);
        public static float Sqrt(float value) => (float)Math.Sqrt(value);
        public static int CeilToInt(float value)=>(int)Math.Ceiling(value);
        public static int Min(int a, int b) => Math.Min(a,b);
        public static int Max(int a, int b) => Math.Max(a,b);
        public static float Max(float a, float b) => Math.Max(a,b);
        public static float Min(float a, float b) => Math.Min(a,b);
        public static int FloorToInt(float value) => (int)Math.Floor(value);
        public static int Clamp(int x, int min, int max) => Math.Clamp(x,min,max);
    }
}
namespace UnityEngine.Serialization { public class FormerlySerializedAsAttribute : Attribute { public FormerlySerializedAsAttribute(string name) { } } }
public class GettingResources
{
    private readonly Dictionary<ResourceType,int> items = new Dictionary<ResourceType,int>();
    public int GetStoredAmount(ResourceType type) => items.TryGetValue(type, out int value) ? value : 0;
    public void Set(ResourceType type, int amount) => items[type] = amount;
    public void Clear() => items.Clear();
    public void AddStoredAmount(ResourceType type, int amount) => items[type] = GetStoredAmount(type) + amount;
    public void RemoveStoredAmount(ResourceType type, int amount) => items[type] = GetStoredAmount(type) - amount;
    public int PreparedFood { get => GetStoredAmount(ResourceType.Food); set => Set(ResourceType.Food, value); }
    public int Bread { get => GetStoredAmount(ResourceType.Bread); set => Set(ResourceType.Bread, value); }
    public int Fish { get => GetStoredAmount(ResourceType.Fish); set => Set(ResourceType.Fish, value); }
    public int Meat { get => GetStoredAmount(ResourceType.Meat); set => Set(ResourceType.Meat, value); }
    public int Berries { get => GetStoredAmount(ResourceType.Berries); set => Set(ResourceType.Berries, value); }
    public int Food => PreparedFood + Bread + Berries;
}
public static class BuildingEntrance { public static bool TryGetWorldPoint(UnityEngine.GameObject obj, out UnityEngine.Vector3 point) { point = default; return false; } }
namespace _Game.Scripts.UI { public class ResourcesManager { public static ResourcesManager Instance; public void UpdateUI() { } } }
