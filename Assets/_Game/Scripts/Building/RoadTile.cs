using System.Collections.Generic;
using UnityEngine;

/// <summary>Готовая клетка дороги. Игровую регистрацию ведёт RoadBuilder.</summary>
public class RoadTile : MonoBehaviour
{
    public Vector2Int Cell { get; private set; }
    public RoadShape Shape { get; private set; }
    public int Ports { get; private set; }
    public RoadShape RequestedShape { get; private set; }
    public int Rotation { get; private set; }
    private RoadBuilder owner;
    private GameObject visual;
    private Mesh mesh;
    public void Initialize(RoadBuilder builder, Vector2Int cell, RoadShape shape, int rotation)
    { owner = builder; Cell = cell; RequestedShape = shape; Rotation = rotation; }
    public void Rebuild(int ports)
    {
        if (visual != null) Destroy(visual);
        if (mesh != null) Destroy(mesh);
        Ports = ports;
        RoadTopology.Resolve(ports, out var shape, out int rotation);
        Shape = shape;
        var prefab = owner.VisualPrefab(shape);
        if (prefab != null)
        {
            visual = Instantiate(prefab, transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, rotation * 90f, 0f);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var obstacle in visual.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true)) obstacle.enabled = false;
        }
        else
        {
            visual = new GameObject("Road visual"); visual.transform.SetParent(transform, false);
            mesh = MakeMesh(ports, owner.cellSize, owner.roadWidth);
            owner.ConformMesh(mesh,transform.position);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = owner.RoadMaterial;
        }
        foreach (var child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
    }
    public static Mesh MakeMesh(int ports, float cell, float width)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
        float w = Mathf.Clamp(width, 0.1f, cell) * 0.5f, h = cell * 0.5f;
        Quad(vertices,triangles,uv,-w,w,-w,w);
        if ((ports & 1) != 0) Quad(vertices,triangles,uv,-w,w,w,h);
        if ((ports & 2) != 0) Quad(vertices,triangles,uv,w,h,-w,w);
        if ((ports & 4) != 0) Quad(vertices,triangles,uv,-w,w,-h,-w);
        if ((ports & 8) != 0) Quad(vertices,triangles,uv,-h,-w,-w,w);
        var result = new Mesh { name = "Road tile" };
        result.SetVertices(vertices); result.SetTriangles(triangles,0); result.SetUVs(0,uv);
        result.RecalculateNormals(); result.RecalculateBounds(); return result;
    }
    private static void Quad(List<Vector3> v,List<int> t,List<Vector2> uv,float x1,float x2,float z1,float z2)
    {
        if (x2 <= x1 || z2 <= z1) return;
        int n = v.Count;
        v.Add(new Vector3(x1,0,z1)); v.Add(new Vector3(x1,0,z2)); v.Add(new Vector3(x2,0,z2)); v.Add(new Vector3(x2,0,z1));
        t.Add(n);t.Add(n+1);t.Add(n+2);t.Add(n);t.Add(n+2);t.Add(n+3);
        uv.Add(new Vector2(x1,z1));uv.Add(new Vector2(x1,z2));uv.Add(new Vector2(x2,z2));uv.Add(new Vector2(x2,z1));
    }
    private void OnDestroy()
    { if (mesh != null) Destroy(mesh); if (owner != null) owner.NotifyRemoved(this); }
}
