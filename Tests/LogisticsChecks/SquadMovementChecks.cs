using System;
using System.Collections.Generic;
using UnityEngine;

public static class SquadMovementChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("Squad: "+why);}
    private static bool Near(Vector3 a,Vector3 b)=>(a-b).magnitude<.001f;
    public static void Run()
    {
        Check(SquadMovementRules.UseDirectAssembly(true,100,3,1.2f),"F sends each unit directly to assigned slot even at long distance");
        Check(SquadMovementRules.UseDirectAssembly(false,2,3,1.2f),"nearby reformation does not walk backwards through virtual center");
        Check(!SquadMovementRules.UseDirectAssembly(false,50,3,1.2f),"long formation march retains coordinated corridor handling");
        var path=new List<Vector3>{Vector3.zero,new Vector3(0,0,4),new Vector3(3,0,4)};
        Check(Math.Abs(SquadMovementRules.Length(path)-7)<.001f,"corner route length");
        Check(Near(SquadMovementRules.Sample(path,2,out var heading),new Vector3(0,0,2))&&Near(heading,Vector3.forward),"front slots advance along first leg");
        Check(Near(SquadMovementRules.Sample(path,5,out heading),new Vector3(1,0,4))&&Near(heading,Vector3.right),"column follows corner instead of crossing obstacle");
        Check(Near(SquadMovementRules.Sample(path,100,out heading),new Vector3(3,0,4)),"tail cannot overshoot route end");
        path.Insert(1,Vector3.zero);
        Check(Near(SquadMovementRules.Sample(path,5,out heading),new Vector3(1,0,4)),"zero-length corners do not corrupt queue");
        var slots=new List<Vector3>{Vector3.zero,Vector3.zero,new Vector3(0,0,2)};
        SquadMovementRules.Separate(slots,1.2f);
        for(int i=0;i<slots.Count;i++)for(int j=i+1;j<slots.Count;j++)Check(Vector3.Distance(slots[i],slots[j])>=1.19f,"formation slots cannot overlap agent diameters");
        var original=new List<Vector3>(slots);SquadMovementRules.Separate(slots,1.2f);
        for(int i=0;i<slots.Count;i++)Check(Near(slots[i],original[i]),"valid formation preserves member-to-slot mapping");
        Check(SquadMovementRules.LimitSpeed(6,3)==3&&SquadMovementRules.LimitSpeed(2,3)==2,"slow soldiers are never accelerated and cavalry waits");
        Check(SquadMovementRules.LimitSpeed(6,0)==0,"temporary stun pauses group");
        var straight=new List<Vector3>{Vector3.zero,new Vector3(0,0,100)};
        Check(Near(SquadMovementRules.SampleMarch(straight,-4,out heading),new Vector3(0,0,-4)),"rear ranks do not collapse onto route origin");
        var departure=new Vector3(8,0,6);var targetOffset=new Vector3(-2,0,-6);
        float blendDistance=SquadMovementRules.TransitionDistance(Vector3.Distance(departure,targetOffset),1.2f);
        Check(Near(SquadMovementRules.MarchPosition(departure,Vector3.zero,Vector3.zero,targetOffset,0,blendDistance),departure),"new move starts at actual unit position, not new formation slot");
        float previousZ=departure.z;
        for(float distance=.25f;distance<=blendDistance;distance+=.25f)
        {
            var center=new Vector3(0,0,distance);
            var waypoint=SquadMovementRules.MarchPosition(departure,Vector3.zero,center,center+targetOffset,distance,blendDistance);
            Check(waypoint.z>previousZ,"reforming a forward march cannot first send this front soldier backwards");previousZ=waypoint.z;
        }
        var endCenter=new Vector3(0,0,blendDistance);
        Check(Near(SquadMovementRules.MarchPosition(departure,Vector3.zero,endCenter,endCenter+targetOffset,blendDistance,blendDistance),endCenter+targetOffset),"formation alignment completes during travel");
        var cornerCenter=new Vector3(3,0,4);var actual=new Vector3(7,0,2);
        Check(Near(SquadMovementRules.MarchPosition(actual,cornerCenter,cornerCenter,Vector3.zero,0,12),actual),"column transition and route replanning restart from actual position without a jump");
        Console.WriteLine("PASS: squad departure without regrouping/backtracking, gradual alignment, column transition, route corners and fixed slots");
    }
}
