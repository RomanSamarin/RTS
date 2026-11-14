using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using _Game.Scripts.UI;

public class PortPanel : MonoBehaviour
{
    public Port port;
    public TMP_Dropdown resourceDropdown;
    public TMP_InputField amountInput;
    public Button loadButton, unloadButton, unloadAllButton, cancelButton, stockTargetButton, clearStockTargetButton, hireButton, fireButton;
    public TMP_Text portStockText, shipStockText, statusText, workersText, messageText;
    private float nextRefreshAt;
    private static readonly ResourceType[] types = (ResourceType[])System.Enum.GetValues(typeof(ResourceType));
    private void Awake()
    {
        if (resourceDropdown != null)
        {
            resourceDropdown.ClearOptions(); var options = new System.Collections.Generic.List<string>();
            foreach (var type in types) options.Add(Label(type)); resourceDropdown.AddOptions(options);
        }
        if (loadButton != null) loadButton.onClick.AddListener(Load);
        if (unloadButton != null) unloadButton.onClick.AddListener(Unload);
        if (unloadAllButton != null) unloadAllButton.onClick.AddListener(UnloadAll);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        if (stockTargetButton != null) stockTargetButton.onClick.AddListener(StockTarget);
        if (clearStockTargetButton != null) clearStockTargetButton.onClick.AddListener(ClearStockTarget);
        if (hireButton != null) hireButton.onClick.AddListener(Hire);
        if (fireButton != null) fireButton.onClick.AddListener(Fire);
    }
    public void Bind(Port value) { port = value; Refresh(); }
    private bool CanControl => port != null && port.isActiveAndEnabled && FactionMember.IsPlayer(port.gameObject);
    private ResourceType Selected => types[Mathf.Clamp(resourceDropdown != null ? resourceDropdown.value : 0, 0, types.Length - 1)];
    private int Amount()
    {
        if (amountInput != null && int.TryParse(amountInput.text, out int value) && value > 0) return value;
        Message("Введите положительное целое количество товара"); return 0;
    }
    public void Load() { Message("Выберите корабль и используйте его грузовую панель"); }
    public void Unload() { Load(); }
    public void UnloadAll() { Load(); }
    public void Cancel() { Load(); }
    public void StockTarget() { if (CanControl) { int amount = Amount(); if (amount > 0) port.SetStockTarget(Selected, amount); } Refresh(); }
    public void ClearStockTarget() { if (CanControl) port.SetStockTarget(Selected, 0); Refresh(); }
    public void Hire() { if (CanControl) port.GetComponent<WorkplaceHiring>()?.TryHire(); Refresh(); }
    public void Fire() { if (CanControl) port.GetComponent<WorkplaceHiring>()?.TryFire(); Refresh(); }
    private void Message(string value) { if (messageText != null) messageText.text = value; }
    private void Update() { if (Time.unscaledTime >= nextRefreshAt) { nextRefreshAt = Time.unscaledTime + .2f; Refresh(); } }
    public void Refresh()
    {
        var ship = port != null && port.Dock != null && port.Dock.Ship != null ? port.Dock.Ship.GetComponent<ShipCargo>() : null;
        if (portStockText != null) portStockText.text = port != null ? Describe("Склад порта", port.Storage) : "Выберите порт";
        if (shipStockText != null) shipStockText.text = ship != null ? Describe("Трюм", ship.Hold) : "Торговый корабль не назначен";
        if (statusText != null) statusText.text = port != null ? $"{port.WaitingTask}\nОсталось: {port.Remaining} · Операций: {port.OrderCount}" : "";
        var hiring = port != null ? port.GetComponent<WorkplaceHiring>() : null;
        if (workersText != null) workersText.text = hiring != null ? $"Рабочие: {hiring.WorkerCount} / {hiring.MaxWorkers}" : "";
        foreach (var button in new[] { stockTargetButton, clearStockTargetButton, hireButton, fireButton })
            if (button != null) button.interactable = CanControl;
        foreach(var button in new[]{loadButton,unloadButton,unloadAllButton,cancelButton})
            if(button!=null)button.interactable=false;
    }
    private static string Describe(string title, CargoHold hold)
    {
        var text = new StringBuilder($"{title}: {hold.Count} / {hold.Capacity} · Свободно: {hold.FreeSpace}\n");
        foreach (var type in types) if (hold.Amount(type) > 0) text.Append(Label(type)).Append(": ").Append(hold.Amount(type)).Append('\n');
        return text.ToString();
    }
    public static string Label(ResourceType type) => type switch
    {
        ResourceType.Wood => "Дерево", ResourceType.Stone => "Камень", ResourceType.Iron => "Железо", ResourceType.Gold => "Золото",
        ResourceType.Wheat => "Пшеница", ResourceType.Flour => "Мука", ResourceType.Bread => "Хлеб", ResourceType.Food => "Готовая еда",
        ResourceType.Fish => "Сырая рыба", ResourceType.Meat => "Сырое мясо", ResourceType.Berries => "Ягоды",
        ResourceType.Sword => "Мечи", ResourceType.Spear => "Копья", ResourceType.Bow => "Луки", ResourceType.Shield => "Щиты",
        ResourceType.SiegeLadder => "Осадные лестницы", _ => type.ToString()
    };
    private void OnDestroy()
    {
        if (loadButton != null) loadButton.onClick.RemoveListener(Load);
        if (unloadButton != null) unloadButton.onClick.RemoveListener(Unload);
        if (unloadAllButton != null) unloadAllButton.onClick.RemoveListener(UnloadAll);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
        if (stockTargetButton != null) stockTargetButton.onClick.RemoveListener(StockTarget);
        if (clearStockTargetButton != null) clearStockTargetButton.onClick.RemoveListener(ClearStockTarget);
        if (hireButton != null) hireButton.onClick.RemoveListener(Hire);
        if (fireButton != null) fireButton.onClick.RemoveListener(Fire);
    }
}
