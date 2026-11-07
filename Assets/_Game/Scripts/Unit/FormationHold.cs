using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Общая память между строевым скриптом и Animator-ом: какой юнит в какую
/// точку строя отправлен. Нужна, чтобы StateMachineBehaviour на состоянии Idle
/// понимал, что юнит пришёл именно в строй, а не просто куда-то пришёл.
///
/// Компонент на юнит не нужен — данные лежат здесь статически.
/// </summary>
public static class FormationHold
{
    private static readonly Dictionary<GameObject, Vector3> points = new Dictionary<GameObject, Vector3>();
    private static readonly Dictionary<GameObject, Vector3> facings = new Dictionary<GameObject, Vector3>();
    private static readonly Dictionary<GameObject, int> groups = new Dictionary<GameObject, int>();
    private static int nextGroup;
    public static int BeginGroup() { unchecked { return ++nextGroup; } }

    /// <summary>Запомнить, что юнит отправлен в точку строя.</summary>
    public static void SetPoint(GameObject unit, Vector3 point)
    { SetPoint(unit, point, unit != null ? unit.transform.forward : Vector3.forward, 0); }
    public static void SetPoint(GameObject unit, Vector3 point, Vector3 forward, int group)
    {
        if (unit == null)
            return;

        Cleanup();

        points[unit] = point;
        MeleeApproach.Release(unit);
        FormationAnchor.Ensure(unit);
        forward.y = 0; facings[unit] = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        groups[unit] = group;
        BattleCombatant.Ensure(unit)?.FormationChanged();
    }
    public static bool TryGetFacing(GameObject unit, out Vector3 facing) { facing = default; return unit != null && facings.TryGetValue(unit, out facing); }
    public static int GetGroup(GameObject unit) => unit != null && groups.TryGetValue(unit, out int group) ? group : 0;

    /// <summary>Точка строя юнита, если он в строй отправлялся.</summary>
    public static bool TryGetPoint(GameObject unit, out Vector3 point)
    {
        point = default;

        if (unit == null || points.Count == 0)
            return false;

        return points.TryGetValue(unit, out point);
    }

    /// <summary>Юнит больше не держит строй (ушёл, погиб, приказ отменён).</summary>
    public static void Clear(GameObject unit)
    {
        if (unit == null)
            return;

        points.Remove(unit);
        FormationAnchor.Release(unit);
        facings.Remove(unit); groups.Remove(unit);
    }

    // Уничтоженные юниты остаются в словаре мусором — чистим при добавлении
    private static void Cleanup()
    {
        if (points.Count < 32)
            return;

        List<GameObject> dead = null;

        foreach (KeyValuePair<GameObject, Vector3> pair in points)
        {
            if (pair.Key != null)
                continue;

            if (dead == null)
                dead = new List<GameObject>();

            dead.Add(pair.Key);
        }

        if (dead == null)
            return;

        foreach (GameObject unit in dead)
        { points.Remove(unit); facings.Remove(unit); groups.Remove(unit); }
    }
}
