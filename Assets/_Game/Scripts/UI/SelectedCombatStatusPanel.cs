using TMPro;
using UnityEngine;

/// <summary>Не создаёт UI. Назначьте свои TMP-поля на панели выбранного юнита.</summary>
public class SelectedCombatStatusPanel : MonoBehaviour
{
    public TMP_Text statusText, modifiersText;
    private float nextRefresh;
    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.15f;
        var selected = ManagerGO.Instance != null ? ManagerGO.Instance.unitsSelected : null;
        var battle = selected != null && selected.Count == 1 && selected[0] != null ? selected[0].GetComponent<BattleCombatant>() : null;
        if (battle == null)
        { if (statusText != null) statusText.text = ""; if (modifiersText != null) modifiersText.text = ""; return; }
        var charge = battle.GetComponent<CavalryCharge>();
        string status = battle.Status;
        bool retreating=battle.GetComponent<GOScript>()?.IsRetreating??false;
        var orders=battle.GetComponent<MilitaryOrders>();
        if(orders!=null&&orders.Current==MilitaryOrders.Order.Patrol)status=orders.Status+(status.Length>0?"\n"+status:"");
        if(retreating)status="Отступает"+(status.Length>0?"\n"+status:"");
        if (charge != null && !retreating) status += (status.Length > 0 ? "\n" : "") + (charge.ChargeReady ? "Натиск готов" : $"Разгон: {charge.RunDistance:0.0} / {charge.runDistance:0.0} м");
        if (statusText != null) statusText.text = status;
        if (modifiersText != null)
        {
            string effects = $"Скорость: {battle.SpeedMultiplier:P0}\nУрон: {battle.DamageMultiplier:P0}\nТемп атак: {battle.AttackRateMultiplier:P0}";
            if (battle.Cohesive && battle.Kind == TroopType.Swordsman) effects += "\nСпереди: −20% урона в ближнем бою, −35% урона стрел";
            if (battle.Holding && battle.Kind == TroopType.Spearman) effects += "\nВыставленные копья против фронтального натиска";
            if (battle.IsDisordered) effects += "\nПолучаемый урон: +25%";
            modifiersText.text = effects;
        }
    }
}
