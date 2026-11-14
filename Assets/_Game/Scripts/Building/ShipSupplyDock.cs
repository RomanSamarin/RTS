using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

/// <summary>One shore loading point bound to one physical ship near its berth.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingHealth))]
public class ShipSupplyDock : MonoBehaviour
{
    private static readonly List<ShipSupplyDock> docks = new List<ShipSupplyDock>();
    public static IReadOnlyList<ShipSupplyDock> All => docks;
    public Transform landDeliveryPoint, berthPoint;
    [Min(.5f)] public float mooringRadius = 3;
    public bool autoBindNearest = true;
    public WarehouseStorage supplyWarehouse;
    [SerializeField] private ShipProvisions ship;
    [SerializeField] private ShipNavigation reservedShip;
    public ShipProvisions Ship => ship;
    public ShipNavigation ReservedShip => reservedShip;
    public Quaternion BerthRotation => berthPoint != null ? berthPoint.rotation : transform.rotation;
    public Vector3 DeliveryPoint => landDeliveryPoint != null && landDeliveryPoint.IsChildOf(transform) ? landDeliveryPoint.position
        : BuildingEntrance.TryGetWorldPoint(gameObject, out var point) ? point : transform.position;
    public Vector3 BerthPoint => berthPoint != null && berthPoint.IsChildOf(transform) ? berthPoint.position : transform.position;
    public string Status => !BuildingRoadAccess.Allows(this) ? "Нет дороги к причалу" : ship == null ? (reservedShip != null ? "Корабль идёт к причалу" : "Корабль не назначен")
        : !CanSupply(ship) ? "Корабль недоступен у причала" : !ship.autoResupply ? "Пополнение выключено"
        : ship.StoredMeals >= ship.capacity ? "Припасы загружены" : ship.RequestedMeals > 0 ? "Ожидает доставку еды" : "Запас выше порога пополнения";
    private float nextSearchAt;
    public bool CanReserve(ShipNavigation value)
    {
        if (value == null || !value.isActiveAndEnabled || !isActiveAndEnabled
            || !FactionMember.AreAllies(gameObject, value.gameObject)) return false;
        var unit = value.GetComponent<Unit>();
        if (unit == null || (unit.HasStarted && unit.unitHealth <= 0)) return false;
        return (reservedShip == null || reservedShip == value)
            && (ship == null || ship.gameObject == value.gameObject);
    }
    public bool TryReserve(ShipNavigation value)
    { if (!CanReserve(value)) return false; reservedShip = value; return true; }
    public void CancelReservation(ShipNavigation value)
    { if (reservedShip == value) reservedShip = null; }
    public bool CompleteMooring(ShipNavigation value)
    {
        if (reservedShip != value || !CanReserve(value) || !value.IsMooredTo(this)) return false;
        var provisions = value.GetComponent<ShipProvisions>();
        if (provisions == null) return false;
        ship = provisions; provisions.SetDock(this); return true;
    }
    private bool ValidShip(ShipProvisions value)
    {
        if (value == null || !value.isActiveAndEnabled || !isActiveAndEnabled || !BuildingRoadAccess.Allows(this)
            || !FactionMember.AreAllies(gameObject, value.gameObject)) return false;
        var health = value.GetComponent<Unit>();
        return health != null && (!health.HasStarted || health.unitHealth > 0)
            && Vector3.Distance(value.transform.position, BerthPoint) <= mooringRadius;
    }
    public bool CanSupply(ShipProvisions value) => ship == value && value != null && value.Dock == this && ValidShip(value)
        && value.GetComponent<ShipNavigation>() is ShipNavigation navigation && navigation.IsMooredTo(this);
    public bool AssignShip(ShipHull hull)
    {
        if (hull == null) return false;
        var navigation = hull.GetComponent<ShipNavigation>() ?? hull.gameObject.AddComponent<ShipNavigation>();
        if (navigation.IsMooredTo(this)) return true;
        return navigation.MoorAt(this);
    }
    public bool AssignNearestShip()
    {
        ShipProvisions nearest = null; float best = float.PositiveInfinity;
        foreach (var candidate in ShipProvisions.All)
        {
            var nav = candidate.GetComponent<ShipNavigation>();
            if (!ValidShip(candidate) || (candidate.Dock != null && candidate.Dock != this)
                || nav == null || !nav.CanAutoDock || !CanReserve(nav)) continue;
            float distance = (candidate.transform.position - BerthPoint).sqrMagnitude;
            if (distance < best) { nearest = candidate; best = distance; }
        }
        return nearest != null && AssignShip(nearest.GetComponent<ShipHull>());
    }
    public void ReleaseShip(ShipProvisions value)
    {
        if (value == null || ship != value) return;
        ship = null; if (value.Dock == this) value.SetDock(null);
        // Abort a partially completed batch immediately; no transfer may happen after departure.
        GetComponent<Port>()?.CancelTransfer();
    }
    public void UnassignShip()
    { autoBindNearest = false; var nav = reservedShip; if (nav != null) nav.Stop(); ReleaseShip(ship); reservedShip = null; }
    public void RequestSupplies()
    { if (ship == null) AssignNearestShip(); if (ship != null) ship.RequestRestock(); }
    public void StopSupplies() { if (ship != null) ship.StopRestocking(); }
    private void OnEnable() { if (!docks.Contains(this)) docks.Add(this); }
    private void Start()
    { if (ship != null && !SaveGameManager.IsLoading) { var initial = ship; ship = null; AssignShip(initial.GetComponent<ShipHull>()); } }
    private void Update()
    {
        if (SaveGameManager.IsLoading) return;
        if (reservedShip != null && (!CanReserve(reservedShip) || reservedShip.DestinationDock != this))
        { var invalid = reservedShip; reservedShip = null; if (invalid.DestinationDock == this) invalid.StopForRoute(); }
        // Losing the entrance road pauses shore logistics, but does not untie the ship.
        if (ship != null && (!FactionMember.AreAllies(gameObject, ship.gameObject)
            || !(ship.GetComponent<ShipNavigation>()?.IsMooredTo(this) ?? false))) ReleaseShip(ship);
        if (ship == null && autoBindNearest && Time.time >= nextSearchAt)
        { nextSearchAt = Time.time + 1; AssignNearestShip(); }
    }
    private void OnDisable()
    { docks.Remove(this); if (SaveGameManager.IsLoading) return; var nav = reservedShip; reservedShip = null; ReleaseShip(ship); if (nav != null) nav.StopForRoute(); }
}
