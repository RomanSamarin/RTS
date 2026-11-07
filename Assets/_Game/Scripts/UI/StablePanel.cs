using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Game.Scripts.UI
{
    /// <summary>Назначьте элементы созданной вручную панели конюшни.</summary>
    public class StablePanel : MonoBehaviour
    {
        public Button queueHorseButton, cancelCurrentButton, cancelAllButton;
        public TMP_Text costText, queueText, stockText, statusText;
        public Image progressFill;
        [SerializeField] private Stable target;
        private bool listening;
        private float nextRefresh;
        private void OnEnable() { Listen(); Refresh(); }
        private void Listen()
        {
            if (listening) return;
            if (queueHorseButton != null) queueHorseButton.onClick.AddListener(QueueHorse);
            if (cancelCurrentButton != null) cancelCurrentButton.onClick.AddListener(CancelCurrent);
            if (cancelAllButton != null) cancelAllButton.onClick.AddListener(CancelAll);
            listening = true;
        }
        public void Bind(Stable stable) { target = stable; Listen(); Refresh(); }
        private void Update() { if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.1f; Refresh(); } }
        public void QueueHorse() { if (target != null) target.QueueHorse(); Refresh(); }
        public void CancelCurrent() { if (target != null) target.CancelCurrentHorse(); Refresh(); }
        public void CancelAll() { if (target != null) target.CancelAllHorses(); Refresh(); }
        private void Refresh()
        {
            bool ready = target != null && target.isActiveAndEnabled;
            int count = target != null ? target.QueueCount : 0;
            if (queueHorseButton != null) queueHorseButton.interactable = ready && count < Mathf.Max(1, target.maxQueue);
            if (cancelCurrentButton != null) cancelCurrentButton.interactable = ready && count > 0;
            if (cancelAllButton != null) cancelAllButton.interactable = ready && count > 0;
            if (costText != null) costText.text = target != null ? $"Лошадь: {target.foodCost} еды, {target.goldCost} золота • {target.horseSeconds:0} сек." : "";
            if (queueText != null) queueText.text = "В очереди: " + count;
            if (stockText != null) stockText.text = target != null ? $"Доставлено: еда {target.StoredFood}, золото {target.StoredGold}" : "";
            if (statusText != null) statusText.text = target != null ? target.Status : "Выберите конюшню";
            if (progressFill != null) progressFill.fillAmount = target != null ? target.Progress : 0;
        }
        private void OnDestroy()
        {
            if (queueHorseButton != null) queueHorseButton.onClick.RemoveListener(QueueHorse);
            if (cancelCurrentButton != null) cancelCurrentButton.onClick.RemoveListener(CancelCurrent);
            if (cancelAllButton != null) cancelAllButton.onClick.RemoveListener(CancelAll);
        }
    }
}
