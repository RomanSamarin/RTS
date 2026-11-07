using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Game.Scripts.UI
{
    public class ProductionRecipeRow : MonoBehaviour
    {
        [Tooltip("Номер рецепта в Recipes здания: 0 — первый, 1 — второй. Для строк, размещённых вручную.")]
        [Min(0)] public int recipeIndex;
        public Button produceButton;
        public Image productIcon;
        public TMP_Text nameText, durationText, costText;
        public Transform costsRoot;
        public ProductionCostRow costPrefab;
        private readonly List<ProductionCostRow> costs = new List<ProductionCostRow>();
        private EquipmentProductionPanel panel;
        private bool listening;
        private void OnEnable()
        {
            Listen();
            var owner = GetComponentInParent<EquipmentProductionPanel>(true);
            if (owner != null && owner.Target != null)
            {
                var recipes = owner.Target.Recipes;
                Bind(owner, recipeIndex, recipeIndex >= 0 && recipeIndex < recipes.Count ? recipes[recipeIndex] : null);
            }
        }
        private void Listen()
        {
            if (!listening && produceButton != null) { produceButton.onClick.AddListener(Produce); listening = true; }
        }
        public void Bind(EquipmentProductionPanel owner, int index, EquipmentProductionBuilding.Recipe recipe)
        {
            Listen(); recipeIndex = index; panel = recipe != null ? owner : null;
            if (panel == null) { SetAvailable(false); return; }
            if (nameText != null) nameText.text = recipe.title;
            if (durationText != null) durationText.text = $"{recipe.seconds:0.#} сек.";
            if (productIcon != null) { productIcon.sprite = panel.GetRecipeSprite(recipe); productIcon.enabled = productIcon.sprite != null; }
            var ingredients = new Dictionary<ResourceType, int>();
            if (recipe.ingredients != null)
                foreach (var item in recipe.ingredients)
                    if (item != null && item.amount > 0)
                    { ingredients.TryGetValue(item.type, out int amount); ingredients[item.type] = amount + item.amount; }
            var text = new StringBuilder(); int row = 0;
            foreach (var item in ingredients)
            {
                if (text.Length > 0) text.Append(" + ");
                text.Append(panel.CostLabel(item.Key, item.Value));
                if (costPrefab != null && costsRoot != null)
                {
                    if (row == costs.Count) costs.Add(Instantiate(costPrefab, costsRoot));
                    costs[row].gameObject.SetActive(true); costs[row].Bind(panel, item.Key, item.Value); row++;
                }
            }
            for (int i = row; i < costs.Count; i++) costs[i].gameObject.SetActive(false);
            if (costText != null) costText.text = costPrefab != null && costsRoot != null ? "" : text.ToString();
            if (produceButton != null) produceButton.interactable = true;
        }
        public void SetAvailable(bool value) { if (produceButton != null) produceButton.interactable = value && panel != null; }
        private void Produce()
        {
            if (panel != null && panel.Target != null) panel.Enqueue(recipeIndex);
            else Debug.LogWarning("[ProductionRecipeRow] Строка не привязана к зданию. Разместите её внутри EquipmentProductionPanel и откройте панель через выбор кузницы.", this);
        }
        private void OnDestroy() { if (produceButton != null) produceButton.onClick.RemoveListener(Produce); }
    }
}
