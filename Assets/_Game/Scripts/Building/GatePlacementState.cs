using UnityEngine;
using UnityEngine.AI;

/// <summary>Отдельный режим: один клик по существующей достроенной секции стены.</summary>
public class GatePlacementState : IBuildingState
{
    private readonly ObjectData data;
    private readonly Grid grid;
    private readonly GridData occupied;
    private readonly ObjectPlacer placer;
    private readonly float buildTime;
    private readonly GameObject sitePrefab, ghost;
    private readonly Material material;
    private string error;
    private WallSegment source;
    private Vector3Int cell;
    private int sourceIndex;
    public GatePlacementState(ObjectData data, Grid grid, GridData occupied, ObjectPlacer placer, float time, GameObject sitePrefab)
    {
        this.data = data; this.grid = grid; this.occupied = occupied; this.placer = placer; buildTime = time; this.sitePrefab = sitePrefab;
        ghost = GameObject.CreatePrimitive(PrimitiveType.Cube); ghost.name = "GatePreview"; ghost.layer = 2;
        ghost.GetComponent<Collider>().enabled = false;
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
        ghost.GetComponent<Renderer>().sharedMaterial = material; ghost.SetActive(false);
    }
    private bool Evaluate(Vector3Int position)
    {
        cell = position; error = ""; source = null;
        sourceIndex = occupied.GetRepresentationIndex(cell);
        var obj = placer.GetPlacedObject(sourceIndex);
        if (obj != null) source = obj.GetComponent<WallSegment>();
        if (source == null || source is WallTower || source is WallGate || !source.gameObject.activeInHierarchy)
        { error = "Выберите достроенную обычную стену. На башне или незавершённой стройке ворота не ставятся."; return false; }
        var health = source.GetComponent<BuildingHealth>();
        if (health == null || health.IsDead||!FactionMember.IsPlayer(source.gameObject)) { error = "Нужна собственная целая стена."; return false; }
        var corner = source.GetComponent<WallCornerConnector>();
        if (corner != null && corner.HasBranches)
        { error = "Ворота устанавливаются на прямой секции, а не на угловом соединении."; return false; }
        var prefab = data.Prefab.GetComponent<WallGate>();
        if (Mathf.Abs(prefab.length - source.length) > 0.01f || Mathf.Abs(prefab.height - source.height) > 0.1f
            || prefab.navigationAgentType != source.navigationAgentType || prefab.transform.localScale != Vector3.one)
        { error = "Размеры и Agent Type ворот должны совпадать со стеной; масштаб корня ворот (1,1,1)."; return false; }
        foreach (var agent in Object.FindObjectsOfType<NavMeshAgent>())
        {
            Vector3 local = source.transform.InverseTransformPoint(agent.transform.position);
            if (Mathf.Abs(local.x) < source.length * 0.5f + agent.radius && Mathf.Abs(local.z) < source.walkwayWidth * 0.5f + agent.radius
                && Mathf.Abs(local.y - source.height) < 1f)
            { error = "Сначала уберите юнитов с перестраиваемой секции стены."; return false; }
        }
        var platform = source.GetComponent<WallWalkway>();
        foreach (var station in Object.FindObjectsOfType<WallStation>())
            if (station.Platform == platform && platform != null)
            { error = "К этой секции назначен защитник. Сначала отдайте ему другой приказ."; return false; }
        if (!BuildCost.CanAfford(data)) { error = "Недостаточно материалов для ворот."; return false; }
        return true;
    }
    public bool OnAction(Vector3Int position)
    {
        if (!Evaluate(position)) { Debug.LogWarning("[Ворота] " + error); return false; }
        var materials = new GameObject("GateMaterials").AddComponent<ConstructionMaterials>();
        if (!materials.Initialize(BuildCost.GetMaterials(data))) { Object.Destroy(materials.gameObject); return false; }
        int index = -1;
        ConstructionSite site = null;
        bool success = false;
        try
        {
            index = placer.PlaceObject(data.Prefab, source.transform.position, source.transform.rotation);
            var gate = placer.GetPlacedObject(index);
            FactionMember.Set(gate,Faction.Player);
            site = ConstructionSite.Begin(gate, buildTime, Vector2Int.one, sitePrefab);
            if (site == null) return false;
            site.Materials = materials; materials.transform.SetParent(site.transform, false);
            var upgrade = site.gameObject.AddComponent<WallGateUpgrade>();
            int oldId = occupied.GetObjectId(cell);
            upgrade.Initialize(source.gameObject, gate, placer, occupied, cell, sourceIndex, index, oldId);
            site.Completed += upgrade.Finish;
            source.gameObject.SetActive(false);
            occupied.RemoveObjectAt(cell); occupied.AddObjectAt(cell, Vector2Int.one, data.ID, index);
            var savedCell = cell; int savedIndex = index;
            gate.GetComponent<BuildingHealth>().Destroyed += _ =>
            { if (occupied.GetRepresentationIndex(savedCell) == savedIndex) occupied.RemoveObjectAt(savedCell); };
            success = true;
            Debug.Log("[Ворота] Начата перестройка стены. Строители доставят материалы."); return true;
        }
        catch (System.Exception exception) { Debug.LogException(exception); return false; }
        finally
        {
            if (!success)
            {
                materials.ReleaseAll(); Object.Destroy(materials.gameObject);
                if (site != null) Object.Destroy(site.gameObject);
                if (index >= 0) placer.RemoveObjectAt(index);
            }
        }
    }
    public void UpdateState(Vector3Int position)
    {
        bool valid = Evaluate(position);
        Vector3 center = source != null ? source.transform.position : grid.CellToWorld(position);
        var part = data.Prefab.GetComponent<WallGate>();
        ghost.SetActive(true);
        ghost.transform.SetPositionAndRotation(center + Vector3.up * part.height * 0.5f, source != null ? source.transform.rotation : Quaternion.identity);
        ghost.transform.localScale = new Vector3(part.length, part.height, part.thickness);
        var color = valid ? Color.green : Color.red; material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
    }
    public void HidePreview() { ghost.SetActive(false); }
    public void EndState() { Object.Destroy(ghost); Object.Destroy(material); }
}
