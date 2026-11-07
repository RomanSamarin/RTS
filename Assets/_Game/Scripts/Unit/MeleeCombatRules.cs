using System;

public static class MeleeCombatRules
{
    public static int Capacity(int configured,float radius,float diameter)
        =>Math.Max(1,Math.Min(Math.Max(1,configured),(int)Math.Floor(2*Math.PI*Math.Max(.1f,radius)/Math.Max(.1f,diameter))));
}

/// <summary>Удар после замаха. Выход из дистанции или смена цели отменяют незавершённый удар.</summary>
public sealed class MeleeStrikeClock
{
    private float elapsed;
    private bool active,hit;
    public void Reset(){active=false;hit=false;elapsed=0;}
    public bool Tick(float delta,bool inRange,float period,float windup)
    {
        if(!inRange){Reset();return false;}
        if(delta<=0)return false;
        period=Math.Max(.05f,period);windup=Math.Max(0,Math.Min(period,windup));
        if(!active){active=true;elapsed=0;hit=false;}
        elapsed+=delta;
        if(!hit&&elapsed>=windup){hit=true;return true;}
        if(elapsed>=period){elapsed=0;hit=false;}
        return false;
    }
}
