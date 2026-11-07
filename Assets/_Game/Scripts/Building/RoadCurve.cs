using System.Collections.Generic;
using UnityEngine;

/// <summary>Геометрия свободных дорог; одинаковые точки для превью, покрытия и подключения.</summary>
public static class RoadCurve
{
    public static bool SnapEndpoint(IReadOnlyList<Vector3> points,ref Vector3 point,float radius,out Vector3 inward)
    {
        inward=new Vector3();if(points.Count<2)return false;
        Vector3 a=point-points[0],b=point-points[points.Count-1];a.y=b.y=0;
        float threshold=radius*radius;
        if(a.sqrMagnitude>threshold&&b.sqrMagnitude>threshold)return false;
        if(a.sqrMagnitude<=b.sqrMagnitude)
        {point=points[0];inward=points[1]-points[0];}
        else {point=points[points.Count-1];inward=points[points.Count-2]-points[points.Count-1];}
        inward.y=0;inward=inward.normalized;return true;
    }
    public static void JunctionRing(Vector3 center,float radius,int segments,List<Vector3> ring)
    {
        ring.Clear();segments=Mathf.Max(8,segments);
        for(int i=0;i<segments;i++)
        {
            float angle=(float)i/segments*Mathf.PI*2f;
            ring.Add(center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));
        }
    }
    public static void Sample(IReadOnlyList<Vector3> knots,float spacing,List<Vector3> points,
        Vector3 startDirection,Vector3 endDirection)
    {
        points.Clear();if(knots.Count==0)return;points.Add(knots[0]);
        spacing=Mathf.Max(.15f,spacing);
        for(int i=0;i<knots.Count-1;i++)
        {
            Vector3 a=knots[i],b=knots[i+1];float length=Vector3.Distance(a,b);
            Vector3 ta=i==0?b-a:(b-knots[i-1])*.5f;
            Vector3 tb=i+2>=knots.Count?b-a:(knots[i+2]-a)*.5f;
            if(knots.Count>=4&&(knots[0]-knots[knots.Count-1]).sqrMagnitude<.0001f)
            {
                Vector3 seam=(knots[1]-knots[knots.Count-2])*.5f;
                if(i==0)ta=seam;
                if(i==knots.Count-2)tb=seam;
            }
            if(i==0&&startDirection.sqrMagnitude>.001f)ta=startDirection.normalized*length;
            if(i==knots.Count-2&&endDirection.sqrMagnitude>.001f)tb=endDirection.normalized*length;
            ta=Vector3.ClampMagnitude(ta,length);tb=Vector3.ClampMagnitude(tb,length);
            int steps=Mathf.Clamp(Mathf.CeilToInt((length+ta.magnitude+tb.magnitude)/spacing),2,1024);
            for(int j=1;j<=steps;j++)
            {
                float t=(float)j/steps,t2=t*t,t3=t2*t;
                Vector3 point=(2*t3-3*t2+1)*a+(t3-2*t2+t)*ta+(-2*t3+3*t2)*b+(t3-t2)*tb;
                if((point-points[points.Count-1]).sqrMagnitude>.000001f)points.Add(point);
            }
        }
    }
    public static Vector3 Closest(Vector3 a,Vector3 b,Vector3 point,out float t)
    {
        Vector3 d=b-a;d.y=0;Vector3 offset=point-a;offset.y=0;
        t=d.sqrMagnitude>.000001f?Mathf.Clamp01(Vector3.Dot(offset,d)/d.sqrMagnitude):0f;
        return a+(b-a)*t;
    }
    // SAT для прямоугольника дорожной полосы с плоскими концами и AABB здания.
    // В отличие от расширенного AABB, не запрещает подход торцом к двери.
    public static bool RibbonIntersectsBox(Vector3 a,Vector3 b,float halfWidth,Bounds box)
    {
        Vector3 d=b-a;d.y=0;float length=d.magnitude;if(length<.0001f)return false;
        Vector3 along=d/length,side=new Vector3(-along.z,0,along.x),center=(a+b)*.5f;
        Vector3 offset=box.center-center;float hx=box.extents.x,hz=box.extents.z;
        float half=length*.5f;
        if(Mathf.Abs(offset.x)>=Mathf.Abs(along.x)*half+Mathf.Abs(side.x)*halfWidth+hx-.01f)return false;
        if(Mathf.Abs(offset.z)>=Mathf.Abs(along.z)*half+Mathf.Abs(side.z)*halfWidth+hz-.01f)return false;
        if(Mathf.Abs(Vector3.Dot(offset,along))>=half+Mathf.Abs(along.x)*hx+Mathf.Abs(along.z)*hz-.01f)return false;
        if(Mathf.Abs(Vector3.Dot(offset,side))>=halfWidth+Mathf.Abs(side.x)*hx+Mathf.Abs(side.z)*hz-.01f)return false;
        return true;
    }
}
