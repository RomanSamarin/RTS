using UnityEngine;
using UnityEngine.AI;

/// <summary>Автоматический путь к живому положению армии после полного лечения.</summary>
[DisallowMultipleComponent]
public class ArmyRejoin : MonoBehaviour
{
    [Min(1)] public float arrivalRadius=4f;
    [Min(10)] public float travelTimeout=180f;
    [Min(1)] public float blockedPathTimeout=20f;
    public bool debugRejoin=true;
    public bool IsReturning { get; private set; }
    public string Status { get; private set; }="";
    private ArmyGroup army;
    private GOScript command;
    private NavMeshAgent agent;
    private Unit unit;
    private int token;
    private float nextPath,activeTravel,blockedSince=-1;
    private Vector3 lastGoal;
    private bool sent;
    public static bool Returning(GameObject obj)=>obj!=null&&(obj.GetComponent<ArmyRejoin>()?.IsReturning??false);
    private void Awake()
    {
        command=GetComponent<GOScript>();agent=GetComponent<NavMeshAgent>();unit=GetComponent<Unit>();
        if(command!=null)command.ManualOrderIssued+=OnManualOrder;
    }
    private void OnManualOrder()
    {
        if(!IsReturning)return;
        if(GetComponent<MilitaryOrders>()?.IsIssuingInternalMove??false)return;
        Cancel("Возвращение отменено новым приказом");
    }
    private bool ValidArmy()
    {
        var member=GetComponent<ArmyMember>();var worker=GetComponent<ResourcesMove>();
        return ArmyRejoinRules.Eligible(true,army!=null&&army.isActiveAndEnabled,member!=null&&member.Army==army,
            army!=null&&FactionMember.AreAllies(gameObject,army.gameObject),worker!=null&&worker.role==ResourcesMove.UnitRole.Soldier);
    }
    public void Begin(ArmyGroup previousArmy)
    {
        Cancel("");army=previousArmy;
        if(command==null||agent==null||unit==null||unit.unitHealth<unit.unitMaxHealth||!ValidArmy())
        {army=null;SetStatus("Армия недоступна: остаётся у больницы");return;}
        IsReturning=true;activeTravel=0;nextPath=0;blockedSince=-1;sent=false;
        SetStatus("Возвращается в армию");
    }
    private void SetStatus(string text)
    {if(Status==text)return;Status=text;if(debugRejoin&&text!="")Debug.Log($"[Возврат из больницы] {name}: {text}",this);}
    private void Pause(string reason)
    {if(command!=null)command.EndSupplyMove(token);token=0;sent=false;blockedSince=-1;SetStatus(reason);}
    public void Cancel(string reason)
    {
        if(command!=null)command.EndSupplyMove(token);
        token=0;IsReturning=false;army=null;
        if(reason!="")SetStatus(reason);
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if(!IsReturning)return;
        if(unit==null||unit.unitHealth<=0||!ValidArmy()){Cancel("Армия распущена, сменена или недоступна");return;}
        var food=GetComponent<UnitEat>();var attack=GetComponent<AttackController>();
        bool combat=unit.RecentlyAttacked||(food!=null&&food.InCombat())
            ||(attack!=null&&attack.targetToAttack!=null&&FactionMember.AreEnemies(gameObject,attack.targetToAttack.gameObject));
        if(combat){Pause("Вступил в бой: возвращение приостановлено");return;}
        if(HospitalPatient.Controls(this)||UnitEat.ControlsMovement(this)||(food!=null&&food.WantEat)||command.HasPlayerOrder)
        {Pause("Перерыв на питание, лечение или другой приоритетный приказ");return;}
        if(!agent.enabled||!agent.isOnNavMesh){Pause("Ожидает доступную поверхность NavMesh");return;}
        if(!army.TryGetRejoinAnchor(gameObject,out var anchor,out var facing,out bool moving))
        {Cancel("Нет доступного места сбора армии");return;}
        var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
        float distance=Vector3.Distance(transform.position,anchor);
        if(ArmyRejoinRules.Arrived(distance,Mathf.Max(1,arrivalRadius),moving)
            &&NavMesh.SamplePosition(anchor,out var anchorFloor,1,filter)&&!NavMesh.Raycast(transform.position,anchorFloor.position,out _,filter))
        {Cancel("Вернулся к армии");return;}
        activeTravel+=Time.deltaTime;
        if(activeTravel>travelTimeout){Cancel("Истекло время возвращения");return;}
        if(Time.time<nextPath)return;nextPath=Time.time+.75f;
        if(!command.OwnsSupplyOrder(token)&&!command.TryBeginSupplyMove(out token))
        {SetStatus("Ожидает освобождения автоматического маршрута");return;}
        facing.y=0;if(facing.sqrMagnitude<.001f)facing=Vector3.forward;facing.Normalize();
        var side=new Vector3(facing.z,0,-facing.x);
        // Небольшое постоянное смещение не собирает выздоровевших в одной точке.
        Vector3 goal=anchor-facing*Mathf.Max(1,arrivalRadius*.5f)+side*((Mathf.Abs(GetInstanceID())%5-2)*.65f);
        if(agent.isOnOffMeshLink)return;
        if(agent.pathPending)return;
        bool onSurface=NavMesh.SamplePosition(goal,out var hit,2,filter);
        bool update=onSurface&&(!sent||!agent.hasPath||agent.pathStatus!=NavMeshPathStatus.PathComplete||(lastGoal-hit.position).sqrMagnitude>.25f);
        if(!onSurface||(update&&!command.MoveForSupply(token,hit.position,.35f)))
        {
            command.EndSupplyMove(token);token=0;sent=false;
            if(blockedSince<0)blockedSince=Time.time;
            SetStatus("Нет полного пути к армии: ожидает проход");
            if(Time.time-blockedSince>=blockedPathTimeout)Cancel("Возвращение остановлено: путь к армии недоступен");
            return;
        }
        blockedSince=-1;lastGoal=hit.position;sent=true;SetStatus("Возвращается в армию");
    }
    private void OnDisable(){Cancel("");}
    private void OnDestroy(){if(command!=null)command.ManualOrderIssued-=OnManualOrder;}
}
