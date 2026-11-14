using System;
using System.Collections.Generic;
using UnityEngine;

static class ShipPassengerChecks
{
    static void Check(bool value,string why){if(!value)throw new Exception("Ship passengers: "+why);}
    public static void Run()
    {
        var roster=new ShipPassengerManifest();
        var infantry=new UnityEngine.Object();var cavalry=new UnityEngine.Object();var wagon=new UnityEngine.Object();var catapult=new UnityEngine.Object();
        Check(ShipPassengerManifest.Cost(false,false,false)==1&&ShipPassengerManifest.Cost(true,false,false)==2
            &&ShipPassengerManifest.Cost(false,true,false)==4&&ShipPassengerManifest.Cost(false,false,true)==6,"GDD weighted seat costs");
        Check(roster.Reserve(infantry,1,16)&&roster.Reserve(cavalry,2,16)&&roster.Reserve(wagon,4,16)&&roster.Reserve(catapult,6,16),"mixed army reserves thirteen seats");
        Check(roster.Used==13&&roster.BoardedSeats==0,"incoming seats cannot be sold again");
        Check(!roster.Reserve(wagon,4,16)&&!roster.Reserve(new UnityEngine.Object(),4,16),"duplicate wagon and over-capacity booking rejected");
        Check(!roster.Board(cavalry)&&roster.Next(false)==infantry,"FIFO boarding cannot bypass first reservation");
        Check(roster.Board(infantry)&&!roster.Board(infantry)&&roster.BoardedSeats==1&&roster.Used==13,"boarding consumes reservation once, not twice");
        roster.Release(cavalry);Check(roster.Used==11&&roster.Next(false)==wagon,"cancellation releases exactly cavalry slots");
        Check(roster.Board(wagon)&&roster.Board(catapult)&&roster.BoardedSeats==11,"hitched wagon occupies four total seats");
        var refs=new Dictionary<string,UnityEngine.Object>{{"infantry",infantry},{"wagon",wagon},{"catapult",catapult}};
        var codec=new SaveStateCodec{Reference=obj=>{foreach(var p in refs)if(p.Value==obj)return p.Key;return null;},Resolve=key=>refs.TryGetValue(key,out var obj)?obj:null};
        var restored=new ShipPassengerManifest();codec.RestoreFields(restored,codec.CaptureFields(roster));
        Check(restored.Used==11&&restored.BoardedSeats==11&&restored.Next(true)==infantry,"saved roster preserves real owners, costs and disembark order");
        restored.Release(infantry);Check(restored.Used==10&&restored.Next(true)==wagon,"successful exit releases only its own place");
        restored.Release(infantry);Check(restored.Used==10,"repeated release does not create capacity");
        wagon.destroyed=true;Check(restored.Used==6&&restored.BoardedSeats==6&&restored.Next(true)==catapult,"dead passenger releases slots and next exit proceeds");
        Check(!restored.Reserve(wagon,4,16)&&!restored.Board(null),"dead and null passengers cannot board");
        Check(!restored.Reserve(new UnityEngine.Object(),1,4)&&restored.Used==6,"capacity shrink does not duplicate or discard existing passengers");
        restored.Release(catapult);Check(restored.Used==0&&restored.BoardedSeats==0&&restored.Next(true)==null,"empty ship after exits");
        var deck = new ShipPassengerManifest();
        var archers = new UnityEngine.Object[6];
        for (int i = 0; i < archers.Length; i++)
        {
            archers[i] = new UnityEngine.Object();
            Check(deck.Reserve(archers[i], 1, 6) && deck.BoardedIndex(archers[i]) == -1, "shore reservation does not occupy a deck position");
        }
        Check(!deck.Reserve(new UnityEngine.Object(), 1, 6), "the seventh archer cannot board");
        for (int i = 0; i < archers.Length; i++)
            Check(deck.Board(archers[i]) && deck.BoardedIndex(archers[i]) == i, "boarded archers occupy distinct positions");
        deck.Release(archers[1]);
        Check(deck.BoardedIndex(archers[1]) == -1 && deck.BoardedIndex(archers[2]) == 1, "a vacant deck position is available after loss");
        Console.WriteLine("PASS: mixed army seat costs, competing reservations, FIFO boarding/exit, cancellation, deaths and saved passenger references");
    }
}
