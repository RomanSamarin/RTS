using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using _Game.Scripts.UI;

/// <summary>Controller for the user's Canvas. Configures the selected merchant ship.</summary>
public class ShipTradeRoutePanel : MonoBehaviour
{
    public bool followSelectedShip=true;
    public ShipTradeRoute route;
    public TMP_Dropdown sourceDropdown,destinationDropdown,resourceDropdown;
    public TMP_InputField amountInput,departureMealsInput;
    public Toggle repeatToggle;
    public Button addOutboundButton,addReturnButton,clearCargoButton,startButton,pauseButton,resumeButton,cancelButton;
    public TMP_Text cargoText,statusText,messageText;
    private readonly List<Port> ports=new List<Port>();
    private static readonly ResourceType[] types=(ResourceType[])Enum.GetValues(typeof(ResourceType));
    private float nextRefreshAt;
    private void Awake()
    {
        if(resourceDropdown!=null){resourceDropdown.ClearOptions();var names=new List<string>();foreach(var type in types)names.Add(PortPanel.Label(type));resourceDropdown.AddOptions(names);}
        if(addOutboundButton!=null)addOutboundButton.onClick.AddListener(AddOutbound);
        if(addReturnButton!=null)addReturnButton.onClick.AddListener(AddReturn);
        if(clearCargoButton!=null)clearCargoButton.onClick.AddListener(ClearCargo);
        if(startButton!=null)startButton.onClick.AddListener(StartRoute);
        if(pauseButton!=null)pauseButton.onClick.AddListener(PauseRoute);
        if(resumeButton!=null)resumeButton.onClick.AddListener(ResumeRoute);
        if(cancelButton!=null)cancelButton.onClick.AddListener(CancelRoute);
        Bind(route);
    }
    public void Bind(ShipTradeRoute value)
    {
        route=value;RefreshPorts(true);
        if(route!=null){if(repeatToggle!=null)repeatToggle.isOn=route.repeat;if(departureMealsInput!=null)departureMealsInput.text=route.departureMeals.ToString();}
        Refresh();
    }
    private bool Controllable=>route!=null&&route.isActiveAndEnabled&&FactionMember.IsPlayer(route.gameObject);
    private bool Editable=>Controllable&&!route.IsRunning;
    private void Message(string message){if(messageText!=null)messageText.text=message;}
    private void Add(bool returning)
    {
        if(!Editable)return;
        if(amountInput==null||!int.TryParse(amountInput.text,out int amount)||amount<=0){Message("Введите положительное количество товара");return;}
        var type=types[Mathf.Clamp(resourceDropdown!=null?resourceDropdown.value:0,0,types.Length-1)];
        var entries=returning?route.returnCargo:route.outbound;
        if(entries==null){entries=new List<ShipTradeRoute.Freight>();if(returning)route.returnCargo=entries;else route.outbound=entries;}
        var found=entries.Find(x=>x!=null&&x.type==type);
        if(found!=null)found.amount=amount;else entries.Add(new ShipTradeRoute.Freight{type=type,amount=amount});
        Message("");Refresh();
    }
    public void AddOutbound(){Add(false);}
    public void AddReturn(){Add(true);}
    public void ClearCargo(){if(Editable){route.outbound?.Clear();route.returnCargo?.Clear();}Refresh();}
    private Port Selected(TMP_Dropdown dropdown)=>dropdown!=null&&dropdown.value>=0&&dropdown.value<ports.Count?ports[dropdown.value]:null;
    public void StartRoute()
    {
        if(!Editable)return;
        if(sourceDropdown!=null)route.sourcePort=Selected(sourceDropdown);
        if(destinationDropdown!=null)route.destinationPort=Selected(destinationDropdown);
        if(repeatToggle!=null)route.repeat=repeatToggle.isOn;
        if(departureMealsInput!=null)
        {if(!int.TryParse(departureMealsInput.text,out int food)||food<0){Message("Введите запас припасов от нуля до вместимости");return;}route.departureMeals=food;}
        bool started=route.TryStartRoute();Message(started?"":route.Status);Refresh();
    }
    public void PauseRoute(){if(Controllable)route.PauseRoute();Refresh();}
    public void ResumeRoute(){if(Controllable)route.ResumeRoute();Refresh();}
    public void CancelRoute(){if(Controllable)route.CancelRoute();Refresh();}
    private void RefreshPorts(bool force)
    {
        var found=new List<Port>();
        foreach(var port in UnityEngine.Object.FindObjectsOfType<Port>())
            if(port.isActiveAndEnabled&&(route==null?FactionMember.IsPlayer(port.gameObject):FactionMember.Of(port.gameObject)==FactionMember.Of(route.gameObject)))found.Add(port);
        found.Sort((a,b)=>string.CompareOrdinal(a.name,b.name));
        bool changed=force||found.Count!=ports.Count;
        if(!changed)for(int i=0;i<found.Count;i++)if(found[i]!=ports[i]){changed=true;break;}
        if(!changed)return;
        var source=force&&route!=null?route.sourcePort:Selected(sourceDropdown);
        var destination=force&&route!=null?route.destinationPort:Selected(destinationDropdown);
        ports.Clear();ports.AddRange(found);var labels=new List<string>();foreach(var port in ports)labels.Add(port.name);
        if(sourceDropdown!=null){sourceDropdown.ClearOptions();sourceDropdown.AddOptions(labels);sourceDropdown.value=Mathf.Max(0,ports.IndexOf(source));}
        if(destinationDropdown!=null){destinationDropdown.ClearOptions();destinationDropdown.AddOptions(labels);destinationDropdown.value=destination!=null?Mathf.Max(0,ports.IndexOf(destination)):Mathf.Min(1,Mathf.Max(0,ports.Count-1));}
    }
    private static void Describe(StringBuilder text,string heading,List<ShipTradeRoute.Freight> entries)
    {
        text.AppendLine(heading);if(entries==null||entries.Count==0){text.AppendLine("— Пустой переход");return;}
        foreach(var entry in entries)if(entry!=null)text.Append(PortPanel.Label(entry.type)).Append(": ").Append(entry.amount).AppendLine();
    }
    public void Refresh()
    {
        if(cargoText!=null)
        {var text=new StringBuilder();if(route!=null){Describe(text,"В порт назначения",route.outbound);Describe(text,"Обратный груз",route.returnCargo);}cargoText.text=text.ToString();}
        if(statusText!=null)statusText.text=route!=null?$"{route.Status}\nВыполнено циклов: {route.CompletedCycles}":"Выберите торговый корабль";
        foreach(var button in new[]{addOutboundButton,addReturnButton,clearCargoButton,startButton})if(button!=null)button.interactable=Editable;
        if(pauseButton!=null)pauseButton.interactable=Controllable&&route.IsRunning;
        if(resumeButton!=null)resumeButton.interactable=Controllable&&route.Current==ShipTradeRoute.Phase.Paused;
        if(cancelButton!=null)cancelButton.interactable=Controllable&&(route.IsRunning||route.Current==ShipTradeRoute.Phase.Paused);
        foreach(var dropdown in new[]{sourceDropdown,destinationDropdown,resourceDropdown})if(dropdown!=null)dropdown.interactable=Editable;
        if(amountInput!=null)amountInput.interactable=Editable;if(departureMealsInput!=null)departureMealsInput.interactable=Editable;if(repeatToggle!=null)repeatToggle.interactable=Editable;
    }
    private void Update()
    {
        if(Time.unscaledTime<nextRefreshAt)return;nextRefreshAt=Time.unscaledTime+.25f;
        if(followSelectedShip)
        {
            var selected=ManagerGO.Instance!=null?ManagerGO.Instance.unitsSelected:null;
            var candidate=selected!=null&&selected.Count==1&&selected[0]!=null?selected[0].GetComponent<ShipTradeRoute>():null;
            if(candidate!=null&&candidate.GetComponent<ShipHull>()?.type!=ShipType.Trade)candidate=null;
            if(candidate!=route)Bind(candidate);
        }
        RefreshPorts(false);Refresh();
    }
    private void OnDestroy()
    {
        if(addOutboundButton!=null)addOutboundButton.onClick.RemoveListener(AddOutbound);if(addReturnButton!=null)addReturnButton.onClick.RemoveListener(AddReturn);
        if(clearCargoButton!=null)clearCargoButton.onClick.RemoveListener(ClearCargo);if(startButton!=null)startButton.onClick.RemoveListener(StartRoute);
        if(pauseButton!=null)pauseButton.onClick.RemoveListener(PauseRoute);if(resumeButton!=null)resumeButton.onClick.RemoveListener(ResumeRoute);if(cancelButton!=null)cancelButton.onClick.RemoveListener(CancelRoute);
    }
}
