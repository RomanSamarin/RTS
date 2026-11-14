using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

/// <summary>Локальные запасы рецепта. Только доставка пополняет сырьё, только рабочий запускает переработку.</summary>
[DisallowMultipleComponent]
public abstract class RecipeBuilding : MonoBehaviour
{
    private static readonly List<RecipeBuilding> buildings = new List<RecipeBuilding>();
    public static IReadOnlyList<RecipeBuilding> All => buildings;
    public abstract ResourceType InputType { get; }
    public abstract ResourceType OutputType { get; }
    public abstract ResourcesMove.UnitRole Profession { get; }
    public virtual IReadOnlyList<ResourceType> InputTypes => new[] { InputType };
    public virtual int AvailableSupply(WarehouseStorage warehouse, ResourceType type)
        =>warehouse!=null&&FactionMember.CanUse(gameObject,warehouse.gameObject)?warehouse.AvailableMaterial(type):0;
    public virtual bool ReserveSupply(Object carrier, WarehouseStorage warehouse, ResourceType type, int amount)
        => AvailableSupply(warehouse,type)>=amount&&warehouse.ReserveMaterial(carrier, type, amount);
    public virtual int PickupSupply(Object carrier, WarehouseStorage warehouse, ResourceType type, int amount, GettingResources cargo)
        =>warehouse!=null&&FactionMember.CanUse(gameObject,warehouse.gameObject)?warehouse.PickupMaterial(carrier, type, amount, cargo):0;
    public virtual void ReleaseSupply(Object carrier, WarehouseStorage warehouse) { if (warehouse != null) warehouse.ReleaseMaterials(carrier); }
    private bool AcceptsInput(ResourceType type)
    { foreach(var input in InputTypes)if(input==type)return true;return false; }
    public virtual int RequestedInputFor(ResourceType type) => AcceptsInput(type) ? RequestedInput : 0;
    public virtual bool ReserveDelivery(Object owner, ResourceType type, int amount)
    {
        if(!AcceptsInput(type)||!ReserveDelivery(owner, amount))return false;
        incomingTypes[owner]=type;return true;
    }
    public virtual string WaitingTask => !BuildingRoadAccess.Allows(this)?"Нет дороги к зданию":StoredInput < Mathf.Max(1, inputPerBatch) ? "Ждёт сырьё" : "Ждёт вывоза продукции";
    [Min(1)] public int inputPerBatch = 1;
    [Min(1)] public int outputPerBatch = 1;
    [Min(0.1f)] public float secondsPerBatch = 5f;
    [Min(1)] public int inputCapacity = 30;
    [Min(1)] public int outputCapacity = 30;
    [Tooltip("Необязательно: склад, с которого доставляют сырьё.")]
    public WarehouseStorage supplyWarehouse;
    [SerializeField, Min(0)] private int storedInput;
    [SerializeField, Min(0)] private int storedOutput;
    [SerializeField] private float workProgress;
    private readonly Dictionary<Object, int> incoming = new Dictionary<Object, int>();
    private readonly Dictionary<Object, ResourceType> incomingTypes = new Dictionary<Object, ResourceType>();
    public virtual int StoredInput => storedInput;
    public virtual int StoredOutput => storedOutput;
    public virtual float WorkProgress => workProgress;
    public virtual bool CanProduce => BuildingRoadAccess.Allows(this) && storedInput >= Mathf.Max(1, inputPerBatch)
        && outputCapacity - storedOutput >= Mathf.Max(1, outputPerBatch);
    public virtual int RequestedInput
    {
        get
        {
            int reserved = 0;
            var expired = new List<Object>();
            foreach (var pair in incoming)
                if (pair.Key == null) expired.Add(pair.Key); else reserved += pair.Value;
            foreach (var owner in expired) {incoming.Remove(owner);incomingTypes.Remove(owner);}
            return Mathf.Max(0, inputCapacity - storedInput - reserved);
        }
    }
    protected virtual void OnEnable() { if (!buildings.Contains(this)) buildings.Add(this); }
    protected virtual void OnDisable() { buildings.Remove(this); incoming.Clear(); incomingTypes.Clear(); }
    public virtual bool ReserveDelivery(Object owner, int amount)
    {
        if (owner == null || amount <= 0 || incoming.ContainsKey(owner) || RequestedInput < amount) return false;
        incoming.Add(owner, amount); return true;
    }
    public virtual void CancelDelivery(Object owner) { incoming.Remove(owner); incomingTypes.Remove(owner); }
    public virtual int Receive(Object owner, GettingResources cargo)
    {
        if (cargo == null || !incoming.TryGetValue(owner, out int reserved)) return 0;
        var input=incomingTypes.TryGetValue(owner,out var reservedType)?reservedType:InputType;
        incoming.Remove(owner);
        incomingTypes.Remove(owner);
        int amount = Mathf.Min(reserved, Mathf.Min(cargo.GetStoredAmount(input), RequestedInput));
        cargo.RemoveStoredAmount(input, amount);
        storedInput += amount;
        return amount;
    }
    public virtual int CollectableOutput(WarehouseStorage destination)
        => destination == null || destination.Accepts(OutputType) ? StoredOutput : 0;

    public virtual int Collect(GettingResources cargo, int capacity, WarehouseStorage destination = null)
    {
        if (cargo == null || (destination != null && !destination.Accepts(OutputType))) return 0;
        int amount = Mathf.Min(storedOutput, Mathf.Max(0, capacity));
        storedOutput -= amount;
        cargo.AddStoredAmount(OutputType, amount);
        return amount;
    }
    // Время вносит каждый присутствующий работник. Сырьё и результат меняются атомарно.
    public virtual void Work(float seconds)
    {
        if (!isActiveAndEnabled || !CanProduce || seconds <= 0f) return;
        workProgress += seconds;
        float duration = Mathf.Max(0.1f, secondsPerBatch);
        while (workProgress >= duration && CanProduce)
        {
            storedInput -= Mathf.Max(1, inputPerBatch);
            storedOutput += Mathf.Max(1, outputPerBatch);
            workProgress -= duration;
        }
        if (!CanProduce) workProgress = 0f;
    }
}
