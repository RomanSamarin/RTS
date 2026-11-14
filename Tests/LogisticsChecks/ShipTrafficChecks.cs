using System;
using System.Collections.Generic;
using UnityEngine;

static class ShipTrafficChecks
{
    static void Check(bool value,string reason){if(!value)throw new Exception("Ship traffic: "+reason);}
    public static void Run()
    {
        Vector3 P(float x,float z)=>new Vector3(x,0,z);
        Check(ShipTrafficRules.Conflict(P(0,-20),P(0,4),P(0,20),P(0,-4),10,6),"predict head-on before contact");
        Check(ShipTrafficRules.Conflict(P(-12,0),P(4,0),P(0,-12),P(0,4),8,5),"predict crossing courses");
        Check(!ShipTrafficRules.Conflict(P(-12,0),P(-4,0),P(12,0),P(4,0),8,5),"diverging courses continue");
        Check(!ShipTrafficRules.Conflict(P(-12,0),P(4,0),P(0,-30),P(0,2),4,5),"same crossing at different times is safe");
        Check(!ShipTrafficRules.GivesWay(1,true,2,true)&&ShipTrafficRules.GivesWay(2,true,1,true),"only one vessel yields in a pair");
        Check(ShipTrafficRules.GivesWay(1,true,2,false)&&!ShipTrafficRules.GivesWay(2,false,1,true),"stationary and moored ship never moves for traffic");
        for(int a=1;a<=4;a++)for(int b=1;b<=4;b++)for(int c=1;c<=4;c++)
            Check(!(ShipTrafficRules.GivesWay(a,true,b,true)&&ShipTrafficRules.GivesWay(b,true,c,true)
                &&ShipTrafficRules.GivesWay(c,true,a,true)),"no cyclic right-of-way for three ships");
        Check(!ShipTrafficRules.SafeStep(P(-10,0),P(10,0),P(0,0),P(0,0),1,3),"swept step cannot tunnel through another hull");
        Check(!ShipTrafficRules.SafeStep(P(0,0),P(2,0),P(8,0),P(-4,0),1,4),"account for other ship's movement during step");
        Check(ShipTrafficRules.SafeStep(P(0,0),P(-2,0),P(2,0),P(0,0),1,4),"crowded ship may back away");
        Check(!ShipTrafficRules.SafeStep(P(0,0),P(1,0),P(2,0),P(0,0),1,4),"crowded ship may not move closer");
        Check(ShipTrafficRules.FollowingSpeed(12,10,2,2)==3&&ShipTrafficRules.FollowingSpeed(8,10,0,2)==0,"slow follower and stop before leader");
        var lead = P(20, 30);
        var firstEscort = ShipTrafficRules.EscortPoint(lead, P(0, 1), 14, 0);
        var secondEscort = ShipTrafficRules.EscortPoint(lead, P(0, 1), 14, 1);
        var thirdEscort = ShipTrafficRules.EscortPoint(lead, P(0, 1), 14, 2);
        Check(firstEscort.x == lead.x && firstEscort.z == lead.z - 14
            && secondEscort.x > lead.x && thirdEscort.x < lead.x, "escorts occupy distinct positions behind the leader");
        var afterTurn = ShipTrafficRules.EscortPoint(lead, P(1, 0), 14, 0);
        Check(afterTurn.x == lead.x - 14 && afterTurn.z == lead.z, "escort position follows a turn");
        var alternateRight = ShipTrafficRules.EscortPoint(lead, P(0, 1), 14, 1, true);
        var alternateLeft = ShipTrafficRules.EscortPoint(lead, P(0, 1), 14, 2, true);
        Check(Vector3.Distance(alternateRight, alternateLeft) >= 14, "narrow channel fallback separates escorts");
        var path=new List<Vector3>();
        bool Direct(Vector3 a,Vector3 b,List<Vector3> result){result.Clear();result.Add(b);return true;}
        Check(ShipTrafficRules.FindRefuge(P(0,0),P(0,1),12,60,_=>true,Direct,path)&&path[path.Count-1].x>0,"starboard refuge preferred");
        Check(ShipTrafficRules.FindRefuge(P(0,0),P(0,1),12,60,p=>p.x<=0,Direct,path)&&path[path.Count-1].x<0,"opposite shoulder when starboard shore blocks");
        bool Channel(Vector3 p)=>Math.Abs(p.x)<1||p.z<=-20;
        bool ChannelPath(Vector3 a,Vector3 b,List<Vector3> result)
        {if(!ShipPathfinder.Segment(a,b,.2f,Channel))return false;return Direct(a,b,result);}
        Check(ShipTrafficRules.FindRefuge(P(0,0),P(0,1),12,60,Channel,ChannelPath,path)&&path.Count>=2
            &&path[0].z<0&&Math.Abs(path[path.Count-1].x)>=12,"reverse to wider pocket in narrow channel");
        Vector3 previous=P(0,0);foreach(var p in path){Check(ShipPathfinder.Segment(previous,p,.1f,Channel),"retreat path stays in navigable water");previous=p;}
        Check(!ShipTrafficRules.FindRefuge(P(0,0),P(0,1),12,60,p=>Math.Abs(p.x)<1,Direct,path)&&path.Count==0,"no pocket means safe wait, no teleport");
        Check(!ShipTrafficRules.HasPassed(P(0,-2),P(0,0),P(0,1),10)
            &&ShipTrafficRules.HasPassed(P(0,11),P(0,0),P(0,1),10),"resume only after priority hull clears conflict");
        Check(ShipTrafficRules.HasPassed(P(0,-12),P(0,0),P(0,-1),10)
            &&!ShipTrafficRules.HasPassed(P(0,-12),P(12,-24),P(0,-1),10),"retreated ship waits until winner passes its refuge, not its original position");
        // A yielded vessel clears sideways, holds, then crosses the wake only after the winner passes.
        Vector3 yielding=P(0,20),winner=P(0,-20),pocket=P(-12,20);
        for(int tick=0;tick<120;tick++)
        {
            Vector3 winnerNext=winner+P(0,.5f);
            if(ShipTrafficRules.SafeStep(winner,winnerNext,yielding,P(0,0),.125f,10))winner=winnerNext;
            Vector3 next=yielding+(pocket-yielding).normalized*Math.Min(.5f,Vector3.Distance(yielding,pocket));
            Check(ShipTrafficRules.SafeStep(yielding,next,winner,P(0,0),.125f,10),"lateral yielding remains collision free");
            yielding=next;
        }
        Check(ShipTrafficRules.HasPassed(winner,P(0,20),P(0,1),10),"priority ship actually gets past waiting vessel");
        Console.WriteLine("PASS: ship traffic head-on/crossing prediction, stable priority, swept hull clearance, following, retreat pockets and safe passing");
    }
}
