using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class HospitalPatient : MonoBehaviour
{
    [Min(1)] public float searchRadius=120;
    [Min(1)] public float travelTimeout=90;
    public Hospital Hospital { get; private set; }
    public bool IsAdmitted { get; private set; }
    public bool IsBusy=>Hospital!=null||IsAdmitted;
    public string Status { get; private set; }="";
    public float Progress=>Hospital!=null?treatment.Progress(Hospital.treatmentSeconds):0;
    private Unit unit;
    private GOScript command;
    private NavMeshAgent agent;
    private AttackController attack;
    private UnitEat food;
    private int routeToken;
    private float nextSearch,nextRepath,started;
    private HospitalTreatment treatment=new HospitalTreatment();
    private Renderer[] renderers;private bool[] rendererStates;
    private Collider[] colliders;private bool[] colliderStates;
    private Animator[] animators;private bool[] animatorStates;
    private Canvas[] canvases;private bool[] canvasStates;
    private Rigidbody[] bodies;private bool[] bodyStates;
    private NavMeshObstacle[] obstacles;private bool[] obstacleStates;
    private bool savedAgent,discharging;
    private Vector3 exitOrigin,door;
    private ArmyGroup recoveryArmy;
    private bool rejoinAfterTreatment;
    public static HospitalPatient Ensure(GameObject obj)
    {
        var worker=obj.GetComponent<ResourcesMove>();
        if(worker==null||worker.role!=ResourcesMove.UnitRole.Soldier||obj.GetComponent<GOScript>()==null)return null;
        var patient=obj.GetComponent<HospitalPatient>();return patient!=null?patient:obj.AddComponent<HospitalPatient>();
    }
    public static bool Inside(GameObject obj)=>obj!=null&&(obj.GetComponentInParent<HospitalPatient>()?.IsAdmitted??false);
    public static bool Controls(Component obj)=>obj.GetComponentInParent<HospitalPatient>()?.IsBusy??false;
    private void Awake()
    {
        unit=GetComponent<Unit>();command=GetComponent<GOScript>();agent=GetComponent<NavMeshAgent>();attack=GetComponent<AttackController>();
        if(command!=null)command.ManualOrderIssued+=CancelTrip;
    }
    private bool InCombat()
    {
        if(unit.RecentlyAttacked)return true;
        if(food==null)food=GetComponent<UnitEat>();
        return (food!=null&&food.InCombat())||(attack!=null&&attack.targetToAttack!=null&&FactionMember.AreEnemies(gameObject,attack.targetToAttack.gameObject));
    }
    private bool PlayerOrder()=>command.HasPlayerOrder||(GetComponent<MilitaryOrders>()?.Current??MilitaryOrders.Order.None)!=MilitaryOrders.Order.None;
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if(unit==null||unit.unitHealth<=0||command==null||agent==null)return;
        if(IsAdmitted)
        {
            if(discharging){TryDischarge();return;}
            if(Hospital==null||!Hospital.isActiveAndEnabled||Hospital.GetComponent<BuildingHealth>().IsDead
                ||!FactionMember.CanUse(gameObject,Hospital.gameObject)){RequestDischarge();return;}
            if(unit.unitHealth>=unit.unitMaxHealth){CompleteTreatment();return;}
            if(!Hospital.Operational){Status="Лечение приостановлено: нет подъезда";return;}
            float healing=treatment.Advance(Time.deltaTime,Hospital.treatmentSeconds,Hospital.healingPerTreatment,()=>
            {
                if(!Hospital.ConsumeTreatment())return false;
                GetComponent<UnitEat>()?.FeedAtHospital();return true;
            });
            if(healing>0)unit.Heal(healing);
            Status=treatment.WaitingFood?"Ожидает еду в больнице":"Лечится";
            if(unit.unitHealth>=unit.unitMaxHealth)CompleteTreatment();
            return;
        }
        if(Hospital!=null)
        {
            if(!Hospital.Operational||!Hospital.HasBed(this)||!FactionMember.CanUse(gameObject,Hospital.gameObject)||InCombat()
                ||!command.OwnsSupplyOrder(routeToken)||Time.time-started>travelTimeout||unit.unitHealth>=unit.unitMaxHealth)
            {CancelTrip();return;}
            if(Vector3.Distance(transform.position,door)<=1.2f){Admit();return;}
            if(Time.time>=nextRepath)
            {nextRepath=Time.time+.75f;if(!command.MoveForSupply(routeToken,door,.1f))CancelTrip();}
            return;
        }
        if(Time.time<nextSearch)return;nextSearch=Time.time+2;
        var worker=GetComponent<ResourcesMove>();
        if(worker==null||worker.role!=ResourcesMove.UnitRole.Soldier||!FactionMember.IsPlayer(gameObject)
            ||unit.unitHealth>=unit.unitMaxHealth||!agent.enabled||!agent.isOnNavMesh||PlayerOrder()||InCombat())return;
        if(food!=null&&food.IsBusy)return;
        Hospital best=null;Vector3 bestDoor=default;float distance=searchRadius;
        var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
        foreach(var hospital in global::Hospital.AllHospitals)
        {
            if(hospital==null||!hospital.Operational||hospital.FreeBeds==0||!FactionMember.CanUse(gameObject,hospital.gameObject))continue;
            if(!NavMesh.SamplePosition(hospital.ServicePoint,out var hit,2,filter))continue;
            float d=Vector3.Distance(transform.position,hit.position);if(d>=distance)continue;
            var path=new NavMeshPath();if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            distance=d;best=hospital;bestDoor=hit.position;
        }
        if(best==null)return;
        if(ArmyRejoin.Returning(gameObject))GetComponent<ArmyRejoin>().Cancel("Возвращение прервано новым лечением");
        if(command.CurrentOrder==MovementOrderState.Order.Supply)command.StopMoving();
        if(!command.TryBeginSupplyMove(out routeToken))return;
        if(!best.Reserve(this)){command.EndSupplyMove(routeToken);return;}
        Hospital=best;door=bestDoor;started=Time.time;nextRepath=0;Status="Идёт в больницу";
        recoveryArmy=GetComponent<ArmyMember>()?.Army;rejoinAfterTreatment=false;
        if(attack!=null)attack.targetToAttack=null;
        if(best.debugHospital)Debug.Log($"[Больница] {name}: идёт на лечение в {best.name}.",this);
    }
    private void Admit()
    {
        command.EndSupplyMove(routeToken);routeToken=0;
        FormationHold.Clear(gameObject);GetComponent<WallStation>()?.Release();MeleeApproach.Release(gameObject);
        exitOrigin=Hospital.exitPoint!=null?Hospital.exitPoint.position:door;
        IsAdmitted=true;discharging=false;treatment=new HospitalTreatment();
        renderers=GetComponentsInChildren<Renderer>(true);rendererStates=new bool[renderers.Length];
        for(int i=0;i<renderers.Length;i++){rendererStates[i]=renderers[i].enabled;renderers[i].enabled=false;}
        colliders=GetComponentsInChildren<Collider>(true);colliderStates=new bool[colliders.Length];
        for(int i=0;i<colliders.Length;i++){colliderStates[i]=colliders[i].enabled;colliders[i].enabled=false;}
        animators=GetComponentsInChildren<Animator>(true);animatorStates=new bool[animators.Length];
        for(int i=0;i<animators.Length;i++){animatorStates[i]=animators[i].enabled;animators[i].enabled=false;}
        canvases=GetComponentsInChildren<Canvas>(true);canvasStates=new bool[canvases.Length];
        for(int i=0;i<canvases.Length;i++){canvasStates[i]=canvases[i].enabled;canvases[i].enabled=false;}
        bodies=GetComponentsInChildren<Rigidbody>(true);bodyStates=new bool[bodies.Length];
        for(int i=0;i<bodies.Length;i++){bodyStates[i]=bodies[i].isKinematic;bodies[i].isKinematic=true;}
        obstacles=GetComponentsInChildren<NavMeshObstacle>(true);obstacleStates=new bool[obstacles.Length];
        for(int i=0;i<obstacles.Length;i++){obstacleStates[i]=obstacles[i].enabled;obstacles[i].enabled=false;}
        savedAgent=agent.enabled;agent.ResetPath();agent.enabled=false;
        FactionMember.Ensure(gameObject).RefreshControlRegistration();Status="Лечится";
        if(Hospital.debugHospital)Debug.Log($"[Больница] {name}: принят, койка занята, солдат скрыт.",this);
    }
    public void CancelTrip()
    {
        if(IsAdmitted)return;
        var hospital=Hospital;Hospital=null;
        if(hospital!=null)hospital.Release(this);
        if(command!=null)command.EndSupplyMove(routeToken);
        routeToken=0;nextSearch=Time.time+3;Status="";
        recoveryArmy=null;rejoinAfterTreatment=false;
    }
    public void HospitalUnavailable(){if(IsAdmitted)RequestDischarge();else CancelTrip();}
    public void RequestDischarge()
    {
        if(!IsAdmitted){CancelTrip();return;}
        rejoinAfterTreatment=false;
        TryDischarge();
    }
    public void RestoreSavedVisibility()
    {
        if(!IsAdmitted)return;
        foreach(var item in GetComponentsInChildren<Renderer>(true))item.enabled=false;
        foreach(var item in GetComponentsInChildren<Collider>(true))item.enabled=false;
        foreach(var item in GetComponentsInChildren<Animator>(true))item.enabled=false;
        foreach(var item in GetComponentsInChildren<Canvas>(true))item.enabled=false;
        foreach(var item in GetComponentsInChildren<Rigidbody>(true))item.isKinematic=true;
        foreach(var item in GetComponentsInChildren<NavMeshObstacle>(true))item.enabled=false;
        var navigation=GetComponent<NavMeshAgent>();if(navigation!=null)navigation.enabled=false;
    }
    private void CompleteTreatment()
    {rejoinAfterTreatment=true;TryDischarge();}
    private void TryDischarge()
    {
        discharging=true;Status="Ожидает свободный выход";
        if(Time.time<nextRepath)return;nextRepath=Time.time+.5f;
        var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
        for(int i=0;i<33;i++)
        {
            float angle=i*Mathf.PI*.25f;float radius=i==0?0:1+(i-1)/8;
            var p=exitOrigin+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
            if(!NavMesh.SamplePosition(p,out var hit,1,filter))continue;
            if(Physics.CheckCapsule(hit.position+Vector3.up*(agent.radius+.05f),
                hit.position+Vector3.up*Mathf.Max(agent.radius+.05f,agent.height-agent.radius),agent.radius,~0,QueryTriggerInteraction.Ignore))continue;
            transform.position=hit.position;Restore();
            if(agent.enabled)agent.Warp(hit.position);
            Physics.SyncTransforms();
            var hospital=Hospital;Hospital=null;if(hospital!=null)hospital.Release(this);
            if(hospital!=null&&hospital.debugHospital)Debug.Log($"[Больница] {name}: выписан, здоровье {unit.unitHealth:0}/{unit.unitMaxHealth:0}.",this);
            discharging=false;nextSearch=Time.time+5;Status="Выписан";
            FactionMember.Ensure(gameObject).RefreshControlRegistration();
            var previousArmy=recoveryArmy;recoveryArmy=null;
            if(rejoinAfterTreatment&&unit.unitHealth>=unit.unitMaxHealth&&previousArmy!=null)
            {
                var rejoin=GetComponent<ArmyRejoin>();if(rejoin==null)rejoin=gameObject.AddComponent<ArmyRejoin>();
                rejoin.Begin(previousArmy);
            }
            rejoinAfterTreatment=false;
            return;
        }
    }
    private void Restore()
    {
        if(!IsAdmitted)return;IsAdmitted=false;
        for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].enabled=rendererStates[i];
        for(int i=0;i<colliders.Length;i++)if(colliders[i]!=null)colliders[i].enabled=colliderStates[i];
        for(int i=0;i<animators.Length;i++)if(animators[i]!=null)animators[i].enabled=animatorStates[i];
        for(int i=0;i<canvases.Length;i++)if(canvases[i]!=null)canvases[i].enabled=canvasStates[i];
        for(int i=0;i<bodies.Length;i++)if(bodies[i]!=null)
        {bodies[i].isKinematic=bodyStates[i];if(!bodyStates[i]){bodies[i].velocity=Vector3.zero;bodies[i].angularVelocity=Vector3.zero;}}
        for(int i=0;i<obstacles.Length;i++)if(obstacles[i]!=null)obstacles[i].enabled=obstacleStates[i];
        if(agent!=null&&unit!=null&&unit.unitHealth>0)agent.enabled=savedAgent;
    }
    private void OnDisable()
    {
        if(Hospital!=null)Hospital.Release(this);
        Hospital=null;
        if(IsAdmitted)Restore();else if(command!=null)command.EndSupplyMove(routeToken);
        routeToken=0;
        discharging=false;
        recoveryArmy=null;rejoinAfterTreatment=false;
    }
    private void OnDestroy(){if(command!=null)command.ManualOrderIssued-=CancelTrip;}
}
