using UnityEngine;
using UnityEngine.AI;

/// <summary>Reserves physical wall frontage, then displays the completed ladder.</summary>
public class SiegeLadderSite : MonoBehaviour
{
    private const float KnockDownReach = 2f;
    [SerializeField] private Faction owner = Faction.Player;
    private readonly LadderClimbQueue<SiegeLadderClimber> climbers = new LadderClimbQueue<SiegeLadderClimber>();
    public WallSegment Wall { get; private set; }
    public Vector3 Top { get; private set; }
    public bool Installed { get; private set; }
    public bool IsValid => Wall != null && Wall.isActiveAndEnabled && !Wall.GetComponent<BuildingHealth>().IsDead;
    public bool CanUse(GameObject soldier) => FactionRules.AreAllies(owner, FactionMember.Of(soldier));
    public bool TryKnockDown(GameObject defender)
    {
        if (!Installed || !isActiveAndEnabled || !IsValid || defender == null ||
            !WallStation.CanUse(defender) || defender.GetComponent<ResourcesMove>()?.role != ResourcesMove.UnitRole.Soldier)
            return false;

        Faction side = FactionMember.Of(defender);
        if (!FactionRules.AreEnemies(side, owner) || !FactionRules.AreAllies(side, FactionMember.Of(Wall.gameObject)))
            return false;

        var agent = defender.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isOnOffMeshLink ||
            agent.agentTypeID != Wall.navigationAgentType || (agent.areaMask & WallNavigation.AreaMask) == 0)
            return false;

        Vector3 position = defender.transform.position;
        Vector3 local = Wall.transform.InverseTransformPoint(position);
        if (Mathf.Abs(local.x) > Wall.length * .5f || Mathf.Abs(local.z) > Wall.walkwayWidth * .5f ||
            Mathf.Abs(local.y - Wall.height) > .5f || Vector3.Distance(position, Top) > KnockDownReach)
            return false;

        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = WallNavigation.AreaMask };
        if (!NavMesh.SamplePosition(position, out _, .5f, filter)) return false;

        Installed = false;
        AbortClimbers("Лестница сброшена");
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
    public void Join(SiegeLadderClimber soldier) { climbers.Join(soldier); }
    public int QueueIndex(SiegeLadderClimber soldier) => climbers.IndexOf(soldier);
    public void Leave(SiegeLadderClimber soldier) { climbers.Release(soldier); }
    public bool LandingClear(SiegeLadderClimber soldier, Vector3 point)
    {
        var agent = soldier.GetComponent<NavMeshAgent>();
        float radius = Mathf.Max(.2f, agent.radius);
        foreach (var other in FindObjectsOfType<SiegeLadderClimber>())
            if (other != soldier && other.ReservesLanding(point, radius * 2 + .2f)) return false;
        foreach (var collider in Physics.OverlapCapsule(point + Vector3.up * (radius + .15f),
            point + Vector3.up * Mathf.Max(radius + .15f, agent.height - radius), radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (collider.GetComponentInParent<SiegeLadderClimber>() == soldier) continue;
            if (collider.bounds.max.y <= point.y + .15f) continue;
            return false;
        }
        return true;
    }
    public bool TryBeginClimb(SiegeLadderClimber soldier, NavMeshAgent agent, out Vector3 landing)
    {
        landing = default;
        if (!IsValid || !Installed || climbers.IndexOf(soldier) != 0 || agent.agentTypeID != Wall.navigationAgentType) return false;
        var local = Wall.transform.InverseTransformPoint(Top); local.z = 0;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = WallNavigation.AreaMask & agent.areaMask };
        if (!NavMesh.SamplePosition(Wall.transform.TransformPoint(local), out var hit, .5f, filter)
            || Mathf.Abs(hit.position.y - Top.y) > .35f || !LandingClear(soldier, hit.position)) return false;
        landing = hit.position; return climbers.TryBegin(soldier);
    }
    public bool TryDisembark(SiegeLadderClimber soldier, Vector3 landing, out Vector3 destination)
    {
        destination = default;
        var agent = soldier.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = WallNavigation.AreaMask & agent.areaMask };
        float spacing = Mathf.Max(1.2f, agent.radius * 2 + .4f);
        for (int step = 1; step <= 4; step++) foreach (float side in new[] { -1f, 1f })
        {
            Vector3 wanted = landing + Wall.transform.right * (spacing * step * side);
            if (!NavMesh.SamplePosition(wanted, out var hit, .4f, filter) || Mathf.Abs(hit.position.y - landing.y) > .3f
                || Vector3.Distance(hit.position, landing) < spacing * .8f || !LandingClear(soldier, hit.position)) continue;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(landing, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            destination = hit.position; return true;
        }
        return false;
    }
    public static bool TryPoint(WallSegment wall, Vector3 click, Vector3 soldier, NavMeshAgent agent,
        out Vector3 foot, out Vector3 top)
    {
        foot = top = default;
        if (wall == null || !wall.isActiveAndEnabled || wall is WallTower || wall.GetComponent<WallGate>() != null
            || wall.GetComponent<BuildingHealth>().IsDead || wall.length < .9f) return false;
        var local = wall.transform.InverseTransformPoint(click);
        float side = wall.transform.InverseTransformPoint(soldier).z >= 0 ? 1 : -1;
        float x = Mathf.Clamp(local.x, -wall.length * .5f + .4f, wall.length * .5f - .4f);
        top = wall.transform.TransformPoint(new Vector3(x, wall.height, side * wall.thickness * .5f));
        var wanted = wall.transform.TransformPoint(new Vector3(x, 0, side * (Mathf.Max(wall.thickness, wall.walkwayWidth) * .5f + 1.2f)));
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask & ~WallNavigation.AreaMask };
        if (!NavMesh.SamplePosition(wanted, out var hit, .6f, filter) || Mathf.Abs(hit.position.y - wanted.y) > .5f) return false;
        foot = hit.position;
        foreach (var other in FindObjectsOfType<SiegeLadderSite>())
            if (Vector3.Distance(other.transform.position, foot) < 1.2f) return false;
        return Space(wall, foot, agent);
    }
    private static bool Space(WallSegment wall, Vector3 foot, NavMeshAgent agent)
    {
        foreach (var body in Physics.OverlapCapsule(foot + Vector3.up * .4f, foot + Vector3.up * 1.5f,
            .35f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (body.GetComponentInParent<WallSegment>() == wall || body.GetComponentInParent<NavMeshAgent>() == agent) continue;
            // Ground below the feet is permitted; units and solid scenery are not.
            if (body.bounds.max.y <= foot.y + .15f) continue;
            return false;
        }
        return true;
    }
    public bool ClearSpace(NavMeshAgent agent) => IsValid && Space(Wall, transform.position, agent);
    public static SiegeLadderSite Reserve(WallSegment wall, Vector3 foot, Vector3 top, Faction owner = Faction.Player)
    {
        var root = new GameObject("Место осадной лестницы");
        root.transform.SetParent(wall.transform, true); root.transform.position = foot;
        var site = root.AddComponent<SiegeLadderSite>(); site.Wall = wall; site.Top = top; site.owner = owner; return site;
    }
    public void Complete()
    {
        if (Installed) return;
        Installed = true; gameObject.name = "Установленная осадная лестница";
        BuildVisuals();
    }

    internal void RestoreSavedVisuals()
    {
        if (!Installed || !IsValid) return;
        var selector = transform.Find("LadderSelection");
        if (selector != null && selector.GetComponent<BoxCollider>() != null) return;
        // Older saves contain the transforms of the generated meshes but not the meshes themselves.
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
        BuildVisuals();
    }

    private void BuildVisuals()
    {
        Vector3 up = (Top - transform.position).normalized;
        Vector3 across = Wall.transform.right;
        float length = Vector3.Distance(Top, transform.position);
        for (int i = -1; i <= 1; i += 2)
            Beam(transform.position + across * (.35f * i) + up * (length * .5f), up, new Vector3(.1f, length, .1f));
        int steps = Mathf.Max(2, Mathf.FloorToInt(length / .3f));
        for (int i = 1; i < steps; i++)
            Beam(transform.position + up * (length * i / steps), across, new Vector3(.09f, .8f, .09f));
        var selector = new GameObject("LadderSelection"); selector.transform.SetParent(transform, true);
        selector.transform.position = (transform.position + Top) * .5f;
        selector.transform.rotation = Quaternion.FromToRotation(Vector3.up, up);
        var box = selector.AddComponent<BoxCollider>(); box.size = new Vector3(.9f, length, .4f); box.isTrigger = true;
    }
    private void Beam(Vector3 center, Vector3 axis, Vector3 scale)
    {
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.transform.SetParent(transform, true); beam.transform.position = center;
        beam.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis); beam.transform.localScale = scale;
        var collider = beam.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
    }
    private void OnDisable()
    {
        if (!Application.isPlaying || SaveGameManager.IsLoading) return;
        AbortClimbers("Подъём прерван");
    }
    private void AbortClimbers(string status)
    {
        foreach (var soldier in climbers.Snapshot())
        {
            climbers.Release(soldier);
            if (soldier != null) soldier.AbortFromLadder(status);
        }
    }
    public static bool HandleClimbClick(Ray ray, System.Collections.Generic.List<GameObject> selected)
    {
        var hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            var site = hit.collider.GetComponentInParent<SiegeLadderSite>();
            if (site != null && site.Installed)
            {
                if (selected.Exists(soldier => soldier != null && FactionRules.AreEnemies(site.owner, FactionMember.Of(soldier))))
                {
                    foreach (var defender in selected)
                        if (site.TryKnockDown(defender)) return true;
                    Debug.LogWarning("[Лестницы] Для сброса нужен пехотинец на своей стене рядом с верхом лестницы.");
                    return true;
                }
                int assigned = 0;
                foreach (var soldier in selected)
                {
                    if (!SiegeLadderClimber.CanClimb(soldier)) continue;
                    var climber = soldier.GetComponent<SiegeLadderClimber>() ?? soldier.AddComponent<SiegeLadderClimber>();
                    if (climber.TryAssign(site)) assigned++;
                }
                if (assigned == 0) Debug.LogWarning("[Лестницы] Нет доступного подхода. Нужны своя установленная лестница и пехота.");
                return true;
            }
            if (!hit.collider.isTrigger) return false;
        }
        return false;
    }
}
