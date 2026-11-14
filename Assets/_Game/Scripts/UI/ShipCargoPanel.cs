using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Follows a single selected player ship. Keep this component on an active Canvas.</summary>
public class ShipCargoPanel : MonoBehaviour
{
    [Tooltip("Optional child containing the visuals. Do not assign this component's own GameObject.")]
    public GameObject contentRoot;
    public ShipCargoRow[] rows;
    public TMP_Text titleText, healthText, cargoText, statusText, messageText;
    public Slider healthSlider, cargoSlider, operationSlider;
    public Button loadButton, unloadButton, unloadAllButton, cancelTransferButton, repairButton, cancelRepairButton;
    public Button retreatButton, stopOrderButton;
    private ShipHull selected;
    private ShipCargo cargo;
    private Port port;
    private bool CanTransfer=>selected!=null&&selected.type==ShipType.Trade&&cargo!=null&&port!=null&&!port.IsRepairing;
    private void Awake()
    {
        if(rows==null||rows.Length==0)rows=GetComponentsInChildren<ShipCargoRow>(true);
        if(loadButton!=null)loadButton.onClick.AddListener(Load);
        if(unloadButton!=null)unloadButton.onClick.AddListener(Unload);
        if(unloadAllButton!=null)unloadAllButton.onClick.AddListener(UnloadAll);
        if(cancelTransferButton!=null)cancelTransferButton.onClick.AddListener(CancelTransfer);
        if(repairButton!=null)repairButton.onClick.AddListener(Repair);
        if(cancelRepairButton!=null)cancelRepairButton.onClick.AddListener(CancelRepair);
        if(retreatButton!=null)retreatButton.onClick.AddListener(Retreat);
        if(stopOrderButton!=null)stopOrderButton.onClick.AddListener(StopOrder);
    }
    private void Update(){Refresh();}
    public void Refresh()
    {
        var selection=ManagerGO.Instance!=null?ManagerGO.Instance.unitsSelected:null;
        var ship=selection!=null&&selection.Count==1&&selection[0]!=null&&FactionMember.IsPlayer(selection[0])?selection[0].GetComponent<ShipHull>():null;
        if(ship!=selected)
        {
            selected=ship;
            foreach(var row in rows)if(row!=null)row.ResetAmount();
            Message("");
        }
        if(contentRoot!=null&&contentRoot!=gameObject&&contentRoot.activeSelf!=(selected!=null))contentRoot.SetActive(selected!=null);
        cargo=selected!=null?selected.GetComponent<ShipCargo>():null;
        var nav=selected!=null?selected.GetComponent<ShipNavigation>():null;
        var dock=nav!=null?nav.DestinationDock:null;
        port=dock!=null&&nav.IsMooredTo(dock)&&dock.CanSupply(selected.GetComponent<ShipProvisions>())?dock.GetComponent<Port>():null;
        if(port!=null&&(!port.isActiveAndEnabled||!FactionMember.IsPlayer(port.gameObject)))port=null;
        var health=selected!=null?selected.GetComponent<Unit>():null;
        if(titleText!=null)titleText.text=selected!=null?selected.name:"Выберите корабль";
        if(healthText!=null)healthText.text=health!=null?$"Корпус: {health.unitHealth:0}/{health.unitMaxHealth:0}":"";
        var transport=selected!=null?selected.GetComponent<ShipTransport>():null;
        var combat=selected!=null?selected.GetComponent<ShipCombat>():null;
        if(cargoText!=null)
        {
            if(combat!=null&&transport!=null) cargoText.text=$"Лучники: {transport.AboardSeats}/{transport.Capacity}";
            else if(selected!=null&&selected.type==ShipType.Trade&&cargo!=null) cargoText.text=$"Груз: {cargo.Hold.Count}/{cargo.Hold.Capacity}";
            else cargoText.text="";
        }
        var repair=selected!=null?selected.RepairPort:null;
        if(statusText!=null)
        {
            if(repair!=null) statusText.text=repair.RepairStatus;
            else if(combat!=null) statusText.text=selected.Status+" · "+combat.Status;
            else if(port!=null) statusText.text=port.WaitingTask;
            else if(selected!=null&&selected.type==ShipType.Trade) statusText.text=selected.Status+" · Для грузовых операций пришвартуйтесь к порту";
            else statusText.text=selected!=null?selected.Status:"";
        }
        SetSlider(healthSlider,health!=null?health.unitHealth:0,health!=null?health.unitMaxHealth:1);
        SetSlider(cargoSlider,cargo!=null?cargo.Hold.Count:0,cargo!=null?cargo.Hold.Capacity:1);
        SetSlider(operationSlider,port!=null?port.WorkProgress:0,port!=null?(port.IsRepairing?port.repairSecondsPerCycle:port.transferSeconds):1);
        foreach(var row in rows)if(row!=null)row.Refresh(cargo,port,CanTransfer);
        if(loadButton!=null)loadButton.interactable=CanTransfer;
        if(unloadButton!=null)unloadButton.interactable=CanTransfer;
        if(unloadAllButton!=null)unloadAllButton.interactable=CanTransfer&&cargo.Hold.Count>0;
        if(cancelTransferButton!=null)cancelTransferButton.interactable=CanTransfer&&port.TargetShip==cargo;
        if(repairButton!=null)repairButton.interactable=port!=null&&!port.IsRepairing&&health!=null&&health.unitHealth>0&&health.unitHealth<health.unitMaxHealth;
        if(cancelRepairButton!=null)cancelRepairButton.interactable=repair!=null;
        if(retreatButton!=null)retreatButton.interactable=nav!=null&&nav.isActiveAndEnabled;
        if(stopOrderButton!=null)stopOrderButton.interactable=nav!=null&&nav.isActiveAndEnabled;
    }
    private static void SetSlider(Slider slider,float value,float maximum)
    {if(slider!=null){slider.minValue=0;slider.maxValue=Mathf.Max(1,maximum);slider.value=value;}}
    private void Message(string message){if(messageText!=null)messageText.text=message;}
    public void Load(){Submit(false);}
    public void Unload(){Submit(true);}
    private void Submit(bool unload)
    {
        Refresh();if(!CanTransfer)return;
        var manifest=new Dictionary<_Game.Scripts.UI.ResourceType,int>();
        foreach(var row in rows)
        {
            if(row==null)continue;
            if(!row.ValidAmount){Message("Введите целые неотрицательные количества");return;}
            if(row.Amount==0)continue;
            if(manifest.ContainsKey(row.resource)){Message("Один ресурс должен иметь одну строку");return;}
            manifest.Add(row.resource,row.Amount);
        }
        if(!port.BeginManifest(cargo,manifest,unload)){Message(port.Status);return;}
        foreach(var row in rows)if(row!=null)row.ResetAmount();
        Message("Заказ принят. Груз переносят рабочие порта.");
    }
    public void UnloadAll(){Refresh();if(CanTransfer)port.UnloadAll();}
    public void CancelTransfer(){Refresh();if(CanTransfer&&port.TargetShip==cargo)port.CancelTransferFromPlayer();}
    public void Repair(){Refresh();if(port!=null){port.RequestRepair(selected);Message(port.RepairStatus);}}
    public void CancelRepair(){Refresh();if(selected!=null&&selected.RepairPort!=null)selected.RepairPort.CancelRepair();}
    public void Retreat()
    {
        Refresh();
        if(selected!=null)ManagerGO.Instance?.BeginRetreat();
    }
    public void StopOrder()
    {
        Refresh();
        if(selected!=null)ManagerGO.Instance?.StopSelected();
    }
    private void OnDestroy()
    {
        if(loadButton!=null)loadButton.onClick.RemoveListener(Load);
        if(unloadButton!=null)unloadButton.onClick.RemoveListener(Unload);
        if(unloadAllButton!=null)unloadAllButton.onClick.RemoveListener(UnloadAll);
        if(cancelTransferButton!=null)cancelTransferButton.onClick.RemoveListener(CancelTransfer);
        if(repairButton!=null)repairButton.onClick.RemoveListener(Repair);
        if(cancelRepairButton!=null)cancelRepairButton.onClick.RemoveListener(CancelRepair);
        if(retreatButton!=null)retreatButton.onClick.RemoveListener(Retreat);
        if(stopOrderButton!=null)stopOrderButton.onClick.RemoveListener(StopOrder);
    }
}
