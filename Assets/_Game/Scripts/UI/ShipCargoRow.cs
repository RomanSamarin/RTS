using TMPro;
using UnityEngine;
using UnityEngine.UI;
using _Game.Scripts.UI;

/// <summary>A manually laid out resource row in the selected ship's Canvas.</summary>
public class ShipCargoRow : MonoBehaviour
{
    public ResourceType resource;
    public Sprite resourceIcon;
    public Image icon;
    public TMP_Text nameText, shipAmountText, portAmountText;
    public TMP_InputField amountInput;
    public Button decreaseButton, increaseButton;
    [Min(1)] public int step=10;
    public Slider cargoSlider;
    public int Amount=>amountInput!=null&&int.TryParse(amountInput.text,out int amount)?Mathf.Max(0,amount):0;
    public bool ValidAmount=>amountInput==null||int.TryParse(amountInput.text,out int value)&&value>=0;
    private void Awake()
    {
        if(decreaseButton!=null)decreaseButton.onClick.AddListener(Decrease);
        if(increaseButton!=null)increaseButton.onClick.AddListener(Increase);
        if(icon!=null&&resourceIcon!=null)icon.sprite=resourceIcon;
        if(nameText!=null)nameText.text=PortPanel.Label(resource);
        if(amountInput!=null)amountInput.contentType=TMP_InputField.ContentType.IntegerNumber;
    }
    public void ResetAmount(){if(amountInput!=null)amountInput.SetTextWithoutNotify("0");}
    public void Decrease(){if(amountInput!=null)amountInput.text=Mathf.Max(0,Amount-Mathf.Max(1,step)).ToString();}
    public void Increase(){if(amountInput!=null)amountInput.text=((int)System.Math.Min(int.MaxValue,(long)Amount+Mathf.Max(1,step))).ToString();}
    public void Refresh(ShipCargo cargo,Port port,bool canTransfer)
    {
        int amount=cargo!=null?cargo.Hold.Amount(resource):0;
        if(shipAmountText!=null)shipAmountText.text=amount.ToString();
        if(portAmountText!=null)portAmountText.text=port!=null?port.Storage.Available(resource).ToString():"—";
        if(cargoSlider!=null){cargoSlider.minValue=0;cargoSlider.maxValue=cargo!=null?Mathf.Max(1,cargo.Hold.Capacity):1;cargoSlider.value=amount;}
        if(amountInput!=null)amountInput.interactable=canTransfer;
        if(decreaseButton!=null)decreaseButton.interactable=canTransfer;
        if(increaseButton!=null)increaseButton.interactable=canTransfer;
    }
    private void OnDestroy()
    {
        if(decreaseButton!=null)decreaseButton.onClick.RemoveListener(Decrease);
        if(increaseButton!=null)increaseButton.onClick.RemoveListener(Increase);
    }
}
