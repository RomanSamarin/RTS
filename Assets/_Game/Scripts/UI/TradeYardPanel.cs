using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using _Game.Scripts.UI;

public sealed class TradeYardPanel : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown resourceDropdown;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private TMP_Text buyPriceText;
    [SerializeField] private TMP_Text sellPriceText;
    [SerializeField] private TMP_Text statusText;

    private readonly List<ResourceType> resources = new List<ResourceType>();
    private TradeYard yard;
    private int amount = 1;
    private float nextRefresh;

    private void OnEnable()
    {
        if (resourceDropdown != null) resourceDropdown.onValueChanged.AddListener(ChangeResource);
        if (minusButton != null) minusButton.onClick.AddListener(Decrease);
        if (plusButton != null) plusButton.onClick.AddListener(Increase);
        if (buyButton != null) buyButton.onClick.AddListener(Buy);
        if (sellButton != null) sellButton.onClick.AddListener(Sell);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        Refresh();
    }

    private void OnDisable()
    {
        if (resourceDropdown != null) resourceDropdown.onValueChanged.RemoveListener(ChangeResource);
        if (minusButton != null) minusButton.onClick.RemoveListener(Decrease);
        if (plusButton != null) plusButton.onClick.RemoveListener(Increase);
        if (buyButton != null) buyButton.onClick.RemoveListener(Buy);
        if (sellButton != null) sellButton.onClick.RemoveListener(Sell);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
    }

    public void Bind(TradeYard target)
    {
        yard = target;
        amount = 1;
        resources.Clear();
        var options = new List<string>();
        if (yard != null)
        {
            foreach (var price in yard.Prices)
            {
                if (price == null || resources.Contains(price.resource) || !yard.TryQuote(price.resource, out _, out _)) continue;
                resources.Add(price.resource);
                options.Add(PortPanel.Label(price.resource));
            }
        }
        if (resourceDropdown != null)
        {
            resourceDropdown.ClearOptions();
            resourceDropdown.AddOptions(options);
            resourceDropdown.SetValueWithoutNotify(0);
            resourceDropdown.RefreshShownValue();
        }
        Refresh();
    }

    private ResourceType Selected => resources[Mathf.Clamp(resourceDropdown != null ? resourceDropdown.value : 0, 0, resources.Count - 1)];
    private bool HasQuote => yard != null && resources.Count > 0;
    private int Maximum => HasQuote ? Mathf.Max(yard.MaxAmount(Selected, true), yard.MaxAmount(Selected, false)) : 0;

    private void ChangeResource(int index) { amount = 1; Refresh(); }
    private void Decrease() { amount = Mathf.Max(1, amount - 1); Refresh(); }
    private void Increase() { if (amount < Maximum) amount++; Refresh(); }
    private void Buy() { if (HasQuote) yard.BeginTrade(Selected, amount, true); Refresh(); }
    private void Sell() { if (HasQuote) yard.BeginTrade(Selected, amount, false); Refresh(); }
    private void Cancel() { if (yard != null) yard.CancelTrade(); Refresh(); }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .2f;
        Refresh();
    }

    private void Refresh()
    {
        bool canChoose = HasQuote && yard.CanControl && !yard.HasOrder;
        bool canTrade = canChoose && BuildingRoadAccess.Allows(yard);
        int maxBuy = HasQuote ? yard.MaxAmount(Selected, true) : 0;
        int maxSell = HasQuote ? yard.MaxAmount(Selected, false) : 0;
        if (resourceDropdown != null) resourceDropdown.interactable = canChoose;
        if (minusButton != null) minusButton.interactable = canChoose && amount > 1;
        if (plusButton != null) plusButton.interactable = canChoose && amount < Mathf.Max(maxBuy, maxSell);
        if (buyButton != null) buyButton.interactable = canTrade && amount <= maxBuy;
        if (sellButton != null) sellButton.interactable = canTrade && amount <= maxSell;
        if (cancelButton != null) cancelButton.interactable = yard != null && yard.CanControl && yard.HasOrder;
        if (amountText != null) amountText.text = HasQuote ? amount.ToString() : "—";
        int buy = 0, sell = 0;
        if (HasQuote) yard.TryQuote(Selected, out buy, out sell);
        if (buyPriceText != null) buyPriceText.text = HasQuote ? $"{(long)amount * buy} золота" : "—";
        if (sellPriceText != null) sellPriceText.text = HasQuote ? $"{(long)amount * sell} золота" : "—";
        if (statusText == null) return;
        if (yard == null) statusText.text = "";
        else if (!yard.HasOrder) statusText.text = yard.WaitingTask;
        else statusText.text = $"{(yard.Buying ? "Покупка" : "Продажа")} · {PortPanel.Label(yard.Commodity)} ×{yard.OrderAmount}\n" +
            $"{yard.WaitingTask} · {yard.StoredInput}/{yard.RequiredAmount} ({PortPanel.Label(yard.InputType)})";
    }
}
