using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Укладывает статические меши модуля вдоль кривой без создания сотен GameObject.</summary>
public static class RoadPrefabVisual
{
    private class Part
    {
        public Mesh mesh;
        public Matrix4x4 matrix;
        public Material[] materials;
        public Vector3[] vertices;
        public Vector2[] uv;
        public int[][] triangles;
    }

    public static bool Build(GameObject prefab,IReadOnlyList<Vector3> path,float width,RoadBuilder ground,
        Transform target,Mesh output,out Material[] materials)
    {
        materials=null;
        var parts=new List<Part>();
        Bounds bounds=default;bool hasBounds=false;
        foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>())
        {
            var source=filter.sharedMesh;var renderer=filter.GetComponent<MeshRenderer>();
            if(source==null||renderer==null||!renderer.enabled)continue;
            if(!source.isReadable)
            {
                Debug.LogWarning("[Дороги] Включите Read/Write у модели префаба дороги. Используется обычная дорога.",prefab);
                return false;
            }
            var matrix=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            var sourceVertices=source.vertices;
            foreach(var v in sourceVertices)
            {
                var p=matrix.MultiplyPoint3x4(v);
                if(!hasBounds){bounds=new Bounds(p,Vector3.zero);hasBounds=true;}else bounds.Encapsulate(p);
            }
            var triangles=new int[source.subMeshCount][];
            for(int sub=0;sub<triangles.Length;sub++)triangles[sub]=source.GetTriangles(sub);
            parts.Add(new Part{mesh=source,matrix=matrix,materials=renderer.sharedMaterials,vertices=sourceVertices,uv=source.uv,triangles=triangles});
        }
        if(!hasBounds||bounds.size.x<.001f||bounds.size.z<.001f||path.Count<2)return false;
        var vertices=new List<Vector3>();var uvs=new List<Vector2>();
        var palette=new List<Material>();var indices=new List<List<int>>();
        var sides=new Vector3[path.Count];
        for(int i=0;i<path.Count;i++)
        {
            var tangent=path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)];tangent.y=0;
            sides[i]=Vector3.Cross(Vector3.up,tangent.normalized);
        }
        for(int segment=1;segment<path.Count;segment++)foreach(var part in parts)
        {
            int first=vertices.Count;var original=part.vertices;var texture=part.uv;
            for(int i=0;i<original.Length;i++)
            {
                var local=part.matrix.MultiplyPoint3x4(original[i]);
                float along=(local.z-bounds.min.z)/bounds.size.z;
                var p=Vector3.Lerp(path[segment-1],path[segment],along)
                    +Vector3.Lerp(sides[segment-1],sides[segment],along)*((local.x-bounds.center.x)/bounds.size.x*width);
                if(ground.SampleGround(p,out float y))p.y=y;
                p.y+=ground.surfaceOffset+local.y-bounds.min.y;
                vertices.Add(target.InverseTransformPoint(p));uvs.Add(texture.Length==original.Length?texture[i]:Vector2.zero);
            }
            for(int sub=0;sub<part.mesh.subMeshCount;sub++)
            {
                Material material=part.materials.Length>0?part.materials[Mathf.Min(sub,part.materials.Length-1)]:ground.RoadMaterial;
                int slot=palette.IndexOf(material);
                if(slot<0){slot=palette.Count;palette.Add(material);indices.Add(new List<int>());}
                foreach(int index in part.triangles[sub])indices[slot].Add(first+index);
            }
        }
        output.Clear();output.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
        output.SetVertices(vertices);output.SetUVs(0,uvs);output.subMeshCount=indices.Count;
        for(int i=0;i<indices.Count;i++)output.SetTriangles(indices[i],i);
        output.RecalculateNormals();output.RecalculateBounds();output.RecalculateTangents();
        materials=palette.ToArray();return true;
    }
}
