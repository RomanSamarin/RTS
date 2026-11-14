using System;
using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

/// <summary>A physical, repeatable freight cycle. It never grants goods or port worker time.</summary>
[DisallowMultipleComponent]
public class ShipTradeRoute : MonoBehaviour
{
    [Serializable] public class Freight { public ResourceType type; [Min(1)] public int amount=10; }
    public enum Phase { Stopped, ToSource, LoadingOutbound, ToDestination, UnloadingOutbound, LoadingReturn, Returning, UnloadingReturn, Paused, Complete }
    public Port sourcePort,destinationPort;
    public List<Freight> outbound=new List<Freight>(),returnCargo=new List<Freight>();
    public bool repeat=true;
    [Min(0)] public int departureMeals=10;
    public Phase Current { get; private set; }
    public string Status { get; private set; }="Рейс не назначен";
    public int CompletedCycles { get; private set; }
    public bool IsRunning => Current!=Phase.Stopped&&Current!=Phase.Paused&&Current!=Phase.Complete;
    private Phase resumePhase;
    private Port origin,target;
    private readonly List<Freight> activeOutbound=new List<Freight>(),activeReturn=new List<Freight>();
    private readonly List<Freight> remainingDelivery=new List<Freight>();
    private readonly List<Freight> pausedCargo=new List<Freight>();
    private bool cycleRepeat;
    private int mealMinimum,ownedNavigationRevision=-1;
    private bool operationPending;
    private Port operationPort;
    private int operationRevision;
    private int operationProgress,operationEntry;
    private bool operationIsUnload;
    private float nextActionAt;
    [NonSerialized] private Port subscribedOrigin,subscribedTarget;
    [NonSerialized] private ShipNavigation subscribedNavigation;
    private ShipNavigation Navigation=>GetComponent<ShipNavigation>();
    private ShipCargo Cargo=>GetComponent<ShipCargo>();
    private ShipProvisions Provisions=>GetComponent<ShipProvisions>();
    public Port ActiveSource=>origin;
    public Port ActiveDestination=>target;
    private bool ValidPort(Port port)
    {
        if(port==null||!port.isActiveAndEnabled||port.Dock==null||!port.Dock.isActiveAndEnabled
            ||FactionMember.Of(port.gameObject)!=FactionMember.Of(gameObject))return false;
        var health=port.GetComponent<BuildingHealth>();return health==null||!health.IsDead;
    }
    private bool Normalize(List<Freight> input,List<Freight> output,bool allowEmpty)
    {
        output.Clear();long total=0;
        if(input!=null)foreach(var entry in input)
        {
            if(entry==null||entry.amount<=0||!Enum.IsDefined(typeof(ResourceType),entry.type))return false;
            total+=entry.amount;if(total>Cargo.Hold.Capacity)return false;
            var existing=output.Find(x=>x.type==entry.type);
            if(existing!=null)existing.amount+=entry.amount;
            else output.Add(new Freight{type=entry.type,amount=entry.amount});
        }
        return allowEmpty||output.Count>0;
    }
    [ContextMenu("Начать грузовой рейс")]
    public void BeginRoute(){TryStartRoute();}
    public bool TryStartRoute()
    {
        if(!isActiveAndEnabled)return Fail("Включите компонент грузового рейса");
        if(Navigation==null||Cargo==null||Provisions==null||GetComponent<ShipHull>()?.type!=ShipType.Trade)
            return Fail("Нужен торговый корабль с навигацией и припасами");
        if(!ValidPort(sourcePort)||!ValidPort(destinationPort)||sourcePort==destinationPort)
            return Fail("Выберите два разных действующих порта своего владельца");
        if(departureMeals<0||departureMeals>Provisions.capacity)return Fail("Порог припасов превышает вместимость корабля");
        var outgoing=new List<Freight>();var incoming=new List<Freight>();
        if(!Normalize(outbound,outgoing,false)||!Normalize(returnCargo,incoming,true))
            return Fail("Проверьте товары: положительное количество и не более вместимости на каждый переход");
        StopOwnedWork(true);Unsubscribe();
        origin=sourcePort;target=destinationPort;activeOutbound.Clear();activeOutbound.AddRange(outgoing);
        activeReturn.Clear();activeReturn.AddRange(incoming);mealMinimum=departureMeals;cycleRepeat=repeat;
        CompletedCycles=0;Current=Phase.ToSource;nextActionAt=0;Status="Идёт к порту отправления";Subscribe();return true;
    }
    private bool Fail(string reason){Status=reason;return false;}
    private void Subscribe()
    {
        Unsubscribe();subscribedNavigation=Navigation;
        if(subscribedNavigation!=null)subscribedNavigation.ManualOrderIssued+=ManualMovement;
        subscribedOrigin=origin;subscribedTarget=target;
        if(subscribedOrigin!=null)subscribedOrigin.ManualTransferIssued+=ManualTransfer;
        if(subscribedTarget!=null)subscribedTarget.ManualTransferIssued+=ManualTransfer;
    }
    private void Unsubscribe()
    {
        if(subscribedNavigation!=null)subscribedNavigation.ManualOrderIssued-=ManualMovement;
        if(subscribedOrigin!=null)subscribedOrigin.ManualTransferIssued-=ManualTransfer;
        if(subscribedTarget!=null)subscribedTarget.ManualTransferIssued-=ManualTransfer;
        subscribedNavigation=null;subscribedOrigin=null;subscribedTarget=null;
    }
    private void ManualMovement(){if(IsRunning)Pause("Рейс приостановлен ручным приказом",false);}
    private void ManualTransfer(ShipCargo ship){if(ship==Cargo&&IsRunning)Pause("Рейс приостановлен ручной грузовой операцией",false);}
    private void StopOwnedWork(bool stopMovement)
    {
        AccountDelivery();
        if(operationPending&&operationPort!=null)operationPort.CancelRouteTransfer(this,operationRevision);
        operationPending=false;operationPort=null;
        if(origin!=null)origin.ClearRouteDemand(this);if(target!=null)target.ClearRouteDemand(this);
        if(stopMovement&&Navigation!=null&&Navigation.CommandRevision==ownedNavigationRevision
            &&Navigation.State!=ShipNavigation.Motion.Moored)Navigation.StopForRoute();
        ownedNavigationRevision=-1;
    }
    private void AccountDelivery()
    {
        if(!operationPending||!operationIsUnload||operationPort==null||operationPort.ProgressRevision!=operationRevision)return;
        int moved=Mathf.Max(0,operationPort.TransferredAmount-operationProgress);operationProgress=operationPort.TransferredAmount;
        if(operationEntry>=0&&operationEntry<remainingDelivery.Count)
            remainingDelivery[operationEntry].amount=Mathf.Max(0,remainingDelivery[operationEntry].amount-moved);
    }
    private void PlanDelivery(List<Freight> manifest)
    {remainingDelivery.Clear();foreach(var entry in manifest)remainingDelivery.Add(new Freight{type=entry.type,amount=Mathf.Min(entry.amount,Cargo.Hold.Amount(entry.type))});}
    [ContextMenu("Приостановить рейс")]
    public void PauseRoute(){if(IsRunning)Pause("Рейс приостановлен",true);}
    private void Pause(string reason,bool stopMovement)
    {
        resumePhase=Current;Current=Phase.Paused;StopOwnedWork(stopMovement);pausedCargo.Clear();
        if(Cargo!=null)foreach(var entry in remainingDelivery)pausedCargo.Add(new Freight{type=entry.type,amount=Cargo.Hold.Amount(entry.type)});
        Status=reason;
    }
    public void ResumeRoute()
    {
        if(Current!=Phase.Paused)return;
        if(!ValidPort(origin)||!ValidPort(target)){Status="Порты недоступны: настройте новый рейс";return;}
        if(resumePhase==Phase.UnloadingOutbound||resumePhase==Phase.UnloadingReturn)
            foreach(var entry in remainingDelivery)
            {
                var before=pausedCargo.Find(x=>x.type==entry.type);
                if(before!=null)entry.amount=Mathf.Max(0,entry.amount-Mathf.Max(0,before.amount-Cargo.Hold.Amount(entry.type)));
            }
        pausedCargo.Clear();
        Current=resumePhase;nextActionAt=0;Status="Рейс возобновлён";Subscribe();
    }
    [ContextMenu("Отменить рейс")]
    public void CancelRoute(){StopOwnedWork(true);Current=Phase.Stopped;Status="Рейс отменён, груз сохранён";Unsubscribe();}
    private bool DepartReady()
    {
        if(!(GetComponent<ShipCrew>()?.Ready??false)){Status="Ждёт полный экипаж";return false;}
        if(Provisions.StoredMeals<mealMinimum)
        {Provisions.RequestRestock();Status=$"Ждёт припасы: {Provisions.StoredMeals}/{mealMinimum}";return false;}
        return true;
    }
    private bool SailingTo(Port port)
    {
        if(Navigation.IsMooredTo(port.Dock))return true;
        if(Navigation.DestinationDock==port.Dock&&Navigation.State!=ShipNavigation.Motion.Idle)
        {Status="Переход к порту: "+Navigation.Status;return false;}
        if(Time.time<nextActionAt)return false;nextActionAt=Time.time+2;
        bool atLocalBerth=Vector3.Distance(transform.position,port.Dock.BerthPoint)<=port.Dock.mooringRadius;
        bool leavingPort=Provisions.Dock!=null&&Navigation.IsMooredTo(Provisions.Dock);
        if(!atLocalBerth&&leavingPort&&!DepartReady())return false;
        if(!atLocalBerth&&!leavingPort&&!(GetComponent<ShipCrew>()?.Ready??false))
        {Status="Ждёт экипаж для продолжения перехода";return false;}
        if(!port.Dock.CanReserve(Navigation))
        {
            Status="Ждёт свободный причал";
            TryLeaveBusyOrigin();return false;
        }
        if(Navigation.MoorForRoute(port.Dock)){ownedNavigationRevision=Navigation.CommandRevision;Status="Идёт к причалу";}
        else Status="Ждёт водный путь: "+Navigation.LastOrderError;
        return false;
    }
    private void TryLeaveBusyOrigin()
    {
        var dock=Provisions.Dock;
        if(dock==null||Navigation.State!=ShipNavigation.Motion.Moored||!DepartReady())return;
        // Free the departure berth so opposite freight cycles cannot hold both ports forever.
        var hull=GetComponent<ShipHull>();float distance=Mathf.Max(12,hull.length*2);
        for(int i=0;i<8;i++)
        {
            float angle=i*Mathf.PI*.25f;
            Vector3 point=dock.BerthPoint+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*distance;
            if(!Navigation.SailForRoute(point))continue;
            ownedNavigationRevision=Navigation.CommandRevision;Status="Освобождает причал и ждёт порт назначения";return;
        }
        Status="Ждёт причал: рядом нет доступного места ожидания";
    }
    private bool Transfer(Port port,List<Freight> manifest,bool unload)
    {
        if(!Navigation.IsMooredTo(port.Dock))
        {if(operationPending)StopOwnedWork(false);SailingTo(port);return false;}
        if(!BuildingRoadAccess.Allows(port)){Status="Ждёт дорогу к порту";return false;}
        port.ClearRouteDemand(this);
        if(!unload)foreach(var entry in manifest)
            port.SetRouteDemand(this,entry.type,Mathf.Max(0,entry.amount-Cargo.Hold.Amount(entry.type)));
        if(operationPending)
        {
            AccountDelivery();
            if(operationPort!=port||port.TransferOwner!=this||port.TransferRevision!=operationRevision)
            {Pause("Грузовая операция прервана — рейс на паузе",false);return false;}
            if(port.OrderCount>0){Status=port.WaitingTask;return false;}
            port.CancelRouteTransfer(this,operationRevision);operationPending=false;operationPort=null;
        }
        for(int index=0;index<manifest.Count;index++)
        {
            var entry=manifest[index];
            if(unload)entry.amount=Mathf.Min(entry.amount,Cargo.Hold.Amount(entry.type));
            int amount=unload?entry.amount:Mathf.Max(0,entry.amount-Cargo.Hold.Amount(entry.type));
            if(amount==0)continue;
            if(port.OrderCount>0){Status="Ждёт завершения другой грузовой операции";return false;}
            if(port.TryBeginRouteTransfer(this,Cargo,entry.type,amount,unload,out operationRevision))
            {operationPending=true;operationPort=port;operationProgress=0;operationEntry=index;operationIsUnload=unload;Status=port.WaitingTask;}
            else Status="Ждёт возможность передачи груза";
            return false;
        }
        port.ClearRouteDemand(this);return true;
    }
    public void Tick()
    {
        if(!IsRunning||SaveGameManager.IsLoading)return;
        if(!ValidPort(origin)||!ValidPort(target)){Pause("Порт уничтожен, отключён или сменил владельца",true);return;}
        if(GetComponent<ShipHull>()?.type!=ShipType.Trade||Navigation==null||!Navigation.isActiveAndEnabled)
        {Pause("Корабль недоступен для грузового рейса",true);return;}
        switch(Current)
        {
            case Phase.ToSource:if(SailingTo(origin)){Current=Phase.LoadingOutbound;nextActionAt=0;}break;
            case Phase.LoadingOutbound:if(Transfer(origin,activeOutbound,false)&&DepartReady()){PlanDelivery(activeOutbound);Current=Phase.ToDestination;nextActionAt=0;}break;
            case Phase.ToDestination:if(SailingTo(target)){Current=Phase.UnloadingOutbound;nextActionAt=0;}break;
            case Phase.UnloadingOutbound:if(Transfer(target,remainingDelivery,true))Current=Phase.LoadingReturn;break;
            case Phase.LoadingReturn:if(Transfer(target,activeReturn,false)&&DepartReady()){PlanDelivery(activeReturn);Current=Phase.Returning;nextActionAt=0;}break;
            case Phase.Returning:if(SailingTo(origin)){Current=Phase.UnloadingReturn;nextActionAt=0;}break;
            case Phase.UnloadingReturn:
                if(Transfer(origin,remainingDelivery,true))
                {CompletedCycles++;Current=cycleRepeat?Phase.LoadingOutbound:Phase.Complete;Status=cycleRepeat?"Начинает следующий рейс":"Рейс завершён";}
                break;
        }
    }
    public void RestoreSavedHooks()
    {Subscribe();nextActionAt=0;if(!IsRunning)StopOwnedWork(false);}
    private void Update(){Tick();}
    private void OnDisable()
    {if(!SaveGameManager.IsLoading&&IsRunning)Pause("Рейс приостановлен: компонент отключён",true);Unsubscribe();}
    private void OnDestroy(){Unsubscribe();}
}
