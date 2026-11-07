using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using _Game.Scripts.UI;

/// <summary>Стоимость, резервы конкретных складов и доставка на одну стройку.</summary>
public class ConstructionMaterials : MonoBehaviour
{
    private readonly Dictionary<ResourceType,int> required = new Dictionary<ResourceType,int>();
    private readonly Dictionary<ResourceType,int> delivered = new Dictionary<ResourceType,int>();
    private readonly List<WarehouseStorage> stores = new List<WarehouseStorage>();
    private readonly Dictionary<Builder, Shipment> jobs = new Dictionary<Builder, Shipment>();
    private float nextReserve;
    private class Shipment
    {
        public WarehouseStorage source;
        public Dictionary<ResourceType, int> items;
        public bool picked;
    }
    public bool Ready
    {
        get { foreach (var item in required) if (Delivered(item.Key) < item.Value) return false; return true; }
    }
    public int Delivered(ResourceType type) => delivered.TryGetValue(type, out int value) ? value : 0;
    public bool Initialize(Dictionary<ResourceType,int> cost)
    {
        if(FactionMember.Find(gameObject)==null)FactionMember.Set(gameObject,Faction.Player);
        foreach (var item in cost) required.Add(item.Key, item.Value);
        ReserveMissing();
        foreach (var item in required)
            if (Reserved(item.Key) < item.Value) { ReleaseAll(); return false; }
        return true;
    }
    private int Reserved(ResourceType type)
    {
        int count = 0;
        foreach (var store in stores) if (store != null && store.isActiveAndEnabled) count += store.MaterialAmount(this, type);
        return count;
    }
    private void ReserveMissing()
    {
        var expired = new List<Builder>();
        foreach (var job in jobs) if (job.Key == null || !job.Key.isActiveAndEnabled) expired.Add(job.Key);
        foreach (var builder in expired) jobs.Remove(builder);
        foreach (var item in required)
        {
            int inTransit = 0;
            foreach (var job in jobs.Values)
                if (job.picked && job.items.TryGetValue(item.Key, out int amount)) inTransit += amount;
            int missing = item.Value - Delivered(item.Key) - Reserved(item.Key) - inTransit;
            foreach (var store in WarehouseStorage.All)
            {
                if (missing <= 0) break;
                if(!FactionMember.CanUse(gameObject,store.gameObject))continue;
                int amount = Mathf.Min(missing, store.AvailableMaterial(item.Key));
                if (amount > 0 && store.ReserveMaterial(this, item.Key, amount))
                {
                    if (!stores.Contains(store)) stores.Add(store);
                    missing -= amount;
                }
            }
        }
    }
    public void ReleaseBuilder(Builder builder) { jobs.Remove(builder); }
    public void ReleaseAll()
    {
        foreach (var store in stores) if (store != null) store.ReleaseMaterials(this);
        jobs.Clear(); stores.Clear();
    }
    private void OnDestroy() { ReleaseAll(); }

    // true: этот кадр занят доставкой/ожиданием. false: можно строить.
    public bool Supply(Builder builder, ConstructionSite site, NavMeshAgent agent, GettingResources cargo, int capacity, float distance)
    {
        if (Time.time >= nextReserve) { nextReserve = Time.time + 2f; ReserveMissing(); }
        if (Ready && !cargo.HasCargo) return false;
        if (cargo.HasCargo)
        {
            Vector3 point = site.GetWorkPoint(builder.transform.position);
            if (Vector3.Distance(builder.transform.position, point) > distance)
            { Move(agent, point); builder.MaterialTask = "Доставляет материалы"; return true; }
            agent.ResetPath();
            foreach (var item in required)
            {
                int amount = Mathf.Min(Mathf.Max(0, item.Value - Delivered(item.Key)), cargo.GetStoredAmount(item.Key));
                cargo.RemoveStoredAmount(item.Key, amount);
                delivered[item.Key] = Delivered(item.Key) + amount;
            }
            jobs.Remove(builder);
            if (Ready) ReleaseAll();
            if (cargo.HasCargo) { builder.ReturnMaterials(cargo); return true; }
            return !Ready;
        }
        if (!jobs.TryGetValue(builder, out var shipment))
        {
            foreach (var store in stores)
            {
                if (store == null || !store.isActiveAndEnabled) continue;
                var available = new Dictionary<ResourceType, int>();
                foreach (var item in required)
                {
                    int claimed = 0;
                    foreach (var job in jobs.Values)
                        if (!job.picked && job.source == store && job.items.TryGetValue(item.Key, out int amount)) claimed += amount;
                    int count = store.MaterialAmount(this, item.Key) - claimed;
                    if (count > 0) available[item.Key] = count;
                }
                if (available.Count == 0) continue;
                Vector3 point = BuildingEntrance.TryGetWorldPoint(store.gameObject, out var door) ? door : store.transform.position;
                var path = new NavMeshPath();
                if (!agent.CalculatePath(point, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                shipment = new Shipment { source = store, items = MaterialLoadPlan.Create(available, Mathf.Max(1, capacity)) };
                jobs[builder] = shipment;
                if (shipment != null) break;
            }
        }
        if (shipment == null) { agent.ResetPath(); builder.MaterialTask = "Ждёт материалы"; return true; }
        if (shipment.source == null || !shipment.source.isActiveAndEnabled || shipment.picked)
        { jobs.Remove(builder); return true; }
        var worker = builder.GetComponent<ResourcesMove>();
        bool arrived = worker != null ? worker.IsAtMiningHome(shipment.source.gameObject)
            : Vector3.Distance(builder.transform.position, shipment.source.transform.position) <= distance;
        if (!arrived)
        {
            Vector3 point = worker != null ? worker.GetApproachPoint(shipment.source.transform) : shipment.source.transform.position;
            if (!Move(agent, point)) jobs.Remove(builder);
            builder.MaterialTask = "Забирает материалы"; return true;
        }
        agent.ResetPath();
        int picked = 0;
        foreach (var type in new List<ResourceType>(shipment.items.Keys))
        {
            int amount = shipment.source.PickupMaterial(this, type, shipment.items[type], cargo);
            shipment.items[type] = amount;
            picked += amount;
        }
        shipment.picked = true;
        if (picked == 0) jobs.Remove(builder);
        builder.MaterialTask = "Доставляет материалы";
        return true;
    }
    private static bool Move(NavMeshAgent agent, Vector3 point)
    {
        var path = new NavMeshPath();
        if (!agent.CalculatePath(point, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        agent.isStopped = false; return agent.SetPath(path);
    }
}
