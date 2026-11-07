using System;
using System.Collections.Generic;

public static class PatrolChecks
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        var source=new List<int>{10,20,30};
        var route=new PatrolRoute<int>(source);source.Clear();
        Check(route.Point==10&&route.Count==3,"route owns snapshot of chosen points");
        int[] expected={20,30,20,10,20,30,20,10};
        foreach(int point in expected){route.Arrive();Check(route.Point==point,"patrol reverses at endpoints without skipping or repeating");}
        var pair=new PatrolRoute<int>(new[]{1,2});
        for(int i=0;i<100;i++){Check(pair.Point==i%2+1,"two-point patrol repeats indefinitely");pair.Arrive();}
        bool rejected=false;try{new PatrolRoute<int>(new[]{1});}catch(ArgumentException){rejected=true;}
        Check(rejected,"single-point route rejected");
        Console.WriteLine("PASS: patrol route snapshot, two/multiple points, reverse traversal and invalid route");
    }
}
