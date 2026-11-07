using TMPro;
using UnityEngine;

/// <summary>Поля и кнопки создаются пользователем; панель читает выбранную коптильню.</summary>
public class SmokehousePanel : MonoBehaviour
{
    public Smokehouse target;
    public TMP_Text stockText,workersText,statusText;
    public UnityEngine.UI.Slider progress;
    private Smokehouse Target=>target!=null?target:
        BuildingSelectionManager.Instance?.Current?.GetComponentInParent<Smokehouse>();
    private void Update()
    {
        var building=Target;
        var hiring=building!=null?building.GetComponent<WorkplaceHiring>():null;
        if(stockText!=null)stockText.text=building!=null?$"Сырьё: {building.StoredInput}/{building.inputCapacity}\nГотовая еда: {building.StoredOutput}/{building.outputCapacity}":"";
        if(workersText!=null)workersText.text=hiring!=null?$"Рабочие: {hiring.WorkerCount}/{hiring.MaxWorkers}":"";
        if(statusText!=null)statusText.text=building==null?"":building.CanProduce?(hiring!=null&&hiring.WorkerCount>0?"Готовит":"Нет работников"):building.WaitingTask;
        if(progress!=null)progress.normalizedValue=building!=null?Mathf.Clamp01(building.WorkProgress/Mathf.Max(.1f,building.secondsPerBatch)):0;
    }
    public void Hire(){var building=Target;if(building!=null&&FactionMember.IsPlayer(building.gameObject))building.GetComponent<WorkplaceHiring>()?.Hire();}
    public void Fire(){var building=Target;if(building!=null&&FactionMember.IsPlayer(building.gameObject))building.GetComponent<WorkplaceHiring>()?.Fire();}
}
