using TMPro;
using UnityEngine;

/// <summary>Не создаёт Canvas. Показывает подключение выбранного здания в вашем TMP-тексте.</summary>
public class BuildingRoadStatusPanel : MonoBehaviour
{
    public TMP_Text statusText;
    private float nextRefresh;
    private void Update()
    {
        if(statusText==null||Time.unscaledTime<nextRefresh)return;
        nextRefresh=Time.unscaledTime+.2f;
        var selected=BuildingSelectionManager.Instance!=null?BuildingSelectionManager.Instance.Current:null;
        var access=selected!=null?selected.GetComponentInParent<BuildingRoadAccess>():null;
        statusText.text=access!=null?access.Status:"";
    }
}
