using System;
using System.Collections.Generic;
using UnityEngine;

public static class RoadCurveChecks
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Road curve: "+message);}
    private static bool Near(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<.001f;
    public static void Run()
    {
        var points=new List<Vector3>();var zero=new Vector3();
        var start=new Vector3(0,0,0);var end=new Vector3(10,0,3);
        RoadCurve.Sample(new[]{start,end},.5f,points,zero,zero);
        Check(Near(points[0],start)&&Near(points[points.Count-1],end),"free endpoints preserved without grid quantization");
        foreach(var point in points)Check(Math.Abs(point.z-point.x*.3f)<.001f,"two unanchored points remain straight");
        var mid=new Vector3(5,0,5);
        RoadCurve.Sample(new[]{start,mid,new Vector3(10,0,0)},.5f,points,zero,zero);
        Check(points.Exists(p=>Near(p,mid)),"curve passes through intermediate control point");
        for(int i=1;i<points.Count;i++)Check(Vector3.Distance(points[i-1],points[i])<.7f,"continuous dense preview strip");
        // Дорога от двери начинается наружу и подходит к конечной двери по нормали.
        RoadCurve.Sample(new[]{start,new Vector3(10,0,10)},.25f,points,new Vector3(0,0,1),new Vector3(1,0,0));
        Vector3 first=(points[1]-points[0]).normalized,last=(points[points.Count-1]-points[points.Count-2]).normalized;
        Check(first.z>.99f&&last.x>.99f,"door approach tangents respected");
        var copy=new List<Vector3>();RoadCurve.Sample(new[]{start,new Vector3(10,0,10)},.25f,copy,new Vector3(0,0,1),new Vector3(1,0,0));
        Check(copy.Count==points.Count,"deterministic preview and built path");
        for(int i=0;i<copy.Count;i++)Check(Near(copy[i],points[i]),"same control points yield same mesh centerline");
        var box=new Bounds(new Vector3(),new Vector3(4,4,4));
        Check(!RoadCurve.RibbonIntersectsBox(new Vector3(0,0,8),new Vector3(0,0,2.08f),.9f,box),"flat road end reaches entrance without false expanded-box collision");
        Check(RoadCurve.RibbonIntersectsBox(new Vector3(-3,0,2.1f),new Vector3(3,0,2.1f),.9f,box),"road side clipping building rejected");
        Check(RoadCurve.RibbonIntersectsBox(new Vector3(-8,0,0),new Vector3(8,0,0),.9f,box),"segment passing through entire building rejected");
        Check(!RoadCurve.RibbonIntersectsBox(new Vector3(5,0,-4),new Vector3(5,0,4),.9f,box),"nearby parallel road allowed");
        var projected=RoadCurve.Closest(new Vector3(0,0,0),new Vector3(10,5,0),new Vector3(5,0,3),out float t);
        Check(Math.Abs(t-.5f)<.001f&&Near(projected,new Vector3(5,2.5f,0)),"road snapping interpolates elevation");
        var straight=new[]{new Vector3(0,0,0),new Vector3(5,0,0),new Vector3(10,0,0)};
        var nearEnd=new Vector3(9.7f,0,0);
        Check(RoadCurve.SnapEndpoint(straight,ref nearEnd,1f,out var inward)&&Near(nearEnd,straight[2])&&inward.x<-.99f,"nearby road end snaps exactly and supplies continuation tangent");
        var middle=new Vector3(5,0,0);
        Check(!RoadCurve.SnapEndpoint(straight,ref middle,1f,out _),"middle branch is not forced to endpoint");
        var ring=new List<Vector3>();RoadCurve.JunctionRing(new Vector3(10,0,0),.915f,32,ring);
        Check(ring.Count==32,"junction fan has closed ring");
        foreach(var p in ring)Check(Math.Abs(Vector3.Distance(p,straight[2])-.915f)<.001f,"junction covers outer cap wedge consistently at every angle");
        // При продолжении конца новой дороги начальный tangent противоположен inward.
        RoadCurve.Sample(new[]{nearEnd,new Vector3(20,0,5)},.25f,points,inward*-1,new Vector3());
        Check((points[1]-points[0]).normalized.x>.99f,"continued road starts along old road instead of a broken cap angle");
        Console.WriteLine("PASS: freeform road controls, smooth bends, perpendicular door approaches, stable previews and ribbon/building collisions");
    }
}
