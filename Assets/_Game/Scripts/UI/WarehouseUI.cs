using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Game.Scripts.UI
{
    public class WarehouseUI : MonoBehaviour
    {
        [SerializeField] private WarehouseStorage warehouse;
        [Header("Количество каждого ресурса на выбранном складе")]
        public TMP_Text woodText;
        public TMP_Text stoneText;
        public TMP_Text ironText;
        public TMP_Text goldText;
        public TMP_Text wheatText;
        public TMP_Text flourText;
        public TMP_Text breadText;
        public TMP_Text foodText;
        public TMP_Text fishText;
        public TMP_Text meatText;
        public TMP_Text berriesText;
        public TMP_Text swordText;
        public TMP_Text spearText;
        public TMP_Text bowText;
        public TMP_Text shieldText;
        public TMP_Text siegeLadderText;
        [Header("Вместимость")]
        public TMP_Text capacityText;
        public TMP_Text usedCapacityText;
        public TMP_Text freeCapacityText;
        [Header("Необязательный найм носильщиков")]
        public Button hireButton, fireButton;
        public TMP_Text workersText;
        [Header("Необязательный общий список")]
        [SerializeField] private TMP_Text contentsText;
        [SerializeField] private bool showEmptyResources;
        public WarehouseStorage Warehouse => warehouse;

        public void Bind(WarehouseStorage target)
        {
            Unsubscribe();
            warehouse = target;
            if (isActiveAndEnabled && warehouse != null) warehouse.StockChanged += Refresh;
            Refresh();
        }

        private void OnEnable()
        {
            if (warehouse != null) warehouse.StockChanged += Refresh;
            if (hireButton != null) hireButton.onClick.AddListener(Hire);
            if (fireButton != null) fireButton.onClick.AddListener(Fire);
            Refresh();
        }
        private void OnDisable()
        {
            Unsubscribe();
            if (hireButton != null) hireButton.onClick.RemoveListener(Hire);
            if (fireButton != null) fireButton.onClick.RemoveListener(Fire);
        }

        private void Hire()
        {
            if (warehouse != null && FactionMember.IsPlayer(warehouse.gameObject))
                warehouse.GetComponent<WorkplaceHiring>()?.Hire();
            Refresh();
        }

        private void Fire()
        {
            if (warehouse != null && FactionMember.IsPlayer(warehouse.gameObject))
                warehouse.GetComponent<WorkplaceHiring>()?.Fire();
            Refresh();
        }
        private void Unsubscribe()
        {
            if (warehouse != null) warehouse.StockChanged -= Refresh;
        }

        public void Refresh()
        {
            if (hireButton != null || fireButton != null || workersText != null)
            {
                var hiring = warehouse != null ? warehouse.GetComponent<WorkplaceHiring>() : null;
                bool owned = hiring != null && FactionMember.IsPlayer(hiring.gameObject);
                if (hireButton != null) hireButton.interactable = owned;
                if (fireButton != null) fireButton.interactable = owned;
                if (workersText != null) workersText.text = hiring != null ? $"Носильщики: {hiring.WorkerCount}/{hiring.maxWorkers}" : "";
            }
            SetText(woodText, ResourceType.Wood);
            SetText(stoneText, ResourceType.Stone);
            SetText(ironText, ResourceType.Iron);
            SetText(goldText, ResourceType.Gold);
            SetText(wheatText, ResourceType.Wheat);
            SetText(flourText, ResourceType.Flour);
            SetText(breadText, ResourceType.Bread);
            SetText(foodText, ResourceType.Food);
            SetText(fishText, ResourceType.Fish);
            SetText(meatText, ResourceType.Meat);
            SetText(berriesText, ResourceType.Berries);
            SetText(swordText, ResourceType.Sword);
            SetText(spearText, ResourceType.Spear);
            SetText(bowText, ResourceType.Bow);
            SetText(shieldText, ResourceType.Shield);
            SetText(siegeLadderText, ResourceType.SiegeLadder);
            if (capacityText != null) capacityText.text = warehouse != null ? warehouse.UsedCapacity + " / " + warehouse.Capacity : "—";
            if (usedCapacityText != null) usedCapacityText.text = warehouse != null ? warehouse.UsedCapacity.ToString() : "—";
            if (freeCapacityText != null) freeCapacityText.text = warehouse != null ? warehouse.FreeCapacity.ToString() : "—";
            if (contentsText == null) return;
            if (warehouse == null) { contentsText.text = "Склад не выбран"; return; }
            var text = new StringBuilder();
            Append(text, ResourceType.Wood, "Дерево");
            Append(text, ResourceType.Stone, "Камень");
            Append(text, ResourceType.Iron, "Железо");
            Append(text, ResourceType.Gold, "Золото");
            Append(text, ResourceType.Wheat, "Пшеница");
            Append(text, ResourceType.Flour, "Мука");
            Append(text, ResourceType.Bread, "Хлеб");
            Append(text, ResourceType.Food, "Еда (всего)");
            Append(text, ResourceType.Fish, "Рыба");
            Append(text, ResourceType.Meat, "Мясо");
            Append(text, ResourceType.Berries, "Ягоды");
            Append(text, ResourceType.Sword, "Мечи");
            Append(text, ResourceType.Spear, "Копья");
            Append(text, ResourceType.Bow, "Луки");
            Append(text, ResourceType.Shield, "Щиты");
            Append(text, ResourceType.SiegeLadder, "Осадные лестницы");
            contentsText.text = warehouse.UsedCapacity == 0 && !showEmptyResources ? "Склад пуст" : text.ToString();
        }
        private void SetText(TMP_Text target, ResourceType type)
        {
            if (target != null) target.text = warehouse != null ? warehouse.GetAmount(type).ToString() : "—";
        }
        private void Append(StringBuilder text, ResourceType type, string label)
        {
            int amount = warehouse.GetAmount(type);
            if (showEmptyResources || amount > 0) text.Append(label).Append(": ").Append(amount).AppendLine();
        }
    }
}
