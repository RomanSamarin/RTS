using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace _Game.Scripts.UI
{
    [DisallowMultipleComponent]
    public class WarehouseStorage : MonoBehaviour
    {
        private static readonly List<WarehouseStorage> warehouses = new List<WarehouseStorage>();
        public static IReadOnlyList<WarehouseStorage> All => warehouses;
        internal static readonly ResourceType[] Types = (ResourceType[])Enum.GetValues(typeof(ResourceType));

        [SerializeField, Min(0)] private int capacity = 2000;
        [SerializeField] private bool foodOnly;
        public bool FoodOnly => foodOnly;
        internal void LimitToFood() => foodOnly = true;
        public bool Accepts(ResourceType type) => !foodOnly || IsEdible(type)
            || type == ResourceType.Wheat || type == ResourceType.Flour
            || type == ResourceType.Fish || type == ResourceType.Meat;

        public int AcceptedCargo(GettingResources cargo)
        {
            int count = 0;
            foreach (var type in Types) if (Accepts(type)) count += cargo.GetStoredAmount(type);
            return count;
        }
        [Header("Ресурсы этого склада. Prepared Food — готовая еда без отдельного вида")]
        [SerializeField, Min(0)] private int wood;
        [SerializeField, Min(0)] private int stone;
        [SerializeField, Min(0)] private int iron;
        [SerializeField, Min(0)] private int gold;
        [SerializeField, Min(0)] private int wheat;
        [SerializeField, Min(0)] private int flour;
        [SerializeField, Min(0)] private int bread;
        [FormerlySerializedAs("food"), SerializeField, Min(0)] private int preparedFood;
        [SerializeField, Min(0)] private int fish;
        [SerializeField, Min(0)] private int meat;
        [SerializeField, Min(0)] private int berries;
        [SerializeField, Min(0)] private int sword;
        [SerializeField, Min(0)] private int spear;
        [SerializeField, Min(0)] private int bow;
        [SerializeField, Min(0)] private int shield;
        [SerializeField, Min(0)] private int siegeLadder;
        public event Action StockChanged;
        public int Capacity => capacity;
        public int UsedCapacity
        {
            get
            {
                int total = 0;
                foreach (ResourceType type in Types) total += GetStoredAmount(type);
                return total;
            }
        }
        private readonly Dictionary<UnityEngine.Object, int> incoming = new Dictionary<UnityEngine.Object, int>();
        public int ReservedCapacity
        {
            get
            {
                var expired = new List<UnityEngine.Object>();
                int count = 0;
                foreach (var item in incoming)
                    if (item.Key == null) expired.Add(item.Key); else count += item.Value;
                foreach (var owner in expired) incoming.Remove(owner);
                return count;
            }
        }
        public int FreeCapacity => Mathf.Max(0, capacity - UsedCapacity - ReservedCapacity);
        public bool TryReserveSpace(UnityEngine.Object owner, int amount)
        {
            if(!BuildingRoadAccess.Allows(this))return false;
            if (owner == null || amount <= 0 || incoming.ContainsKey(owner) || FreeCapacity < amount) return false;
            incoming.Add(owner, amount);
            NotifyChanged();
            return true;
        }
        public void ReleaseSpace(UnityEngine.Object owner)
        {
            if (incoming.Remove(owner)) NotifyChanged();
        }
        public int DepositReserved(UnityEngine.Object owner, GettingResources cargo)
        {
            incoming.Remove(owner);
            return Deposit(cargo);
        }
        public int Food => preparedFood + bread + berries;
        private readonly Dictionary<UnityEngine.Object, int> outgoingFood = new Dictionary<UnityEngine.Object, int>();
        public int ReservedFood
        {
            get
            {
                var expired = new List<UnityEngine.Object>(); int total = 0;
                foreach (var item in outgoingFood)
                    if (item.Key == null) expired.Add(item.Key); else total += item.Value;
                foreach (var owner in expired) outgoingFood.Remove(owner);
                return total;
            }
        }
        private readonly Dictionary<UnityEngine.Object, Dictionary<ResourceType, int>> materials = new Dictionary<UnityEngine.Object, Dictionary<ResourceType, int>>();
        public int ReservedMaterial(ResourceType type)
        {
            int total = 0;
            foreach (var entry in materials) if (entry.Key != null && entry.Value.TryGetValue(type, out int count)) total += count;
            return total;
        }
        public int MaterialAmount(UnityEngine.Object owner, ResourceType type)
            => materials.TryGetValue(owner, out var stock) && stock.TryGetValue(type, out int count) ? count : 0;
        public int AvailableMaterial(ResourceType type)
            => Mathf.Min(GetAmount(type), Mathf.Max(0, GetStoredAmount(type) - ReservedMaterial(type)));
        public bool ReserveMaterial(UnityEngine.Object owner, ResourceType type, int amount)
        {
            if(!BuildingRoadAccess.Allows(this))return false;
            if (owner == null || amount <= 0 || AvailableMaterial(type) < amount) return false;
            if (!materials.TryGetValue(owner, out var stock)) materials[owner] = stock = new Dictionary<ResourceType, int>();
            stock[type] = MaterialAmount(owner, type) + amount;
            NotifyChanged(); return true;
        }
        public void ReleaseMaterials(UnityEngine.Object owner) { if (materials.Remove(owner)) NotifyChanged(); }
        public int ReleaseMaterial(UnityEngine.Object owner, ResourceType type, int amount)
        {
            int released = Mathf.Min(Mathf.Max(0, amount), MaterialAmount(owner, type));
            if (released > 0) { materials[owner][type] -= released; NotifyChanged(); }
            return released;
        }
        public int PickupMaterial(UnityEngine.Object owner, ResourceType type, int amount, GettingResources cargo)
        {
            if(!BuildingRoadAccess.Allows(this))return 0;
            if (cargo == null || amount <= 0) return 0;
            int taken = Mathf.Min(amount, MaterialAmount(owner, type));
            if (taken <= 0) return 0;
            materials[owner][type] -= taken;
            SetStoredAmount(type, GetStoredAmount(type) - taken);
            cargo.AddStoredAmount(type, taken);
            NotifyChanged(); return taken;
        }
        public int AvailableFood => Mathf.Max(0, Food - ReservedFood - ReservedMaterial(ResourceType.Food)
            - ReservedMaterial(ResourceType.Bread) - ReservedMaterial(ResourceType.Berries));
        public bool TryReserveFood(UnityEngine.Object owner, int amount)
        {
            if(!BuildingRoadAccess.Allows(this))return false;
            if (owner == null || amount <= 0 || outgoingFood.ContainsKey(owner) || AvailableFood < amount) return false;
            outgoingFood.Add(owner, amount); NotifyChanged(); return true;
        }
        public void ReleaseFood(UnityEngine.Object owner)
        {
            if (outgoingFood.Remove(owner)) NotifyChanged();
        }
        public int ReservedFoodFor(UnityEngine.Object owner) => outgoingFood.TryGetValue(owner, out int amount) ? amount : 0;
        public int PickupReservedFood(UnityEngine.Object owner, GettingResources cargo)
        {
            if(!BuildingRoadAccess.Allows(this))return 0;
            if (cargo == null || !outgoingFood.TryGetValue(owner, out int amount)) return 0;
            outgoingFood.Remove(owner);
            // Передаём реальные виды еды, а не создаём новую порцию.
            int remaining = Mathf.Min(amount, AvailableFood);
            int taken = remaining;
            foreach (var type in new[] { ResourceType.Food, ResourceType.Bread, ResourceType.Berries })
            {
                int count = Mathf.Min(remaining, Mathf.Max(0, GetStoredAmount(type) - ReservedMaterial(type)));
                SetStoredAmount(type, GetStoredAmount(type) - count);
                switch (type)
                {
                    case ResourceType.Food: cargo.PreparedFood += count; break;
                    case ResourceType.Bread: cargo.Bread += count; break;
                    case ResourceType.Fish: cargo.Fish += count; break;
                    case ResourceType.Meat: cargo.Meat += count; break;
                    case ResourceType.Berries: cargo.Berries += count; break;
                }
                remaining -= count;
            }
            NotifyChanged(); return taken;
        }
        private static bool IsEdible(ResourceType type) => type == ResourceType.Food || type == ResourceType.Bread
            || type == ResourceType.Berries;

        private void Awake() { ValidateStock(); }
        private void OnValidate() { ValidateStock(); }
        private void ValidateStock()
        {
            capacity = Mathf.Max(0, capacity);
            int remaining = capacity;
            foreach (ResourceType type in Types)
            {
                int count = Mathf.Clamp(GetStoredAmount(type), 0, remaining);
                SetStoredAmount(type, count);
                remaining -= count;
            }
        }

        private void OnEnable()
        {
            if (!warehouses.Contains(this)) warehouses.Add(this);
            NotifyChanged();
        }
        private void OnDisable()
        {
            incoming.Clear();
            outgoingFood.Clear();
            materials.Clear();
            warehouses.Remove(this);
            NotifyChanged();
        }

        public bool SetCapacity(int value)
        {
            if (value < UsedCapacity + ReservedCapacity || value < 0) return false;
            capacity = value;
            NotifyChanged();
            return true;
        }

        public int GetAmount(ResourceType type) => type == ResourceType.Food ? AvailableFood
            : IsEdible(type) ? Mathf.Min(Mathf.Max(0, GetStoredAmount(type) - ReservedMaterial(type)), AvailableFood)
            : Mathf.Max(0, GetStoredAmount(type) - ReservedMaterial(type));
        // Raw food excludes the separately stored food kinds; used for transfers/capacity only.
        internal int GetStoredAmount(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return wood;
                case ResourceType.Stone: return stone;
                case ResourceType.Iron: return iron;
                case ResourceType.Gold: return gold;
                case ResourceType.Wheat: return wheat;
                case ResourceType.Flour: return flour;
                case ResourceType.Bread: return bread;
                case ResourceType.Food: return preparedFood;
                case ResourceType.Fish: return fish;
                case ResourceType.Meat: return meat;
                case ResourceType.Berries: return berries;
                case ResourceType.Sword: return sword;
                case ResourceType.Spear: return spear;
                case ResourceType.Bow: return bow;
                case ResourceType.Shield: return shield;
                case ResourceType.SiegeLadder: return siegeLadder;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        private void SetStoredAmount(ResourceType type, int amount)
        {
            switch (type)
            {
                case ResourceType.Wood: wood = amount; break;
                case ResourceType.Stone: stone = amount; break;
                case ResourceType.Iron: iron = amount; break;
                case ResourceType.Gold: gold = amount; break;
                case ResourceType.Wheat: wheat = amount; break;
                case ResourceType.Flour: flour = amount; break;
                case ResourceType.Bread: bread = amount; break;
                case ResourceType.Food: preparedFood = amount; break;
                case ResourceType.Fish: fish = amount; break;
                case ResourceType.Meat: meat = amount; break;
                case ResourceType.Berries: berries = amount; break;
                case ResourceType.Sword: sword = amount; break;
                case ResourceType.Spear: spear = amount; break;
                case ResourceType.Bow: bow = amount; break;
                case ResourceType.Shield: shield = amount; break;
                case ResourceType.SiegeLadder: siegeLadder = amount; break;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        // Returns the accepted amount. The caller keeps the remainder.
        public int AddResource(ResourceType type, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!Accepts(type)) return 0;
            int accepted = Mathf.Min(amount, FreeCapacity);
            SetStoredAmount(type, checked(GetStoredAmount(type) + accepted));
            if (accepted > 0) NotifyChanged();
            return accepted;
        }

        public bool TrySpendResource(ResourceType type, int amount)
        {
            if(!BuildingRoadAccess.Allows(this))return false;
            if (amount < 0 || GetAmount(type) < amount) return false;
            RemoveAvailable(type, amount);
            return true;
        }

        internal int RemoveAvailable(ResourceType type, int amount)
        {
            int removed = Mathf.Min(amount, GetAmount(type));
            if (removed == 0) return 0;
            if (type == ResourceType.Food)
            {
                int remaining = removed;
                foreach (ResourceType foodType in new[] { ResourceType.Food, ResourceType.Bread, ResourceType.Berries })
                {
                    int take = Mathf.Min(remaining, Mathf.Max(0, GetStoredAmount(foodType) - ReservedMaterial(foodType)));
                    SetStoredAmount(foodType, GetStoredAmount(foodType) - take);
                    remaining -= take;
                }
            }
            else SetStoredAmount(type, GetStoredAmount(type) - removed);
            NotifyChanged();
            return removed;
        }

        public int Deposit(GettingResources cargo)
        {
            if(!BuildingRoadAccess.Allows(this))return 0;
            if (cargo == null) return 0;
            foreach (ResourceType type in Types)
                if (cargo.GetStoredAmount(type) < 0) throw new ArgumentOutOfRangeException(nameof(cargo));
            int transferred = 0;
            foreach (ResourceType type in Types)
            {
                if (!Accepts(type)) continue;
                int accepted = Mathf.Min(cargo.GetStoredAmount(type), FreeCapacity);
                SetStoredAmount(type, GetStoredAmount(type) + accepted);
                cargo.RemoveStoredAmount(type, accepted);
                transferred += accepted;
            }
            if (transferred > 0) NotifyChanged();
            return transferred;
        }

        private void NotifyChanged()
        {
            StockChanged?.Invoke();
            if (ResourcesManager.Instance != null) ResourcesManager.Instance.UpdateUI();
        }
    }
}
