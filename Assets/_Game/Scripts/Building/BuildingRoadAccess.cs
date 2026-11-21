using UnityEngine;
using UnityEngine.AI;

/// <summary>Подъезд к входу, без требования соединения с ратушей или складом.</summary>
[DisallowMultipleComponent]
public class BuildingRoadAccess : MonoBehaviour
{
    public bool requiresRoad = true;
    [Tooltip("Точная точка подключения. Если пусто, ищется дочерний SnapPoint, затем BuildingEntrance.")]
    public Transform connectionPoint;
    [Min(0.05f)] public float connectionDistance = 1.25f;
    [Min(0.1f)] public float heightTolerance = 2f;
    [SerializeField] private bool connected;
    private int checkedRevision=-1;
    private float nextCheck;
    public bool IsConnected { get { Refresh();return connected; } }
    public string Status => IsConnected ? "Есть подъезд" : "Нет дороги к зданию";
    public Transform SnapPoint
    {
        get
        {
            if(connectionPoint!=null)return connectionPoint;
            foreach(var child in GetComponentsInChildren<Transform>(true))
                if(child!=transform&&child.name=="SnapPoint")return child;
            return null;
        }
    }
    private void Refresh()
    {
        if(!requiresRoad||GetComponent<WallSegment>()!=null||GetComponent<BanditCamp>()!=null){connected=true;return;}
        if(checkedRevision==RoadBuilder.Revision && Time.unscaledTime<nextCheck)return;
        checkedRevision=RoadBuilder.Revision;nextCheck=Time.unscaledTime+.5f;
        var roads=RoadBuilder.Instance;
        connected=false;
        if(roads==null||!roads.isActiveAndEnabled)return;
        if(TryConnectionPoint(out var point,out _))connected=roads.HasConnection(point,connectionDistance,heightTolerance);
    }
    public bool TryConnectionPoint(out Vector3 point,out Vector3 outward)
    {
        point=transform.position;outward=transform.forward;outward.y=0;
        if(GetComponent<WallSegment>()!=null||GetComponent<BanditCamp>()!=null)return false;
        var body=SolidBody(gameObject);
        var snap=SnapPoint;
        if(snap!=null)
        {
            point=snap.position;outward=snap.forward;outward.y=0;
            if(outward.sqrMagnitude<.001f)outward=transform.forward;
            outward.Normalize();
            if(RoadBuilder.Instance!=null&&RoadBuilder.Instance.SampleGround(point,out float ground))point.y=ground;
            return true;
        }
        bool explicitPoint=connectionPoint!=null;
        if(explicitPoint)point=connectionPoint.position;
        else explicitPoint=BuildingEntrance.TryGetWorldPoint(gameObject,out point);
        if(!explicitPoint)point=transform.position;
        if(body!=null)
        {
            Bounds bounds=body.bounds;
            Vector3 direction=explicitPoint?point-bounds.center:transform.forward;direction.y=0;
            if(direction.sqrMagnitude<.001f)direction=transform.forward;
            // Нормаль ближайшей стороны здания, а не диагональ к pivot.
            if(Mathf.Abs(direction.x)/Mathf.Max(.01f,bounds.extents.x)>Mathf.Abs(direction.z)/Mathf.Max(.01f,bounds.extents.z))outward=direction.x>=0?Vector3.right:Vector3.left;
            else outward=direction.z>=0?Vector3.forward:Vector3.back;
            if(!explicitPoint)
            {
                point=bounds.ClosestPoint(bounds.center+outward*(bounds.extents.magnitude+10f));
            }
            // Дверь может находиться внутри коллайдера крыльца: выводим торец дороги к границе.
            float support=Mathf.Abs(outward.x)*bounds.extents.x+Mathf.Abs(outward.z)*bounds.extents.z;
            float projection=Vector3.Dot(point-bounds.center,outward);
            if(projection<support+.08f)point+=outward*(support+.08f-projection);
        }
        if(outward.sqrMagnitude<.001f)outward=Vector3.forward;
        outward.Normalize();
        if(RoadBuilder.Instance!=null&&RoadBuilder.Instance.SampleGround(point,out float y))point.y=y;
        return true;
    }
    public static Collider SolidBody(GameObject building)
    {
        var root=building.GetComponent<Collider>();
        if(root!=null&&root.enabled&&!root.isTrigger)return root;
        foreach(var body in building.GetComponentsInChildren<Collider>())
            if(body.enabled&&!body.isTrigger)return body;
        return null;
    }
    internal static bool TryWorkplacePath(GameObject workplace, NavMeshAgent agent, out NavMeshPath path, out Vector3 destination)
    {
        path = null;
        destination = default;
        if (workplace == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        var access = workplace.GetComponent<BuildingRoadAccess>();
        Vector3 point = workplace.transform.position;
        Vector3 outward = workplace.transform.forward;
        if (access == null || !access.TryConnectionPoint(out point, out outward))
        {
            point = workplace.transform.position;
            if (BuildingEntrance.TryGetWorldPoint(workplace, out var entrance)) point = entrance;
        }
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        var candidate = new NavMeshPath();
        for (int i = 0; i < 3; i++)
        {
            Vector3 probe = point + outward * (i * .75f);
            if (!NavMesh.SamplePosition(probe, out var hit, 1.25f, filter) ||
                !agent.CalculatePath(hit.position, candidate) || candidate.status != NavMeshPathStatus.PathComplete) continue;
            path = candidate;
            destination = hit.position;
            return true;
        }
        return false;
    }
    public static void Ensure(GameObject building)
    {
        if(building==null||building.transform is RectTransform||building.GetComponentInParent<Canvas>(true)!=null||
            building.GetComponent<BuildingRoadAccess>()!=null||building.GetComponent<WallSegment>()!=null||
            building.GetComponent<BanditCamp>()!=null||building.GetComponent<ConstructionSite>()!=null||!FactionMember.HasPlayerOwner(building))return;
        building.AddComponent<BuildingRoadAccess>();
    }
    public static bool Allows(Component building) => building==null||Allows(building.gameObject);
    public static bool Allows(GameObject building)
    {
        if(building==null)return true;
        var access=building.GetComponent<BuildingRoadAccess>();
        return access==null||!access.enabled||access.IsConnected;
    }
    public static bool PauseWorker(ResourcesMove worker)
    {
        if(worker==null||worker.AssignedWorkplace==null||Allows(worker.AssignedWorkplace))return false;
        var command=worker.GetComponent<GOScript>();
        if(UnitEat.ControlsMovement(worker)||(command!=null&&command.HasPlayerOrder))return false;
        var attack=worker.GetComponent<AttackController>();
        if(attack!=null)attack.targetToAttack=null;
        var agent=worker.GetComponent<NavMeshAgent>();
        if(agent!=null&&agent.enabled&&agent.isOnNavMesh)agent.ResetPath();
        var animator=worker.GetComponentInChildren<Animator>();
        if(animator!=null&&animator.runtimeAnimatorController!=null)
            foreach(var parameter in animator.parameters)
                if(parameter.type==AnimatorControllerParameterType.Bool&&(parameter.name=="isAttacking"||parameter.name=="IsFollowing"))animator.SetBool(parameter.nameHash,false);
        return true;
    }
    private void OnDrawGizmosSelected()
    {
        TryConnectionPoint(out var point,out var outward);
        Gizmos.color=connected?Color.green:Color.red;Gizmos.DrawWireSphere(point,connectionDistance);
        Gizmos.DrawLine(point,point+outward*2f);
    }
}
