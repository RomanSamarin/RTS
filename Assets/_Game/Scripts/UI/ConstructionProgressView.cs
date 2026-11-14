using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ConstructionProgressView : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField] private TMP_Text percentText;
    [SerializeField] private bool faceCamera = true;

    private ConstructionSite site;
    private int displayedPercent = -1;

    internal void Bind(ConstructionSite construction)
    {
        site = construction;
        UpdateProgress();
    }

    private void LateUpdate()
    {
        if (site == null || SaveGameManager.IsLoading) return;
        if (faceCamera && Camera.main is Camera camera)
            transform.rotation = camera.transform.rotation;
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        float progress = Mathf.Clamp01(site.Progress01);
        if (fill != null) fill.fillAmount = progress;
        int percent = Mathf.RoundToInt(progress * 100);
        if (percentText != null && percent != displayedPercent)
            percentText.text = percent + "%";
        displayedPercent = percent;
    }
}
