using System.Collections.Generic;
using UnityEngine;

/// <summary>Real residents occupy crew slots; no new population is spawned.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(ShipHull))]
public class ShipCrew : MonoBehaviour
{
    public bool autoRecruit = true;
    [Min(1)] public float recruitmentRadius = 120;
    private readonly List<ShipCrewMember> members = new List<ShipCrewMember>();
    private float nextRecruitAt, nextWarningAt;
    public bool Disembarking { get; private set; }
    public int Required => Mathf.Max(1, GetComponent<ShipHull>().RequiredCrew);
    public int Aboard { get { int count = 0; foreach (var m in members) if (m != null && m.Crew == this && m.IsAboard && m.IsAlive) count++; return count; } }
    public int Assigned { get { int count = 0; foreach (var m in members) if (m != null && m.Crew == this && m.IsAlive) count++; return count; } }
    public int Incoming => Mathf.Max(0, Assigned - Aboard);
    public bool Ready => isActiveAndEnabled && !Disembarking && Aboard >= Required;
    public ShipSupplyDock Dock
    {
        get
        {
            var dock = GetComponent<ShipProvisions>()?.Dock;
            return dock != null && (GetComponent<ShipNavigation>()?.IsMooredTo(dock) ?? false) ? dock : null;
        }
    }
    public bool HasHungryCrew
    {
        get { foreach (var m in members) if (m != null && m.IsAboard && m.IsAlive && (m.GetComponent<UnitEat>()?.WantEat ?? false)) return true; return false; }
    }
    public string Warning => HasHungryCrew ? "Экипаж голодает: нужны припасы"
        : GetComponent<ShipProvisions>() is ShipProvisions p && Aboard > 0 && p.StoredMeals < Required ? "Мало припасов для экипажа" : "";
    public string Status => Disembarking ? "Экипаж высаживается" : Ready ? "Экипаж готов"
        : Incoming > 0 ? "Моряки идут к причалу" : Dock == null ? "Для набора экипажа нужна швартовка"
        : !autoRecruit ? "Набор экипажа выключен" : "Нужны свободные жители с путём к причалу";
    internal bool Register(ShipCrewMember member)
    {
        if (!isActiveAndEnabled || member == null || Assigned >= Required || members.Contains(member)) return false;
        members.Add(member); return true;
    }
    internal void Remove(ShipCrewMember member) { members.Remove(member); }
    [ContextMenu("Набрать экипаж")]
    public void RecruitCrew() { autoRecruit = true; Disembarking = false; nextRecruitAt = 0; Recruit(); }
    [ContextMenu("Отменить набор экипажа")]
    public void CancelRecruitment()
    {
        autoRecruit = false;
        foreach (var member in new List<ShipCrewMember>(members)) if (member != null && !member.IsAboard) member.CancelBoarding();
    }
    [ContextMenu("Высадить экипаж")]
    public void DisembarkCrew()
    {
        if (Dock == null) { Debug.LogWarning("[Экипаж] Для высадки пришвартуйте корабль.", this); return; }
        CancelRecruitment(); Disembarking = Aboard > 0;
    }
    private void Recruit()
    {
        var dock = Dock;
        if (!autoRecruit || Disembarking || dock == null || !BuildingRoadAccess.Allows(dock)) return;
        var residents = new List<ResourcesMove>(Object.FindObjectsOfType<ResourcesMove>());
        residents.Sort((a,b) => (a.transform.position - dock.DeliveryPoint).sqrMagnitude.CompareTo((b.transform.position - dock.DeliveryPoint).sqrMagnitude));
        foreach (var resident in residents)
        {
            if (Assigned >= Required) break;
            if (!resident.IsUnemployed || !resident.CanChangeRole || FactionMember.Of(resident.gameObject) != FactionMember.Of(gameObject)
                || Vector3.Distance(resident.transform.position, dock.DeliveryPoint) > recruitmentRadius) continue;
            var sailor = resident.GetComponent<ShipCrewMember>() ?? resident.gameObject.AddComponent<ShipCrewMember>();
            sailor.TryBoard(this, dock);
        }
    }
    private void Update()
    {
        if (SaveGameManager.IsLoading) return;
        members.RemoveAll(m => m == null || m.Crew != this);
        if (Disembarking && Aboard == 0) Disembarking = false;
        if (Time.time >= nextRecruitAt && Assigned < Required) { nextRecruitAt = Time.time + 1; Recruit(); }
        if (Warning != "" && Time.time >= nextWarningAt)
        { nextWarningAt = Time.time + 30; Debug.LogWarning("[Корабль] " + name + ": " + Warning, this); }
    }
    private void OnDisable() { if (!SaveGameManager.IsLoading) CancelRecruitment(); }
    private void OnDestroy()
    {
        if (SaveGameManager.IsLoading) return;
        LoseCrew();
    }
    internal void LoseCrew(GettingResources salvage = null)
    {
        foreach (var member in new List<ShipCrewMember>(members))
        {
            if (member == null || member.Crew != this) continue;
            if (member.IsAboard && salvage != null)
                LootTransfer.Move(member.GetComponent<GettingResources>(), salvage, int.MaxValue);
            member.ShipLost();
        }
    }
}
