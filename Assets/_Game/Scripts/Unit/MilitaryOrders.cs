using UnityEngine;
using UnityEngine.AI;

/// <summary>Длительные боевые приказы и ограничение автоматического преследования.</summary>
public class MilitaryOrders : MonoBehaviour
{
    public enum Order { None, AttackMove, Hold, Returning, Patrol }
    public Order Current { get; private set; }
    [Tooltip("Сообщения о приказах и переходах состояния в Console.")]
    public bool debugOrders=true;
    [Min(1)] public float acquisitionRadius=10f;
    [Min(1)] public float pursuitDistance=15f;
    public string Status { get; private set; }="";
    public bool BlocksFood => Current==Order.AttackMove||Current==Order.Returning||Current==Order.Patrol;
    private PatrolRoute<Vector3> patrol;
    private float nextTravel;
    private GOScript command;
    private AttackController attack;
    private NavMeshAgent agent;
    private Vector3 destination,anchor;
    private bool issuing,tracking;
    private float nextSearch;
    private float nextTargetAccess;
    private string lastPause="";
    private bool loggedTravel;
    public bool IsIssuingInternalMove=>issuing;
    private Vector3 loggedDestination;
    private void Trace(string message)
    {if(debugOrders)Debug.Log($"[Боевые приказы] {name}: {message}",this);}
    private string NotReadyReason()
    {
        if(command==null)return "нет GOScript";
        if(attack==null)return "нет AttackController";
        if(agent==null)return "нет NavMeshAgent";
        if(!agent.enabled)return "NavMeshAgent выключен";
        return "юнит не стоит на NavMesh";
    }
    private bool RequireReady(string order)
    {if(Ready)return true;Trace($"Отказ: {order} — {NotReadyReason()}.");return false;}
    private void Pause(string reason)
    {
        if(lastPause==reason)return;
        if(reason!="")Trace("Приказ приостановлен: "+reason);
        else if(lastPause!="")Trace("Выполнение приказа возобновлено.");
        lastPause=reason;
    }
    private void OnDisable(){Cancel();}

    public static MilitaryOrders Ensure(GameObject obj)
    {
        var worker=obj.GetComponent<ResourcesMove>();
        if(worker==null||worker.role!=ResourcesMove.UnitRole.Soldier||obj.GetComponent<GOScript>()==null)return null;
        var orders=obj.GetComponent<MilitaryOrders>();return orders!=null?orders:obj.AddComponent<MilitaryOrders>();
    }
    private void Awake()
    {
        command=GetComponent<GOScript>();attack=GetComponent<AttackController>();agent=GetComponent<NavMeshAgent>();
        if(command!=null)command.ManualOrderIssued+=Cancel;
    }
    private void OnDestroy(){if(command!=null)command.ManualOrderIssued-=Cancel;}
    public void Cancel()
    {
        if(issuing)return;
        if(Current!=Order.None||tracking)Trace($"Отменён приказ {Current} (новый приказ, отключение или смена стороны).");
        Current=Order.None;patrol=null;tracking=false;Status="";loggedTravel=false;lastPause="";
    }
    private bool Ready=>command!=null&&attack!=null&&agent!=null&&agent.enabled&&agent.isOnNavMesh;
    public bool Patrol(System.Collections.Generic.IList<Vector3> points)
    {
        if(!RequireReady("патрулирование")||points==null||points.Count<2)return false;
        // Проверяем все участки до отмены прежнего приказа.
        var path=new NavMeshPath();
        Vector3 previous=transform.position;
        var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
        for(int i=0;i<points.Count;i++)
        {
            if(!NavMesh.CalculatePath(previous,points[i],filter,path)||path.status!=NavMeshPathStatus.PathComplete)
            {Trace($"Патруль не начат: нет полного пути к точке {i+1}.");return false;}
            if(i>0&&(!NavMesh.CalculatePath(points[i],previous,filter,path)||path.status!=NavMeshPathStatus.PathComplete))
            {Trace($"Патруль не начат: нет обратного пути от точки {i+1}.");return false;}
            previous=points[i];
        }
        Stop();patrol=new PatrolRoute<Vector3>(points);Current=Order.Patrol;
        destination=patrol.Point;anchor=transform.position;nextSearch=0;nextTravel=0;
        Travel(destination);Status=$"Патруль: точка 1/{patrol.Count}";
        Trace($"Патруль начат: {patrol.Count} точек, маршрут туда и обратно.");return true;
    }
    public bool AttackMove(Vector3 point)
    {
        if(!RequireReady("атака с перемещением"))return false;
        var path=new NavMeshPath();
        if(!agent.CalculatePath(point,path)||path.status!=NavMeshPathStatus.PathComplete){Status="Нет пути к цели";Trace($"Отказ: нет полного пути к {point}, статус {path.status}.");return false;}
        Stop();destination=point;anchor=transform.position;Current=Order.AttackMove;
        Travel(point);Status="Атака с перемещением";Trace($"Получена атака с перемещением: {point}. Поиск {acquisitionRadius} м, преследование {pursuitDistance} м.");return true;
    }
    public void Hold()
    {
        if(!RequireReady("удержание позиции"))return;
        Stop();anchor=transform.position;Current=Order.Hold;Status="Удерживает позицию";
        FormationHold.SetPoint(gameObject,anchor,transform.forward,FormationHold.BeginGroup());
        Trace($"Удерживает позицию {anchor}. Преследование запрещено.");
    }
    public void HoldFacing(Vector3 forward,int group)
    {
        Hold();if(Current!=Order.Hold)return;
        forward.y=0;if(forward.sqrMagnitude<.001f)return;
        FormationHold.SetPoint(gameObject,anchor,forward.normalized,group);
        Trace($"Направление обороны: {forward.normalized}.");
    }
    public void Stop()
    {
        Cancel();
        if(command==null)return;
        command.StopMoving();FormationHold.Clear(gameObject);
        if(attack!=null)attack.targetToAttack=null;
        command.NotifyMilitaryOrder();
        if(agent!=null&&agent.enabled&&agent.isOnNavMesh){agent.isStopped=false;agent.ResetPath();}
        Trace("Остановка: движение, цель и удержание сброшены.");
    }
    private void Travel(Vector3 point)
    {
        if(!loggedTravel||(point-loggedDestination).sqrMagnitude>.01f)
        {Trace($"Движение к {point}, режим {Current}.");loggedTravel=true;loggedDestination=point;}
        issuing=true;
        try{command.MoveTo(point);}finally{issuing=false;}
    }
    public static bool BlocksChase(Component unit)
        =>unit.GetComponentInParent<MilitaryOrders>()?.Current==Order.Hold;
    public static bool AllowsTarget(GameObject unit,Transform target)
    {
        if(unit.GetComponent<GOScript>()?.IsRetreating??false)return false;
        var orders=unit.GetComponent<MilitaryOrders>();
        return orders==null||orders.Allows(target);
    }
    private bool Allows(Transform target)
    {
        if(target==null||Current==Order.Returning)return false;
        if(Current==Order.Hold)
        {
            var archer=GetComponent<Archer>();float range=archer!=null?archer.attackRange:attack.meleeRange;
            return Vector3.Distance(anchor,AttackController.CombatPoint(target,anchor))<=range;
        }
        return !tracking||Vector3.Distance(anchor,AttackController.CombatPoint(target,anchor))<=pursuitDistance;
    }
    private bool Living(Transform target)
    {
        if(target==null||!target.gameObject.activeInHierarchy||!FactionMember.AreEnemies(gameObject,target.gameObject))return false;
        var unit=target.GetComponent<Unit>();var building=target.GetComponent<BuildingHealth>();
        return unit!=null?unit.unitHealth>0:building!=null&&!building.IsDead;
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if(!Ready){if(Current!=Order.None)Pause(NotReadyReason());return;}
        if(GetComponent<Unit>()?.unitHealth<=0)return;
        if(!FactionMember.IsPlayer(gameObject)){Cancel();return;}
        if(UnitEat.ControlsMovement(this)){if(Current!=Order.None)Pause("перерыв на еду");return;}
        if(BattleCombatant.BlocksActions(this)){if(Current!=Order.None)Pause("боевой эффект блокирует действия");return;}
        Pause("");
        var target=attack.targetToAttack;
        bool advancing=Current==Order.AttackMove||Current==Order.Patrol;
        if(advancing&&target!=null&&Time.time>=nextTargetAccess)
        {
            nextTargetAccess=Time.time+.5f;
            if(!MeleeApproach.CanTarget(gameObject,target))
            {Trace($"Цель {target.name}: нет свободного доступного места, ищет другую.");attack.targetToAttack=null;target=null;}
        }
        if(Current==Order.None&&command.HasPlayerOrder){tracking=false;return;}
        if(target==null&&tracking&&Current==Order.None)
        {
            Trace("Автоматическая цель потеряна. Возврат к месту вступления в бой.");
            command.StopMoving();Current=Order.Returning;Travel(anchor);
        }
        if(target!=null&&!tracking){if(Current!=Order.Hold)anchor=transform.position;tracking=true;Trace($"Вступает в бой с {target.name}. Опорная точка {anchor}.");}
        if(target!=null&&(!Living(target)||!Allows(target)))
        {
            Trace($"Цель {target.name} сброшена: {(!Living(target)?"погибла, недоступна или сменила сторону":Current==Order.Hold?"вышла из дальности удержания":"превышен радиус преследования")}.");
            attack.targetToAttack=null;target=null;command.StopMoving();
            if(Current==Order.None){Current=Order.Returning;Travel(anchor);}
        }
        if(Current==Order.Returning)
        {
            if(!command.isCommandToMove){Trace("Возврат завершён или движение прекращено.");Current=Order.None;tracking=false;}return;
        }
        if(Current==Order.Hold)
        {
            if(FormationHold.TryGetFacing(gameObject,out var facing))
                transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(facing),540*Time.deltaTime);
            if(Vector3.Distance(transform.position,anchor)>.4f)agent.SetDestination(anchor);
            else if(agent.hasPath)agent.ResetPath();
        }
        if(target!=null)
        {
            if(advancing&&command.isCommandToMove)command.StopMoving();
            if(Current==Order.Patrol)Status="Патруль: в бою";
            return;
        }
        if(advancing)
        {
            if(Time.time>=nextSearch)
            {
                nextSearch=Time.time+.35f;float nearest=acquisitionRadius;
                foreach(var member in FactionMember.All)
                {
                    if(member==null||!Living(member.transform)||!Allows(member.transform))continue;
                    float distance=Vector3.Distance(transform.position,AttackController.CombatPoint(member.transform,transform.position));
                    if(distance>=nearest)continue;
                    if(!MeleeApproach.CanTarget(gameObject,member.transform))continue;
                    var path=new NavMeshPath();
                    var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
                    if(!NavMesh.SamplePosition(AttackController.CombatPoint(member.transform,transform.position),out var approach,2f,filter)
                        ||!agent.CalculatePath(approach.position,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                    nearest=distance;attack.targetToAttack=member.transform;
                }
                if(attack.targetToAttack!=null)
                {
                    var selected=attack.targetToAttack;command.StopMoving();
                    if(!MeleeApproach.CanTarget(gameObject,selected,true)){attack.targetToAttack=null;return;}
                    anchor=transform.position;tracking=true;
                    if(Current==Order.Patrol)Status="Патруль: в бою";
                    Trace($"Найдена цель {selected.name}: маршрут приостановлен на время боя.");return;
                }
            }
            if(tracking){Trace("Бой завершён или цель потеряна. Продолжает маршрут.");loggedTravel=false;}
            tracking=false;
            if(Current==Order.Patrol)
            {
                if(!command.isCommandToMove&&Time.time>=nextTravel)
                {
                    nextTravel=Time.time+1f;
                    if(Vector3.Distance(transform.position,destination)<.7f)patrol.Arrive();
                    destination=patrol.Point;
                    var path=new NavMeshPath();
                    if(!agent.CalculatePath(destination,path)||path.status!=NavMeshPathStatus.PathComplete)
                    {Status="Патруль: путь перекрыт";return;}
                    Travel(destination);Status=$"Патруль: точка {patrol.Index+1}/{patrol.Count}";
                }
                return;
            }
            if(!command.isCommandToMove)
            {
                if(Vector3.Distance(transform.position,destination)<.7f){Current=Order.None;Status="Приказ выполнен";Trace($"Атака с перемещением выполнена: достигнута точка {destination}.");}
                else Travel(destination);
            }
        }
        else if(Current==Order.None)tracking=false;
    }
}
