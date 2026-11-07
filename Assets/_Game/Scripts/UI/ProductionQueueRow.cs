using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Game.Scripts.UI
{
    public class ProductionQueueRow : MonoBehaviour
    {
        public Image productIcon;
        public Image progressFill;
        public Slider progressSlider;
        public TMP_Text nameText, statusText, progressText;
        public Button cancelButton;
        public GameObject activeHighlight;
        private EquipmentProductionPanel panel;
        private int orderId;
        private bool repeating, listening;
        public void Bind(EquipmentProductionPanel owner, EquipmentProductionBuilding.QueueItem item, string activeStatus)
        {
            panel = owner; orderId = item.orderId; repeating = item.repeating;
            if (!listening && cancelButton != null) { cancelButton.onClick.AddListener(Cancel); listening = true; }
            var recipe = item.recipe;
            if (nameText != null) nameText.text = recipe != null ? recipe.title : "Неизвестный рецепт";
            if (productIcon != null) { productIcon.sprite = recipe != null ? panel.GetRecipeSprite(recipe) : null; productIcon.enabled = productIcon.sprite != null; }
            float duration = recipe != null ? Mathf.Max(0.1f, recipe.seconds) : 1f;
            float elapsed = item.active ? Mathf.Clamp(item.progress, 0f, duration) : 0f;
            float value = Mathf.Clamp01(elapsed / duration);
            if (progressFill != null) progressFill.fillAmount = value;
            if (progressSlider != null) { progressSlider.interactable = false; progressSlider.minValue = 0; progressSlider.maxValue = 1; progressSlider.SetValueWithoutNotify(value); }
            if (progressText != null) progressText.text = $"{value * 100:0}% · {elapsed:0.#} / {duration:0.#} сек.";
            if (statusText != null) statusText.text = item.active ? activeStatus + (item.repeating ? " · Повтор" : "") : "Ожидает";
            if (activeHighlight != null) activeHighlight.SetActive(item.active);
        }
        private void Cancel() { if (panel != null) panel.CancelOrder(orderId, repeating); }
        private void OnDestroy() { if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel); }
    }
}
