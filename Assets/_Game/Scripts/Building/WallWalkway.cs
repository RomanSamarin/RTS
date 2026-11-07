using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Существует только у готовой стены. Пол настила и позиции защитников.</summary>
[DisallowMultipleComponent]
public class WallWalkway : MonoBehaviour
{
    public static readonly List<WallWalkway> All = new List<WallWalkway>();
    public WallSegment Segment { get; private set; }
    public Vector3 Top => transform.TransformPoint(Vector3.up * Segment.height);
    private GameObject deck;
    private readonly Dictionary<WallStation, Vector3> occupants = new Dictionary<WallStation, Vector3>();
    private void OnEnable() { Refresh(); }
    public void Refresh()
    {
        Segment = GetComponent<WallSegment>();
        if (Segment == null || !isActiveAndEnabled) return;
        if (!All.Contains(this)) All.Add(this);
        if (deck == null)
        {
            deck = new GameObject("WalkwayFloor");
            deck.transform.SetParent(transform, false); deck.layer = gameObject.layer;
            var box = deck.AddComponent<BoxCollider>();
            box.center = Vector3.up * (Segment.height - 0.05f);
            box.size = new Vector3(Segment.length, 0.1f, Segment.walkwayWidth);
        }
        WallNavigation.Changed();
    }
    private void OnDisable()
    {
        // Пока link ещё существует, снимаем с него юнитов внутри уничтожаемой башни.
        if (Segment is WallTower tower && Application.isPlaying)
        {
            Vector3 exit = tower.stairExit != null ? tower.stairExit.position : Top;
            foreach (var agent in FindObjectsOfType<NavMeshAgent>())
            {
                if (!agent.enabled || !agent.isOnNavMesh || !agent.isOnOffMeshLink) continue;
                var link = agent.currentOffMeshLinkData;
                if ((link.startPos - exit).sqrMagnitude < 0.25f || (link.endPos - exit).sqrMagnitude < 0.25f)
                    WallStation.FallFrom(this, agent.GetComponent<Unit>());
            }
        }
        All.Remove(this); WallNavigation.RemoveTower(this); WallNavigation.Changed();
        var units = new List<WallStation>(occupants.Keys);
        foreach (var station in units) if (station != null) station.PlatformLost(this);
        occupants.Clear();
        if (Segment == null || !Application.isPlaying) return;
        // Опора может исчезнуть и под юнитом, который идёт к позиции на другой секции.
        foreach (var agent in FindObjectsOfType<NavMeshAgent>())
        {
            Vector3 local = transform.InverseTransformPoint(agent.transform.position);
            bool onDeck = Mathf.Abs(local.x) <= Segment.length * 0.5f && Mathf.Abs(local.z) <= Segment.walkwayWidth * 0.5f;
            var corner = GetComponent<WallCornerConnector>();
            if ((!onDeck && (corner == null || !corner.Contains(local))) || Mathf.Abs(local.y - Segment.height) > 0.8f) continue;
            var unit = agent.GetComponent<Unit>();
            if (unit != null && unit.unitHealth > 0) WallStation.FallFrom(this, unit);
        }
    }
    public void Release(WallStation station) { occupants.Remove(station); }
    public bool TryPosition(WallStation station, NavMeshAgent agent, Vector3 clicked, out Vector3 point)
    {
        point = default;
        var stale = new List<WallStation>();
        foreach (var item in occupants) if (item.Key == null) stale.Add(item.Key);
        foreach (var item in stale) occupants.Remove(item);
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = WallNavigation.AreaMask };
        float spacing = Mathf.Max(0.8f, agent.radius * 2f + 0.1f);
        int nx = Mathf.Max(1, Mathf.FloorToInt(Segment.length / spacing));
        int nz = Mathf.Max(1, Mathf.FloorToInt(Segment.walkwayWidth / spacing));
        float best = float.PositiveInfinity;
        for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++)
        {
            Vector3 candidate = transform.TransformPoint(new Vector3((x - (nx - 1) * 0.5f) * spacing,
                Segment.height, (z - (nz - 1) * 0.5f) * spacing));
            if (!NavMesh.SamplePosition(candidate, out var hit, 0.35f, filter) || Mathf.Abs(hit.position.y - Top.y) > 0.3f) continue;
            bool occupied = false;
            foreach (var slot in occupants)
                if (slot.Key != station && (slot.Value - hit.position).sqrMagnitude < spacing * spacing * 0.8f) { occupied = true; break; }
            if (occupied) continue;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float score = (hit.position - clicked).sqrMagnitude;
            if (score < best) { best = score; point = hit.position; }
        }
        if (float.IsPositiveInfinity(best)) return false;
        occupants[station] = point; return true;
    }
    public static bool HandleClick(Ray ray, List<GameObject> selected)
    {
        if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore)) return false;
        var platform = hit.collider.GetComponentInParent<WallWalkway>();
        if (platform == null) return false;
        int assigned = 0;
        foreach (var unit in selected)
        {
            if (unit == null || !WallStation.CanUse(unit)) continue;
            var station = unit.GetComponent<WallStation>();
            if (station == null) station = unit.AddComponent<WallStation>();
            if (station.Assign(platform, hit.point)) assigned++;
        }
        if (assigned == 0) Debug.LogWarning("[Стены] Нет свободных доступных позиций. Достройте башню и соединённые секции; проверьте Agent Type.");
        return true;
    }
}
