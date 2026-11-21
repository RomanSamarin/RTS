using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingHealth), typeof(WorkplaceHiring))]
public class Watchtower : MonoBehaviour
{
    [Min(1f)] public float sightRadius = 45f;
    [Min(.1f)] public float scanInterval = .75f;
    [Min(0f)] public float warningCooldown = 15f;
    public string areaName;

    private static readonly List<Watchtower> towers = new List<Watchtower>();
    public static IReadOnlyList<Watchtower> All => towers;
    [NonSerialized] private readonly HashSet<Unit> contacts = new HashSet<Unit>();
    [NonSerialized] private readonly HashSet<Unit> scan = new HashSet<Unit>();
    [NonSerialized] private float nextScanAt;
    private float nextWarningAt;
    [NonSerialized] private bool pendingWarning;

    public IReadOnlyCollection<Unit> Contacts => contacts;
    public string Status { get; private set; } = "Нет дозорного";
    public WorkplaceHiring Hiring => GetComponent<WorkplaceHiring>();

    private void OnEnable() => towers.Add(this);

    private void OnDisable()
    {
        towers.Remove(this);
        ClearContacts();
        nextScanAt = 0f;
    }

    private bool OnDuty(ResourcesMove worker)
    {
        return worker != null && worker.isActiveAndEnabled && worker.role == ResourcesMove.UnitRole.Watchman
            && worker.AssignedWorkplace == gameObject && !worker.IsReturningToWorkplace
            && worker.GetComponent<Unit>() is Unit unit && unit.unitHealth > 0
            && !UnitEat.ControlsMovement(worker) && !(worker.GetComponent<GOScript>()?.HasPlayerOrder ?? false)
            && worker.IsAtAssignedWorkplace;
    }

    private void Update()
    {
        if (SaveGameManager.IsLoading || Time.time < nextScanAt) return;
        nextScanAt = Time.time + Mathf.Max(.1f, scanInterval);
        if (GetComponent<BuildingHealth>().IsDead || !FactionMember.HasPlayerOwner(gameObject))
        {
            Status = "Башня недоступна";
            ClearContacts();
            return;
        }
        if (!BuildingRoadAccess.Allows(this))
        {
            Status = "Нет дороги к башне";
            ClearContacts();
            return;
        }

        var workers = Hiring.Workers;
        bool staffed = false;
        foreach (var worker in workers)
            if (OnDuty(worker)) { staffed = true; break; }
        if (!staffed)
        {
            Status = workers.Count == 0 ? "Нет дозорного" : "Дозорный ещё не на посту";
            ClearContacts();
            return;
        }

        ScanBandits();
        Status = contacts.Count == 0 ? "Ведёт дозор" : $"Разбойники замечены: {contacts.Count}";
    }

    private void ScanBandits()
    {
        scan.Clear();
        float rangeSquared = sightRadius * sightRadius;
        foreach (var member in FactionMember.All)
        {
            if (member == null || !member.isActiveAndEnabled || member.Side != Faction.Bandits
                || !member.TryGetComponent<Unit>(out var unit) || !unit.isActiveAndEnabled || unit.unitHealth <= 0) continue;
            Vector3 offset = unit.transform.position - transform.position;
            if (offset.x * offset.x + offset.z * offset.z > rangeSquared) continue;
            scan.Add(unit);
            if (!contacts.Contains(unit) && !SeenByAnotherTower(unit)) pendingWarning = true;
        }

        contacts.Clear();
        contacts.UnionWith(scan);
        if (contacts.Count == 0) pendingWarning = false;
        if (!pendingWarning || Time.time < nextWarningAt) return;

        pendingWarning = false;
        nextWarningAt = Time.time + Mathf.Max(0f, warningCooldown);
        string location = string.IsNullOrWhiteSpace(areaName) ? name.Replace("(Clone)", "").Trim() : areaName;
        string warning = $"Дозор: замечены разбойники ({contacts.Count}) · {location}";
        if (RaidManager.Instance != null) RaidManager.Instance.Warn(warning);
        else Debug.Log(warning, this);
    }

    private bool SeenByAnotherTower(Unit unit)
    {
        foreach (var tower in towers)
            if (tower != this && tower.contacts.Contains(unit)) return true;
        return false;
    }

    private void ClearContacts()
    {
        contacts.Clear();
        pendingWarning = false;
    }
}
