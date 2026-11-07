using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Game.Scripts.UI
{
    /// <summary>Привязка пользовательской панели. Создаёт только строки из назначенных UI-префабов.</summary>
    public class EquipmentProductionPanel : MonoBehaviour
    {
        [Serializable] public class ResourceIcon { public ResourceType type; public Sprite sprite; }
        public List<ResourceIcon> resourceIcons = new List<ResourceIcon>();
        [Header("Шапка и персонал")]
        public TMP_Text titleText, workersText, statusText, queueCountText, messageText;
        public Button hireButton, fireButton, clearQueueButton, closeButton;
        public Toggle repeatToggle;
        [Header("Строки, созданные вами")]
        public Transform recipesRoot, queueRoot;
        public ProductionRecipeRow recipePrefab;
        public ProductionQueueRow queuePrefab;
        [SerializeField] private EquipmentProductionBuilding target;
        public EquipmentProductionBuilding Target => target;
        public Sprite GetRecipeSprite(EquipmentProductionBuilding.Recipe recipe)
            =>recipe.producesUnit?(target as Workshop)?.catapultIcon:GetSprite(recipe.output);
        private WorkplaceHiring hiring;
        private readonly List<ProductionRecipeRow> recipeRows = new List<ProductionRecipeRow>();
        private readonly List<ProductionRecipeRow> manualRecipeRows = new List<ProductionRecipeRow>();
        private readonly List<ProductionQueueRow> queueRows = new List<ProductionQueueRow>();
        private bool listening, bound;
        private float nextRefresh, messageUntil;

        private void OnEnable()
        {
            Listen();
            if (target != null && !bound) Bind(target);
            Refresh();
        }
        private void Listen()
        {
            if (listening) return;
            if (hireButton != null) hireButton.onClick.AddListener(Hire);
            if (fireButton != null) fireButton.onClick.AddListener(Fire);
            if (clearQueueButton != null) clearQueueButton.onClick.AddListener(ClearQueue);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (repeatToggle != null) repeatToggle.onValueChanged.AddListener(SetRepeat);
            listening = true;
        }
        public void Bind(EquipmentProductionBuilding building)
        {
            Listen(); target = building; bound = true;
            hiring = target != null ? target.GetComponent<WorkplaceHiring>() : null;
            if (titleText != null) titleText.text = target is Blacksmith ? "Кузница" : target is Workshop ? "Мастерская" : target != null ? target.name : "Производство";
            int count = target != null ? target.Recipes.Count : 0;
            manualRecipeRows.Clear();
            foreach (var row in GetComponentsInChildren<ProductionRecipeRow>(true))
            {
                if (row == recipePrefab || recipeRows.Contains(row) || row.GetComponentInParent<EquipmentProductionPanel>(true) != this) continue;
                manualRecipeRows.Add(row);
                int index = row.recipeIndex;
                bool valid = index >= 0 && index < count && target.Recipes[index] != null;
                row.Bind(this, index, valid ? target.Recipes[index] : null);
                if (target != null && !valid)
                    Debug.LogWarning($"[ProductionPanel] У строки {row.name} Recipe Index={index}, но такого рецепта нет в выбранном здании.", row);
            }
            if (count > 0 && manualRecipeRows.Count == 0 && (recipePrefab == null || recipesRoot == null))
                Debug.LogWarning("[ProductionPanel] Добавьте ProductionRecipeRow внутрь панели либо назначьте Recipe Prefab и Recipes Root.", this);
            if (queuePrefab == null || queueRoot == null)
                Debug.LogWarning("[ProductionPanel] Назначьте Queue Prefab и Queue Root для отображения очереди.", this);
            for (int i = 0; i < count && recipePrefab != null && recipesRoot != null; i++)
            {
                if (i == recipeRows.Count) recipeRows.Add(Instantiate(recipePrefab, recipesRoot));
                recipeRows[i].gameObject.SetActive(true); recipeRows[i].Bind(this, i, target.Recipes[i]);
            }
            for (int i = count; i < recipeRows.Count; i++) recipeRows[i].gameObject.SetActive(false);
            messageUntil = 0; Text(messageText, ""); Refresh();
        }
        private void Update()
        {
            if (bound && target == null) { Close(); return; }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f; Refresh();
        }
        public void Refresh()
        {
            bool available = target != null && target.isActiveAndEnabled;
            int workers = hiring != null ? hiring.WorkerCount : 0;
            Text(workersText, $"Рабочие: {workers} / {(hiring != null ? hiring.MaxWorkers : 0)}");
            if (hireButton != null) hireButton.interactable = available && hiring != null && workers < hiring.MaxWorkers;
            if (fireButton != null) fireButton.interactable = available && hiring != null && workers > 0;
            if (clearQueueButton != null) clearQueueButton.interactable = available && (target.QueueCount > 0 || target.repeatSelectedRecipe);
            if (repeatToggle != null) { repeatToggle.interactable = available; repeatToggle.SetIsOnWithoutNotify(target != null && target.repeatSelectedRecipe); }
            foreach (var row in recipeRows) row.SetAvailable(available && target.QueueCount < 1000);
            foreach (var row in manualRecipeRows) if (row != null) row.SetAvailable(available && target.QueueCount < 1000);
            string status = ProductionStatus();
            Text(statusText, status);
            var queue = target != null ? target.GetQueueSnapshot() : new List<EquipmentProductionBuilding.QueueItem>();
            Text(queueCountText, "Заказов: " + (target != null ? target.QueueCount : 0)
                + (target != null && target.repeatSelectedRecipe ? " · Повтор включён" : ""));
            if (queuePrefab != null && queueRoot != null)
                for (int i = 0; i < queue.Count; i++)
                {
                    if (i == queueRows.Count) queueRows.Add(Instantiate(queuePrefab, queueRoot));
                    queueRows[i].gameObject.SetActive(true); queueRows[i].Bind(this, queue[i], status);
                }
            for (int i = queue.Count; i < queueRows.Count; i++) queueRows[i].gameObject.SetActive(false);
            if (Time.unscaledTime >= messageUntil) Text(messageText, "");
        }
        private string ProductionStatus()
        {
            if(target!=null&&!BuildingRoadAccess.Allows(target))return "Нет дороги к зданию";
            if (target == null) return "Выберите здание";
            if (!target.isActiveAndEnabled) return "Здание недоступно";
            if (target.ActiveRecipe == null) return "Нет заказа";
            if(target is Workshop workshop&&workshop.WaitingForCatapultExit)return "Катапульта готова: освободите выход";
            if (Time.timeScale == 0) return "Пауза";
            if (hiring == null || hiring.WorkerCount == 0) return "Нет работников";
            if (!target.CanProduce) return target.WaitingTask;
            foreach (var worker in hiring.Workers)
            {
                var production = worker != null ? worker.GetComponent<ProductionWorker>() : null;
                if (production != null && production.IsWorking) return "Изготавливается";
            }
            return "Ждёт возвращения работников";
        }
        public void Enqueue(int recipeIndex)
        {
            if (target == null || !target.isActiveAndEnabled) return;
            if (!target.Enqueue(recipeIndex)) Message("Не удалось добавить заказ: проверьте рецепт и лимит очереди.");
            Refresh();
        }
        public void CancelOrder(int orderId, bool repeating)
        {
            if (target == null) return;
            if (repeating)
            {
                // Запоздалый клик по повтору не отменяет появившийся конечный заказ.
                if (target.QueueCount == 0) target.SetRepeat(false);
            }
            else target.CancelOrder(orderId);
            Refresh();
        }
        public void ClearQueue()
        {
            if (target == null) return;
            target.ClearQueue(); target.SetRepeat(false); Refresh();
        }
        public void SetRepeat(bool value) { if (target != null) { target.SetRepeat(value); Refresh(); } }
        public void Hire()
        {
            if (target == null || !target.isActiveAndEnabled || hiring == null) return;
            if (!hiring.TryHire()) Message("Найм не выполнен: проверьте свободных жителей, радиус найма и лимит рабочих.");
            Refresh();
        }
        public void Fire()
        {
            if (target == null || !target.isActiveAndEnabled || hiring == null) return;
            if (!hiring.TryFire()) Message("Увольнение не выполнено: нет работников или работник несёт груз.");
            Refresh();
        }
        public void Close()
        {
            var selection = BuildingSelectionManager.Instance;
            if (selection != null && selection.Current != null && selection.Current.Panel != null
                && transform.IsChildOf(selection.Current.Panel.transform)) selection.DeselectCurrent();
            else gameObject.SetActive(false);
        }
        private void Message(string message) { Text(messageText, message); messageUntil = Time.unscaledTime + 4f; }
        private static void Text(TMP_Text label, string value) { if (label != null && label.text != value) label.text = value; }
        public Sprite GetSprite(ResourceType type)
        { foreach (var entry in resourceIcons) if (entry != null && entry.type == type) return entry.sprite; return null; }
        public string CostLabel(ResourceType type, int amount)
        {
            string title = type == ResourceType.Wood ? "дерева" : type == ResourceType.Iron ? "железа"
                : type == ResourceType.Stone ? "камня" : type == ResourceType.Gold ? "золота"
                : type == ResourceType.Food ? "еды" : type.ToString();
            return amount + " " + title;
        }
        private void OnDestroy()
        {
            if (hireButton != null) hireButton.onClick.RemoveListener(Hire);
            if (fireButton != null) fireButton.onClick.RemoveListener(Fire);
            if (clearQueueButton != null) clearQueueButton.onClick.RemoveListener(ClearQueue);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (repeatToggle != null) repeatToggle.onValueChanged.RemoveListener(SetRepeat);
        }
    }
}
