using System;
using _Game.Scripts.UI;
using UnityEngine;

static class ShipCrewChecks
{
    static void Check(bool condition, string name) { if (!condition) throw new Exception("Ship crew: " + name); }
    public static void Run()
    {
        var ship=new ShipProvisions(); var dock=new ShipSupplyDock { ship=ship }; ship.SetDock(dock);
        var courier=new UnityEngine.Object(); var cargo=new GettingResources(); cargo.Set(ResourceType.Bread,3);
        Check(ship.ReserveDelivery(courier,dock,3)&&ship.Receive(courier,dock,cargo)==3,"three physical meals aboard");
        var a=new HungerTimer(); var b=new HungerTimer();
        bool Hungry(float now, bool hungry, float due, int amount, HungerTimer timer, out bool fed, out bool damage)
            => ShipCrewNutrition.Tick(now,hungry,due,amount,120,20,timer,ship.TryConsume,out fed,out damage);
        Check(!Hungry(10,false,60,1,a,out bool fed,out bool damage)&&!fed&&!damage&&ship.StoredMeals==3,
            "no consumption before resident's own next meal");
        Check(!Hungry(60,false,60,1,a,out fed,out damage)&&fed&&!damage&&ship.StoredMeals==2,"first sailor eats one portion");
        Check(!Hungry(60,false,60,2,b,out fed,out damage)&&fed&&!damage&&ship.StoredMeals==0,"second sailor uses own portion setting");
        Check(Hungry(120,false,120,1,a,out fed,out damage)&&!fed&&!damage&&a.DamageBeginsAt==240,"empty hold starts normal grace");
        Check(Hungry(200,true,120,1,a,out fed,out damage)&&!damage&&a.DamageBeginsAt==240,"repeated requests do not reset grace");
        Check(Hungry(240,true,120,1,a,out fed,out damage)&&damage,"damage only after full grace");
        Check(Hungry(240,true,120,1,a,out fed,out damage)&&!damage,"no duplicate damage in same frame");
        // A stock reservation is not a meal. Only delivery makes food available.
        Check(ship.ReserveDelivery(courier,dock,2),"reserve incoming food");
        Check(Hungry(259,true,120,2,a,out fed,out damage)&&!fed&&!damage,"incoming food cannot be eaten remotely");
        cargo.Set(ResourceType.Bread,1); Check(ship.Receive(courier,dock,cargo)==1,"partial physical delivery");
        Check(Hungry(260,true,120,2,a,out fed,out damage)&&!fed&&damage&&ship.StoredMeals==1,"insufficient portion never partly charged");
        Check(!Hungry(260,true,120,1,b,out fed,out damage)&&fed&&ship.StoredMeals==0,"other sailor may use remaining whole portion");
        // Carry the hunger episode through the real save codec, then rebase to new scene time.
        var codec=new SaveStateCodec(); var fields=codec.CaptureFields(a); var restored=new HungerTimer();
        codec.timeOffset=1000; codec.RestoreFields(restored,fields);
        Check(restored.DamageBeginsAt==1240&&restored.NextDamageAt==1280,"saved hunger timings rebased");
        Check(Hungry(1279,true,1120,1,restored,out fed,out damage)&&!damage,"saved crew does not get early starvation damage");
        Check(Hungry(1280,true,1120,1,restored,out fed,out damage)&&damage,"saved grace not restarted");
        ship.RequestRestock(); Check(ship.ReserveDelivery(courier,dock,1),"new reserve");
        cargo.Set(ResourceType.Bread,1); ship.Receive(courier,dock,cargo);
        Check(!Hungry(1300,true,1120,1,restored,out fed,out damage)&&fed&&!damage&&!restored.Active,"real food ends saved hunger episode");
        Console.WriteLine("PASS: individual crew meals, physical stock, whole portions, grace, repeated damage, real refills and saved hunger");
    }
}
