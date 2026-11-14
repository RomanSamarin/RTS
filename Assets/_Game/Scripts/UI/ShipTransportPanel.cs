using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Select a transport first, then troops. The bound ship remains selected in this panel.</summary>
public class ShipTransportPanel : MonoBehaviour
{
    public ShipTransport transport;
    public bool bindSelectedTransport=true;
    public Button boardSelectedButton,cancelBoardingButton,disembarkButton;
    public TMP_Text capacityText,passengersText,provisionsText,statusText,messageText,warningText;
    private void Awake()
    {
        if(boardSelectedButton!=null)boardSelectedButton.onClick.AddListener(BoardSelected);
        if(cancelBoardingButton!=null)cancelBoardingButton.onClick.AddListener(CancelBoarding);
        if(disembarkButton!=null)disembarkButton.onClick.AddListener(Disembark);
    }
    public void Bind(ShipTransport value){transport=value;}
    private bool CanControl=>transport!=null&&transport.isActiveAndEnabled&&FactionMember.IsPlayer(transport.gameObject);
    public void BoardSelected()
    {if(!CanControl)return;int count=transport.BoardSelected();if(messageText!=null)messageText.text=$"Назначено на посадку: {count}";}
    public void CancelBoarding(){if(CanControl)transport.CancelBoarding();}
    public void Disembark(){if(CanControl)transport.DisembarkAll();}
    private void Update()
    {
        if(bindSelectedTransport&&ManagerGO.Instance!=null)
            foreach(var selected in ManagerGO.Instance.unitsSelected)
            {var candidate=selected!=null?selected.GetComponent<ShipTransport>():null;if(candidate!=null&&candidate.Capacity>0&&FactionMember.IsPlayer(candidate.gameObject)){transport=candidate;break;}}
        if(capacityText!=null)
        {
            if(transport==null) capacityText.text="Выберите транспортный или боевой корабль";
            else if(transport.IsArcherDeck) capacityText.text=$"Лучники: {transport.AboardSeats}/{transport.Capacity} · Идут на посадку: {transport.ReservedSeats}";
            else capacityText.text=$"Места: {transport.UsedSeats}/{transport.Capacity} · На борту: {transport.AboardSeats} · Резерв: {transport.ReservedSeats}";
        }
        var list=transport!=null?transport.Passengers:new List<ShipPassenger>();var text=new StringBuilder();
        foreach(var p in list)text.Append(p.name).Append(" · ").Append(p.SeatCost).Append(" мест · ").Append(p.Status).AppendLine();
        if(passengersText!=null)passengersText.text=text.ToString();
        var stock=transport!=null?transport.GetComponent<ShipProvisions>():null;
        if(provisionsText!=null)provisionsText.text=stock!=null?$"Припасы: {stock.StoredMeals}/{stock.capacity}":"";
        if(statusText!=null)statusText.text=transport!=null?transport.name+" · "+transport.Status:"";
        if(warningText!=null)warningText.text=transport!=null&&transport.HungryPassengers>0?$"Пассажиры голодают: {transport.HungryPassengers}. Пополните припасы":"";
        if(boardSelectedButton!=null)boardSelectedButton.interactable=CanControl&&transport.Dock!=null&&!transport.Disembarking&&transport.UsedSeats<transport.Capacity;
        if(cancelBoardingButton!=null)cancelBoardingButton.interactable=CanControl&&transport.ReservedSeats>0;
        if(disembarkButton!=null)disembarkButton.interactable=CanControl&&transport.Dock!=null&&transport.AboardSeats>0;
    }
    private void OnDestroy()
    {
        if(boardSelectedButton!=null)boardSelectedButton.onClick.RemoveListener(BoardSelected);
        if(cancelBoardingButton!=null)cancelBoardingButton.onClick.RemoveListener(CancelBoarding);
        if(disembarkButton!=null)disembarkButton.onClick.RemoveListener(Disembark);
    }
}
