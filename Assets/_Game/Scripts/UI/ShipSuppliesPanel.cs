using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShipSuppliesPanel : MonoBehaviour
{
    public ShipSupplyDock dock;
    public Button bindSelectedButton, bindNearestButton, loadButton, stopButton, unmoorButton;
    public TMP_Text shipText, provisionsText, reservedText, statusText, messageText;
    public Button recruitCrewButton, cancelCrewButton, disembarkCrewButton;
    public TMP_Text crewText, crewWarningText;
    private float nextRefreshAt;
    private void Awake()
    {
        if (bindSelectedButton != null) bindSelectedButton.onClick.AddListener(BindSelected);
        if (bindNearestButton != null) bindNearestButton.onClick.AddListener(BindNearest);
        if (loadButton != null) loadButton.onClick.AddListener(Load);
        if (stopButton != null) stopButton.onClick.AddListener(StopLoading);
        if (unmoorButton != null) unmoorButton.onClick.AddListener(Unmoor);
        if (recruitCrewButton != null) recruitCrewButton.onClick.AddListener(RecruitCrew);
        if (cancelCrewButton != null) cancelCrewButton.onClick.AddListener(CancelCrew);
        if (disembarkCrewButton != null) disembarkCrewButton.onClick.AddListener(DisembarkCrew);
    }
    public void Bind(ShipSupplyDock value) { dock = value; Refresh(); }
    private bool CanControl => dock != null && dock.isActiveAndEnabled && FactionMember.IsPlayer(dock.gameObject);
    public void BindSelected()
    {
        if (!CanControl || ManagerGO.Instance == null) return;
        foreach (var selected in ManagerGO.Instance.unitsSelected)
        {
            var ship = selected != null ? selected.GetComponent<ShipHull>() : null;
            if (ship != null && dock.AssignShip(ship)) { Refresh(); return; }
        }
        if (messageText != null) messageText.text = "Выберите свободный корабль: требуется водный путь и свободный причал";
    }
    public void BindNearest() { if (CanControl) dock.AssignNearestShip(); Refresh(); }
    public void Load() { if (CanControl) dock.RequestSupplies(); Refresh(); }
    public void StopLoading() { if (CanControl) dock.StopSupplies(); Refresh(); }
    public void Unmoor() { if (CanControl) dock.UnassignShip(); Refresh(); }
    private ShipCrew Crew => dock!=null&&dock.Ship!=null?dock.Ship.GetComponent<ShipCrew>():null;
    public void RecruitCrew() { if(CanControl)Crew?.RecruitCrew(); Refresh(); }
    public void CancelCrew() { if(CanControl)Crew?.CancelRecruitment(); Refresh(); }
    public void DisembarkCrew() { if(CanControl)Crew?.DisembarkCrew(); Refresh(); }
    private void Update()
    { if (Time.unscaledTime >= nextRefreshAt) { nextRefreshAt = Time.unscaledTime + .2f; Refresh(); } }
    public void Refresh()
    {
        var ship = dock != null ? dock.Ship : null;
        if (shipText != null) shipText.text = ship != null ? ship.name : dock != null && dock.ReservedShip != null
            ? dock.ReservedShip.name + " — подходит" : "Корабль не назначен";
        if (provisionsText != null) provisionsText.text = ship != null ? $"Припасы: {ship.StoredMeals} / {ship.capacity}" : "Припасы: —";
        if (reservedText != null) reservedText.text = ship != null ? $"Ожидает доставки: {ship.ReservedMeals}" : "";
        if (statusText != null) statusText.text = dock != null ? dock.Status : "Назначьте причал";
        if (loadButton != null) loadButton.interactable = CanControl;
        if (stopButton != null) stopButton.interactable = CanControl && ship != null;
        if (bindSelectedButton != null) bindSelectedButton.interactable = CanControl;
        if (bindNearestButton != null) bindNearestButton.interactable = CanControl;
        if (unmoorButton != null) unmoorButton.interactable = CanControl && (ship != null || dock.ReservedShip != null);
        var crew=Crew;
        if(crewText!=null)crewText.text=crew!=null?$"Экипаж: {crew.Aboard}/{crew.Required} · Идут: {crew.Incoming}":"Экипаж: —";
        if(crewWarningText!=null)crewWarningText.text=crew!=null?crew.Warning:"";
        if(recruitCrewButton!=null)recruitCrewButton.interactable=CanControl&&crew!=null&&crew.Assigned<crew.Required;
        if(cancelCrewButton!=null)cancelCrewButton.interactable=CanControl&&crew!=null&&crew.autoRecruit;
        if(disembarkCrewButton!=null)disembarkCrewButton.interactable=CanControl&&crew!=null&&crew.Aboard>0;
    }
    private void OnDestroy()
    {
        if (bindSelectedButton != null) bindSelectedButton.onClick.RemoveListener(BindSelected);
        if (bindNearestButton != null) bindNearestButton.onClick.RemoveListener(BindNearest);
        if (loadButton != null) loadButton.onClick.RemoveListener(Load);
        if (stopButton != null) stopButton.onClick.RemoveListener(StopLoading);
        if (unmoorButton != null) unmoorButton.onClick.RemoveListener(Unmoor);
        if (recruitCrewButton != null) recruitCrewButton.onClick.RemoveListener(RecruitCrew);
        if (cancelCrewButton != null) cancelCrewButton.onClick.RemoveListener(CancelCrew);
        if (disembarkCrewButton != null) disembarkCrewButton.onClick.RemoveListener(DisembarkCrew);
    }
}
