using System;
using UnityEngine;
using _Game.Scripts.UI;

// Адаптер появления в Unity. Проверяются настоящие очередь, доставка и транзакция Workshop.
public static class CatapultFactory
{
    public static bool SpawnAllowed;
    public static int SpawnCount;
    public static bool Valid(GameObject prefab)=>true;
    public static bool TrySpawn(GameObject prefab,GameObject workshop,Transform point)
    {if(!SpawnAllowed)return false;SpawnCount++;return true;}
}
public static class RamFactory
{
    public static bool SpawnAllowed;
    public static int SpawnCount;
    public static bool Valid(GameObject prefab) => true;
    public static bool TrySpawn(GameObject prefab, GameObject workshop, Transform point)
    {
        if (!SpawnAllowed) return false;
        SpawnCount++;
        return true;
    }
}
public static class SiegeChecks
{
    private static void Check(bool value,string reason){if(!value)throw new Exception("Siege: "+reason);}
    private static void Deliver(Workshop workshop,ResourceType type,int amount)
    {
        var carrier=new UnityEngine.Object();var cargo=new GettingResources();cargo.Set(type,amount);
        Check(workshop.ReserveDelivery(carrier,type,amount),"reserve physical assembly input");
        Check(workshop.Receive(carrier,cargo)==amount&&cargo.GetStoredAmount(type)==0,"deliver actual assembly materials once");
    }
    public static void Run()
    {
        Check(!SiegeRules.InRange(5,8,40)&&SiegeRules.InRange(8,8,40)&&SiegeRules.InRange(40,8,40)&&!SiegeRules.InRange(41,8,40),"minimum and maximum fire range");
        Check(SiegeRules.Splash(0,3)==1&&SiegeRules.Splash(3,3)==0&&SiegeRules.Splash(6,3)==0,"splash cannot damage beyond radius");
        Time.time=100;CatapultFactory.SpawnAllowed=false;CatapultFactory.SpawnCount=0;
        var shop=new Workshop();shop.SetRepeat(false);shop.QueueCatapult();
        Check(shop.Recipes.Count==6&&shop.Recipes.Count==6&&shop.ActiveRecipe.producesUnit&&!shop.ActiveRecipe.producesLadderCart&&!shop.ActiveRecipe.producesRam,"workshop appends ram without replacing other siege recipes");
        shop.Work(100);Check(CatapultFactory.SpawnCount==0,"no machine without delivered resources");
        Deliver(shop,ResourceType.Wood,30);Deliver(shop,ResourceType.Iron,10);Deliver(shop,ResourceType.Stone,5);
        shop.Work(89);Check(CatapultFactory.SpawnCount==0&&shop.GetInputAmount(ResourceType.Wood)==30,"cannot consume materials before full build time");
        shop.Work(1);Check(shop.WorkProgress==90&&shop.QueueCount==1&&shop.GetInputAmount(ResourceType.Iron)==10,"blocked exit preserves paid progress, queue and resources");
        CatapultFactory.SpawnAllowed=true;Time.time+=2;shop.Work(.1f);
        Check(CatapultFactory.SpawnCount==1&&shop.QueueCount==0&&shop.StoredOutput==0,"machine appears once rather than entering carrier cargo");
        Check(shop.GetInputAmount(ResourceType.Wood)==0&&shop.GetInputAmount(ResourceType.Iron)==0&&shop.GetInputAmount(ResourceType.Stone)==0,"successful spawn consumes recipe once");
        shop.Work(100);Check(CatapultFactory.SpawnCount==1,"empty finite queue cannot spawn again");
        var cancel=new Workshop();cancel.SetRepeat(false);cancel.QueueCatapult();
        Deliver(cancel,ResourceType.Wood,30);Deliver(cancel,ResourceType.Iron,10);Deliver(cancel,ResourceType.Stone,5);
        CatapultFactory.SpawnAllowed=false;Time.time+=2;cancel.Work(90);
        Check(cancel.CancelOrder(cancel.GetQueueSnapshot()[0].orderId)&&cancel.WorkProgress==0&&cancel.GetInputAmount(ResourceType.Wood)==30,"cancel preserves unused materials after blocked assembly");
        CatapultFactory.SpawnAllowed=true;
        var ramShop = new Workshop();
        ramShop.SetRepeat(false);
        ramShop.QueueRam();
        Check(ramShop.ActiveRecipe.producesRam && ramShop.SiegeLadderRecipeIndex == 3, "ram does not shift existing recipe indices");
        Deliver(ramShop, ResourceType.Wood, 25);
        Deliver(ramShop, ResourceType.Iron, 6);
        Time.time += 2;
        ramShop.Work(60);
        Check(ramShop.WaitingForRamExit && ramShop.GetInputAmount(ResourceType.Wood) == 25, "blocked ram exit preserves the order and stock");
        RamFactory.SpawnAllowed = true;
        Time.time += 2;
        ramShop.Work(1);
        ramShop.Work(100);
        Check(RamFactory.SpawnCount == 1 && ramShop.QueueCount == 0 && ramShop.GetInputAmount(ResourceType.Iron) == 0,
            "ram spawns once and consumes physically delivered materials");
        Console.WriteLine("PASS: catapult recipe migration, physical materials, finite queue, blocked exit, one spawn, cancellation and range/splash");
    }
}
