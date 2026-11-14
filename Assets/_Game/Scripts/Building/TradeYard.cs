using System;
using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;
using Object = UnityEngine.Object;

[RequireComponent(typeof(GettingResources))]
public sealed class TradeYard : RecipeBuilding
{
    [Serializable]
    public sealed class Price
    {
        public ResourceType resource;
        [Min(1)] public int buyPrice = 2;
        [Min(1)] public int sellPrice = 1;
    }

    [SerializeField, Min(1)] private int storageCapacity = 200;
    [SerializeField] private List<Price> prices = new List<Price>
    {
        new Price { resource = ResourceType.Wood },
        new Price { resource = ResourceType.Stone, buyPrice = 3 },
        new Price { resource = ResourceType.Iron, buyPrice = 5, sellPrice = 3 },
        new Price { resource = ResourceType.Wheat },
        new Price { resource = ResourceType.Bread, buyPrice = 3, sellPrice = 2 },
        new Price { resource = ResourceType.Food, buyPrice = 3, sellPrice = 2 },
        new Price { resource = ResourceType.Sword, buyPrice = 8, sellPrice = 5 },
        new Price { resource = ResourceType.Bow, buyPrice = 6, sellPrice = 4 }
    };

    private readonly CargoHold stock = new CargoHold();
    private ResourceType commodity;
    private bool buying;
    private int amount;
    private int gold;
    private float progress;
    private string status = "Нет активной сделки";

    public IReadOnlyList<Price> Prices => prices;
    public bool HasOrder => amount > 0;
    public bool CanControl => isActiveAndEnabled && FactionRules.IsControllable(FactionMember.Of(gameObject));
    public int OrderAmount => amount;
    public ResourceType Commodity => commodity;
    public bool Buying => buying;
    public int RequiredAmount => buying ? gold : amount;
    public override ResourceType InputType => buying ? ResourceType.Gold : commodity;
    public override ResourceType OutputType => buying ? commodity : ResourceType.Gold;
    public override ResourcesMove.UnitRole Profession => ResourcesMove.UnitRole.Carrier;
    public override int StoredInput => HasOrder ? Mathf.Min(RequiredAmount, Storage.Amount(InputType)) : 0;
    public override int StoredOutput => CollectableOutput(null);
    public override float WorkProgress => progress;
    private CargoHold Storage => stock.Initialize(GetComponent<GettingResources>(), Mathf.Max(1, storageCapacity));
    private int OutputAmount => buying ? amount : gold;

    public override int RequestedInput => HasOrder && CanControl && BuildingRoadAccess.Allows(this)
        ? Mathf.Min(Storage.FreeSpace, Mathf.Max(0, RequiredAmount - StoredInput - Storage.Incoming(InputType))) : 0;

    public override bool CanProduce => HasOrder && CanControl && BuildingRoadAccess.Allows(this) &&
        StoredInput >= RequiredAmount && Storage.FreeSpace + RequiredAmount >= OutputAmount;

    public override string WaitingTask
    {
        get
        {
            if (!BuildingRoadAccess.Allows(this)) return "Нет дороги к торговому двору";
            if (!HasOrder) return status;
            if (StoredInput < RequiredAmount) return buying ? "Ждёт доставки золота" : "Ждёт доставки товара";
            return CanProduce ? "Оформляет сделку" : "Ждёт вывоза запасов";
        }
    }

    public bool TryQuote(ResourceType resource, out int buyPrice, out int sellPrice)
    {
        buyPrice = 0;
        sellPrice = 0;
        if (resource == ResourceType.Gold || !Enum.IsDefined(typeof(ResourceType), resource)) return false;
        foreach (var price in prices)
        {
            if (price == null || price.resource != resource) continue;
            buyPrice = Mathf.Max(1, price.buyPrice);
            sellPrice = Mathf.Clamp(price.sellPrice, 1, buyPrice);
            return true;
        }
        return false;
    }

    public int MaxAmount(ResourceType resource, bool buy)
        => TryQuote(resource, out int buyPrice, out int sellPrice)
            ? Storage.Capacity / (buy ? buyPrice : sellPrice) : 0;

    public bool BeginTrade(ResourceType resource, int quantity, bool buy)
    {
        if (!CanControl || HasOrder || !BuildingRoadAccess.Allows(this) || quantity <= 0 ||
            !TryQuote(resource, out int buyPrice, out int sellPrice)) return false;
        int price = buy ? buyPrice : sellPrice;
        if (quantity > Storage.Capacity / price) return false;

        commodity = resource;
        buying = buy;
        amount = quantity;
        gold = quantity * price;
        progress = 0;
        return true;
    }

    public void CancelTrade()
    {
        if (CanControl && HasOrder) EndTrade("Сделка отменена. Носильщики вернут доставленный груз");
    }

    private void EndTrade(string message)
    {
        stock.ClearReservations();
        amount = gold = 0;
        progress = 0;
        status = message;
    }

    public override bool ReserveDelivery(Object owner, int count) => ReserveDelivery(owner, InputType, count);
    public override bool ReserveDelivery(Object owner, ResourceType type, int count)
        => type == InputType && count > 0 && count <= RequestedInput &&
           (!(owner is MonoBehaviour carrier) || FactionMember.CanUse(carrier.gameObject, gameObject)) &&
           Storage.ReserveIncoming(owner, type, count);

    public override int Receive(Object owner, GettingResources cargo)
    {
        if (owner == null) return 0;
        if (CanControl && BuildingRoadAccess.Allows(this) &&
            (!(owner is MonoBehaviour carrier) || FactionMember.CanUse(carrier.gameObject, gameObject)))
            return Storage.Receive(owner, cargo);
        CancelDelivery(owner);
        return 0;
    }

    public override void CancelDelivery(Object owner)
    {
        if (!ReferenceEquals(owner, null)) stock.CancelIncoming(owner);
    }

    private int Collectable(ResourceType type)
        => Mathf.Max(0, Storage.Amount(type) - (HasOrder && type == InputType ? RequiredAmount : 0));

    public override int CollectableOutput(WarehouseStorage destination)
    {
        int count = 0;
        foreach (var type in WarehouseStorage.Types)
            if (destination == null || destination.Accepts(type)) count += Collectable(type);
        return count;
    }

    public override int Collect(GettingResources cargo, int capacity, WarehouseStorage destination = null)
    {
        var inventory = GetComponent<GettingResources>();
        if (cargo == null || ReferenceEquals(cargo, inventory)) return 0;
        int moved = 0;
        foreach (var type in WarehouseStorage.Types)
        {
            if (moved >= capacity) break;
            if (destination != null && !destination.Accepts(type)) continue;
            int count = Mathf.Min(capacity - moved, Collectable(type));
            inventory.RemoveStoredAmount(type, count);
            cargo.AddStoredAmount(type, count);
            moved += count;
        }
        return moved;
    }

    private void Update()
    {
        if (!SaveGameManager.IsLoading) Work(Time.deltaTime);
    }

    public override void Work(float seconds)
    {
        if (seconds <= 0 || !CanProduce) return;
        progress += seconds;
        if (progress < Mathf.Max(.1f, secondsPerBatch)) return;

        var inventory = GetComponent<GettingResources>();
        inventory.RemoveStoredAmount(InputType, RequiredAmount);
        inventory.AddStoredAmount(OutputType, OutputAmount);
        EndTrade("Сделка завершена. Носильщики заберут груз");
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        stock.ClearReservations();
    }
}
