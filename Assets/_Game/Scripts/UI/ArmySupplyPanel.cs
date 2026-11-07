using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Game.Scripts.UI
{
    /// <summary>Кнопки для пользовательской панели управления армией и повозкой.</summary>
    public class ArmySupplyPanel : MonoBehaviour
    {
        public ArmyGroup army;
        public FoodCart cart;
        public Button assignSoldiersButton, bindCartButton, assignEscortsButton, removeSoldiersButton;
        public Button hitchHorseButton, unhitchHorseButton, refillButton, followButton, stopFollowingButton;
        public TMP_Text armyText, foodText, horseText, statusText, warningText, starvationText, messageText;
        private float nextRefresh;
        private void Awake()
        {
            if (assignSoldiersButton != null) assignSoldiersButton.onClick.AddListener(AssignSoldiers);
            if (bindCartButton != null) bindCartButton.onClick.AddListener(BindSelectedCart);
            if (assignEscortsButton != null) assignEscortsButton.onClick.AddListener(AssignEscorts);
            if (removeSoldiersButton != null) removeSoldiersButton.onClick.AddListener(RemoveSoldiers);
            if (hitchHorseButton != null) hitchHorseButton.onClick.AddListener(HitchHorse);
            if (unhitchHorseButton != null) unhitchHorseButton.onClick.AddListener(UnhitchHorse);
            if (refillButton != null) refillButton.onClick.AddListener(Refill);
            if (followButton != null) followButton.onClick.AddListener(Follow);
            if (stopFollowingButton != null) stopFollowingButton.onClick.AddListener(StopFollowing);
        }
        private ArmyGroup EnsureArmy()
        { if (army == null) army = new GameObject("Армия").AddComponent<ArmyGroup>(); return army; }
        private FoodCart CurrentCart => army != null && army.SupplyCart != null ? army.SupplyCart : cart;
        public void AssignSoldiers() { EnsureArmy().AssignSelectedSoldiers(); Refresh(); }
        public void AssignEscorts() { EnsureArmy().AssignSelectedEscorts(); Refresh(); }
        public void RemoveSoldiers() { if (army != null) army.RemoveSelectedSoldiers(); Refresh(); }
        public void BindSelectedCart()
        {
            if (ManagerGO.Instance != null)
                foreach (var selected in ManagerGO.Instance.unitsSelected)
                { var wagon = selected != null ? selected.GetComponent<FoodCart>() : null; if (wagon != null) { cart = wagon; break; } }
            if (cart == null) { Message("Выберите повозку или назначьте Cart в Inspector."); return; }
            EnsureArmy().AssignCart(cart); Refresh();
        }
        public void HitchHorse()
        {
            var wagon = CurrentCart; if (wagon == null) { Message("Сначала назначьте повозку."); return; }
            CartHorse selectedHorse = null;
            if (ManagerGO.Instance != null)
                foreach (var selected in ManagerGO.Instance.unitsSelected)
                { var horse = selected != null ? selected.GetComponent<CartHorse>() : null; if (horse != null) { selectedHorse = horse; break; } }
            if (selectedHorse != null)
            { if (!wagon.AttachHorse(selectedHorse)) Message("Подведите живую свободную лошадь к повозке."); }
            else wagon.AttachNearestHorse();
            Refresh();
        }
        public void UnhitchHorse() { if (CurrentCart != null) CurrentCart.DetachHorse(); Refresh(); }
        public void Refill() { if (CurrentCart != null) CurrentCart.ReturnForFood(); Refresh(); }
        public void Follow() { if (CurrentCart != null) CurrentCart.ResumeFollowing(); Refresh(); }
        public void StopFollowing() { if (CurrentCart != null) CurrentCart.StopFollowing(); Refresh(); }
        private void Update() { if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.2f; Refresh(); } }
        public void Refresh()
        {
            var wagon = CurrentCart;
            if (armyText != null) armyText.text = army != null ? $"{army.armyName}: {army.MemberCount} бойцов, голодных: {army.HungryCount}" : "Армия не назначена";
            if (foodText != null) foodText.text = wagon != null ? $"Еда: {wagon.AvailableMeals} / {wagon.capacity} · Радиус: {wagon.supplyRadius:0} м" : "Нет повозки";
            if (horseText != null) horseText.text = wagon != null && wagon.AttachedHorse != null ? "Лошадь: " + wagon.AttachedHorse.name : "Лошадь не привязана";
            if (statusText != null) statusText.text = wagon != null ? wagon.Status : "Назначьте повозку";
            if (warningText != null) warningText.text = army != null ? army.Warning : "";
            if (starvationText != null)
            {
                float seconds = army != null ? army.SecondsToStarvation : float.PositiveInfinity;
                starvationText.text = float.IsPositiveInfinity(seconds) ? "" : seconds > 0 ? $"До урона от голода: {seconds:0} сек." : "Бойцы теряют здоровье от голода!";
            }
        }
        private void Message(string text) { if (messageText != null) messageText.text = text; }
        private void OnDestroy()
        {
            if (assignSoldiersButton != null) assignSoldiersButton.onClick.RemoveListener(AssignSoldiers);
            if (bindCartButton != null) bindCartButton.onClick.RemoveListener(BindSelectedCart);
            if (assignEscortsButton != null) assignEscortsButton.onClick.RemoveListener(AssignEscorts);
            if (removeSoldiersButton != null) removeSoldiersButton.onClick.RemoveListener(RemoveSoldiers);
            if (hitchHorseButton != null) hitchHorseButton.onClick.RemoveListener(HitchHorse);
            if (unhitchHorseButton != null) unhitchHorseButton.onClick.RemoveListener(UnhitchHorse);
            if (refillButton != null) refillButton.onClick.RemoveListener(Refill);
            if (followButton != null) followButton.onClick.RemoveListener(Follow);
            if (stopFollowingButton != null) stopFollowingButton.onClick.RemoveListener(StopFollowing);
        }
    }
}
