using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Подключается к созданному игроком UI; новые элементы интерфейса не создаёт.</summary>
public class DeliveryPriorityPanel : MonoBehaviour
{
    [Tooltip("Необязательно: фиксированное здание. Иначе используется выбранное здание.")]
    public GameObject targetBuilding;
    public TMP_Dropdown priorityDropdown;
    public TMP_Text priorityText;
    private static readonly string[] labels={"Низкий","Обычный","Высокий","Срочный"};
    private void OnEnable()
    {
        if(priorityDropdown!=null)
        {
            priorityDropdown.ClearOptions();
            priorityDropdown.AddOptions(new List<string>(labels));
            priorityDropdown.onValueChanged.AddListener(SetPriority);
        }
        Refresh();
    }
    private void OnDisable()
    {if(priorityDropdown!=null)priorityDropdown.onValueChanged.RemoveListener(SetPriority);}
    private GameObject Target()
    {
        if(targetBuilding!=null)return targetBuilding;
        var selected=BuildingSelectionManager.Instance!=null?BuildingSelectionManager.Instance.Current:null;
        if(selected==null)return null;
        var existing=selected.GetComponentInParent<DeliveryPriority>();
        if(existing!=null)return existing.gameObject;
        // Обычно UiSelectBuilding находится на корне вместе с производством и здоровьем.
        return selected.gameObject;
    }
    private void Update()=>Refresh();
    private void Refresh()
    {
        var target=Target();
        bool available=target!=null&&target.activeInHierarchy&&FactionMember.IsPlayer(target);
        int value=(int)DeliveryPriority.Of(target);
        if(priorityDropdown!=null)
        {priorityDropdown.interactable=available;priorityDropdown.SetValueWithoutNotify(value);}
        if(priorityText!=null)priorityText.text=available?"Приоритет доставки: "+labels[value]:"";
    }
    public void SetPriority(int value)
    {
        var target=Target();
        if(target==null||!target.activeInHierarchy||!FactionMember.IsPlayer(target))return;
        var setting=target.GetComponent<DeliveryPriority>();
        if(setting==null)setting=target.AddComponent<DeliveryPriority>();
        setting.SetPriority(value);Refresh();
    }
    public void SetLow()=>SetPriority(0);
    public void SetNormal()=>SetPriority(1);
    public void SetHigh()=>SetPriority(2);
    public void SetUrgent()=>SetPriority(3);
}
