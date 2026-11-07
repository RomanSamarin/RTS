using System;
using UnityEngine;

public static class ArrowTrajectoryChecks
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception("Arrow: " + message); }
    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a,b)<0.0001f;
    public static void Run()
    {
        var start=new Vector3(0,1,0); var end=new Vector3(12,1,0);
        Check(Near(ArrowTrajectory.Position(start,end,0,3),start),"exact launch point");
        Check(Near(ArrowTrajectory.Position(start,end,1,3),end),"exact destination");
        Check(Near(ArrowTrajectory.Position(start,end,.5f,3),new Vector3(6,4,0)),"apex rises above straight line");
        Check(ArrowTrajectory.Tangent(start,end,0,3).y>0 && ArrowTrajectory.Tangent(start,end,1,3).y<0,"arrow points up then down");
        var higher=new Vector3(12,7,0);
        Check(Near(ArrowTrajectory.Position(start,higher,.5f,2),new Vector3(6,6,0)),"height difference supported");
        Check(Near(ArrowTrajectory.Position(start,end,2,3),end),"no overshoot of endpoint");
        Check(ArrowTrajectory.HitSphere(new Vector3(0,1,0),new Vector3(20,1,0),end,.5f,out float fraction)
            && Math.Abs(fraction-.575f)<.0001f,"fast segment first impact, no tunnelling");
        Check(!ArrowTrajectory.HitSphere(start,end,new Vector3(6,1,2),.35f,out _),"target dodging aside is missed");
        Check(!ArrowTrajectory.HitSphere(start,end,new Vector3(15,1,0),.35f,out _),"no damage past end of segment");
        Check(!ArrowTrajectory.HitSphere(start,start,end,.35f,out _),"zero motion has no false impact");
        Check(ArrowTrajectory.HitSphere(start,start,start,.35f,out _),"starting within target sphere");
        // Проверяем, что стрела не попадает в наземную цель, пролетая высоко над ней.
        var left=ArrowTrajectory.Position(start,end,.4f,3);
        var right=ArrowTrajectory.Position(start,end,.6f,3);
        Check(!ArrowTrajectory.HitSphere(left,right,new Vector3(6,1,0),.35f,out _),"arc altitude respected");
        Console.WriteLine("PASS: arrow arc endpoints/apex, tangent rotation, elevated targets, fast hits and dodging");
    }
}
