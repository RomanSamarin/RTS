using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Attach to a user-created Canvas panel. Buttons do not create additional UI.</summary>
public class SiegeLadderCartPanel : MonoBehaviour
{
    public SiegeLadderCart cart;
    public bool useSelectedCart = true;
    public Button hitchButton, unhitchButton, loadNearbyButton, returnForLaddersButton;
    public Button unloadToWarehouseButton, dropLaddersButton, cancelLoadingButton;
    public TMP_Text cargoText, horseText, statusText;
    private void Awake()
    {
        if (hitchButton != null) hitchButton.onClick.AddListener(HitchHorse);
        if (unhitchButton != null) unhitchButton.onClick.AddListener(UnhitchHorse);
        if (loadNearbyButton != null) loadNearbyButton.onClick.AddListener(LoadNearby);
        if (returnForLaddersButton != null) returnForLaddersButton.onClick.AddListener(ReturnForLadders);
        if (unloadToWarehouseButton != null) unloadToWarehouseButton.onClick.AddListener(UnloadToWarehouse);
        if (dropLaddersButton != null) dropLaddersButton.onClick.AddListener(DropLadders);
        if (cancelLoadingButton != null) cancelLoadingButton.onClick.AddListener(CancelLoading);
    }
    private SiegeLadderCart Current
    {
        get
        {
            if (!useSelectedCart) return cart != null && FactionMember.IsPlayer(cart.gameObject) ? cart : null;
            if (ManagerGO.Instance != null) foreach (var selected in ManagerGO.Instance.unitsSelected)
            {
                var wagon = selected != null ? selected.GetComponent<SiegeLadderCart>() : null;
                if (wagon != null && FactionMember.IsPlayer(wagon.gameObject)) return wagon;
            }
            return null;
        }
    }
    public void HitchHorse()
    {
        var wagon = Current; if (wagon == null) return;
        if (ManagerGO.Instance != null) foreach (var selected in ManagerGO.Instance.unitsSelected)
        {
            var horse = selected != null ? selected.GetComponent<CartHorse>() : null;
            if (horse != null) { wagon.AttachHorse(horse); return; }
        }
        wagon.AttachNearestHorse();
    }
    public void UnhitchHorse() { Current?.DetachHorse(); }
    public void LoadNearby() { Current?.LoadNearestWarehouse(); }
    public void ReturnForLadders() { Current?.ReturnForLadders(); }
    public void UnloadToWarehouse() { Current?.UnloadNearestWarehouse(); }
    public void DropLadders() { Current?.DropLadders(); }
    public void CancelLoading() { Current?.CancelLoading(); }
    private void Update()
    {
        var wagon = Current;
        if (cargoText != null) cargoText.text = wagon != null ? $"Лестницы: {wagon.LadderCount} / {wagon.Capacity}" : "Выберите осадную повозку";
        if (horseText != null) horseText.text = wagon != null && wagon.AttachedHorse != null ? "Лошадь: " + wagon.AttachedHorse.name : "Лошадь не привязана";
        if (statusText != null) statusText.text = wagon != null ? wagon.Status : "";
        if (hitchButton != null) hitchButton.interactable = wagon != null && wagon.AttachedHorse == null;
        if (unhitchButton != null) unhitchButton.interactable = wagon != null && wagon.AttachedHorse != null;
        if (loadNearbyButton != null) loadNearbyButton.interactable = wagon != null && wagon.FreeSpace > 0;
        if (returnForLaddersButton != null) returnForLaddersButton.interactable = wagon != null && wagon.CanMove && wagon.FreeSpace > 0;
        if (unloadToWarehouseButton != null) unloadToWarehouseButton.interactable = wagon != null && wagon.LadderCount > 0;
        if (dropLaddersButton != null) dropLaddersButton.interactable = wagon != null && wagon.LadderCount > 0;
        if (cancelLoadingButton != null) cancelLoadingButton.interactable = wagon != null;
    }
    private void OnDestroy()
    {
        if (hitchButton != null) hitchButton.onClick.RemoveListener(HitchHorse);
        if (unhitchButton != null) unhitchButton.onClick.RemoveListener(UnhitchHorse);
        if (loadNearbyButton != null) loadNearbyButton.onClick.RemoveListener(LoadNearby);
        if (returnForLaddersButton != null) returnForLaddersButton.onClick.RemoveListener(ReturnForLadders);
        if (unloadToWarehouseButton != null) unloadToWarehouseButton.onClick.RemoveListener(UnloadToWarehouse);
        if (dropLaddersButton != null) dropLaddersButton.onClick.RemoveListener(DropLadders);
        if (cancelLoadingButton != null) cancelLoadingButton.onClick.RemoveListener(CancelLoading);
    }
}
