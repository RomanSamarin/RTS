using System;
using _Game.Scripts.UI;

// Unity движение проверяется в сцене; здесь настоящая логика запасов и рецептов.
public static class RecipeChecks
{
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); }
    public static void Run()
    {
        var mill = new Mill { inputCapacity = 2, outputCapacity = 1, secondsPerBatch = 5 };
        var bakery = new Bakery { inputCapacity = 2, outputCapacity = 2, secondsPerBatch = 3 };
        var stock = new WarehouseStorage();
        stock.AddResource(ResourceType.Wheat, 2);
        var carrier = new UnityEngine.Object();
        var other = new UnityEngine.Object();
        var pack = new GettingResources();
        mill.Work(100);
        Check(mill.StoredOutput == 0 && mill.WorkProgress == 0, "no remote wheat or idle time banking");
        Check(stock.ReserveMaterial(carrier, ResourceType.Wheat, 2) && mill.ReserveDelivery(carrier, 2), "reserve source and input slots");
        Check(!mill.ReserveDelivery(other, 1) && !stock.ReserveMaterial(other, ResourceType.Wheat, 1), "no double booking");
        stock.PickupMaterial(carrier, ResourceType.Wheat, 2, pack);
        mill.Work(10);
        Check(mill.StoredOutput == 0 && pack.GetStoredAmount(ResourceType.Wheat) == 2, "in transit cannot produce");
        Check(mill.Receive(carrier, pack) == 2 && mill.Receive(carrier, pack) == 0, "delivery once only");
        mill.Work(2);
        Check(mill.StoredOutput == 0 && mill.StoredInput == 2, "batch requires time");
        mill.Work(3);
        mill.Work(100);
        Check(mill.StoredOutput == 1 && mill.StoredInput == 1, "full output stops without wasting input");
        Check(mill.Collect(pack, 1) == 1 && mill.StoredOutput == 0, "flour enters carrier backpack");
        Check(bakery.StoredInput == 0 && stock.GetAmount(ResourceType.Flour) == 0, "no teleport between stages");
        stock.Deposit(pack);
        Check(stock.ReserveMaterial(carrier, ResourceType.Flour, 1) && bakery.ReserveDelivery(carrier, 1), "second stage reserves real flour");
        stock.PickupMaterial(carrier, ResourceType.Flour, 1, pack);
        bakery.Receive(carrier, pack);
        bakery.Work(3);
        Check(bakery.StoredInput == 0 && bakery.StoredOutput == 1, "flour becomes bread");
        bakery.Collect(pack, 10);
        stock.Deposit(pack);
        Check(stock.Food == 1 && stock.GetAmount(ResourceType.Flour) == 0, "only finished bread is food");
        mill.Work(5);
        Check(mill.StoredInput == 0 && mill.StoredOutput == 1, "production resumes after pickup");
        Check(mill.ReserveDelivery(other, 2), "cancel setup");
        mill.CancelDelivery(other);
        Check(mill.RequestedInput == 2, "cancellation frees input space");
        Check(mill.ReserveDelivery(other, 2), "death setup");
        other.destroyed = true;
        Check(mill.RequestedInput == 2, "dead carrier releases input reserve");
        var batch = new Bakery { inputPerBatch = 2, outputPerBatch = 3, outputCapacity = 3 };
        pack.Set(ResourceType.Flour, 2);
        batch.ReserveDelivery(carrier, 2); batch.Receive(carrier, pack); batch.Work(5); batch.Work(5);
        Check(batch.StoredInput == 0 && batch.StoredOutput == 3, "multiple worker ticks cannot duplicate last batch");
        Console.WriteLine("PASS: recipes, physical transfers, capacity, cancellation and competing reservations");
    }
}
public class ResourcesMove
{
    public enum UnitRole { Miller, Baker, Hunter, BerryGatherer, Blacksmith, Craftsman, Villager, Horse, Smoker, Shipwright, PortWorker, Carrier }
    public bool isWorking;
    public bool CanPerformWork = true;
}
public class WorkplaceHiring { public ResourcesMove.UnitRole profession; public int maxWorkers; }
