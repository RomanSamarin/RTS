using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bind these fields to the user's Canvas; follows a single selected ship.</summary>
public class ShipCrewPanel : MonoBehaviour
{
    public TMP_Text crewText, provisionsText, statusText, warningText;
    public Button recruitButton, cancelRecruitmentButton, disembarkButton;
    private ShipCrew selected;
    private void Awake()
    {
        if(recruitButton!=null)recruitButton.onClick.AddListener(Recruit);
        if(cancelRecruitmentButton!=null)cancelRecruitmentButton.onClick.AddListener(CancelRecruitment);
        if(disembarkButton!=null)disembarkButton.onClick.AddListener(Disembark);
    }
    public void Recruit(){if(selected!=null)selected.RecruitCrew();}
    public void CancelRecruitment(){if(selected!=null)selected.CancelRecruitment();}
    public void Disembark(){if(selected!=null)selected.DisembarkCrew();}
    private void Update()
    {
        var selection=ManagerGO.Instance!=null?ManagerGO.Instance.unitsSelected:null;
        selected=selection!=null&&selection.Count==1&&selection[0]!=null&&FactionMember.IsPlayer(selection[0])?selection[0].GetComponent<ShipCrew>():null;
        if(crewText!=null)crewText.text=selected!=null?$"Экипаж: {selected.Aboard}/{selected.Required} · Идут: {selected.Incoming}":"Экипаж: —";
        var stock=selected!=null?selected.GetComponent<ShipProvisions>():null;
        if(provisionsText!=null)provisionsText.text=stock!=null?$"Припасы: {stock.StoredMeals}/{stock.capacity}":"Припасы: —";
        if(statusText!=null)statusText.text=selected!=null?selected.GetComponent<ShipNavigation>().Status+" · "+selected.Status:"Выберите корабль";
        if(warningText!=null)warningText.text=selected!=null?selected.Warning:"";
        if(recruitButton!=null)recruitButton.interactable=selected!=null&&selected.Dock!=null&&selected.Assigned<selected.Required;
        if(cancelRecruitmentButton!=null)cancelRecruitmentButton.interactable=selected!=null&&selected.autoRecruit;
        if(disembarkButton!=null)disembarkButton.interactable=selected!=null&&selected.Dock!=null&&selected.Aboard>0;
    }
    private void OnDestroy()
    {
        if(recruitButton!=null)recruitButton.onClick.RemoveListener(Recruit);
        if(cancelRecruitmentButton!=null)cancelRecruitmentButton.onClick.RemoveListener(CancelRecruitment);
        if(disembarkButton!=null)disembarkButton.onClick.RemoveListener(Disembark);
    }
}
