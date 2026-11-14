using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShipTransport : MonoBehaviour
{
    private readonly ShipPassengerManifest manifest=new ShipPassengerManifest();
    [Min(.1f)] public float boardingInterval=.5f;
    private float nextBoardAt,nextExitAt,nextWarningAt;
    public bool Disembarking{get;private set;}
    private ShipHull Hull=>GetComponent<ShipHull>();
    public bool IsArcherDeck=>Hull.ArcherPositions>0;
    public int Capacity=>IsArcherDeck?Hull.ArcherPositions:Hull.TransportCapacity;
    public int UsedSeats=>manifest.Used;
    public int AboardSeats=>manifest.BoardedSeats;
    public int ReservedSeats=>UsedSeats-AboardSeats;
    public int HungryPassengers{get{int count=0;foreach(var p in Passengers)if(p.IsAboard&&(p.GetComponent<UnitEat>()?.WantEat??false))count++;return count;}}
    public bool BlocksDeparture=>isActiveAndEnabled&&(ReservedSeats>0||Disembarking);
    public string Status=>Disembarking?"Высадка армии":ReservedSeats>0?"Посадка армии":$"Места: {UsedSeats}/{Capacity}";
    public ShipSupplyDock Dock
    {
        get
        {
            var dock=GetComponent<ShipProvisions>()?.Dock;
            return isActiveAndEnabled&&dock!=null&&dock.GetComponent<Port>()!=null&&dock.isActiveAndEnabled&&BuildingRoadAccess.Allows(dock)
                &&!(dock.GetComponent<BuildingHealth>()?.IsDead??true)
                &&(GetComponent<ShipNavigation>()?.IsMooredTo(dock)??false)&&FactionMember.AreAllies(gameObject,dock.gameObject)?dock:null;
        }
    }
    public List<ShipPassenger> Passengers
    {get{var list=new List<ShipPassenger>();foreach(var owner in manifest.Owners())if(owner is ShipPassenger p)list.Add(p);return list;}}
    public bool TryBoard(GameObject unit)
    {
        if(!isActiveAndEnabled||Capacity<=0||Disembarking||Dock==null||unit==null||unit.GetComponent<ShipHull>()!=null||unit.GetComponent<CartHorse>()!=null)return false;
        var passenger=unit.GetComponent<ShipPassenger>()??unit.AddComponent<ShipPassenger>();return passenger.TryBoard(this);
    }
    internal bool Reserve(ShipPassenger passenger,int cost)=>manifest.Reserve(passenger,cost,Capacity);
    internal int DeckIndex(ShipPassenger passenger)=>manifest.BoardedIndex(passenger);
    internal void Release(ShipPassenger passenger){manifest.Release(passenger);}
    internal bool CanBoard(ShipPassenger passenger)=>Time.time>=nextBoardAt&&manifest.Next(false)==passenger;
    internal bool Board(ShipPassenger passenger)
    {if(!CanBoard(passenger)||!manifest.Board(passenger))return false;nextBoardAt=Time.time+Mathf.Max(.1f,boardingInterval);return true;}
    internal bool CanExit(ShipPassenger passenger)=>Disembarking&&Time.time>=nextExitAt&&manifest.Next(true)==passenger;
    internal void Exited(ShipPassenger passenger){manifest.Release(passenger);nextExitAt=Time.time+Mathf.Max(.1f,boardingInterval);}
    [ContextMenu("Отменить посадку армии")]
    public void CancelBoarding(){foreach(var p in Passengers)if(!p.IsAboard)p.CancelBoarding();}
    [ContextMenu("Высадить армию")]
    public void DisembarkAll()
    {
        if(Dock==null){Debug.LogWarning("[Транспорт] Для высадки нужен доступный свой или союзный порт с дорогой.",this);return;}
        CancelBoarding();Disembarking=AboardSeats>0;nextExitAt=0;
    }
    public int BoardSelected()
    {
        if(ManagerGO.Instance==null)return 0;int count=0;
        foreach(var selected in new List<GameObject>(ManagerGO.Instance.unitsSelected))
        {var root=selected!=null?selected.GetComponentInParent<Unit>():null;if(root!=null&&TryBoard(root.gameObject))count++;}
        return count;
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if(Disembarking&&(AboardSeats==0||Dock==null))Disembarking=false;
        bool hungry=HungryPassengers>0;
        if(hungry&&Time.time>=nextWarningAt){nextWarningAt=Time.time+30;Debug.LogWarning("[Транспорт] Пассажиры голодают: нужны корабельные припасы.",this);}
    }
    private void OnDisable(){if(!SaveGameManager.IsLoading)CancelBoarding();}
    private void OnDestroy(){if(!SaveGameManager.IsLoading)LosePassengers();}
    internal void LosePassengers(GettingResources salvage = null)
    {
        foreach(var passenger in Passengers)
        {
            if(passenger.Ship==this)passenger.ShipLost(salvage);
        }
    }
    public static bool HandleBoardingClick(Ray ray,List<GameObject> selected)
    {
        if(!Physics.Raycast(ray,out var hit,Mathf.Infinity,~0,QueryTriggerInteraction.Ignore))return false;
        var transport=hit.collider.GetComponentInParent<ShipTransport>();
        if(transport==null||transport.Capacity<=0||!FactionMember.IsPlayer(transport.gameObject))return false;
        bool armySelected=false;
        foreach(var obj in selected)
        {
            var unit=obj!=null?obj.GetComponentInParent<Unit>():null;if(unit==null)continue;
            if(unit.GetComponent<ResourcesMove>()?.role==ResourcesMove.UnitRole.Soldier||unit.GetComponent<Catapult>()!=null
                ||unit.GetComponent<FoodCart>()!=null||unit.GetComponent<SiegeLadderCart>()!=null){armySelected=true;break;}
        }
        if(!armySelected)return false;
        int assigned=0;foreach(var obj in new List<GameObject>(selected))
        {var unit=obj!=null?obj.GetComponentInParent<Unit>():null;if(unit!=null&&FactionMember.IsPlayer(unit.gameObject)&&transport.TryBoard(unit.gameObject))assigned++;}
        if(assigned==0)Debug.LogWarning("[Транспорт] Посадка недоступна: проверьте порт, свободные места, путь и состояние бойцов.",transport);
        return true;
    }
}
