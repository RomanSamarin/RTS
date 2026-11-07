using System.Collections.Generic;
using UnityEngine;

public static class SquadMovementRules
{
    public static bool UseDirectAssembly(bool requested,float routeLength,float formationRadius,float spacing)
        =>requested||routeLength<=formationRadius*2+spacing;
    public static float Length(IReadOnlyList<Vector3> path)
    {float length=0;for(int i=1;i<path.Count;i++)length+=Vector3.Distance(path[i-1],path[i]);return length;}
    public static Vector3 Sample(IReadOnlyList<Vector3> path,float distance,out Vector3 forward)
    {
        forward=Vector3.forward;if(path.Count==0)return Vector3.zero;
        for(int i=1;i<path.Count;i++)
        {
            Vector3 delta=path[i]-path[i-1];float length=delta.magnitude;
            if(length<.001f)continue;
            forward=delta/length;
            if(distance<=length)return path[i-1]+forward*distance;
            distance-=length;
        }
        return path[path.Count-1];
    }
    public static float LimitSpeed(float ownSpeed,float slowest)=>Mathf.Min(ownSpeed,slowest);
    public static float TransitionDistance(float maximumShift,float spacing)
        =>Mathf.Max(spacing*4,maximumShift*2);
    public static Vector3 MarchPosition(Vector3 initialPosition,Vector3 initialCenter,Vector3 center,
        Vector3 formationPosition,float travelled,float transitionDistance)
    {
        float t=Mathf.Clamp01(travelled/Mathf.Max(.1f,transitionDistance));
        t=t*t*(3-2*t);
        Vector3 translated=initialPosition+center-initialCenter;
        return translated+(formationPosition-translated)*t;
    }
    public static Vector3 SampleMarch(IReadOnlyList<Vector3> path,float distance,out Vector3 forward)
    {
        var point=Sample(path,Mathf.Max(0,distance),out forward);
        // Задние ряды уже стоят позади центра. Не стягиваем их в начало маршрута.
        return distance<0?point+forward*distance:point;
    }
    public static void Separate(List<Vector3> points,float spacing)
    {
        // Исправляем только пересекающиеся места, сохраняя принадлежность места бойцу.
        for(int pass=0;pass<16;pass++)for(int i=0;i<points.Count;i++)for(int j=i+1;j<points.Count;j++)
        {
            Vector3 delta=points[j]-points[i];delta.y=0;float distance=delta.magnitude;
            if(distance>=spacing-.001f)continue;
            var direction=distance>.001f?delta/distance:Vector3.right;
            var push=direction*((spacing-distance)*.5f);
            points[i]-=push;points[j]+=push;
        }
    }
}
