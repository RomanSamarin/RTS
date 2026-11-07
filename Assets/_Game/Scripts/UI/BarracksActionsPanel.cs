using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Game.Scripts.UI
{
    /// <summary>Кнопки и очередь для созданной пользователем панели казармы.</summary>
    public class BarracksActionsPanel : MonoBehaviour
    {
        public Button cancelCurrentButton, cancelAllButton, disbandSelectedButton;
        public Button setRallyPointButton, clearRallyPointButton;
        public TMP_Text queueCountText, statusText, messageText;
        public Transform queueRoot;
        public BarracksTrainingRow queuePrefab;
        [SerializeField] private Barracks target;
        private readonly List<BarracksTrainingRow> rows = new List<BarracksTrainingRow>();
        private bool listening;
        private float nextRefresh, messageUntil;
        private void OnEnable() { Listen(); Refresh(); }
        private void Listen()
        {
            if (listening) return;
            if (cancelCurrentButton != null) cancelCurrentButton.onClick.AddListener(CancelCurrent);
            if (cancelAllButton != null) cancelAllButton.onClick.AddListener(CancelAll);
            if (disbandSelectedButton != null) disbandSelectedButton.onClick.AddListener(DisbandSelected);
            if (setRallyPointButton != null) setRallyPointButton.onClick.AddListener(SetRallyPoint);
            if (clearRallyPointButton != null) clearRallyPointButton.onClick.AddListener(ClearRallyPoint);
            listening = true;
        }
        public void Bind(Barracks building) { Listen(); target = building; Message(""); Refresh(); }
        private void Update()
        { if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.1f; Refresh(); } }
        public void Refresh()
        {
            int count = target != null ? target.QueueCount : 0;
            if (queueCountText != null) queueCountText.text = "Обучение: " + count;
            if (statusText != null) statusText.text = target != null ? target.Status : "Выберите казарму";
            if (cancelCurrentButton != null) cancelCurrentButton.interactable = count > 0;
            if (cancelAllButton != null) cancelAllButton.interactable = count > 0;
            if (setRallyPointButton != null) setRallyPointButton.interactable = target != null && target.isActiveAndEnabled;
            if (clearRallyPointButton != null) clearRallyPointButton.interactable = target != null && target.HasRallyPoint;
            var soldier = SelectedSoldier();
            if (disbandSelectedButton != null) disbandSelectedButton.interactable = target != null && target.isActiveAndEnabled && soldier != null;
            var queue = target != null ? target.GetTrainingQueue() : new List<Barracks.TrainingOrderView>();
            if (queueRoot != null && queuePrefab != null)
                for (int i = 0; i < queue.Count; i++)
                {
                    if (i == rows.Count) rows.Add(Instantiate(queuePrefab, queueRoot));
                    rows[i].gameObject.SetActive(true); rows[i].Bind(this, queue[i], target.Status);
                }
            for (int i = queue.Count; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
            if (messageText != null && Time.unscaledTime >= messageUntil) messageText.text = "";
        }
        private static GameObject SelectedSoldier()
        {
            if (ManagerGO.Instance == null || ManagerGO.Instance.unitsSelected.Count != 1) return null;
            var selected = ManagerGO.Instance.unitsSelected[0];
            var worker = selected != null ? selected.GetComponent<ResourcesMove>() : null;
            return worker != null && worker.role == ResourcesMove.UnitRole.Soldier ? selected : null;
        }
        public void CancelOrder(int id) { if (target != null) target.CancelOrder(id); Refresh(); }
        public void CancelCurrent() { if (target != null) target.CancelCurrentTraining(); Refresh(); }
        public void CancelAll() { if (target != null) target.CancelAllTraining(); Refresh(); }
        public void SetRallyPoint() { if (target != null) target.CollectedPoints(); }
        public void ClearRallyPoint() { if (target != null) target.ClearRallyPoint(); Refresh(); }
        public void DisbandSelected()
        {
            if (target == null) return;
            if (!target.TryDisband(SelectedSoldier())) Message(target.LastError);
            else Message("Солдат распущен. Снаряжение ожидает носильщика в казарме.");
            Refresh();
        }
        private void Message(string message) { if (messageText != null) messageText.text = message; messageUntil = Time.unscaledTime + 5f; }
        private void OnDestroy()
        {
            if (cancelCurrentButton != null) cancelCurrentButton.onClick.RemoveListener(CancelCurrent);
            if (cancelAllButton != null) cancelAllButton.onClick.RemoveListener(CancelAll);
            if (disbandSelectedButton != null) disbandSelectedButton.onClick.RemoveListener(DisbandSelected);
            if (setRallyPointButton != null) setRallyPointButton.onClick.RemoveListener(SetRallyPoint);
            if (clearRallyPointButton != null) clearRallyPointButton.onClick.RemoveListener(ClearRallyPoint);
        }
    }
}
