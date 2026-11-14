using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class ShipPassenger : MonoBehaviour
{
    public ShipTransport Ship{get;private set;}
    public bool IsAboard{get;private set;}
    public bool IsAssigned=>Ship!=null||IsAboard;
    public int SeatCost{get;private set;}
    public string Status=>IsAboard?(Ship!=null&&Ship.Disembarking?"Ожидает высадку":"На борту"):IsAssigned?"Идёт на посадку":"";
    private ShipSupplyDock boardingDock;
    private GOScript command;private NavMeshAgent agent;
    private int token;
    private float startedAt,nextRepathAt;
    private CartHorse companion;
    private Vector3 companionOffset;
    private ShipPassengerBody body=new ShipPassengerBody(),companionBody=new ShipPassengerBody();
    private static ShipPassenger Find(GameObject obj)
    {
        if(obj==null)return null;
        var own=obj.GetComponentInParent<ShipPassenger>();if(own!=null&&own.IsAssigned)return own;
        return obj.GetComponentInParent<ShipPassengerCompanion>()?.passenger;
    }
    public static bool Aboard(GameObject obj)=>Find(obj)?.IsAboard??false;
    public static bool Controls(GameObject obj)=>Find(obj)?.IsAssigned??false;
    private void Awake(){command=GetComponent<GOScript>();agent=GetComponent<NavMeshAgent>();if(command!=null)command.ManualOrderIssued+=CancelBoarding;}
    public bool TryBoard(ShipTransport ship)
    {
        var unit=GetComponent<Unit>();var worker=GetComponent<ResourcesMove>();
        bool cart=GetComponent<FoodCart>()!=null||GetComponent<SiegeLadderCart>()!=null,catapult=GetComponent<Catapult>()!=null;
        bool soldier=worker!=null&&worker.role==ResourcesMove.UnitRole.Soldier;
        if(IsAssigned||RamCrewMember.Assigned(gameObject)||!isActiveAndEnabled||ship==null||ship.Dock==null||unit==null||unit.unitHealth<=0||(!soldier&&!cart&&!catapult)
            ||!FactionMember.AreAllies(gameObject,ship.gameObject)||ShipCrewMember.Aboard(gameObject)||(GetComponent<ShipCrewMember>()?.IsAssigned??false)
            ||HospitalPatient.Controls(this)||(GetComponent<SiegeLadderClimber>()?.IsBusy??false)||(GetComponent<SiegeLadderInstaller>()?.IsBusy??false)
            ||unit.RecentlyAttacked||(GetComponent<UnitEat>()?.InCombat()??false)||command==null||agent==null||!agent.enabled||!agent.isOnNavMesh
            ||!Approach(ship.Dock,out var door))return false;
        if(ship.IsArcherDeck&&(!soldier||cart||catapult||GetComponent<Archer>()==null
            ||BattleCombatant.TypeOf(gameObject)!=TroopType.Archer))return false;
        var horse=GetComponent<FoodCart>()?.AttachedHorse??GetComponent<SiegeLadderCart>()?.AttachedHorse;
        if(cart&&(horse==null||!horse.IsAlive))return false;
        int cost=ship.IsArcherDeck?1:ShipPassengerManifest.Cost(BattleCombatant.TypeOf(gameObject)==TroopType.Cavalry,cart,catapult);
        if(!ship.Reserve(this,cost))return false;
        command.NotifyMilitaryOrder();command.StopMoving();GetComponent<MilitaryOrders>()?.Cancel();
        GetComponent<SquadMember>()?.Leave();GetComponent<ArmyRejoin>()?.Cancel("");
        GetComponent<Catapult>()?.StopOrder();GetComponent<FoodCart>()?.StopFollowing();GetComponent<SiegeLadderCart>()?.CancelLoading();
        GetComponent<UnitEat>()?.CancelForCommand(false);FormationHold.Clear(gameObject);FormationAnchor.Release(gameObject);MeleeApproach.Release(gameObject);GetComponent<WallStation>()?.Release();
        var attack=GetComponent<AttackController>();if(attack!=null)attack.targetToAttack=null;
        if(!command.TryBeginSupplyMove(out token)||!command.MoveForSupply(token,door,.1f))
        {command.EndSupplyMove(token);token=0;ship.Release(this);return false;}
        Ship=ship;boardingDock=ship.Dock;SeatCost=cost;startedAt=Time.time;nextRepathAt=Time.time+.75f;
        companion=horse;
        if(companion!=null)
        {var link=companion.GetComponent<ShipPassengerCompanion>()??companion.gameObject.AddComponent<ShipPassengerCompanion>();link.passenger=this;}
        if(soldier&&GetComponent<UnitEat>()==null)gameObject.AddComponent<UnitEat>();
        return true;
    }
    private bool Approach(ShipSupplyDock dock,out Vector3 point)
    {
        point=default;if(dock==null||agent==null||!agent.enabled||!agent.isOnNavMesh)return false;
        var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
        if(!NavMesh.SamplePosition(dock.DeliveryPoint,out var hit,1.5f,filter))return false;
        var path=new NavMeshPath();if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
        point=hit.position;return true;
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading||!IsAssigned)return;
        if(IsAboard)
        {if(Ship==null){ShipLost();return;}if(Ship.CanExit(this))TryExit();return;}
        if(Ship==null||!Ship.isActiveAndEnabled||Ship.Dock!=boardingDock||!FactionMember.AreAllies(gameObject,Ship.gameObject)
            ||GetComponent<Unit>().unitHealth<=0||GetComponent<Unit>().RecentlyAttacked||(GetComponent<UnitEat>()?.InCombat()??false)
            ||!command.OwnsSupplyOrder(token)||Time.time-startedAt>90||!Approach(boardingDock,out var door)
            ||(companion!=null&&(!companion.IsAlive||!FactionMember.AreAllies(gameObject,companion.gameObject)
                ||(GetComponent<FoodCart>()?.AttachedHorse??GetComponent<SiegeLadderCart>()?.AttachedHorse)!=companion)))
        {CancelBoarding();return;}
        if(Vector3.Distance(transform.position,door)<=1.2f&&Ship.CanBoard(this))
        {
            if(!Ship.Board(this))return;
            command.EndSupplyMove(token);token=0;GetComponent<UnitEat>()?.CancelForCommand(false);
            body.Capture(gameObject,Ship.IsArcherDeck);
            if(companion!=null){companionOffset=companion.transform.position-transform.position;companionBody.Capture(companion.gameObject);}
            IsAboard=true;
            if(Ship.IsArcherDeck)transform.rotation=Ship.transform.rotation;
            PlaceOnShip();
            RefreshRegistration();return;
        }
        if(Time.time>=nextRepathAt)
        {nextRepathAt=Time.time+.75f;if(!command.MoveForSupply(token,door,.1f))CancelBoarding();}
    }
    private void RefreshRegistration()
    {FactionMember.Ensure(gameObject).RefreshControlRegistration();if(companion!=null)FactionMember.Ensure(companion.gameObject).RefreshControlRegistration();}
    public void CancelBoarding()
    {
        if(IsAboard)return;var ship=Ship;Ship=null;boardingDock=null;
        if(command!=null)command.EndSupplyMove(token);token=0;
        if(ship!=null)ship.Release(this);ClearCompanion();
    }
    private void ClearCompanion()
    {if(companion!=null){var link=companion.GetComponent<ShipPassengerCompanion>();if(link!=null&&link.passenger==this)link.passenger=null;}companion=null;}
    private bool LandPoint(ShipSupplyDock dock,Vector3 candidate,NavMeshAgent navigation,out Vector3 point)
    {
        point=default;if(navigation==null)return false;
        var filter=new NavMeshQueryFilter{agentTypeID=navigation.agentTypeID,areaMask=navigation.areaMask};
        if(!NavMesh.SamplePosition(dock.DeliveryPoint,out var entrance,1.5f,filter)||!NavMesh.SamplePosition(candidate,out var hit,.75f,filter))return false;
        var path=new NavMeshPath();if(!NavMesh.CalculatePath(entrance.position,hit.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
        point=hit.position;return true;
    }
    private void TryExit()
    {
        var dock=Ship.Dock;if(dock==null||!FactionMember.AreAllies(gameObject,dock.gameObject)||Time.time<nextRepathAt)return;
        nextRepathAt=Time.time+.5f;Physics.SyncTransforms();
        for(int i=0;i<49;i++)
        {
            float angle=i*Mathf.PI*.25f,radius=i==0?0:1+(i-1)/8;
            var candidate=dock.DeliveryPoint+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
            if(!LandPoint(dock,candidate,agent,out var point)||!body.ExitClear(point))continue;
            Vector3 horsePoint=default;
            if(companion!=null&&(!LandPoint(dock,point+companionOffset,companion.GetComponent<NavMeshAgent>(),out horsePoint)
                ||Vector3.Distance(horsePoint,point+companionOffset)>.8f||!companionBody.ExitClear(horsePoint)))continue;
            var ship=Ship;IsAboard=false;Ship=null;boardingDock=null;
            body.Restore(point);if(companion!=null)companionBody.Restore(horsePoint);
            GetComponent<CavalryCharge>()?.ResetTransportMotion();
            RefreshRegistration();ClearCompanion();ship.Exited(this);Physics.SyncTransforms();return;
        }
    }
    private void LateUpdate(){if(!SaveGameManager.IsLoading&&IsAboard&&Ship!=null)PlaceOnShip();}
    internal void PlaceOnShip()
    {
        var deck=Ship.GetComponent<ShipCombat>();
        int index=deck!=null?Ship.DeckIndex(this):-1;
        transform.position=index>=0?deck.DeckPosition(index):Ship.transform.position;
    }
    public void RestoreSavedHooks()
    {
        if(!IsAboard)return;
        body.Hide(Ship!=null&&Ship.IsArcherDeck);
        companionBody.Hide();
        if(Ship!=null)PlaceOnShip();
        RefreshRegistration();
    }
    public void ShipLost(GettingResources salvage = null)
    {
        if(!IsAboard){CancelBoarding();return;}
        var ship=Ship;Ship=null;if(ship!=null)ship.Release(this);
        if(salvage!=null)
        {
            LootTransfer.Move(GetComponent<GettingResources>(),salvage,int.MaxValue);
            if(companion!=null)LootTransfer.Move(companion.GetComponent<GettingResources>(),salvage,int.MaxValue);
        }
        if(companion!=null)companion.GetComponent<Unit>()?.Destroy((GameObject)null);
        GetComponent<Unit>()?.Destroy((GameObject)null);
    }
    private void OnDisable(){if(SaveGameManager.IsLoading)return;if(!IsAboard)CancelBoarding();else{var ship=Ship;Ship=null;if(ship!=null)ship.Release(this);}}
    private void OnDestroy(){if(command!=null)command.ManualOrderIssued-=CancelBoarding;ClearCompanion();}
}
