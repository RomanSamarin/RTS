using System;
using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;
using Object = UnityEngine.Object;

/// <summary>Несколько рецептов, локальные запасы по видам, конечная очередь и повтор выбранного рецепта.</summary>
public abstract class EquipmentProductionBuilding : RecipeBuilding
{
    [Serializable]
    public class Stock
    {
        public ResourceType type;
        [Min(0)] public int amount;
        public Stock(ResourceType type, int amount) { this.type = type; this.amount = amount; }
    }
    [Serializable]
    public class Recipe
    {
        public string title;
        public ResourceType output;
        public bool producesUnit;
        public bool producesLadderCart;
        public bool producesRam;
        public ShipType shipType;
        [Min(1)] public int quantity = 1;
        [Min(0.1f)] public float seconds = 30f;
        public List<Stock> ingredients = new List<Stock>();
    }
    [Tooltip("На входе Input Capacity ограничивает каждый вид сырья; Output Capacity — весь готовый груз.")]
    public bool repeatSelectedRecipe = true;
    [SerializeField] private int selectedRecipe;
    [SerializeField] private List<Recipe> recipes = new List<Recipe>();
    [SerializeField] private List<Stock> inputStock = new List<Stock>();
    [SerializeField] private List<Stock> outputStock = new List<Stock>();
    [SerializeField] private List<int> productionQueue = new List<int>();
    [SerializeField] private List<int> queueOrderIds = new List<int>();
    [SerializeField] private int nextOrderId;
    [SerializeField] private float progress;
    private struct Delivery { public ResourceType type; public int amount; }
    private readonly Dictionary<Object, Delivery> deliveries = new Dictionary<Object, Delivery>();
    protected abstract List<Recipe> DefaultRecipes();
    protected virtual void ConfigureDefaults() { }
    protected virtual void PrepareRecipes(List<Recipe> values) { }
    private void EnsureRecipes() { if (recipes.Count == 0) { ConfigureDefaults(); recipes = DefaultRecipes(); } PrepareRecipes(recipes); }
    private void Awake() { EnsureRecipes(); }
    private void OnValidate() { EnsureRecipes(); }
    public IReadOnlyList<Recipe> Recipes { get { EnsureRecipes(); return recipes; } }
    public int QueueCount => productionQueue.Count;
    public int SelectedRecipe => selectedRecipe;
    public struct QueueItem
    {
        public int orderId;
        public Recipe recipe;
        public bool active, repeating;
        public float progress;
    }
    private int NewOrderId()
    {
        unchecked { nextOrderId++; if (nextOrderId <= 0) nextOrderId = 1; }
        while (queueOrderIds.Contains(nextOrderId)) nextOrderId++;
        return nextOrderId;
    }
    private void EnsureQueueIds()
    {
        if (queueOrderIds.Count == productionQueue.Count) return;
        queueOrderIds.Clear();
        foreach (var unused in productionQueue) queueOrderIds.Add(NewOrderId());
    }
    public List<QueueItem> GetQueueSnapshot()
    {
        EnsureRecipes(); EnsureQueueIds();
        var result = new List<QueueItem>();
        for (int i = 0; i < productionQueue.Count; i++)
        {
            int index = productionQueue[i];
            result.Add(new QueueItem { orderId = queueOrderIds[i],
                recipe = index >= 0 && index < recipes.Count ? recipes[index] : null,
                active = i == 0, progress = i == 0 ? progress : 0f });
        }
        if (result.Count == 0 && repeatSelectedRecipe && ActiveRecipe != null)
            result.Add(new QueueItem { recipe = ActiveRecipe, active = true, repeating = true, progress = progress });
        return result;
    }
    public bool CancelOrder(int orderId)
    {
        EnsureQueueIds();
        int index = queueOrderIds.IndexOf(orderId);
        if (index < 0) return false;
        productionQueue.RemoveAt(index); queueOrderIds.RemoveAt(index);
        if (index == 0) progress = 0;
        return true;
    }
    public Recipe ActiveRecipe
    {
        get
        {
            EnsureRecipes();
            int index = productionQueue.Count > 0 ? productionQueue[0] : repeatSelectedRecipe ? selectedRecipe : -1;
            return index >= 0 && index < recipes.Count ? recipes[index] : null;
        }
    }
    public string CurrentRecipeName => ActiveRecipe != null ? ActiveRecipe.title : "Нет заказа";
    public override ResourceType InputType => InputTypes.Count > 0 ? InputTypes[0] : ResourceType.Iron;
    public override ResourceType OutputType => ActiveRecipe != null ? ActiveRecipe.output : ResourceType.Sword;
    public override IReadOnlyList<ResourceType> InputTypes => new List<ResourceType>(Ingredients(ActiveRecipe).Keys);
    public override int StoredInput => Total(inputStock);
    public override int StoredOutput => Total(outputStock);
    public override float WorkProgress => progress;
    public int GetInputAmount(ResourceType type) => Amount(inputStock, type);
    public int GetOutputAmount(ResourceType type) => Amount(outputStock, type);
    public override int RequestedInput
    {
        get { int amount = 0; foreach (var type in InputTypes) amount += RequestedInputFor(type); return amount; }
    }
    public override int RequestedInputFor(ResourceType type)
    {
        if (!Ingredients(ActiveRecipe).ContainsKey(type)) return 0;
        return Mathf.Max(0, inputCapacity - GetInputAmount(type) - Reserved(type));
    }
    private bool HasIngredients
    {
        get
        {
            var required = Ingredients(ActiveRecipe);
            if (ActiveRecipe == null || required.Count == 0) return false;
            foreach (var item in required) if (GetInputAmount(item.Key) < item.Value) return false;
            return true;
        }
    }
    public override bool CanProduce => isActiveAndEnabled && BuildingRoadAccess.Allows(this) && ActiveRecipe != null && ActiveRecipe.quantity > 0
        && HasIngredients && CanAcceptOutput(ActiveRecipe);
    protected virtual bool CanAcceptOutput(Recipe recipe)=>!recipe.producesUnit&&outputCapacity-StoredOutput>=recipe.quantity;
    protected virtual bool TryFinishOutput(Recipe recipe)
    {if(recipe.producesUnit)return false;Add(outputStock,recipe.output,recipe.quantity);return true;}
    public override string WaitingTask => !BuildingRoadAccess.Allows(this)?"Нет дороги к зданию":ActiveRecipe == null ? "Нет заказа" : !HasIngredients ? "Ждёт сырьё" : "Ждёт вывоза продукции";
    public bool SelectRecipe(int index)
    {
        EnsureRecipes();
        if (index < 0 || index >= recipes.Count) return false;
        if (productionQueue.Count == 0 && selectedRecipe != index) progress = 0;
        selectedRecipe = index; return true;
    }
    // Подходит для Button.OnClick с целочисленным индексом рецепта.
    public void SetRecipe(int index) { SelectRecipe(index); }
    public void SetRepeat(bool value)
    {
        if (productionQueue.Count == 0 && repeatSelectedRecipe != value) progress = 0;
        repeatSelectedRecipe = value;
    }
    public bool Enqueue(int index)
    {
        EnsureRecipes();
        if (index < 0 || index >= recipes.Count || productionQueue.Count >= 1000) return false;
        EnsureQueueIds();
        if (productionQueue.Count == 0 && (!repeatSelectedRecipe || selectedRecipe != index)) progress = 0;
        productionQueue.Add(index); queueOrderIds.Add(NewOrderId()); return true;
    }
    public void QueueRecipe(int index) { Enqueue(index); }
    public void ClearQueue() { productionQueue.Clear(); queueOrderIds.Clear(); progress = 0; }
    public override bool ReserveDelivery(Object owner, int amount) => ReserveDelivery(owner, InputType, amount);
    public override bool ReserveDelivery(Object owner, ResourceType type, int amount)
    {
        if (owner == null || amount <= 0 || deliveries.ContainsKey(owner) || RequestedInputFor(type) < amount) return false;
        deliveries.Add(owner, new Delivery { type = type, amount = amount }); return true;
    }
    public override void CancelDelivery(Object owner) { deliveries.Remove(owner); }
    protected int Reserved(ResourceType type)
    {
        int amount = 0; var expired = new List<Object>();
        foreach (var item in deliveries)
            if (item.Key == null) expired.Add(item.Key); else if (item.Value.type == type) amount += item.Value.amount;
        foreach (var owner in expired) deliveries.Remove(owner);
        return amount;
    }
    public override int Receive(Object owner, GettingResources cargo)
    {
        if (cargo == null || !deliveries.TryGetValue(owner, out var delivery)) return 0;
        deliveries.Remove(owner);
        // Рецепт мог смениться в пути; уже согласованное сырьё всё равно принимается.
        int amount = Mathf.Min(delivery.amount, Mathf.Min(cargo.GetStoredAmount(delivery.type),
            Mathf.Max(0, inputCapacity - GetInputAmount(delivery.type) - Reserved(delivery.type))));
        cargo.RemoveStoredAmount(delivery.type, amount); Add(inputStock, delivery.type, amount); return amount;
    }
    public override int CollectableOutput(WarehouseStorage destination)
    {
        int amount = 0;
        foreach (var item in outputStock)
            if (destination == null || destination.Accepts(item.type)) amount += item.amount;
        return amount;
    }

    public override int Collect(GettingResources cargo, int capacity, WarehouseStorage destination = null)
    {
        if (cargo == null) return 0;
        int taken = 0;
        foreach (var item in outputStock)
        {
            if (destination != null && !destination.Accepts(item.type)) continue;
            int amount = Mathf.Min(item.amount, Mathf.Max(0, capacity - taken));
            item.amount -= amount; cargo.AddStoredAmount(item.type, amount); taken += amount;
        }
        return taken;
    }
    public override void Work(float seconds)
    {
        if (!CanProduce || seconds <= 0) return;
        EnsureQueueIds();
        progress += seconds;
        while (CanProduce)
        {
            var recipe = ActiveRecipe;
            float duration = Mathf.Max(0.1f, recipe.seconds);
            if (progress < duration) break;
            if(!TryFinishOutput(recipe)){progress=duration;return;}
            foreach (var item in Ingredients(recipe)) Add(inputStock, item.Key, -item.Value);
            progress -= duration;
            if (productionQueue.Count > 0) { productionQueue.RemoveAt(0); queueOrderIds.RemoveAt(0); }
        }
        if (!CanProduce) progress = 0;
    }
    protected override void OnDisable() { base.OnDisable(); deliveries.Clear(); }
    protected bool TryConsumeInputs(IReadOnlyDictionary<ResourceType,int> required)
    {
        if(required==null)return false;
        foreach(var item in required)if(item.Value<0||GetInputAmount(item.Key)<item.Value)return false;
        foreach(var item in required)Add(inputStock,item.Key,-item.Value);
        return true;
    }
    private static Dictionary<ResourceType, int> Ingredients(Recipe recipe)
    {
        var result = new Dictionary<ResourceType, int>();
        if (recipe == null || recipe.ingredients == null) return result;
        foreach (var item in recipe.ingredients)
        {
            if (item == null || item.amount <= 0) continue;
            result.TryGetValue(item.type, out int amount); result[item.type] = amount + item.amount;
        }
        return result;
    }
    private static int Amount(List<Stock> stock, ResourceType type)
    { int sum = 0; foreach (var item in stock) if (item.type == type) sum += item.amount; return sum; }
    private static int Total(List<Stock> stock)
    { int sum = 0; foreach (var item in stock) sum += item.amount; return sum; }
    private static void Add(List<Stock> stock, ResourceType type, int amount)
    {
        if (amount == 0) return;
        foreach (var item in stock) if (item.type == type) { item.amount += amount; return; }
        stock.Add(new Stock(type, amount));
    }
}
