using UnityEngine;
using UnityEngine.AI;

/// <summary>Двухфазная замена модели одного человека с сохранением его состояния.</summary>
public static class UnitModelTransfer
{
    public static bool TryPrepare(GameObject source, GameObject prefab, ResourcesMove.UnitRole role,
        Vector3 position, out GameObject replacement)
    {
        replacement = null;
        if (source == null || prefab == null || source.GetComponent<Unit>() == null || prefab.GetComponent<Unit>() == null) return false;
        var prefabAgent = prefab.GetComponent<NavMeshAgent>();
        if (prefabAgent == null) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = prefabAgent.agentTypeID, areaMask = prefabAgent.areaMask };
        if (!NavMesh.SamplePosition(position, out var hit, 3f, filter)) return false;
        replacement = Object.Instantiate(prefab, hit.position, source.transform.rotation, source.transform.parent);
        SaveGameManager.Stamp(replacement,prefab);
        var cargo = replacement.GetComponent<GettingResources>();
        if (cargo == null) cargo = replacement.AddComponent<GettingResources>();
        cargo.Clear();
        var worker = replacement.GetComponent<ResourcesMove>();
        if (worker == null) worker = replacement.AddComponent<ResourcesMove>();
        worker.SetRole(role);
        var unit = replacement.GetComponent<Unit>();
        var oldUnit = source.GetComponent<Unit>();
        unit.woodReward = unit.stoneReward = unit.wheatReward = unit.fishReward = 0;
        unit.SetInitialHealthRatio(oldUnit.unitMaxHealth > 0 ? oldUnit.unitHealth / oldUnit.unitMaxHealth : 1f);
        var attack = replacement.GetComponent<AttackController>();
        if (attack != null) { attack.targetToAttack = null; }
        FactionMember.Set(replacement,FactionMember.Of(source));
        var oldFood = source.GetComponent<UnitEat>();
        if (oldFood != null)
        {
            var food = replacement.GetComponent<UnitEat>();
            if (food == null) food = replacement.AddComponent<UnitEat>();
            food.mode = role == ResourcesMove.UnitRole.Villager ? UnitEat.FeedingMode.Settlement : oldFood.mode;
            food.RestoreNutrition(oldFood.CaptureNutrition());
        }
        return true;
    }
    public static void Commit(GameObject source, GameObject replacement, bool preserveFormation)
    {
        var house = HousesScript.FindHouseOf(source);
        if (house != null) { house.Evict(source); if (!house.TrySettle(replacement)) HousesScript.TrySettleAutomatically(replacement); }
        if (FormationHold.TryGetPoint(source, out var point))
        {
            var facing = FormationHold.TryGetFacing(source, out var front) ? front : source.transform.forward;
            int group = FormationHold.GetGroup(source);
            FormationHold.Clear(source);
            if (preserveFormation) FormationHold.SetPoint(replacement, point, facing, group);
        }
        if (ManagerGO.Instance != null)
        {
            ManagerGO.Instance.ReplaceControlGroupMember(source, replacement);
            var selected = ManagerGO.Instance.unitsSelected;
            int index = selected.IndexOf(source); if (index >= 0) selected[index] = replacement;
            var indicator = replacement.transform.Find("Indicator"); if (indicator != null) indicator.gameObject.SetActive(index >= 0);
        }
        source.SetActive(false); Object.Destroy(source);
    }
}
