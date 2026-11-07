using UnityEngine;

/// <summary>Переход солдат → житель выполняется только у активной казармы, с физической сдачей груза.</summary>
[DisallowMultipleComponent]
public class SoldierService : MonoBehaviour
{
    public GameObject villagerPrefab;
    public string LastError { get; private set; } = "";
    private bool discharged;
    public bool CanDisband(Barracks barracks, out string reason)
    {
        reason = "";
        var worker = GetComponent<ResourcesMove>(); var unit = GetComponent<Unit>();
        if (discharged || !isActiveAndEnabled || worker == null || worker.role != ResourcesMove.UnitRole.Soldier
            || unit == null || !unit.enabled || unit.unitHealth <= 0)
        { reason = "Выбранный юнит не является живым солдатом."; return false; }
        if (barracks == null || !barracks.isActiveAndEnabled || barracks.Equipment == null || !barracks.Equipment.isActiveAndEnabled)
        { reason = "Нужна действующая казарма."; return false; }
        if(!FactionMember.CanUse(gameObject,barracks.gameObject)){reason="Это чужая казарма.";return false;}
        if (Vector3.Distance(transform.position, barracks.transform.position) > Mathf.Max(1f, barracks.disbandRadius))
        { reason = "Верните солдата в поселение к казарме."; return false; }
        var combat = GetComponent<TroopCombat>();
        if (combat != null && !combat.CanReleaseHorse(out _))
        { reason = "Рядом нет NavMesh для возвращаемой лошади."; return false; }
        var food = GetComponent<UnitEat>(); var command = GetComponent<GOScript>();
        var attack = GetComponent<AttackController>();
        bool activeTarget = false;
        if (attack != null && attack.targetToAttack != null && attack.targetToAttack.gameObject.activeInHierarchy)
        {
            var enemy = attack.targetToAttack.GetComponentInParent<Unit>();
            var building = attack.targetToAttack.GetComponentInParent<BuildingHealth>();
            activeTarget = (enemy != null && enemy.unitHealth > 0) || (building != null && !building.IsDead);
        }
        if (unit.RecentlyAttacked || (food != null && food.InCombat())
            || activeTarget || (command != null && command.CurrentOrder == MovementOrderState.Order.Combat))
        { reason = "Нельзя распустить солдата во время боя."; return false; }
        GameObject civilian = villagerPrefab != null ? villagerPrefab : barracks.GetVillagerPrefab();
        if (civilian == null || civilian.GetComponent<Unit>() == null || civilian.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
        { reason = "Назначьте Villager Prefab с Unit и NavMeshAgent на казарме."; return false; }
        return true;
    }
    public bool TryDisband(Barracks barracks)
    {
        if (!CanDisband(barracks, out string reason)) { LastError = reason; return false; }
        var civilian = villagerPrefab != null ? villagerPrefab : barracks.GetVillagerPrefab();
        if (!UnitModelTransfer.TryPrepare(gameObject, civilian, ResourcesMove.UnitRole.Villager, transform.position, out var resident))
        { LastError = "Не удалось разместить жителя на NavMesh."; return false; }
        // Возвращается только фактически имеющееся снаряжение и добыча, без создания нового комплекта.
        var cargo = GetComponent<GettingResources>();
        if (cargo != null && !barracks.Equipment.AcceptReturnedCargo(cargo)) { Destroy(resident); LastError = "Сдача снаряжения недоступна."; return false; }
        discharged = true; LastError = "";
        GetComponent<TroopCombat>()?.ReleaseHorse();
        UnitModelTransfer.Commit(gameObject, resident, false);
        return true;
    }
    public void DisbandAtNearestBarracks()
    {
        Barracks nearest = null; float distance = float.PositiveInfinity;
        foreach (var building in FindObjectsOfType<Barracks>())
        {
            if (!building.isActiveAndEnabled||!FactionMember.CanUse(gameObject,building.gameObject)) continue;
            float d = (building.transform.position - transform.position).sqrMagnitude;
            if (d < distance) { nearest = building; distance = d; }
        }
        if (!TryDisband(nearest)) Debug.LogWarning("[SoldierService] " + LastError, this);
    }
}
