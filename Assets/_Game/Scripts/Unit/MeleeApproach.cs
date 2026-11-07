using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Места ближнего боя вокруг цели. Остальные бойцы ожидают во внешнем кольце.</summary>
public class MeleeApproach : MonoBehaviour
{
    private static readonly List<MeleeApproach> all=new List<MeleeApproach>();
    private Transform target;
    private NavMeshAgent agent;
    private Vector3 slot;
    private bool reserved,front;
    private float nextPlan,nextPath;
    private float savedStopping;
    private bool ownsStopping;
    public static void Release(GameObject obj){obj.GetComponent<MeleeApproach>()?.Restore();}
    private void Restore()
    {if(ownsStopping&&agent!=null)agent.stoppingDistance=savedStopping;ownsStopping=false;reserved=false;target=null;}
    private void LateUpdate()
    {
        if(SaveGameManager.IsLoading)return;
        var attack=GetComponent<AttackController>();var command=GetComponent<GOScript>();
        if(attack==null||attack.targetToAttack==null||attack.targetToAttack.GetComponentInParent<Unit>()?.transform!=target
            ||UnitEat.ControlsMovement(this)||(command!=null&&command.isCommandToMove))Restore();
    }
    private bool LiveClaim(MeleeApproach other,Transform enemy)
    {
        if(other==this||!other.isActiveAndEnabled||!other.reserved||!other.front||other.target!=enemy||other.agent==null)return false;
        var health=other.GetComponent<Unit>();var ac=other.GetComponent<AttackController>();
        return health!=null&&health.unitHealth>0&&ac!=null&&ac.targetToAttack!=null
            &&ac.targetToAttack.GetComponentInParent<Unit>()?.transform==enemy&&!UnitEat.ControlsMovement(other);
    }
    private bool HasCapacity(Transform enemy,float reach)
    {
        var ac=enemy.GetComponent<AttackController>();var body=enemy.GetComponent<NavMeshAgent>();
        float radius=Mathf.Max(agent.radius+(body!=null?body.radius:.5f)+.1f,reach*.75f);
        int capacity=MeleeCombatRules.Capacity(ac!=null?ac.maxMeleeAttackers:6,radius,agent.radius*2+.15f),claimed=0;
        foreach(var other in all)if(LiveClaim(other,enemy))claimed++;
        return claimed<capacity;
    }
    private static bool ClearContact(Vector3 point,Transform enemy,NavMeshQueryFilter filter)
        =>NavMesh.SamplePosition(enemy.position,out var floor,.5f,filter)&&!NavMesh.Raycast(point,floor.position,out _,filter);
    public static bool CanStrike(GameObject actor,Transform enemy)
    {
        if(enemy.GetComponentInParent<Unit>()==null)return true;
        var agent=actor.GetComponent<NavMeshAgent>();
        if(agent==null||!agent.enabled||!agent.isOnNavMesh)return false;
        return ClearContact(actor.transform.position,enemy,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask});
    }
    public static bool CanTarget(GameObject actor,Transform enemy,bool claim=false)
    {
        if(enemy==null)return false;
        if(actor.GetComponent<Archer>()!=null)return true;
        var body=actor.GetComponent<NavMeshAgent>();var ac=actor.GetComponent<AttackController>();
        if(body==null||!body.enabled||!body.isOnNavMesh||ac==null)return false;
        var unit=enemy.GetComponentInParent<Unit>();
        if(unit==null)return true;
        if(unit.unitHealth<=0||!FactionMember.AreEnemies(actor,unit.gameObject))return false;
        enemy=unit.transform;
        var approach=actor.GetComponent<MeleeApproach>();if(approach==null)approach=actor.AddComponent<MeleeApproach>();
        if(!approach.HasCapacity(enemy,ac.meleeRange))return false;
        if(claim&&approach.target==enemy&&approach.reserved&&approach.front
            &&Vector3.Distance(approach.slot,enemy.position)<=ac.meleeRange&&CanStrike(actor,enemy))return true;
        bool holding=BattleCombatant.Defends(ac)||MilitaryOrders.BlocksChase(ac)||WallStation.Holding(ac);
        var filter=new NavMeshQueryFilter{agentTypeID=body.agentTypeID,areaMask=body.areaMask};
        Vector3 bestPoint=actor.transform.position;float best=float.PositiveInfinity;bool found=false;
        var enemyBody=enemy.GetComponent<NavMeshAgent>();
        float radius=Mathf.Max(body.radius+(enemyBody!=null?enemyBody.radius:.5f)+.1f,ac.meleeRange*.75f);
        float currentDistance=Vector3.Distance(actor.transform.position,enemy.position);
        if(currentDistance>=body.radius+(enemyBody!=null?enemyBody.radius:.5f)+.05f&&currentDistance<=ac.meleeRange
            &&approach.Free(actor.transform.position,body.radius*2+.15f,enemy)&&ClearContact(actor.transform.position,enemy,filter))
        {
            if(claim){approach.target=enemy;approach.slot=actor.transform.position;approach.front=true;approach.reserved=true;}
            return true;
        }
        int count=Mathf.Max(4,Mathf.FloorToInt(2*Mathf.PI*radius/(body.radius*2+.15f)));
        for(int i=0;i<(holding?1:count);i++)
        {
            float angle=2*Mathf.PI*i/count;
            var p=holding?actor.transform.position:enemy.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
            if(holding&&Vector3.Distance(p,enemy.position)>ac.meleeRange)continue;
            if(!NavMesh.SamplePosition(p,out var hit,.3f,filter)||!approach.Free(hit.position,body.radius*2+.15f,enemy))continue;
            if(!ClearContact(hit.position,enemy,filter))continue;
            float score=(actor.transform.position-hit.position).sqrMagnitude;if(score>=best)continue;
            var path=new NavMeshPath();if(!body.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            best=score;bestPoint=hit.position;found=true;
        }
        if(found&&claim){approach.target=enemy;approach.slot=bestPoint;approach.front=true;approach.reserved=true;approach.nextPlan=Time.time+.5f;}
        return found;
    }
    private void Awake(){agent=GetComponent<NavMeshAgent>();}
    private void OnEnable(){if(!all.Contains(this))all.Add(this);}
    private void OnDisable(){all.Remove(this);Restore();}
    public static bool Drive(Component actor,float reach,out bool ready)
    {
        ready=false;
        var attack=actor.GetComponentInParent<AttackController>();if(attack==null||attack.targetToAttack==null)return false;
        var root=attack.gameObject;
        if(root.GetComponent<Archer>()!=null||BattleCombatant.Defends(actor)||MilitaryOrders.BlocksChase(actor)||WallStation.Holding(actor))return false;
        var worker=root.GetComponent<ResourcesMove>();
        if(root.GetComponent<EnemyAi>()==null&&(worker==null||worker.role!=ResourcesMove.UnitRole.Soldier))return false;
        var target=attack.targetToAttack.GetComponentInParent<Unit>();
        if(target==null||target.unitHealth<=0||!FactionMember.AreEnemies(root,target.gameObject))return false;
        var order=root.GetComponent<GOScript>();if(order!=null&&order.isCommandToMove)return false;
        var approach=root.GetComponent<MeleeApproach>();if(approach==null)approach=root.AddComponent<MeleeApproach>();
        if(approach.agent==null||!approach.agent.enabled||!approach.agent.isOnNavMesh)return false;
        return approach.Tick(target.transform,attack.meleeRange,out ready);
    }
    private bool Free(Vector3 point,float spacing,Transform enemy=null)
    {
        foreach(var other in all)
        {
            if(other==this||!other.reserved||other.target!=(enemy!=null?enemy:target)||other.agent==null)continue;
            var attack=other.GetComponent<AttackController>();
            if(attack==null||attack.targetToAttack==null||attack.targetToAttack.GetComponentInParent<Unit>()?.transform!=target)continue;
            var alive=other.GetComponent<Unit>();if(alive==null||alive.unitHealth<=0)continue;
            var delta=other.slot-point;delta.y=0;
            float required=agent.radius+other.agent.radius+.15f;
            if(delta.sqrMagnitude<required*required)return false;
        }
        return true;
    }
    private bool Tick(Transform enemy,float reach,out bool ready)
    {
        ready=false;
        if(!ownsStopping){savedStopping=agent.stoppingDistance;ownsStopping=true;}
        agent.stoppingDistance=Mathf.Min(.1f,savedStopping);
        if(target!=enemy){target=enemy;reserved=false;nextPlan=0;}
        var enemyAgent=enemy.GetComponent<NavMeshAgent>();
        float contact=agent.radius+(enemyAgent!=null?enemyAgent.radius:.5f)+.05f;
        float distanceToEnemy=Vector3.Distance(transform.position,enemy.position);
        bool capacity=HasCapacity(enemy,reach);
        if(capacity&&distanceToEnemy>=contact&&distanceToEnemy<=reach&&Free(transform.position,agent.radius*2+.15f)&&CanStrike(gameObject,enemy))
        {
            // Уже подошедшие противники дерутся на месте, вместо взаимного обхода по кругу.
            reserved=true;front=true;slot=transform.position;ready=true;
            if(agent.hasPath)agent.ResetPath();return true;
        }
        if(Time.time>=nextPlan)
        {
            nextPlan=Time.time+.5f;bool hadSlot=reserved;reserved=false;
            var body=enemy.GetComponent<NavMeshAgent>();
            float radius=Mathf.Max(agent.radius+(body!=null?body.radius:.5f)+.1f,reach*.75f);
            float spacing=agent.radius*2+.15f;
            int count=Mathf.Max(4,Mathf.FloorToInt(2*Mathf.PI*radius/spacing));
            float best=float.PositiveInfinity;Vector3 bestPoint=transform.position;bool found=false,bestFront=false;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            // Сначала заполняется ближнее кольцо; очередь не проталкивается к центру цели.
            for(int ring=capacity?0:1;ring<3&&!found;ring++)for(int i=0;i<count;i++)
            {
                float angle=2*Mathf.PI*i/count;
                var point=enemy.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(radius+ring*spacing);
                if(!NavMesh.SamplePosition(point,out var hit,.3f,filter)||!Free(hit.position,spacing))continue;
                if(ring==0&&!ClearContact(hit.position,enemy,filter))continue;
                float distance=(transform.position-hit.position).sqrMagnitude;
                // Не переставляем уже подошедшего бойца на противоположную сторону цели.
                if(hadSlot)distance+=(hit.position-slot).sqrMagnitude;
                if(distance>=best)continue;
                var path=new NavMeshPath();if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                best=distance;bestPoint=hit.position;found=true;bestFront=ring==0;
            }
            reserved=found;slot=bestPoint;front=bestFront;
        }
        if(!reserved){if(agent.hasPath)agent.ResetPath();return true;}
        float remaining=Vector3.Distance(transform.position,slot);
        ready=front&&capacity&&remaining<.45f&&Vector3.Distance(transform.position,target.position)<=reach&&CanStrike(gameObject,enemy);
        if(remaining<.25f||ready){if(agent.hasPath)agent.ResetPath();return true;}
        if(Time.time>=nextPath)
        {nextPath=Time.time+.35f;agent.isStopped=false;agent.SetDestination(slot);}
        return true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry(){all.Clear();}
}
