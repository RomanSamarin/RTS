using System;
public static class ArmyRejoinChecks
{
    private static void Check(bool value,string reason){if(!value)throw new Exception("Army rejoin: "+reason);}
    public static void Run()
    {
        Check(ArmyRejoinRules.Eligible(true,true,true,true,true),"fully healed soldier returns to original active army");
        Check(!ArmyRejoinRules.Eligible(false,true,true,true,true),"early discharge cannot send wounded soldier back");
        Check(!ArmyRejoinRules.Eligible(true,false,true,true,true),"disbanded army cannot be used");
        Check(!ArmyRejoinRules.Eligible(true,true,false,true,true),"reassignment cannot silently restore old army");
        Check(!ArmyRejoinRules.Eligible(true,true,true,false,true),"ownership change cancels return");
        Check(!ArmyRejoinRules.Eligible(true,true,true,true,false),"disbanded soldier cannot return as civilian");
        Check(ArmyRejoinRules.Arrived(3,4,false),"arrival near stationary army");
        Check(!ArmyRejoinRules.Arrived(3,4,true),"continue following moving army instead of stopping behind");
        Check(!ArmyRejoinRules.Arrived(8,4,false),"hospital exit is not arrival to remote army");
        Console.WriteLine("PASS: hospital army return eligibility, early discharge, disband/reassignment/ownership and moving army arrival");
    }
}
