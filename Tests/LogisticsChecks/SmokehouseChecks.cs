using System;
using _Game.Scripts.UI;

public static class SmokehouseChecks
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        var stock=new WarehouseStorage();stock.AddResource(ResourceType.Fish,4);stock.AddResource(ResourceType.Meat,4);
        var a=new UnityEngine.Object();var b=new UnityEngine.Object();var c=new UnityEngine.Object();
        Check(stock.Food==0&&stock.AvailableFood==0&&!stock.TryReserveFood(a,1),"raw fish and meat cannot feed settlement or wagon");
        var smoker=new Smokehouse{inputCapacity=4,outputCapacity=2,secondsPerBatch=5};
        var pack=new GettingResources();
        Check(smoker.ReserveDelivery(a,ResourceType.Fish,2)&&smoker.ReserveDelivery(b,ResourceType.Meat,2),"alternate ingredients share capacity");
        Check(!smoker.ReserveDelivery(c,ResourceType.Meat,1)&&!smoker.ReserveDelivery(c,ResourceType.Flour,1),"no overbooking or invalid input");
        Check(stock.ReserveMaterial(a,ResourceType.Fish,2)&&stock.ReserveMaterial(b,ResourceType.Meat,2),"reserve real raw materials");
        stock.PickupMaterial(a,ResourceType.Fish,2,pack);stock.PickupMaterial(b,ResourceType.Meat,2,pack);
        Check(pack.Food==0,"raw cargo is not a meal");smoker.Work(100);Check(smoker.StoredOutput==0,"no production before delivery");
        Check(smoker.Receive(b,pack)==2&&pack.Fish==2&&pack.Meat==0,"meat delivery consumes only its reserved kind");
        Check(smoker.Receive(a,pack)==2&&smoker.Receive(a,pack)==0,"fish delivery happens once");
        smoker.Work(4);Check(smoker.StoredOutput==0,"cooking requires worker time");
        smoker.Work(6);Check(smoker.StoredOutput==2&&smoker.StoredInput==2,"full output preserves remaining raw input");
        Check(smoker.Collect(pack,1)==1&&pack.PreparedFood==1,"cooked food enters carrier backpack");
        stock.Deposit(pack);Check(stock.Food==1&&stock.GetStoredAmount(ResourceType.Fish)==2,"only physically returned cooked portion is edible");
        smoker.Work(5);Check(smoker.StoredOutput==2&&smoker.StoredInput==1,"production resumes after pickup");
        var cancelled=new Smokehouse{inputCapacity=1};
        Check(cancelled.ReserveDelivery(a,ResourceType.Meat,1),"meat reservation setup");cancelled.CancelDelivery(a);
        pack.Fish=1;Check(cancelled.ReserveDelivery(a,1)&&cancelled.Receive(a,pack)==1,"cancel clears old ingredient kind");
        Console.WriteLine("PASS: raw food exclusion, alternative smokehouse inputs, physical transport, cooking time and competing reservations");
    }
}
