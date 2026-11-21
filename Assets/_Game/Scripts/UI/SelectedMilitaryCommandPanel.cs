using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SelectedMilitaryCommandPanel : MonoBehaviour
{
    public GameObject panelRoot;
    public Button attackButton, holdButton, patrolButton, retreatButton, faceDefenseButton, stopButton;
    public TMP_Text hintText;

    private void OnEnable() => SetListeners(true);

    private void OnDisable()
    {
        SetListeners(false);
        if (panelRoot != null && panelRoot != gameObject) panelRoot.SetActive(false);
    }

    private void SetListeners(bool add)
    {
        Bind(attackButton, Attack, add);
        Bind(holdButton, Hold, add);
        Bind(patrolButton, Patrol, add);
        Bind(retreatButton, Retreat, add);
        Bind(faceDefenseButton, FaceDefense, add);
        Bind(stopButton, Stop, add);
    }

    private static void Bind(Button button, UnityAction action, bool add)
    {
        if (button == null) return;
        if (add) button.onClick.AddListener(action);
        else button.onClick.RemoveListener(action);
    }

    private static bool CanIssue => !SaveGameManager.IsLoading && !SoldiersFormation.IsPlacing
        && !Barracks.BlocksWorldInput && !RoadBuilder.BlocksWorldInput && !RtsNetworkConstruction.BlocksWorldInput
        && !(PlacementSystem.Instance?.BlocksUnitInput ?? false);

    private void Attack() { if (CanIssue) ManagerGO.Instance?.BeginAttackMove(); }
    private void Hold() { if (CanIssue) ManagerGO.Instance?.HoldSelected(); }
    private void Patrol() { if (CanIssue) ManagerGO.Instance?.BeginPatrol(); }
    private void Retreat() { if (CanIssue) ManagerGO.Instance?.BeginRetreat(); }
    private void FaceDefense() { if (CanIssue) ManagerGO.Instance?.BeginDefenseFacing(); }
    private void Stop() { if (CanIssue) ManagerGO.Instance?.StopSelected(); }

    private void LateUpdate()
    {
        var manager = ManagerGO.Instance;
        bool soldiers = false, defenders = false, siege = false, ships = false, ladderCarts = false, rams = false;
        bool networkFighters = false;
        if (manager != null && !SaveGameManager.IsLoading)
        {
            foreach (var selected in manager.unitsSelected)
            {
                if (selected == null || !selected.TryGetComponent<Unit>(out var unit)
                    || !unit.isActiveAndEnabled || unit.unitHealth <= 0f) continue;
                networkFighters |= selected.TryGetComponent<RtsNetworkUnit>(out var networkUnit)
                    && networkUnit.IsSpawned && networkUnit.IsMeleeFighter;
                if (selected.TryGetComponent<ResourcesMove>(out var worker) && worker.role == ResourcesMove.UnitRole.Soldier
                    && selected.GetComponent<GOScript>() != null)
                {
                    soldiers = true;
                    var troop = BattleCombatant.TypeOf(selected);
                    defenders |= troop == TroopType.Swordsman || troop == TroopType.Spearman;
                }
                siege |= selected.GetComponent<Catapult>() != null;
                rams |= selected.GetComponent<BatteringRam>() != null;
                ships |= selected.GetComponent<ShipNavigation>() != null;
                ladderCarts |= selected.GetComponent<SiegeLadderCart>() != null;
            }
        }

        bool show = soldiers || siege || ships || ladderCarts || rams || networkFighters;
        if (panelRoot != null && panelRoot != gameObject) panelRoot.SetActive(show);
        bool available = show && CanIssue;
        if (attackButton != null) attackButton.interactable = available && (soldiers || siege || networkFighters);
        if (holdButton != null) holdButton.interactable = available && (soldiers || siege || ships || networkFighters);
        if (patrolButton != null) patrolButton.interactable = available && (soldiers || networkFighters);
        if (retreatButton != null) retreatButton.interactable = available && (soldiers || siege || ships || networkFighters);
        if (faceDefenseButton != null) faceDefenseButton.interactable = available && (defenders || networkFighters);
        if (stopButton != null) stopButton.interactable = available;
        if (hintText != null) hintText.text = show ? Hint(manager, ships) : "";
    }

    private static string Hint(ManagerGO manager, bool ships)
    {
        if (manager.IsChoosingRetreat) return ships ? "ЛКМ по воде для отступления" : "ЛКМ по земле для отступления";
        if (manager.IsChoosingPatrol) return "Укажи две точки ЛКМ. Shift добавляет промежуточную точку";
        if (manager.IsChoosingAttackMove) return "Укажи цель на земле";
        return manager.IsChoosingDefenseFacing ? "Укажи направление обороны" : "";
    }
}
