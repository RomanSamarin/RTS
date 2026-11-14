using System;
using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;
using Object = UnityEngine.Object;

[RequireComponent(typeof(ShipSupplyDock), typeof(WorkplaceHiring), typeof(GettingResources))]
public partial class Port : RecipeBuilding
{
    [Serializable] public class StockTarget { public ResourceType type; [Min(0)] public int amount; }
    [Serializable] private class Order { public ShipCargo ship; public ResourceType type; public int remaining; public bool unload; }
    [Min(1)] public int storageCapacity = 300;
    [Min(1)] public int transferBatchSize = 10;
    [Min(.1f)] public float transferSeconds = 2;
    public bool returnSurplusToWarehouse = true;
    public List<StockTarget> stockTargets = new List<StockTarget>();
    private readonly CargoHold stock = new CargoHold();
    private readonly List<Order> orders = new List<Order>();
    private int reservedBatch;
    private float progress;
    private readonly Dictionary<Object,List<StockTarget>> routeDemands=new Dictionary<Object,List<StockTarget>>();
    public Object TransferOwner { get; private set; }
    public int TransferRevision { get; private set; }
    public int ProgressRevision { get; private set; }
    public int TransferredAmount { get; private set; }
    public event Action<ShipCargo> ManualTransferIssued;
    public void SetRouteDemand(Object owner,ResourceType type,int amount)
    {
        if(owner==null||!Enum.IsDefined(typeof(ResourceType),type))return;
        if(!routeDemands.TryGetValue(owner,out var entries))routeDemands[owner]=entries=new List<StockTarget>();
        foreach(var entry in entries)if(entry.type==type){entry.amount=Mathf.Max(0,amount);return;}
        entries.Add(new StockTarget{type=type,amount=Mathf.Max(0,amount)});
    }
    public void ClearRouteDemand(Object owner){if(owner!=null)routeDemands.Remove(owner);}
    private void PruneRouteDemands()
    {var dead=new List<Object>();foreach(var pair in routeDemands)if(pair.Key==null)dead.Add(pair.Key);foreach(var owner in dead)routeDemands.Remove(owner);}
    public string Status { get; private set; } = "Нет грузовой операции";
    public CargoHold Storage => stock.Initialize(GetComponent<GettingResources>(), storageCapacity);
    public ShipSupplyDock Dock => GetComponent<ShipSupplyDock>();
    private Order Active => orders.Count > 0 ? orders[0] : null;
    public int Remaining => Active != null ? Active.remaining : 0;
    public ShipCargo TargetShip => Active != null ? Active.ship : null;
    public bool Unloading => Active != null && Active.unload;
    public int OrderCount => orders.Count;
    public override ResourcesMove.UnitRole Profession => ResourcesMove.UnitRole.PortWorker;
    public override ResourceType InputType => InputTypes.Count > 0 ? InputTypes[0] : ResourceType.Wood;
    public override ResourceType OutputType => ResourceType.Wood;
    public override int StoredInput => Storage.Count;
    public override float WorkProgress => IsRepairing ? repairProgress : progress;
    public override string WaitingTask => !BuildingRoadAccess.Allows(this) ? "Нет дороги к порту" : IsRepairing ? RepairStatus : Status;
    private int Needed(ResourceType type)
    {
        if(IsRepairing)return RepairDemand(type);
        int desired = 0;
        foreach (var target in stockTargets) if (target != null && target.type == type) desired = Mathf.Max(desired, target.amount);
        PruneRouteDemands();
        foreach(var entries in routeDemands.Values)foreach(var entry in entries)if(entry.type==type)desired=Mathf.Max(desired,entry.amount);
        long queued=0;
        foreach(var order in orders)if(!order.unload&&order.type==type)queued+=order.remaining;
        desired=Mathf.Max(desired,(int)Math.Min(int.MaxValue,queued));
        return desired;
    }
    public override IReadOnlyList<ResourceType> InputTypes
    {
        get
        {
            var result = new List<ResourceType>();
            if(IsRepairing){foreach(var cost in RepairCost())result.Add(cost.Key);return result;}
            foreach (var target in stockTargets) if (target != null && target.amount > 0 && !result.Contains(target.type)) result.Add(target.type);
            PruneRouteDemands();
            foreach(var entries in routeDemands.Values)foreach(var entry in entries)if(entry.amount>0&&!result.Contains(entry.type))result.Add(entry.type);
            foreach(var order in orders)if(!order.unload&&!result.Contains(order.type))result.Add(order.type);
            return result;
        }
    }
    public override int RequestedInputFor(ResourceType type) => isActiveAndEnabled && BuildingRoadAccess.Allows(this)
        ? Mathf.Min(Storage.FreeSpace, Mathf.Max(0, Needed(type) - (IsRepairing?Storage.Available(type):Storage.Amount(type)) - Storage.Incoming(type))) : 0;
    public override int RequestedInput { get { int total = 0; foreach (var type in InputTypes) total += RequestedInputFor(type); return total; } }
    public override bool ReserveDelivery(Object owner, ResourceType type, int amount)
        => amount > 0 && RequestedInputFor(type) >= amount && Storage.ReserveIncoming(owner, type, amount);
    public override bool ReserveDelivery(Object owner, int amount) => ReserveDelivery(owner, InputType, amount);
    public override void CancelDelivery(Object owner) { Storage.CancelIncoming(owner); }
    public override int Receive(Object owner, GettingResources cargo) => Storage.Receive(owner, cargo);
    public int Collectable(ResourceType type) => returnSurplusToWarehouse
        ? Mathf.Max(0, Storage.Available(type) - Mathf.Max(0, Needed(type)
            - (Active != null && !Active.unload && Active.type == type ? reservedBatch : 0))) : 0;
    public override int StoredOutput { get { int count = 0; foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) count += Collectable(type); return count; } }
    public override int CollectableOutput(WarehouseStorage destination)
    {
        int amount = 0;
        foreach (var type in WarehouseStorage.Types)
            if (destination == null || destination.Accepts(type)) amount += Collectable(type);
        return amount;
    }

    public int ReserveCollection(Object owner, int capacity, WarehouseStorage destination = null)
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (destination != null && !destination.Accepts(type)) continue;
            int amount = Mathf.Min(Mathf.Max(0, capacity), Collectable(type));
            if (amount > 0 && Storage.ReserveOutgoing(owner, type, amount)) return amount;
        }
        return 0;
    }
    public void CancelCollection(Object owner) { Storage.CancelOutgoing(owner); }
    public int CollectReserved(Object owner, GettingResources cargo, int capacity) => Storage.Pickup(owner, cargo, capacity);
    public override int Collect(GettingResources cargo, int capacity, WarehouseStorage destination = null) => 0; // Requires a carrier reservation.
    public void SetStockTarget(ResourceType type, int amount)
    {
        if (!Enum.IsDefined(typeof(ResourceType), type)) return;
        foreach (var target in stockTargets) if (target != null && target.type == type) { target.amount = Mathf.Max(0, amount); return; }
        stockTargets.Add(new StockTarget { type = type, amount = Mathf.Max(0, amount) });
    }
    private ShipCargo CurrentTradeShip()
    {
        var provisions = Dock != null ? Dock.Ship : null;
        if (provisions == null || !Dock.CanSupply(provisions)) return null;
        var hull = provisions.GetComponent<ShipHull>();
        return hull != null && hull.type == ShipType.Trade ? hull.GetComponent<ShipCargo>() : null;
    }
    public bool BeginLoading(ResourceType type, int amount) => Begin(type, amount, false);
    public bool BeginUnloading(ResourceType type, int amount) => Begin(type, amount, true);
    /// <summary>One manual manifest replaces the previous order only after complete validation.</summary>
    public bool BeginManifest(ShipCargo ship, IReadOnlyDictionary<ResourceType,int> manifest, bool unload)
    {
        if(!isActiveAndEnabled||IsRepairing||!BuildingRoadAccess.Allows(this)||ship==null||CurrentTradeShip()!=ship||manifest==null||manifest.Count==0)
        {Status=IsRepairing?"Сначала завершите или отмените ремонт":"Нужен выбранный торговый корабль у причала порта и дорога";return false;}
        long total=0;
        foreach(var item in manifest)
        {
            if(item.Value<=0||!Enum.IsDefined(typeof(ResourceType),item.Key)||(unload&&item.Value>ship.Hold.Amount(item.Key)))
            {Status="Проверьте количество товара в заказе";return false;}
            total+=item.Value;
        }
        if(!unload&&total>ship.Hold.Capacity-ship.Hold.Count)
        {Status="В трюме недостаточно места для всего заказа";return false;}
        ManualTransferIssued?.Invoke(ship);
        CancelTransfer();ProgressRevision=TransferRevision;TransferredAmount=0;
        foreach(var item in manifest)orders.Add(new Order{ship=ship,type=item.Key,remaining=item.Value,unload=unload});
        PrepareBatch();return true;
    }
    private bool Begin(ResourceType type, int amount, bool unload)
    {
        var ship = CurrentTradeShip();
        if (IsRepairing || !isActiveAndEnabled || !BuildingRoadAccess.Allows(this) || ship == null || amount <= 0 || !Enum.IsDefined(typeof(ResourceType), type))
        { Status = "Нужны торговый корабль у причала, дорога и количество товара"; return false; }
        ManualTransferIssued?.Invoke(ship);
        CancelTransfer();ProgressRevision=TransferRevision;TransferredAmount=0;
        orders.Add(new Order { ship = ship, type = type, remaining = amount, unload = unload });
        PrepareBatch(); return true;
    }
    public bool TryBeginRouteTransfer(Object owner,ShipCargo ship,ResourceType type,int amount,bool unload,out int revision)
    {
        revision=0;
        if(IsRepairing||owner==null||ship==null||OrderCount>0||CurrentTradeShip()!=ship||!isActiveAndEnabled||!BuildingRoadAccess.Allows(this)
            ||amount<=0||!Enum.IsDefined(typeof(ResourceType),type))return false;
        CancelTransfer();TransferOwner=owner;ProgressRevision=TransferRevision;TransferredAmount=0;
        orders.Add(new Order{ship=ship,type=type,remaining=amount,unload=unload});
        revision=TransferRevision;PrepareBatch();return true;
    }
    public void CancelRouteTransfer(Object owner,int revision)
    {if(TransferOwner==owner&&TransferRevision==revision)CancelTransfer();}
    public void CancelTransferFromPlayer()
    {var ship=CurrentTradeShip();if(ship!=null)ManualTransferIssued?.Invoke(ship);CancelTransfer();}
    public bool UnloadAll()
    {
        var ship = CurrentTradeShip(); if (IsRepairing || !isActiveAndEnabled || ship == null || !BuildingRoadAccess.Allows(this)) return false;
        ManualTransferIssued?.Invoke(ship);
        CancelTransfer();
        ProgressRevision=TransferRevision;TransferredAmount=0;
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            int amount = ship.Hold.Available(type);
            if (amount > 0) orders.Add(new Order { ship = ship, type = type, remaining = amount, unload = true });
        }
        PrepareBatch(); return orders.Count > 0;
    }
    private CargoHold From => Active.unload ? Active.ship.Hold : Storage;
    private CargoHold To => Active.unload ? Storage : Active.ship.Hold;
    private bool PrepareBatch()
    {
        if (IsRepairing || Active == null) return false;
        if (Active.ship == null || !Active.ship.isActiveAndEnabled || CurrentTradeShip() != Active.ship)
        { CancelTransfer(); Status = "Операция отменена: корабль сменился или отошёл от причала"; return false; }
        if (reservedBatch > 0) return true;
        int amount = Mathf.Min(transferBatchSize, Mathf.Min(Active.remaining, Mathf.Min(From.Available(Active.type), To.FreeSpace)));
        if (amount <= 0)
        {
            Status = To.FreeSpace == 0 ? "Ждёт свободное место у получателя" : Active.unload ? "Товара больше нет на корабле" : "Ждёт доставку товара в порт";
            if (Active.unload && From.Amount(Active.type) == 0) { orders.RemoveAt(0); return PrepareBatch(); }
            if (Active.unload && From.Amount(Active.type) > 0 && From.Available(Active.type) == 0) Status = "Товар занят другой операцией";
            return false;
        }
        if (!From.ReserveOutgoing(this, Active.type, amount)) return false;
        if (!To.ReserveIncoming(this, Active.type, amount)) { From.CancelOutgoing(this); return false; }
        reservedBatch = amount; Status = Active.unload ? "Разгружает корабль" : "Загружает корабль"; return true;
    }
    public override bool CanProduce => IsRepairing ? CanRepair : isActiveAndEnabled && BuildingRoadAccess.Allows(this) && PrepareBatch();
    public override void Work(float seconds)
    {
        if(IsRepairing){WorkRepair(seconds);return;}
        if (seconds <= 0 || !CanProduce) return;
        progress += seconds;
        if (progress < Mathf.Max(.1f, transferSeconds)) return;
        int moved = From.Transfer(this, To); reservedBatch = 0; progress = 0;
        if (moved == 0) { Status = "Передача приостановлена: проверьте груз и место"; return; }
        TransferredAmount+=moved;
        Active.remaining -= moved;
        if (Active.remaining <= 0) { orders.RemoveAt(0); Status = "Грузовая операция завершена"; }
        PrepareBatch();
    }
    public void CancelTransfer()
    {
        TransferRevision++;TransferOwner=null;
        if (Active != null)
        {
            Storage.CancelIncoming(this); Storage.CancelOutgoing(this);
            if (Active.ship != null) { Active.ship.Hold.CancelIncoming(this); Active.ship.Hold.CancelOutgoing(this); }
        }
        orders.Clear(); reservedBatch = 0; progress = 0; Status = "Грузовая операция отменена";
    }
    private void Update() { TickRepair(); if (!SaveGameManager.IsLoading && Active != null) PrepareBatch(); }
    protected override void OnDisable() { DisableRepair(); CancelTransfer(); stock.ClearReservations(); base.OnDisable(); }
    private void Reset()
    { var hiring = GetComponent<WorkplaceHiring>(); if (hiring != null) { hiring.profession = ResourcesMove.UnitRole.PortWorker; hiring.maxWorkers = 3; } }
}
