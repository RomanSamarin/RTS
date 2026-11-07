using System;
using System.Collections.Generic;

public static class DeliveryPriorityChecks
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        var attempts=new List<DeliveryPriorityLevel>();int reservations=0;
        bool planned=DeliveryPriorityRules.TryPlan(level=>
        {
            attempts.Add(level);
            if(level==DeliveryPriorityLevel.Urgent)return false; // Нет сырья/места/пути.
            if(level==DeliveryPriorityLevel.High){reservations++;return true;}
            throw new Exception("lower tier must not reserve after successful high tier");
        });
        Check(planned&&reservations==1&&attempts.Count==2,"unavailable urgent order falls back to high without duplicate reservation");
        bool urgentProduction=false,normalTavern=false;
        Check(DeliveryPriorityRules.TryPlan(level=>
        {
            if(level==DeliveryPriorityLevel.Normal){normalTavern=true;return true;}
            if(level==DeliveryPriorityLevel.Urgent){urgentProduction=true;return true;}
            return false;
        })&&urgentProduction&&!normalTavern,"priority precedes delivery category");
        attempts.Clear();
        Check(!DeliveryPriorityRules.TryPlan(level=>{attempts.Add(level);return false;})&&attempts.Count==4,"no feasible job leaves worker waiting");
        Check(attempts[0]==DeliveryPriorityLevel.Urgent&&attempts[3]==DeliveryPriorityLevel.Low,"low priority remains eligible after higher tiers fail");
        Console.WriteLine("PASS: cross-category delivery priority, infeasible fallback, one reservation and empty queue");
    }
}
