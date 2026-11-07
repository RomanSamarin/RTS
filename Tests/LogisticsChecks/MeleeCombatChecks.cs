using System;

public static class MeleeCombatChecks
{
    private static void Check(bool ok,string reason){if(!ok)throw new Exception("Melee: "+reason);}
    public static void Run()
    {
        Check(MeleeCombatRules.Capacity(6,1.5f,1.15f)==6,"configured cap limits normal-size ring");
        Check(MeleeCombatRules.Capacity(10,1.5f,3f)==3,"large agents reduce real ring capacity");
        Check(MeleeCombatRules.Capacity(0,1.5f,1f)==1,"invalid configuration remains bounded");
        var clock=new MeleeStrikeClock();
        Check(!clock.Tick(.15f,true,1,.3f),"damage does not happen on animation entry");
        Check(clock.Tick(.15f,true,1,.3f),"damage occurs after windup");
        Check(!clock.Tick(.7f,true,1,.3f),"same swing cannot deal damage twice");
        Check(clock.Tick(.3f,true,1,.3f),"next cycle has a new windup");
        Check(!clock.Tick(1,false,1,.3f),"out-of-range target cannot be hit");
        Check(!clock.Tick(.2f,true,1,.3f),"moving out resets unfinished swing");
        clock.Reset();
        Check(!clock.Tick(.2f,true,1,.3f),"switching targets resets old windup");
        Check(!clock.Tick(0,true,1,.3f),"paused game cannot strike");
        Check(clock.Tick(.2f,true,1,.3f),"new target receives only completed new swing");
        Console.WriteLine("PASS: melee crowd capacity, agent size, strike windup, one hit per swing, cancelled target and pause");
    }
}
