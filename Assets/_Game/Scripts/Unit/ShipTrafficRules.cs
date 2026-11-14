using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Planar traffic policies shared by the runtime controller and isolated checks.</summary>
public static class ShipTrafficRules
{
    public static Vector3 Flat(Vector3 value) { value.y=0; return value; }
    public static bool Conflict(Vector3 a, Vector3 velocityA, Vector3 b, Vector3 velocityB, float radius, float horizon)
    {
        Vector3 separation=Flat(b-a), relative=Flat(velocityB-velocityA);
        float t=relative.sqrMagnitude>.0001f?Mathf.Max(0,Mathf.Min(horizon,-Vector3.Dot(separation,relative)/relative.sqrMagnitude)):0;
        return (separation+relative*t).sqrMagnitude < radius*radius;
    }
    public static bool GivesWay(int selfId, bool selfMoving, int otherId, bool otherMoving)
        => selfMoving && (!otherMoving || selfId>otherId);

    // Continuous relative sweep, not just an endpoint test. An initially crowded ship may escape outward.
    public static bool SafeStep(Vector3 start, Vector3 end, Vector3 other, Vector3 otherVelocity, float seconds, float radius)
    {
        Vector3 separation=Flat(start-other), step=Flat(end-start-otherVelocity*seconds);
        float initial=separation.sqrMagnitude;
        if(initial<radius*radius)
            return Vector3.Dot(separation,step)>=0 && (separation+step).sqrMagnitude>initial+.00001f;
        float t=step.sqrMagnitude>.000001f?Mathf.Clamp01(-Vector3.Dot(separation,step)/step.sqrMagnitude):0;
        return (separation+step*t).sqrMagnitude>=radius*radius;
    }
    public static float FollowingSpeed(float forwardGap, float clearance, float leaderSpeed, float reactionSeconds)
        => Mathf.Max(0, leaderSpeed+(forwardGap-clearance)/Mathf.Max(.2f,reactionSeconds));

    public static Vector3 EscortPoint(Vector3 leader, Vector3 heading, float spacing, int slot, bool alternate = false)
    {
        heading = Flat(heading).normalized;
        var right = new Vector3(heading.z, 0, -heading.x);
        int row = (slot + 1) / 2;
        float side = slot == 0 ? 0f : slot % 2 == 1 ? 1f : -1f;
        if (alternate) return leader - heading * (spacing * (slot + 2));
        return leader - heading * (spacing * (1f + .85f * row)) + right * (side * spacing * .8f);
    }

    public static bool HasPassed(Vector3 other, Vector3 yieldStart, Vector3 otherHeading, float clearance)
        => Vector3.Dot(Flat(other-yieldStart),Flat(otherHeading).normalized)>clearance;

    // Prefer starboard. If the shoulder is closed, find a wider pocket behind the yielding vessel.
    public static bool FindRefuge(Vector3 start, Vector3 heading, float spacing, float maxRetreat,
        Func<Vector3,bool> free, Func<Vector3,Vector3,List<Vector3>,bool> path, List<Vector3> result)
    {
        result.Clear(); heading=Flat(heading).normalized;
        if(heading.sqrMagnitude<.5f)return false;
        var right=new Vector3(heading.z,0,-heading.x);
        int levels=Mathf.Min(8,Mathf.Max(0,Mathf.FloorToInt(maxRetreat/Mathf.Max(1,spacing))));
        for(int level=0;level<=levels;level++)
        {
            Vector3 back=start-heading*(level*spacing);
            foreach(float sign in new[]{1f,-1f})
            {
                Vector3 pocket=back+right*(sign*spacing);
                if(!free(back)||!free(pocket))continue;
                var first=new List<Vector3>(); var second=new List<Vector3>();
                if(level>0&&!path(start,back,first))continue;
                if(!path(back,pocket,second))continue;
                result.AddRange(first); result.AddRange(second); return true;
            }
        }
        return false;
    }
}
