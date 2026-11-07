using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Отдельный NavMesh настилов. С землёй соединяется только лестницами башен.</summary>
public class WallNavigation : MonoBehaviour
{
    public const int Area = 30;
    public const int AreaMask = 1 << Area;
    private static WallNavigation instance;
    private static bool quitting;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; quitting = false; WallWalkway.All.Clear(); }
    private bool dirty;
    private float nextBuild;
    private float nextLinkCheck;
    private class Surface
    {
        public NavMeshData data;
        public NavMeshDataInstance handle;
        public AsyncOperation operation;
    }
    private readonly Dictionary<int, Surface> surfaces = new Dictionary<int, Surface>();
    private readonly Dictionary<WallWalkway, NavMeshLinkInstance> stairs = new Dictionary<WallWalkway, NavMeshLinkInstance>();
    public static void Changed()
    {
        if (!Application.isPlaying || quitting) return;
        if (instance == null)
        {
            instance = new GameObject("WallNavigation").AddComponent<WallNavigation>();
            DontDestroyOnLoad(instance.gameObject);
        }
        instance.dirty = true; instance.nextBuild = Time.time + 0.15f;
    }
    public static void RemoveTower(WallWalkway platform)
    {
        if (instance == null || !instance.stairs.TryGetValue(platform, out var link)) return;
        if (link.valid) link.Remove(); instance.stairs.Remove(platform);
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        bool busy = false;
        foreach (var surface in surfaces.Values)
        {
            if (surface.operation == null) continue;
            if (!surface.operation.isDone) busy = true;
            else surface.operation = null;
        }
        if (busy) return;
        if (dirty && Time.time >= nextBuild) { Rebuild(); return; }
        if (Time.time < nextLinkCheck) return;
        nextLinkCheck = Time.time + 0.5f;
        foreach (var platform in WallWalkway.All)
            if (platform != null && platform.Segment is WallTower && !stairs.ContainsKey(platform)) AddStairs(platform);
    }
    private void Rebuild()
    {
        dirty = false;
        foreach (var platform in WallWalkway.All)
        {
            if (platform == null) continue;
            var corner = platform.GetComponent<WallCornerConnector>();
            if (corner == null) corner = platform.gameObject.AddComponent<WallCornerConnector>();
            corner.Refresh(platform);
        }
        var types = new HashSet<int>(surfaces.Keys);
        foreach (var part in WallWalkway.All) if (part != null) types.Add(part.Segment.navigationAgentType);
        foreach (int type in types)
        {
            var settings = NavMesh.GetSettingsByID(type);
            if (settings.agentTypeID < 0) { Debug.LogWarning("[Стены] Не существует NavMesh Agent Type " + type); continue; }
            // Узкие секции на метровой сетке должны соединяться в сплошной боевой ход.
            settings.agentRadius = Mathf.Min(settings.agentRadius, 0.2f);
            settings.overrideVoxelSize = true; settings.voxelSize = 0.08f;
            settings.minRegionArea = 0;
            var sources = new List<NavMeshBuildSource>();
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            bool first = true;
            foreach (var platform in WallWalkway.All)
            {
                if (platform == null || platform.Segment.navigationAgentType != type) continue;
                var part = platform.Segment;
                Vector3 center = platform.Top - Vector3.up * 0.1f;
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(center, platform.transform.rotation, Vector3.one),
                    size = new Vector3(part.length, 0.2f, part.walkwayWidth), area = Area });
                platform.GetComponent<WallCornerConnector>()?.AddNavigationSources(sources);
                if (first) { bounds = new Bounds(center, Vector3.zero); first = false; }
                bounds.Encapsulate(center + Vector3.one * Mathf.Max(part.length, part.walkwayWidth));
                bounds.Encapsulate(center - Vector3.one * Mathf.Max(part.length, part.walkwayWidth));
            }
            bounds.Expand(4f);
            if (!surfaces.TryGetValue(type, out var surface))
            {
                surface = new Surface { data = new NavMeshData(type) }; surfaces.Add(type, surface);
                surface.handle = NavMesh.AddNavMeshData(surface.data);
            }
            surface.operation = NavMeshBuilder.UpdateNavMeshDataAsync(surface.data, settings, sources, bounds);
        }
    }
    private void AddStairs(WallWalkway platform)
    {
        var tower = (WallTower)platform.Segment;
        var topFilter = new NavMeshQueryFilter { agentTypeID = tower.navigationAgentType, areaMask = AreaMask };
        Vector3 exit = tower.stairExit != null ? tower.stairExit.position : platform.Top;
        if (!NavMesh.SamplePosition(exit, out var upper, 0.4f, topFilter)) return;
        var groundFilter = new NavMeshQueryFilter { agentTypeID = tower.navigationAgentType, areaMask = ~AreaMask };
        Vector3[] candidates = tower.stairEntrance != null ? new[] { tower.stairEntrance.position } : new[] {
            platform.transform.TransformPoint(new Vector3(0, 0, -Mathf.Max(tower.thickness, tower.walkwayWidth) * 0.5f - 1f)),
            platform.transform.TransformPoint(new Vector3(0, 0, Mathf.Max(tower.thickness, tower.walkwayWidth) * 0.5f + 1f)) };
        foreach (var entry in candidates)
        {
            if (!NavMesh.SamplePosition(entry, out var lower, 0.75f, groundFilter)
                || Mathf.Abs(lower.position.y - entry.y) > 0.75f) continue;
            var link = NavMesh.AddLink(new NavMeshLinkData { startPosition = lower.position,
                endPosition = upper.position, bidirectional = true, agentTypeID = tower.navigationAgentType,
                width = 0, costModifier = 2f, area = Area });
            if (link.valid) { stairs[platform] = link; return; }
        }
    }
    private void OnDestroy()
    {
        foreach (var link in stairs.Values) if (link.valid) link.Remove();
        foreach (var surface in surfaces.Values)
        { NavMeshBuilder.Cancel(surface.data); if (surface.handle.valid) surface.handle.Remove(); Destroy(surface.data); }
        if (instance == this) instance = null;
    }
    private void OnApplicationQuit() { quitting = true; }
}
