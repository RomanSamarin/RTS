using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Game.Scripts.UI
{
    /// <summary>Отдельная панель действий выбранного юнита; не зависит от открытого меню здания.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SelectedMilitaryActions : MonoBehaviour
    {
        public Button cancelTrainingButton, disbandButton;
        public TMP_Text messageText;
        private CanvasGroup group;
        private float messageUntil;
        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (cancelTrainingButton != null) cancelTrainingButton.onClick.AddListener(CancelTraining);
            if (disbandButton != null) disbandButton.onClick.AddListener(Disband);
        }
        private static GameObject Selected()
        {
            if (ManagerGO.Instance == null || ManagerGO.Instance.unitsSelected.Count != 1) return null;
            return ManagerGO.Instance.unitsSelected[0];
        }
        private void Update()
        {
            var selected = Selected(); var worker = selected != null ? selected.GetComponent<ResourcesMove>() : null;
            bool recruit = worker != null && worker.IsTrainingRecruit;
            bool soldier = worker != null && worker.role == ResourcesMove.UnitRole.Soldier;
            group.alpha = recruit || soldier ? 1 : 0; group.interactable = group.blocksRaycasts = recruit || soldier;
            if (cancelTrainingButton != null) cancelTrainingButton.gameObject.SetActive(recruit);
            if (disbandButton != null) disbandButton.gameObject.SetActive(soldier);
            if (messageText != null && Time.unscaledTime >= messageUntil) messageText.text = "";
        }
        public void CancelTraining()
        {
            var selected = Selected(); var recruit = selected != null ? selected.GetComponent<TrainingRecruit>() : null;
            if (recruit != null) recruit.CancelTraining();
        }
        public void Disband()
        {
            var selected = Selected(); var worker = selected != null ? selected.GetComponent<ResourcesMove>() : null;
            if (worker == null || worker.role != ResourcesMove.UnitRole.Soldier) return;
            var service = selected.GetComponent<SoldierService>();
            if (service == null) service = selected.AddComponent<SoldierService>();
            service.DisbandAtNearestBarracks();
            if (messageText != null) messageText.text = service.LastError;
            messageUntil = Time.unscaledTime + 5f;
        }
        private void OnDestroy()
        {
            if (cancelTrainingButton != null) cancelTrainingButton.onClick.RemoveListener(CancelTraining);
            if (disbandButton != null) disbandButton.onClick.RemoveListener(Disband);
        }
    }
}
