using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

public partial class Port
{
    [Header("Ремонт корпуса")]
    [Min(1)] public float repairHealthPerCycle=50;
    [Min(.1f)] public float repairSecondsPerCycle=5;
    [Min(0)] public int repairWoodPerCycle=2,repairIronPerCycle=0;
    private ShipHull repairTarget;
    private bool repairRequested;
    private bool repairArrived;
    private float repairProgress;
    private int repairNavigationRevision=-1;
    [System.NonSerialized] private ShipNavigation repairNavigation;
    public bool IsRepairing=>repairRequested;
    public ShipHull RepairTarget=>repairTarget;
    public string RepairStatus{get;private set;}="Ремонт не назначен";
    private ShipSupplyDock RepairDock=>GetComponent<ShipSupplyDock>();
    private Unit RepairHealth=>repairTarget!=null?repairTarget.GetComponent<Unit>():null;
    private string RepairConfigurationError=>repairHealthPerCycle<=0||repairSecondsPerCycle<=0||repairWoodPerCycle<0||repairIronPerCycle<0
        ||(repairWoodPerCycle==0&&repairIronPerCycle==0)?"Задайте положительные здоровье, время и стоимость ремонта"
        :(long)repairWoodPerCycle+repairIronPerCycle>storageCapacity?"Увеличьте Storage Capacity для ремонта":null;
    private Dictionary<ResourceType,int> RepairCost()
    {
        var cost=new Dictionary<ResourceType,int>();
        if(repairWoodPerCycle>0)cost[ResourceType.Wood]=repairWoodPerCycle;
        if(repairIronPerCycle>0)cost[ResourceType.Iron]=repairIronPerCycle;
        return cost;
    }
    private bool RepairTargetValid=>repairTarget!=null&&repairTarget.isActiveAndEnabled&&RepairHealth!=null&&RepairHealth.unitHealth>0
        &&FactionMember.CanUse(repairTarget.gameObject,gameObject)&&RepairDock!=null&&RepairDock.isActiveAndEnabled
        &&!(GetComponent<BuildingHealth>()?.IsDead??false)&&repairTarget.RepairPort==this;
    private bool AtRepairDock=>RepairTargetValid&&RepairDock.Ship==repairTarget.GetComponent<ShipProvisions>()
        &&(repairTarget.GetComponent<ShipNavigation>()?.IsMooredTo(RepairDock)??false);
    private bool CanRepair
    {
        get
        {
            if(!IsRepairing)return false;
            if(!isActiveAndEnabled||!AtRepairDock||!BuildingRoadAccess.Allows(this)||RepairConfigurationError!=null
                ||RepairHealth.unitHealth>=RepairHealth.unitMaxHealth)return false;
            foreach(var item in RepairCost())if(Storage.Available(item.Key)<item.Value)return false;
            return true;
        }
    }
    public bool RequestRepair(ShipHull ship)
    {
        if(!isActiveAndEnabled||ship==null||!ship.isActiveAndEnabled||RepairDock==null||!RepairDock.isActiveAndEnabled||(GetComponent<BuildingHealth>()?.IsDead??false))
        {RepairStatus="Нужны действующий порт, причал и корабль";return false;}
        var health=ship.GetComponent<Unit>();var navigation=ship.GetComponent<ShipNavigation>();
        if(health==null||health.unitHealth<=0||health.unitHealth>=health.unitMaxHealth||navigation==null||!FactionMember.CanUse(ship.gameObject,gameObject))
        {RepairStatus="Нужен повреждённый живой союзный корабль";return false;}
        if(RepairConfigurationError!=null){RepairStatus=RepairConfigurationError;return false;}
        if(IsRepairing){if(repairTarget==ship)return true;RepairStatus="Порт уже ремонтирует другой корабль";return false;}
        if(ship.RepairPort!=null&&ship.RepairPort!=this){RepairStatus="Корабль уже назначен другому порту";return false;}
        if(!navigation.IsMooredTo(RepairDock)&&!navigation.MoorAt(RepairDock))
        {RepairStatus="Не удалось подойти к ремонтному причалу: "+navigation.LastOrderError;return false;}
        ship.GetComponent<ShipTradeRoute>()?.PauseRoute();
        CancelTransferFromPlayer();
        repairTarget=ship;repairRequested=true;repairProgress=0;ship.SetRepairPort(this);
        repairArrived=AtRepairDock;
        repairNavigationRevision=navigation.CommandRevision;BindRepairNavigation();RepairStatus="Ожидает ремонта";return true;
    }
    [ContextMenu("Ремонтировать корабль у причала")]
    public void RepairMooredShip()
    {RequestRepair(RepairDock!=null&&RepairDock.Ship!=null?RepairDock.Ship.GetComponent<ShipHull>():null);}
    public void RepairSelectedShip()
    {
        if(ManagerGO.Instance==null)return;
        foreach(var selected in ManagerGO.Instance.unitsSelected)
        {var ship=selected!=null?selected.GetComponent<ShipHull>():null;if(ship!=null&&RequestRepair(ship))return;}
    }
    private void BindRepairNavigation()
    {
        if(repairNavigation!=null)repairNavigation.ManualOrderIssued-=ManualRepairMovement;
        repairNavigation=repairTarget!=null?repairTarget.GetComponent<ShipNavigation>():null;
        if(repairNavigation!=null)repairNavigation.ManualOrderIssued+=ManualRepairMovement;
    }
    private void ManualRepairMovement(){EndRepair("Ремонт отменён приказом игрока",false);}
    [ContextMenu("Отменить ремонт корабля")]
    public void CancelRepair(){EndRepair("Ремонт отменён; материалы остаются в порту",true);}
    private void EndRepair(string status,bool stopApproach)
    {
        var nav=repairNavigation;
        if(nav!=null)nav.ManualOrderIssued-=ManualRepairMovement;
        if(repairTarget!=null&&repairTarget.RepairPort==this)repairTarget.SetRepairPort(null);
        repairRequested=false;repairArrived=false;repairTarget=null;repairNavigation=null;repairProgress=0;RepairStatus=status;
        if(stopApproach&&nav!=null&&nav.CommandRevision==repairNavigationRevision&&nav.State!=ShipNavigation.Motion.Moored)nav.StopForRoute();
        repairNavigationRevision=-1;
    }
    private void WorkRepair(float seconds)
    {

        if(seconds<=0||!CanRepair)return;
        int affordableCycles=int.MaxValue;
        foreach(var cost in RepairCost())affordableCycles=Mathf.Min(affordableCycles,Storage.Available(cost.Key)/cost.Value);
        repairProgress=Mathf.Min(repairProgress+seconds,affordableCycles*repairSecondsPerCycle);
        while(IsRepairing&&CanRepair&&repairProgress>=repairSecondsPerCycle)
        {
            if(!TrySpendRepairMaterials())return;
            RepairHealth.Heal(Mathf.Min(repairHealthPerCycle,RepairHealth.unitMaxHealth-RepairHealth.unitHealth));
            repairProgress-=repairSecondsPerCycle;
            if(RepairHealth.unitHealth>=RepairHealth.unitMaxHealth){EndRepair("Корабль отремонтирован",false);return;}
        }
    }
    public void TickRepair()
    {
        if(SaveGameManager.IsLoading||!IsRepairing)return;
        if(!RepairTargetValid){EndRepair("Ремонт отменён: корабль или причал недоступен",true);return;}
        if(repairArrived&&!AtRepairDock){EndRepair("Ремонт отменён: корабль отошёл от причала",true);return;}
        if(AtRepairDock)repairArrived=true;
        if(RepairHealth.unitHealth>=RepairHealth.unitMaxHealth){EndRepair("Корабль отремонтирован",false);return;}
        RepairStatus=!BuildingRoadAccess.Allows(this)?"Ремонт приостановлен: нет дороги"
            :RepairConfigurationError??(!AtRepairDock?"Ждёт швартовку корабля":!CanRepair?"Ждёт доставку материалов":"Ремонтирует корпус");
    }

    public void RestoreSavedRepairHooks()
    {if(IsRepairing&&repairTarget!=null)repairTarget.SetRepairPort(this);BindRepairNavigation();}
    private int RepairDemand(ResourceType type)
    {
        if(!IsRepairing||!RepairTargetValid||RepairConfigurationError!=null)return 0;
        int cycles=Mathf.CeilToInt(Mathf.Max(0,RepairHealth.unitMaxHealth-RepairHealth.unitHealth)/repairHealthPerCycle);
        cycles=Mathf.Min(cycles,storageCapacity/(repairWoodPerCycle+repairIronPerCycle));
        return cycles*(type==ResourceType.Wood?repairWoodPerCycle:type==ResourceType.Iron?repairIronPerCycle:0);
    }
    private bool TrySpendRepairMaterials()
    {
        foreach(var cost in RepairCost())if(Storage.Available(cost.Key)<cost.Value)return false;
        var inventory=GetComponent<GettingResources>();
        foreach(var cost in RepairCost())inventory.RemoveStoredAmount(cost.Key,cost.Value);
        return true;
    }
    private void DisableRepair()
    {
        if(!SaveGameManager.IsLoading&&IsRepairing)EndRepair("Ремонт отменён: порт отключён",true);
        if(repairNavigation!=null)repairNavigation.ManualOrderIssued-=ManualRepairMovement;
    }
}
