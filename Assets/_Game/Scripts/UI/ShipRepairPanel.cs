using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Optional section of the user's port Canvas.</summary>
public class ShipRepairPanel : MonoBehaviour
{
    public Port yard;
    public Button repairSelectedButton,repairMooredButton,cancelRepairButton;
    public TMP_Text shipText,healthText,costText,statusText;
    public Slider progressSlider;
    private void Awake()
    {
        if(repairSelectedButton!=null)repairSelectedButton.onClick.AddListener(RepairSelected);
        if(repairMooredButton!=null)repairMooredButton.onClick.AddListener(RepairMoored);
        if(cancelRepairButton!=null)cancelRepairButton.onClick.AddListener(CancelRepair);
    }
    public void Bind(Port value){yard=value;Refresh();}
    private bool CanControl=>yard!=null&&yard.isActiveAndEnabled&&FactionMember.IsPlayer(yard.gameObject);
    public void RepairSelected(){if(CanControl)yard.RepairSelectedShip();Refresh();}
    public void RepairMoored(){if(CanControl)yard.RepairMooredShip();Refresh();}
    public void CancelRepair(){if(CanControl)yard.CancelRepair();Refresh();}
    public void Refresh()
    {
        var target=yard!=null?yard.RepairTarget:null;var unit=target!=null?target.GetComponent<Unit>():null;
        if(shipText!=null)shipText.text=target!=null?target.name:"Корабль не назначен";
        if(healthText!=null)healthText.text=unit!=null?$"Корпус: {unit.unitHealth:0}/{unit.unitMaxHealth:0}":"";
        if(costText!=null)costText.text=yard!=null?$"{yard.repairHealthPerCycle:0} HP · Дерево: {yard.repairWoodPerCycle} · Железо: {yard.repairIronPerCycle} · {yard.repairSecondsPerCycle:0.#} с работы":"";
        if(statusText!=null)statusText.text=yard!=null?yard.RepairStatus:"Выберите порт";
        if(progressSlider!=null){progressSlider.minValue=0;progressSlider.maxValue=1;progressSlider.value=yard!=null&&yard.IsRepairing?Mathf.Clamp01(yard.WorkProgress/Mathf.Max(.1f,yard.repairSecondsPerCycle)):0;}
        if(repairSelectedButton!=null)repairSelectedButton.interactable=CanControl&&!yard.IsRepairing;
        if(repairMooredButton!=null)repairMooredButton.interactable=CanControl&&!yard.IsRepairing;
        if(cancelRepairButton!=null)cancelRepairButton.interactable=CanControl&&yard.IsRepairing;
    }
    private void Update(){Refresh();}
    private void OnDestroy()
    {
        if(repairSelectedButton!=null)repairSelectedButton.onClick.RemoveListener(RepairSelected);
        if(repairMooredButton!=null)repairMooredButton.onClick.RemoveListener(RepairMoored);
        if(cancelRepairButton!=null)cancelRepairButton.onClick.RemoveListener(CancelRepair);
    }
}
