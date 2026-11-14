using System;
using _Game.Scripts.UI;

public static class EquipmentProductionChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Deliver(RecipeBuilding building, ResourceType type, int amount)
    {
        var owner = new UnityEngine.Object(); var pack = new GettingResources(); pack.Set(type, amount);
        Check(building.ReserveDelivery(owner, type, amount), "reserve typed input");
        Check(building.Receive(owner, pack) == amount && pack.GetStoredAmount(type) == 0, "delivery drains real input");
    }
    public static void Run()
    {
        var smith = new Blacksmith();
        Check(smith.Profession == ResourcesMove.UnitRole.Blacksmith, "smith profession");
        smith.Work(100);
        Check(smith.StoredOutput == 0, "no production without delivered ingredients");
        Deliver(smith, ResourceType.Iron, 3);
        smith.Work(29);
        Check(smith.GetInputAmount(ResourceType.Iron) == 3 && smith.StoredOutput == 0, "batch requires full work time");
        smith.Work(1);
        Check(smith.GetInputAmount(ResourceType.Iron) == 0 && smith.GetOutputAmount(ResourceType.Sword) == 1, "sword costs three iron");
        smith.SelectSpear();
        Deliver(smith, ResourceType.Wood, 2);
        smith.Work(100);
        Check(!smith.CanProduce && smith.GetInputAmount(ResourceType.Wood) == 2, "spear cannot consume wood without iron");
        var stock = new WarehouseStorage(); stock.AddResource(ResourceType.Iron, 1);
        var a = new UnityEngine.Object(); var b = new UnityEngine.Object(); var pack = new GettingResources();
        Check(stock.ReserveMaterial(a, ResourceType.Iron, 1) && smith.ReserveDelivery(a, ResourceType.Iron, 1), "reserve warehouse and smith");
        Check(!stock.ReserveMaterial(b, ResourceType.Iron, 1), "second carrier cannot take same iron");
        smith.Work(25);
        Check(smith.GetOutputAmount(ResourceType.Spear) == 0, "warehouse reservation is not delivered input");
        stock.PickupMaterial(a, ResourceType.Iron, 1, pack);
        smith.Work(25);
        Check(smith.GetOutputAmount(ResourceType.Spear) == 0, "cargo in transit cannot be used");
        Check(smith.Receive(a, pack) == 1 && smith.Receive(a, pack) == 0, "typed delivery once only");
        smith.Work(25);
        Check(smith.GetOutputAmount(ResourceType.Spear) == 1 && smith.GetOutputAmount(ResourceType.Sword) == 1, "recipe change preserves output types");
        Check(smith.GetInputAmount(ResourceType.Wood) == 0 && smith.GetInputAmount(ResourceType.Iron) == 0, "all spear ingredients consumed exactly once");
        Check(smith.Collect(pack, 1) == 1 && smith.StoredOutput == 1, "output pickup respects carrier capacity");
        smith.Collect(pack, 1); stock.Deposit(pack);
        Check(stock.GetAmount(ResourceType.Sword) == 1 && stock.GetAmount(ResourceType.Spear) == 1, "real weapons delivered to warehouse");

        var changed = new Blacksmith { inputCapacity = 3 };
        Check(changed.ReserveDelivery(a, ResourceType.Iron, 3), "old recipe reserves iron");
        changed.SelectSpear(); pack.Set(ResourceType.Iron, 3);
        Check(changed.Receive(a, pack) == 3 && changed.GetInputAmount(ResourceType.Wood) == 0, "recipe change in transit does not relabel cargo");
        Check(changed.ReserveDelivery(a, ResourceType.Wood, 3) && !changed.ReserveDelivery(b, ResourceType.Wood, 1), "no input overbooking");
        changed.CancelDelivery(a);
        Check(changed.RequestedInputFor(ResourceType.Wood) == 3, "cancel frees typed input slots");
        Check(changed.ReserveDelivery(b, ResourceType.Wood, 3), "dead carrier setup"); b.destroyed = true;
        Check(changed.RequestedInputFor(ResourceType.Wood) == 3, "dead carrier frees typed slots");
        Check(!changed.SelectRecipe(99) && !changed.Enqueue(-1), "invalid recipes rejected");

        var workshop = new Workshop { outputCapacity = 1 };
        Check(workshop.Profession == ResourcesMove.UnitRole.Craftsman, "workshop profession");
        Deliver(workshop, ResourceType.Wood, 8);
        workshop.Work(25); workshop.SelectShield(); workshop.Work(100);
        Check(workshop.GetOutputAmount(ResourceType.Bow) == 1 && workshop.GetOutputAmount(ResourceType.Shield) == 0
            && workshop.GetInputAmount(ResourceType.Wood) == 5, "full shared output pauses without wasting input");
        var arrows = new GettingResources(); workshop.Collect(arrows, 1); workshop.Work(20);
        Check(workshop.GetOutputAmount(ResourceType.Shield) == 1 && workshop.GetInputAmount(ResourceType.Wood) == 3, "shield resumes after output pickup");

        var queue = new Blacksmith(); queue.SetRepeat(false);
        queue.QueueSword(); queue.QueueSpear();
        Deliver(queue, ResourceType.Iron, 4);
        queue.Work(30);
        Check(queue.QueueCount == 1 && queue.OutputType == ResourceType.Spear, "queue advances to next recipe");
        Deliver(queue, ResourceType.Wood, 2); queue.Work(25); queue.Work(100);
        Check(queue.QueueCount == 0 && queue.StoredOutput == 2 && queue.ActiveRecipe == null && queue.RequestedInput == 0, "finite queue stops requesting and producing");
        queue.QueueSword(); Deliver(queue, ResourceType.Iron, 3); queue.Work(15); queue.ClearQueue();
        Check(queue.GetInputAmount(ResourceType.Iron) == 3 && queue.StoredOutput == 2 && queue.WorkProgress == 0, "cancel uncompleted batch keeps physical resources");
        var panelQueue = new Blacksmith(); panelQueue.SetRepeat(false);
        panelQueue.QueueSword(); panelQueue.QueueSpear(); panelQueue.QueueSword();
        var before = panelQueue.GetQueueSnapshot();
        int firstId = before[0].orderId, middleId = before[1].orderId, lastId = before[2].orderId;
        Check(firstId != middleId && middleId != lastId && firstId != lastId, "queue row IDs are unique");
        Deliver(panelQueue, ResourceType.Iron, 9); panelQueue.Work(10);
        Check(panelQueue.CancelOrder(middleId) && panelQueue.WorkProgress == 10 && panelQueue.QueueCount == 2,
            "cancelling waiting row preserves active progress");
        Check(panelQueue.GetQueueSnapshot()[1].orderId == lastId, "row IDs remain stable after removal");
        panelQueue.Work(20);
        Check(!panelQueue.CancelOrder(firstId) && panelQueue.QueueCount == 1,
            "late click on completed row cannot cancel next order");
        panelQueue.Work(5);
        Check(panelQueue.CancelOrder(lastId) && panelQueue.WorkProgress == 0 && panelQueue.GetInputAmount(ResourceType.Iron) == 6,
            "cancel active row preserves unconsumed ingredients");
        Check(panelQueue.GetQueueSnapshot().Count == 0, "finite empty queue has no active row");
        panelQueue.SetRepeat(true);
        var repeated = panelQueue.GetQueueSnapshot();
        Check(repeated.Count == 1 && repeated[0].repeating && repeated[0].active, "repeat mode has explicit active row");
        panelQueue.SetRepeat(false);
        Check(panelQueue.GetQueueSnapshot().Count == 0, "disabling repeat removes synthetic row");
        panelQueue.QueueSword(); var idBeforeSnapshot = panelQueue.GetQueueSnapshot()[0].orderId;
        Check(panelQueue.GetQueueSnapshot()[0].orderId == idBeforeSnapshot, "refresh does not change order IDs");
        panelQueue.ClearQueue(); panelQueue.QueueSword();
        Check(!panelQueue.CancelOrder(idBeforeSnapshot) && panelQueue.QueueCount == 1, "cleared row cannot cancel later order");
        var ladders = new Workshop { outputCapacity = 1 };
        ladders.SetRepeat(false);
        Check(ladders.Recipes.Count == 6 && ladders.SiegeLadderRecipeIndex == 3, "ladder index preserved after adding ram");
        Check(ladders.Recipes.Count == 6, "recipe preparation does not duplicate siege equipment");
        ladders.QueueSiegeLadder(); ladders.QueueSiegeLadder();
        ladders.Work(100);
        Check(ladders.StoredOutput == 0, "ladder requires physical wood delivery");
        Deliver(ladders, ResourceType.Wood, 16);
        ladders.Work(34);
        Check(ladders.StoredOutput == 0 && ladders.GetInputAmount(ResourceType.Wood) == 16, "ladder work does not spend wood early");
        ladders.Work(1); ladders.Work(100);
        Check(ladders.GetOutputAmount(ResourceType.SiegeLadder) == 1 && ladders.QueueCount == 1
            && ladders.GetInputAmount(ResourceType.Wood) == 8, "ladder costs eight wood, full output blocks second order");
        var ladderCargo = new GettingResources();
        Check(ladders.Collect(ladderCargo, 1) == 1 && ladderCargo.GetStoredAmount(ResourceType.SiegeLadder) == 1,
            "carrier picks up real ladder from workshop");
        var ladderWarehouse = new WarehouseStorage();
        Check(ladderWarehouse.Deposit(ladderCargo) == 1 && ladderCargo.GetStoredAmount(ResourceType.SiegeLadder) == 0
            && ladderWarehouse.GetStoredAmount(ResourceType.SiegeLadder) == 1, "ladder physically delivered to warehouse");
        ladders.Work(10);
        Check(ladders.CancelOrder(ladders.GetQueueSnapshot()[0].orderId) && ladders.GetInputAmount(ResourceType.Wood) == 8,
            "cancel unfinished ladder preserves wood");

        var oldWorkshop = new Workshop();
        var oldRecipes = new System.Collections.Generic.List<EquipmentProductionBuilding.Recipe>
        {
            new EquipmentProductionBuilding.Recipe { title = "Custom bow", output = ResourceType.Bow, seconds = 42 },
            new EquipmentProductionBuilding.Recipe { title = "Old catapult", producesUnit = true }
        };
        typeof(EquipmentProductionBuilding).GetField("recipes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(oldWorkshop, oldRecipes);
        oldWorkshop.SetRepeat(false); oldWorkshop.QueueRecipe(0);
        Check(oldWorkshop.SiegeLadderRecipeIndex == 2 && oldWorkshop.Recipes[0].seconds == 42
            && oldWorkshop.GetQueueSnapshot()[0].recipe == oldRecipes[0], "existing workshop gains ladder without replacing custom recipes or queue indices");
        Check(oldWorkshop.Recipes.Count == 5 && oldWorkshop.Recipes[4].producesRam, "existing workshop gains ram once at the end");
        Console.WriteLine("PASS: equipment recipes, siege ladder production/delivery/cancellation, recipe migration, queue and capacity");
    }
}
