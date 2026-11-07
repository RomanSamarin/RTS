using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Общий маршрут, постоянные места, временная колонна и восстановление формы.</summary>
public class SquadMovement : MonoBehaviour
{
    private class Member
    {
        public SquadMember link;public GOScript command;public NavMeshAgent agent;
        public Vector3 slot,offset,lastWaypoint;public int rank;public bool sent,arrived;
        public Vector3 probe;
        public Vector3 departure;
        public float nextRepath;
    }
    private readonly List<Member> members=new List<Member>();
    private readonly List<Vector3> route=new List<Vector3>();
    private NavMeshQueryFilter filter;
    private Vector3 finish,finalForward;
    private float progress,length,spacing,width,nextTick,nextPath,wideSince=-1,blockedSince=-1,slowest;
    private int formationGroup;
    private float columnLength;
    private float lastMotion;
    private bool assembling;
    private Vector3 transitionCenter;
    private float transitionProgress,transitionDistance;
    public float Speed=>slowest;
    public bool IsColumn { get; private set; }
    public string Status { get; private set; }

    public static bool MoveSelected(List<GameObject> units,Vector3 center,float defaultSpacing)
    {
        if(units.Count<2)return false;
        var slots=new List<Vector3>();Vector3 oldCenter=Vector3.zero;bool formed=true;int group=FormationHold.GetGroup(units[0]);
        foreach(var unit in units)
        {
            if(!Eligible(unit))return false;
            if(group==0||FormationHold.GetGroup(unit)!=group||!FormationHold.TryGetPoint(unit,out var slot)){formed=false;slot=unit.transform.position;}
            slots.Add(slot);oldCenter+=slot;
        }
        oldCenter/=slots.Count;
        Vector3 facing=Vector3.forward;
        if(formed)FormationHold.TryGetFacing(units[0],out facing);
        else {facing=center-oldCenter;facing.y=0;if(facing.sqrMagnitude<.001f)facing=Vector3.forward;facing.Normalize();}
        if(formed)for(int i=0;i<slots.Count;i++)slots[i]=center+slots[i]-oldCenter;
        else
        {
            // У произвольной группы сохраняем порядок бойцов слева направо, затем по глубине.
            var right=new Vector3(facing.z,0,-facing.x);
            units.Sort((a,b)=>Vector3.Dot(a.transform.position-oldCenter,right).CompareTo(Vector3.Dot(b.transform.position-oldCenter,right)));
            int columns=Mathf.CeilToInt(Mathf.Sqrt(units.Count)),rows=Mathf.CeilToInt((float)units.Count/columns);
            for(int i=0;i<slots.Count;i++)slots[i]=center+right*((i%columns-(columns-1)*.5f)*defaultSpacing)
                +facing*((i/columns-(rows-1)*.5f)*defaultSpacing);
        }
        return MoveSlots(units,slots,facing);
    }
    private static bool Eligible(GameObject unit)
    {
        if(unit==null||!unit.activeInHierarchy||!FactionMember.IsPlayer(unit))return false;
        var worker=unit.GetComponent<ResourcesMove>();var agent=unit.GetComponent<NavMeshAgent>();var health=unit.GetComponent<Unit>();
        return health!=null&&health.enabled&&health.unitHealth>0&&worker!=null&&(worker.role==ResourcesMove.UnitRole.Soldier||unit.GetComponent<Catapult>()!=null)&&unit.GetComponent<GOScript>()!=null
            &&agent!=null&&agent.enabled&&agent.isOnNavMesh;
    }
    public static bool MoveSlots(List<GameObject> units,List<Vector3> targets,Vector3 facing,bool assemble=false)
    {
        if(units.Count<2||units.Count!=targets.Count)return false;
        foreach(var unit in units)if(!Eligible(unit))return false;
        var first=units[0].GetComponent<NavMeshAgent>();int mask=first.areaMask;float gap=.8f;
        Vector3 start=Vector3.zero,finish=Vector3.zero;
        foreach(var unit in units)
        {
            var agent=unit.GetComponent<NavMeshAgent>();if(agent.agentTypeID!=first.agentTypeID)return false;
            mask&=agent.areaMask;gap=Mathf.Max(gap,agent.radius*2+.2f);start+=unit.transform.position;
        }
        var filter=new NavMeshQueryFilter{agentTypeID=first.agentTypeID,areaMask=mask};
        for(int i=0;i<32;i++)filter.SetAreaCost(i,first.GetAreaCost(i));
        var slots=new List<Vector3>(targets);SquadMovementRules.Separate(slots,gap);
        for(int i=0;i<slots.Count;i++)
        {
            if(!NavMesh.SamplePosition(slots[i],out var hit,1.5f,filter))return false;
            slots[i]=hit.position;finish+=hit.position;
            for(int j=0;j<i;j++)if(Vector3.Distance(slots[j],slots[i])<gap*.85f)return false;
            var check=new NavMeshPath();if(!units[i].GetComponent<NavMeshAgent>().CalculatePath(slots[i],check)||check.status!=NavMeshPathStatus.PathComplete)return false;
        }
        start/=units.Count;finish/=units.Count;
        if(!NavMesh.SamplePosition(start,out var startHit,4f,filter)||!NavMesh.SamplePosition(finish,out var finishHit,4f,filter))return false;
        var path=new NavMeshPath();if(!NavMesh.CalculatePath(startHit.position,finishHit.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
        var session=new GameObject("Squad movement").AddComponent<SquadMovement>();
        session.filter=filter;session.finish=finishHit.position;session.finalForward=facing;session.spacing=gap;
        session.columnLength=units.Count*gap;
        session.route.AddRange(path.corners);session.length=SquadMovementRules.Length(session.route);session.formationGroup=FormationHold.BeginGroup();
        float extent=0;
        foreach(var slot in slots)extent=Mathf.Max(extent,Vector3.Distance(slot,finishHit.position));
        session.assembling=SquadMovementRules.UseDirectAssembly(assemble,session.length,extent,gap);
        Vector3 direction=session.route.Count>1?session.route[1]-session.route[0]:facing;direction.y=0;direction.Normalize();
        var side=new Vector3(direction.z,0,-direction.x);
        for(int i=0;i<units.Count;i++)
        {
            var unit=units[i];var command=unit.GetComponent<GOScript>();
            Vector3 departure=unit.transform.position;
            // Обычный ПКМ сразу ведёт вперёд из текущего места, без сбора вокруг центра.
            Vector3 firstGoal=slots[i];
            if(!session.assembling)
            {
                var lead=departure+direction*Mathf.Min(1,session.length);
                var check=new NavMeshPath();
                if(NavMesh.SamplePosition(lead,out var hit,gap,filter)
                    &&unit.GetComponent<NavMeshAgent>().CalculatePath(hit.position,check)&&check.status==NavMeshPathStatus.PathComplete)firstGoal=hit.position;
            }
            command.MoveTo(firstGoal);
            var link=unit.GetComponent<SquadMember>();if(link==null)link=unit.AddComponent<SquadMember>();link.Join(session);
            Vector3 offset=slots[i]-finishHit.position;
            session.width=Mathf.Max(session.width,Mathf.Abs(Vector3.Dot(offset,side)));
            session.members.Add(new Member{link=link,command=command,agent=unit.GetComponent<NavMeshAgent>(),slot=slots[i],departure=departure,
                offset=new Vector3(Vector3.Dot(offset,side),0,Vector3.Dot(offset,direction))});
            FormationHold.SetPoint(unit,slots[i],facing,session.formationGroup);
        }
        // В колонну первым входит тот, кто уже впереди, а не обладатель нового переднего места.
        session.members.Sort((a,b)=>Vector3.Dot(b.departure-a.departure,direction).CompareTo(0));
        for(int i=0;i<session.members.Count;i++)session.members[i].rank=i;
        foreach(var m in session.members)m.probe=m.agent.transform.position;
        session.BeginTransition(session.route[0]);
        session.lastMotion=Time.time;session.nextPath=Time.time+1;session.Status="Движение строем";session.RefreshSpeed();return true;
    }
    public void Remove(SquadMember member){members.RemoveAll(m=>m.link==member);}
    private void RefreshSpeed()
    {
        slowest=float.PositiveInfinity;
        foreach(var member in members)slowest=Mathf.Min(slowest,member.command.NaturalSpeed);
        if(float.IsPositiveInfinity(slowest))slowest=0;
    }
    private bool CorridorWide(Vector3 point,Vector3 forward)
    {
        var side=new Vector3(forward.z,0,-forward.x).normalized*(width+spacing*.5f);
        return !NavMesh.Raycast(point,point+side,out _,filter)&&!NavMesh.Raycast(point,point-side,out _,filter);
    }
    private Vector3 FormationWaypoint(Member member,float distance)
    {
        float offset=IsColumn?-member.rank*spacing:member.offset.z;
        var point=SquadMovementRules.SampleMarch(route,distance+offset,out var tangent);
        return point+new Vector3(tangent.z,0,-tangent.x)*(IsColumn?0:member.offset.x);
    }
    private void BeginTransition(Vector3 center)
    {
        transitionCenter=center;transitionProgress=progress;float maximumShift=0;
        foreach(var member in members)
        {
            member.departure=member.agent.transform.position;
            maximumShift=Mathf.Max(maximumShift,Vector3.Distance(member.departure,FormationWaypoint(member,progress)));
        }
        transitionDistance=SquadMovementRules.TransitionDistance(maximumShift,spacing);
    }
    private void Finish(Member member)
    {
        member.arrived=true;member.link.Leave();member.command.StopMoving();
        FormationHold.SetPoint(member.command.gameObject,member.slot,finalForward,formationGroup);
    }
    private void UpdateAssembly()
    {
        Status="Расстановка на закреплённые места";
        if(slowest<=.01f){lastMotion=Time.time;return;}
        foreach(var member in new List<Member>(members))
        {
            if((member.agent.transform.position-member.probe).sqrMagnitude>.01f)
            {lastMotion=Time.time;member.probe=member.agent.transform.position;}
            if(member.agent.isOnOffMeshLink||member.agent.pathPending)continue;
            if(Vector3.Distance(member.agent.transform.position,member.slot)<.3f){Finish(member);continue;}
            // MoveTo уже выдал путь прямо к месту. Не меняем его на промежуточный центр.
            if(Time.time>=member.nextRepath&&(!member.agent.hasPath||member.agent.pathStatus!=NavMeshPathStatus.PathComplete))
            {
                member.nextRepath=Time.time+1;
                member.command.SetSquadWaypoint(member.slot);
            }
        }
        if(members.Count==0){Destroy(gameObject);return;}
        if(Time.time-lastMotion>12)Abort("Не удаётся занять места построения: проверьте NavMesh, коллайдеры и размеры агентов");
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        for(int i=members.Count-1;i>=0;i--)
        {
            var m=members[i];
            if(m.link==null||!m.link.isActiveAndEnabled||!Eligible(m.link.gameObject)||m.link.GetComponent<Unit>().unitHealth<=0)
            {if(m.link!=null)m.link.Leave();else members.RemoveAt(i);}
        }
        if(members.Count==0){Destroy(gameObject);return;}
        RefreshSpeed();foreach(var m in members)m.agent.speed=Mathf.Min(m.command.NaturalSpeed,slowest);
        if(Time.time<nextTick)return;float dt=Mathf.Min(.25f,Time.time-nextTick+.15f);nextTick=Time.time+.15f;
        if(assembling){UpdateAssembly();return;}
        var center=SquadMovementRules.Sample(route,progress,out var forward);
        if(Time.time>=nextPath&&!members.Exists(m=>m.agent.isOnOffMeshLink))
        {
            nextPath=Time.time+1;
            var path=new NavMeshPath();
            if(!NavMesh.SamplePosition(center,out var anchor,2f,filter)
                ||!NavMesh.CalculatePath(anchor.position,finish,filter,path)||path.status!=NavMeshPathStatus.PathComplete)
            {
                Status="Проход закрыт: ожидание";
                foreach(var m in members)if(m.agent.hasPath)m.agent.ResetPath();
                if(blockedSince<0)blockedSince=Time.time;
                if(Time.time-blockedSince>20)Abort("Проход недоступен более 20 секунд");
                return;
            }
            blockedSince=-1;
            // Перепрокладываем центр маршрута при изменении NavMesh (ворота/разрушение).
            if(NavMesh.Raycast(center,SquadMovementRules.Sample(route,Mathf.Min(length,progress+3),out _),out _,filter))
            {route.Clear();route.AddRange(path.corners);length=SquadMovementRules.Length(route);progress=0;BeginTransition(route[0]);}
        }
        if(blockedSince>=0)return;
        foreach(var m in members)
        {
            if((m.agent.transform.position-m.probe).sqrMagnitude>.01f){lastMotion=Time.time;m.probe=m.agent.transform.position;}
        }
        if(slowest<=.01f){lastMotion=Time.time;return;}
        if(Time.time-lastMotion>12){Abort("Отряд не продвигается: приказ остановлен, проверьте препятствия и размеры агентов");return;}
        var ahead=SquadMovementRules.Sample(route,Mathf.Min(length,progress+3),out var aheadForward);
        bool wide=Time.time-lastMotion<2&&CorridorWide(center,forward)&&CorridorWide(ahead,aheadForward);
        if(!wide)
        {
            if(!IsColumn){IsColumn=true;foreach(var m in members)m.sent=false;BeginTransition(center);}
            wideSince=-1;
        }
        else if(IsColumn)
        {
            if(wideSince<0)wideSince=Time.time;
            if(Time.time-wideSince>1.5f){IsColumn=false;BeginTransition(center);}
        }
        Status=IsColumn?"Проход колонной":"Движение строем";
        bool lagging=false,complete=true;
        foreach(var m in members)
        {
            if(m.arrived)continue;complete=false;
            if(m.sent&&Vector3.Distance(m.agent.transform.position,m.lastWaypoint)>Mathf.Max(4,spacing*3))lagging=true;
        }
        if(complete){Destroy(gameObject);return;}
        if(!lagging)progress=Mathf.Min(length+columnLength,progress+slowest*dt);
        foreach(var m in new List<Member>(members))
        {
            if(m.arrived)continue;
            if(m.agent.isOnOffMeshLink)continue;
            bool final=progress>=length+(IsColumn?m.rank*spacing:0);
            Vector3 desired;
            if(final)desired=m.slot;
            else
            {
                var movingCenter=SquadMovementRules.Sample(route,progress,out _);
                desired=SquadMovementRules.MarchPosition(m.departure,transitionCenter,movingCenter,
                    FormationWaypoint(m,progress),progress-transitionProgress,transitionDistance);
            }
            if(final&&Vector3.Distance(m.agent.transform.position,m.slot)<.3f)
            {
                Finish(m);continue;
            }
            if(!NavMesh.SamplePosition(desired,out var hit,spacing*.6f,filter))continue;
            if(Time.time>=m.nextRepath&&(!m.sent||(m.lastWaypoint-hit.position).sqrMagnitude>.04f||!m.agent.hasPath))
            {
                m.nextRepath=Time.time+.3f+(m.rank%4)*.025f;
                if(m.command.SetSquadWaypoint(hit.position)){m.lastWaypoint=hit.position;m.sent=true;}
            }
        }
    }
    private void Abort(string reason)
    {
        Debug.LogWarning("[Отряд] "+reason,this);
        foreach(var m in new List<Member>(members)){m.link.Leave();m.command.StopMoving();FormationHold.Clear(m.command.gameObject);}
        Destroy(gameObject);
    }
    private void OnDestroy(){foreach(var m in new List<Member>(members))if(m.link!=null)m.link.Leave();}
}
