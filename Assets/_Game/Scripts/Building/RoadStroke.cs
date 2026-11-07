using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

/// <summary>Одна дорога — один переиспользуемый меш, без коллайдеров превью.</summary>
public class RoadStroke : MonoBehaviour
{
    public readonly List<Vector3> Points=new List<Vector3>();
    public float Width { get; private set; }
    public Bounds Footprint { get; private set; }
    public bool RoundStart { get; private set; }
    public bool RoundEnd { get; private set; }
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private RoadBuilder owner;
    private bool registered;
    public bool IsBuilt=>registered;
    private readonly List<Vector3> vertices=new List<Vector3>();
    private readonly List<Vector2> uv=new List<Vector2>();
    private readonly List<int> triangles=new List<int>();
    private readonly List<Vector3> ring=new List<Vector3>();
    public void Rebuild(RoadBuilder builder,IReadOnlyList<Vector3> points,float width,Material material,bool ready,bool roundStart=false,bool roundEnd=false)
    {
        bool wasReady=registered;
        owner=builder;registered=ready;Width=width;
        int roadLayer=LayerMask.NameToLayer("RoadSurface");
        gameObject.layer=ready&&roadLayer>=0?roadLayer:2;
        var modifier=GetComponent<NavMeshModifier>();
        if(ready&&RoadNavigation.RoadArea>=0)
        {
            if(modifier==null)modifier=gameObject.AddComponent<NavMeshModifier>();
            modifier.enabled=true;modifier.overrideArea=true;modifier.area=RoadNavigation.RoadArea;
        }
        else if(modifier!=null)modifier.enabled=false;
        RoundStart=roundStart;RoundEnd=roundEnd;
        Points.Clear();for(int i=0;i<points.Count;i++)Points.Add(points[i]);
        if(mesh==null)
        {
            mesh=new Mesh {name="Freeform road"};mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            meshRenderer=gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        meshRenderer.sharedMaterial=material;vertices.Clear();uv.Clear();triangles.Clear();
        if(Points.Count<2){mesh.Clear();if(ready||wasReady)RoadNavigation.Changed();return;}
        float distance=0;Bounds bounds=new Bounds(Points[0],Vector3.zero);
        for(int i=0;i<Points.Count;i++)
        {
            Vector3 before=Points[Mathf.Max(0,i-1)],after=Points[Mathf.Min(Points.Count-1,i+1)];
            Vector3 direction=after-before;direction.y=0;
            if(direction.sqrMagnitude<.000001f)direction=Vector3.forward;
            Vector3 right=Vector3.Cross(Vector3.up,direction.normalized)*width*.5f;
            Vector3 left=Points[i]-right,r=Points[i]+right;
            if(builder.SampleGround(left,out float y))left.y=y;
            if(builder.SampleGround(r,out y))r.y=y;
            left.y+=builder.surfaceOffset+(ready?0f:.025f);r.y+=builder.surfaceOffset+(ready?0f:.025f);
            vertices.Add(transform.InverseTransformPoint(left));vertices.Add(transform.InverseTransformPoint(r));
            if(i>0)distance+=Vector3.Distance(Points[i-1],Points[i]);
            uv.Add(new Vector2(0,distance));uv.Add(new Vector2(1,distance));
            bounds.Encapsulate(left);bounds.Encapsulate(r);
            if(i==0)continue;
            int n=i*2;triangles.Add(n-2);triangles.Add(n);triangles.Add(n-1);
            triangles.Add(n-1);triangles.Add(n);triangles.Add(n+1);
        }
        if(roundStart)AddJunction(Points[0],width,builder,ready,ref bounds);
        if(roundEnd)AddJunction(Points[Points.Count-1],width,builder,ready,ref bounds);
        Footprint=bounds;mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        if(ready&&builder.roadPrefab!=null)
        {
            if(RoadPrefabVisual.Build(builder.roadPrefab,Points,width,builder,transform,mesh,out var materials))
                meshRenderer.sharedMaterials=materials;
        }
        if(ready||wasReady)RoadNavigation.Changed();
    }
    private void AddJunction(Vector3 center,float width,RoadBuilder builder,bool ready,ref Bounds bounds)
    {
        // Перекрываем наружный клин между двумя повернутыми торцами, без длинных острых углов.
        RoadCurve.JunctionRing(center,width*.5f+.015f,32,ring);
        int first=vertices.Count;
        if(builder.SampleGround(center,out float height))center.y=height;
        center.y+=builder.surfaceOffset+(ready ? .003f : .028f);
        vertices.Add(transform.InverseTransformPoint(center));uv.Add(new Vector2(.5f,.5f));bounds.Encapsulate(center);
        for(int i=0;i<ring.Count;i++)
        {
            Vector3 p=ring[i];if(builder.SampleGround(p,out height))p.y=height;
            p.y+=builder.surfaceOffset+(ready ? .003f : .028f);
            vertices.Add(transform.InverseTransformPoint(p));bounds.Encapsulate(p);
            uv.Add(new Vector2(.5f+(p.x-center.x)/width,.5f+(p.z-center.z)/width));
        }
        for(int i=0;i<ring.Count;i++)
        {triangles.Add(first);triangles.Add(first+1+(i+1)%ring.Count);triangles.Add(first+1+i);}
    }
    public Vector3 ClosestPoint(Vector3 point,out float squared)
    {
        squared=float.PositiveInfinity;Vector3 best=point;
        for(int i=1;i<Points.Count;i++)
        {
            Vector3 p=RoadCurve.Closest(Points[i-1],Points[i],point,out _);Vector3 delta=p-point;delta.y=0;
            if(delta.sqrMagnitude<squared){squared=delta.sqrMagnitude;best=p;}
        }
        return best;
    }
    private void OnDisable(){if(registered){if(owner!=null)owner.InvalidateConnections();RoadNavigation.Changed();}}
    private void OnEnable(){if(registered){if(owner!=null)owner.InvalidateConnections();RoadNavigation.Changed();}}
    private void OnDestroy(){if(mesh!=null)Destroy(mesh);if(registered){if(owner!=null)owner.NotifyRemoved(this);RoadNavigation.Changed();}}
}
