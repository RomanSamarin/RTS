using UnityEngine;
using UnityEngine.AI;

/// <summary>Стоящий боец не уступает место из-за симуляции avoidance. Натиск освобождает фиксацию.</summary>
[DefaultExecutionOrder(300)]
public class FormationAnchor : MonoBehaviour
{
    private NavMeshAgent agent;
    private Rigidbody body;
    private bool locked,savedPosition,savedStopped,savedKinematic;
    private int savedPriority;
    private ObstacleAvoidanceType savedAvoidance;
    private void Awake(){agent=GetComponent<NavMeshAgent>();body=GetComponent<Rigidbody>();}
    public static void Ensure(GameObject obj){if(obj.GetComponent<FormationAnchor>()==null)obj.AddComponent<FormationAnchor>();}
    public static void Release(GameObject obj){obj.GetComponent<FormationAnchor>()?.Unlock();}
    private void LateUpdate()
    {
        if(SaveGameManager.IsLoading)return;
        if(agent==null||!agent.enabled||!agent.isOnNavMesh){Unlock();return;}
        var command=GetComponent<GOScript>();var battle=GetComponent<BattleCombatant>();
        bool hold=FormationHold.TryGetPoint(gameObject,out var point)&&Vector3.Distance(transform.position,point)<.3f
            &&(command==null||!command.HasPlayerOrder)&&!UnitEat.ControlsMovement(this)
            &&(battle==null||(!battle.IsStunned&&!battle.IsDisordered))&&GetComponent<Unit>()?.unitHealth>0;
        if(!hold){Unlock();return;}
        if(!locked)
        {
            savedPosition=agent.updatePosition;savedStopped=agent.isStopped;savedPriority=agent.avoidancePriority;
            savedAvoidance=agent.obstacleAvoidanceType;
            if(body!=null){savedKinematic=body.isKinematic;body.isKinematic=true;}
            locked=true;agent.updatePosition=false;agent.avoidancePriority=0;
            agent.obstacleAvoidanceType=ObstacleAvoidanceType.NoObstacleAvoidance;
        }
        agent.isStopped=true;if(agent.hasPath)agent.ResetPath();
        agent.nextPosition=transform.position;
    }
    private void Unlock()
    {
        if(!locked)return;locked=false;
        if(agent!=null)
        {
            if(agent.enabled&&agent.isOnNavMesh){agent.nextPosition=transform.position;agent.isStopped=savedStopped;}
            agent.updatePosition=savedPosition;agent.avoidancePriority=savedPriority;agent.obstacleAvoidanceType=savedAvoidance;
        }
        if(body!=null)body.isKinematic=savedKinematic;
    }
    private void OnDisable(){Unlock();}
}
