using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WatchtowerPanel : MonoBehaviour
{
    public Button hireButton, fireButton;
    public TMP_Text statusText, workersText, radiusText;
    private Watchtower tower;

    public void Bind(Watchtower selected)
    {
        tower = selected;
        Refresh();
    }

    private void OnEnable()
    {
        if (hireButton != null) hireButton.onClick.AddListener(Hire);
        if (fireButton != null) fireButton.onClick.AddListener(Fire);
        Refresh();
    }

    private void OnDisable()
    {
        if (hireButton != null) hireButton.onClick.RemoveListener(Hire);
        if (fireButton != null) fireButton.onClick.RemoveListener(Fire);
    }

    private bool CanControl => tower != null && tower.isActiveAndEnabled
        && FactionMember.IsPlayer(tower.gameObject) && !SaveGameManager.IsLoading;

    private void Hire() { if (CanControl) tower.Hiring.TryHire(); }
    private void Fire() { if (CanControl) tower.Hiring.TryFire(); }
    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        var hiring = tower != null ? tower.Hiring : null;
        int workers = hiring != null ? hiring.WorkerCount : 0;
        if (statusText != null) statusText.text = tower != null ? tower.Status : "";
        if (radiusText != null) radiusText.text = tower != null ? $"Радиус дозора: {tower.sightRadius:0} м" : "";
        if (workersText != null) workersText.text = hiring != null ? $"Дозорные: {workers}/{hiring.MaxWorkers}" : "";
        if (hireButton != null) hireButton.interactable = CanControl && hiring != null && workers < hiring.MaxWorkers;
        if (fireButton != null) fireButton.interactable = CanControl && workers > 0;
    }
}
