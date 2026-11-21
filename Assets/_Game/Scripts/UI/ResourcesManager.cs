using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace _Game.Scripts.UI
{
    public class ResourcesManager : MonoBehaviour
    {
        public static ResourcesManager Instance = null;
        [Header("Стартовый запас — распределяется по складам в пределах вместимости")]
        [SerializeField, Min(0)] private int wood;
        [Min(0)] public int stone;
        [SerializeField, Min(0)] private int iron;
        [SerializeField, Min(0)] private int gold;
        [Min(0)] public int wheat;
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

        [Header("Верхняя панель — пять ресурсов")]
        public TMP_Text woodText;
        public TMP_Text stoneText;
        [FormerlySerializedAs("wheatText")] public TMP_Text foodText;
        public TMP_Text ironText;
        public TMP_Text goldText;
        [SerializeField, HideInInspector] private TMP_Text fishText;
        public event Action ResourcesChanged;
        // Wheat and flour are ingredients, not edible food (GDD 1.2).
        public int Food => GetAmount(ResourceType.Food);
        public int TotalFood => Food;
        private bool distributing;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (fishText != null && fishText != foodText && fishText != woodText &&
                fishText != stoneText && fishText != ironText && fishText != goldText)
                fishText.gameObject.SetActive(false);
            // Existing warehouse prefabs work without manual component migration.
            foreach (GameObject building in GameObject.FindGameObjectsWithTag("Warehouse"))
                if (building.GetComponent<WarehouseStorage>() == null) building.AddComponent<WarehouseStorage>();
            UpdateUI();
        }

        private void Start() { UpdateUI(); }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int GetAmount(ResourceType type)
        {
            int total = 0;
            foreach (WarehouseStorage warehouse in WarehouseStorage.All)
                if (FactionMember.IsPlayer(warehouse.gameObject)) total = checked(total + warehouse.GetAmount(type));
            return total;
        }

        private int GetInitialAmount(ResourceType type)
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

        private void SetInitialAmount(ResourceType type, int amount)
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

        // Returns the amount physically accepted by the available warehouses.
        public int AddResource(ResourceType type, int amount)
            => AddResource(type, amount, FactionMember.LocalPlayer);

        private int AddResource(ResourceType type, int amount, Faction owner)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int remaining = amount;
            foreach (WarehouseStorage warehouse in WarehouseStorage.All)
            {
                if (FactionMember.Of(warehouse.gameObject) != owner) continue;
                remaining -= warehouse.AddResource(type, remaining);
                if (remaining == 0) break;
            }
            return amount - remaining;
        }

        public bool TrySpendResource(ResourceType type, int amount)
        {
            if (amount < 0 || GetAmount(type) < amount) return false;
            int remaining = amount;
            // Events update UI only after the complete transaction.
            bool previous = distributing;
            distributing = true;
            foreach (WarehouseStorage warehouse in WarehouseStorage.All)
            {
                if (!FactionMember.IsPlayer(warehouse.gameObject)) continue;
                remaining -= warehouse.RemoveAvailable(type, remaining);
                if (remaining == 0) break;
            }
            distributing = previous;
            if (!distributing) UpdateUI();
            return true;
        }

        public void TakeResource(GettingResources res)
        {
            if (res == null) return;
            foreach (WarehouseStorage warehouse in WarehouseStorage.All)
            {
                if (!FactionMember.CanUse(res.gameObject, warehouse.gameObject)) continue;
                warehouse.Deposit(res);
                if (!res.HasCargo) break;
            }
        }

        // Serialized manager values are starting stock, distributed into real buildings.
        // Overflow remains pending here until physical space becomes available.
        private void DistributeInitialStock()
        {
            foreach (ResourceType type in WarehouseStorage.Types)
            {
                int initial = Mathf.Max(0, GetInitialAmount(type));
                SetInitialAmount(type, initial - AddResource(type, initial, Faction.Player));
            }
        }

        public void UpdateUI()
        {
            if (distributing) return;
            distributing = true;
            DistributeInitialStock();
            distributing = false;
            if (woodText != null) woodText.text = GetAmount(ResourceType.Wood).ToString();
            if (stoneText != null) stoneText.text = GetAmount(ResourceType.Stone).ToString();
            if (foodText != null) foodText.text = TotalFood.ToString();
            if (ironText != null) ironText.text = GetAmount(ResourceType.Iron).ToString();
            if (goldText != null) goldText.text = GetAmount(ResourceType.Gold).ToString();
            ResourcesChanged?.Invoke();
        }

        // Existing building and unit costs still use wheat and fish separately.
        public bool HasEnough(int w, int s, int wh, int f, int i = 0, int g = 0)
        {
            return w >= 0 && s >= 0 && wh >= 0 && f >= 0 && i >= 0 && g >= 0 &&
                   GetAmount(ResourceType.Wood) >= w && GetAmount(ResourceType.Stone) >= s &&
                   GetAmount(ResourceType.Wheat) >= wh && GetAmount(ResourceType.Fish) >= f &&
                   GetAmount(ResourceType.Iron) >= i && GetAmount(ResourceType.Gold) >= g;
        }

        public void Spend(int w, int s, int wh, int f, int i = 0, int g = 0)
        {
            if (!HasEnough(w, s, wh, f, i, g)) return;
            distributing = true;
            TrySpendResource(ResourceType.Wood, w);
            TrySpendResource(ResourceType.Stone, s);
            TrySpendResource(ResourceType.Wheat, wh);
            TrySpendResource(ResourceType.Fish, f);
            TrySpendResource(ResourceType.Iron, i);
            TrySpendResource(ResourceType.Gold, g);
            distributing = false;
            UpdateUI();
        }
    }
}
