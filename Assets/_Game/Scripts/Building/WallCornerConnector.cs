using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Закрывает внутреннюю половину клетки, когда следующая стена поворачивает на 90°.</summary>
[DisallowMultipleComponent]
public class WallCornerConnector : MonoBehaviour
{
    private WallWalkway platform;
    private bool positive, negative;
    public bool HasBranches => positive || negative;
    private readonly List<GameObject> pieces = new List<GameObject>();
    public void Refresh(WallWalkway owner)
    {
        platform = owner;
        bool plus = false, minus = false;
        var part = owner.Segment;
        foreach (var neighbor in WallWalkway.All)
        {
            if (neighbor == null || neighbor == owner || neighbor.Segment.navigationAgentType != part.navigationAgentType) continue;
            Vector3 offset = transform.InverseTransformPoint(neighbor.transform.position);
            if (Mathf.Abs(offset.y) > part.maxGroundStep + 0.01f || Mathf.Abs(offset.x) > 0.1f
                || Mathf.Abs(Mathf.Abs(offset.z) - part.length) > 0.1f
                || Mathf.Abs(neighbor.Segment.height - part.height) > 0.1f
                || Mathf.Abs(Vector3.Dot(neighbor.transform.right, transform.forward)) < 0.95f) continue;
            if (offset.z > 0) plus = true; else minus = true;
        }
        // Полноразмерная площадка башни уже заполняет угловую клетку.
        if (part is WallGate || (part.thickness >= part.length && part.walkwayWidth >= part.length)) { plus = false; minus = false; }
        if (positive == plus && negative == minus) return;
        positive = plus; negative = minus;
        foreach (var piece in pieces) if (piece != null) { piece.SetActive(false); Destroy(piece); }
        pieces.Clear();
        if (plus) CreatePiece(1);
        if (minus) CreatePiece(-1);
    }
    private void CreatePiece(int sign)
    {
        var part = platform.Segment;
        var renderer = GetComponentInChildren<MeshRenderer>();
        var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = "WallCornerJoint"; piece.layer = gameObject.layer;
        piece.transform.SetParent(transform, false);
        piece.transform.localPosition = new Vector3(0, part.height * 0.5f, sign * part.length * 0.25f);
        piece.transform.localScale = new Vector3(Mathf.Max(part.thickness, part.walkwayWidth), part.height, part.length * 0.5f);
        if (renderer != null) piece.GetComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial;
        var obstacle = piece.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(1, Mathf.Max(0.1f, 1f - 0.5f / part.height), 1);
        obstacle.center = new Vector3(0, -0.25f / part.height, 0);
        obstacle.carving = true; obstacle.carveOnlyStationary = true;
        pieces.Add(piece);
    }
    public void AddNavigationSources(List<NavMeshBuildSource> sources)
    {
        if (platform == null) return;
        if (positive) AddSource(1, sources);
        if (negative) AddSource(-1, sources);
    }
    private void AddSource(int sign, List<NavMeshBuildSource> sources)
    {
        var part = platform.Segment;
        Vector3 center = transform.TransformPoint(new Vector3(0, part.height - 0.1f, sign * part.length * 0.25f));
        sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
            transform = Matrix4x4.TRS(center, transform.rotation, Vector3.one),
            size = new Vector3(part.walkwayWidth, 0.2f, part.length * 0.5f), area = WallNavigation.Area });
    }
    public bool Contains(Vector3 local)
    {
        if (platform == null || Mathf.Abs(local.x) > Mathf.Max(platform.Segment.thickness, platform.Segment.walkwayWidth) * 0.5f) return false;
        return (positive && local.z >= 0 && local.z <= platform.Segment.length * 0.5f)
            || (negative && local.z <= 0 && local.z >= -platform.Segment.length * 0.5f);
    }
}
