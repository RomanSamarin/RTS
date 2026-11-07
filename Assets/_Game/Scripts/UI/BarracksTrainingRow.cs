using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Game.Scripts.UI
{
    public class BarracksTrainingRow : MonoBehaviour
    {
        public TMP_Text nameText, recruitText, statusText, progressText;
        public Image progressFill;
        public Button cancelButton;
        private BarracksActionsPanel panel;
        private int orderId;
        private bool listening;
        public void Bind(BarracksActionsPanel owner, Barracks.TrainingOrderView item, string status)
        {
            panel = owner; orderId = item.id;
            if (!listening && cancelButton != null) { cancelButton.onClick.AddListener(Cancel); listening = true; }
            if (nameText != null) nameText.text = item.title;
            if (recruitText != null) recruitText.text = item.recruit != null ? item.recruit.name : "Нет жителя";
            if (statusText != null) statusText.text = item.active ? status : "Ждёт очереди";
            float value = Mathf.Clamp01(item.elapsed / Mathf.Max(0.1f, item.duration));
            if (progressFill != null) progressFill.fillAmount = value;
            if (progressText != null) progressText.text = $"{value * 100:0}% · {item.elapsed:0.#} / {item.duration:0.#} сек.";
        }
        private void Cancel() { if (panel != null) panel.CancelOrder(orderId); }
        private void OnDestroy() { if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel); }
    }
}
