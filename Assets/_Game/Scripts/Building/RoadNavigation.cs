using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>Обновляет наземный NavMesh с дорожным покрытием. Настилы стен не затрагиваются.</summary>
public class RoadNavigation : MonoBehaviour
{
    public static RoadNavigation Instance { get; private set; }
    private static bool quitting;
    private static int roadArea=-1;
    public static int RoadArea
    { get {if(roadArea<0)roadArea=NavMesh.GetAreaFromName("Roads");return roadArea;} }
    [Tooltip("Необязательно. Без ссылки автоматически выбирается наземная поверхность для каждого Agent Type.")]
    public NavMeshSurface groundSurface;
    public LayerMask groundLayers=1<<6;
    [Min(.05f)] public float rebuildDelay=.35f;
    [Min(1f)] public float roadCost=1f;
    [Min(1f)] public float groundCost=3f;
    public bool IsUpdating=>queue.IsBuilding||queue.IsDirty;
    public string Status { get; private set; }="Готово";

    private sealed class SurfaceState
    {
        public NavMeshSurface surface;
        public NavMeshData original,runtime;
        public CollectObjects collect;
        public LayerMask layers;
        public NavMeshCollectGeometry geometry;
        public bool enabled;
        public bool primary;
    }
    private readonly List<SurfaceState> states=new List<SurfaceState>();
    private readonly List<SurfaceState> jobs=new List<SurfaceState>();
    private readonly RoadNavigationQueue queue=new RoadNavigationQueue();
    private AsyncOperation operation;
    private int jobIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics(){Instance=null;quitting=false;roadArea=-1;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfMissing()
    {if(Instance==null&&FindObjectOfType<RoadNavigation>()==null)new GameObject("RoadNavigation").AddComponent<RoadNavigation>();}
    private void Awake()
    {
        if(Instance!=null&&Instance!=this){enabled=false;return;}
        Instance=this;ApplyGlobalCosts();
    }
    private void ApplyGlobalCosts()
    {
        int ground=NavMesh.GetAreaFromName("Walkable");
        if(RoadArea>=0)NavMesh.SetAreaCost(RoadArea,Mathf.Max(1f,roadCost));
        if(ground>=0)NavMesh.SetAreaCost(ground,Mathf.Max(1f,groundCost));
    }
    public static void Changed()
    {
        if(!Application.isPlaying||quitting)return;
        if(Instance==null)new GameObject("RoadNavigation").AddComponent<RoadNavigation>();
        Instance.queue.Changed(Time.unscaledTime,Instance.rebuildDelay);
    }
    public static void ConfigureAgent(NavMeshAgent agent)
    {
        if(agent==null||RoadArea<0)return;
        int ground=NavMesh.GetAreaFromName("Walkable");
        // Водные и другие специальные агенты без Walkable не получают наземные дороги.
        if(ground<0||(agent.areaMask&(1<<ground))==0)return;
        agent.areaMask|=1<<RoadArea;
        agent.SetAreaCost(RoadArea,Instance!=null?Mathf.Max(1f,Instance.roadCost):1f);
        agent.SetAreaCost(ground,Instance!=null?Mathf.Max(1f,Instance.groundCost):3f);
    }
    private void Update()
    {
        if(operation!=null)
        {
            if(!operation.isDone)return;
            operation=null;
        }
        if(queue.IsBuilding)
        {
            StartNextJob();return;
        }
        if(!queue.TryBegin(Time.unscaledTime))return;
        if(RoadArea<0||LayerMask.NameToLayer("RoadSurface")<0)
        {Status="Нужны область Roads и слой RoadSurface";Debug.LogWarning("[RoadNavigation] "+Status,this);queue.Complete();return;}
        ApplyGlobalCosts();PrepareSurfaces();jobs.Clear();
        foreach(var state in states)if(state.primary&&state.surface!=null&&state.surface.isActiveAndEnabled)jobs.Add(state);
        jobIndex=0;
        if(jobs.Count==0){Status="Не найден наземный NavMeshSurface";Debug.LogWarning("[RoadNavigation] "+Status,this);queue.Complete();return;}
        Status="Обновление дорожной навигации";StartNextJob();
    }
    private void StartNextJob()
    {
        while(jobIndex<jobs.Count)
        {
            var state=jobs[jobIndex++];var surface=state.surface;
            if(surface==null||!surface.isActiveAndEnabled)continue;
            if(state.runtime==null)
            {
                if(surface.navMeshData!=null)
                {
                    // Не изменяем исходный запечённый asset в Editor/проекте.
                    state.runtime=Instantiate(surface.navMeshData);state.runtime.name=surface.name+" (roads runtime)";
                    surface.RemoveData();surface.navMeshData=state.runtime;surface.AddData();
                }
                else
                {
                    // Однократная инициализация, если в сцене ещё нет запечённых данных.
                    surface.BuildNavMesh();state.runtime=surface.navMeshData;
                    continue;
                }
            }
            operation=surface.UpdateNavMesh(state.runtime);
            if(operation!=null&&!operation.isDone)return;
            operation=null;
        }
        queue.Complete();Status="Дорожная навигация обновлена";
        RefreshAgents();
    }
    private bool IsGroundSurface(NavMeshSurface surface)
    {
        return surface!=null&&surface.isActiveAndEnabled&&surface.defaultArea==NavMesh.GetAreaFromName("Walkable")
            &&((surface.layerMask.value&groundLayers.value)!=0);
    }
    private static int Score(NavMeshSurface surface)
    {
        int score=surface.navMeshData!=null?1000:0;
        if(surface.collectObjects==CollectObjects.All)score+=100;
        if(surface.GetComponent<Terrain>()!=null)score+=50;
        return score;
    }
    private bool Captured(NavMeshSurface surface)
    {foreach(var state in states)if(state.surface==surface)return true;return false;}
    private void PrepareSurfaces()
    {
        var best=new Dictionary<int,NavMeshSurface>();
        foreach(var state in states)if(state.primary&&state.surface!=null)best[state.surface.agentTypeID]=state.surface;
        foreach(var surface in FindObjectsOfType<NavMeshSurface>())
        {
            if(!IsGroundSurface(surface)||Captured(surface))continue;
            if(best.TryGetValue(surface.agentTypeID,out var existing)&&Captured(existing))continue;
            if(!best.TryGetValue(surface.agentTypeID,out var candidate)||Score(surface)>Score(candidate))best[surface.agentTypeID]=surface;
        }
        if(IsGroundSurface(groundSurface)&&(!best.TryGetValue(groundSurface.agentTypeID,out var chosen)||!Captured(chosen)))
            best[groundSurface.agentTypeID]=groundSurface;
        foreach(var surface in FindObjectsOfType<NavMeshSurface>())
        {
            if(!IsGroundSurface(surface)||Captured(surface))continue;
            var state=new SurfaceState{surface=surface,original=surface.navMeshData,collect=surface.collectObjects,
                layers=surface.layerMask,geometry=surface.useGeometry,enabled=surface.enabled,primary=best[surface.agentTypeID]==surface};
            states.Add(state);
            if(state.primary)
            {
                surface.collectObjects=CollectObjects.All;
                surface.layerMask=groundLayers.value|(1<<LayerMask.NameToLayer("RoadSurface"));
                surface.useGeometry=NavMeshCollectGeometry.RenderMeshes;
            }
            else
            {
                // Перекрывающиеся старые наземные данные не должны обходить стоимость Roads.
                // Одна поверхность каждого Agent Type собирает всю землю. Водные не трогаем.
                surface.enabled=false;
            }
        }
    }
    private void RefreshAgents()
    {
        foreach(var agent in FindObjectsOfType<NavMeshAgent>())
        {
            ConfigureAgent(agent);
            if(!agent.enabled||!agent.isOnNavMesh||agent.isOnOffMeshLink||!agent.hasPath)continue;
            Vector3 goal=agent.destination;
            if(float.IsNaN(goal.x)||float.IsInfinity(goal.x)||float.IsNaN(goal.z)||float.IsInfinity(goal.z))continue;
            // Сохраняем сам приказ и stoppingDistance, меняется только рассчитанный маршрут.
            agent.SetDestination(goal);
        }
    }
    private void OnApplicationQuit(){quitting=true;}
    private void OnDisable(){ReleaseSurfaces();}
    private void ReleaseSurfaces()
    {
        foreach(var state in states)
        {
            if(state.runtime!=null)NavMeshBuilder.Cancel(state.runtime);
            if(state.surface!=null)
            {
                if(state.primary)
                {
                    state.surface.RemoveData();state.surface.navMeshData=state.original;
                    state.surface.collectObjects=state.collect;state.surface.layerMask=state.layers;state.surface.useGeometry=state.geometry;
                    if(state.surface.isActiveAndEnabled)state.surface.AddData();
                }
                else state.surface.enabled=state.enabled;
            }
            if(state.runtime!=null&&state.runtime!=state.original)Destroy(state.runtime);
        }
        states.Clear();jobs.Clear();operation=null;queue.Reset();
    }
    private void OnDestroy(){ReleaseSurfaces();if(Instance==this)Instance=null;}
}
