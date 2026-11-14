using UnityEngine;

/// <summary>Designer-marked horizontal water rectangle, not a land NavMesh.</summary>
public class ShipLaunchArea : MonoBehaviour
{
    [Min(1)] public float width = 30, length = 30;
    [Tooltip("Layers containing terrain/seabed colliders. Must be assigned.")]
    public LayerMask seabedLayers;
    public LayerMask blockingLayers = ~0;
    [Min(1)] public float probeHeight = 50;
    public float SurfaceHeight => transform.position.y;
    public bool ContainsPoint(Vector3 point)
    { var local = transform.InverseTransformPoint(point); return ShipLaunchRules.Contains(local.x, local.z, width, length); }
    public static ShipLaunchArea FindAt(Vector3 point)
    {
        ShipLaunchArea best = null; float distance = float.PositiveInfinity;
        foreach (var area in Object.FindObjectsOfType<ShipLaunchArea>())
        {
            if (!area.isActiveAndEnabled || !area.ContainsPoint(point)) continue;
            float height = Mathf.Abs(area.SurfaceHeight - point.y);
            if (height < distance) { best = area; distance = height; }
        }
        return best;
    }
    public bool CanLaunch(Vector3 position, Quaternion rotation, float hullWidth, float hullLength,
        float minimumDepth, float clearanceHeight, out string reason)
        => CanOccupy(position, rotation, hullWidth, hullLength, minimumDepth, clearanceHeight, null, out reason);

    public bool CanOccupy(Vector3 position, Quaternion rotation, float hullWidth, float hullLength,
        float minimumDepth, float clearanceHeight, Transform ignore, out string reason, bool ignoreShips = false)
    {
        reason = null;
        if (!isActiveAndEnabled) { reason = "Зона воды отключена"; return false; }
        if (Vector3.Dot(transform.up, Vector3.up) < .999f) { reason = "Зона воды должна быть горизонтальной"; return false; }
        if ((transform.lossyScale - Vector3.one).sqrMagnitude > .0001f)
        { reason = "Масштаб зоны воды и её родителей должен быть 1; задавайте Width и Length"; return false; }
        if (seabedLayers.value == 0) { reason = "Назначьте Seabed Layers зоны спуска"; return false; }
        position.y = SurfaceHeight;
        // Centre, corners and edge centres must all fit in water and have enough depth.
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
        {
            Vector3 point = position + rotation * new Vector3(x * (hullWidth * .5f + .3f), 0, z * (hullLength * .5f + .3f));
            Vector3 local = transform.InverseTransformPoint(point);
            if (!ShipLaunchRules.Contains(local.x, local.z, width, length)) { reason = "Корпус выходит за границы зоны воды"; return false; }
            if (Physics.Raycast(point + Vector3.up * probeHeight, Vector3.down, out var bottom,
                probeHeight + minimumDepth, seabedLayers, QueryTriggerInteraction.Ignore)
                && !ShipLaunchRules.EnoughDepth(SurfaceHeight, bottom.point.y, minimumDepth))
            { reason = "На месте спуска суша или недостаточная глубина"; return false; }
        }
        var center = position + Vector3.up * (clearanceHeight * .5f + .1f);
        var half = new Vector3(hullWidth * .5f + .3f, clearanceHeight * .5f, hullLength * .5f + .3f);
        foreach (var body in Physics.OverlapBox(center, half, rotation, blockingLayers, QueryTriggerInteraction.Ignore))
        {
            if (ignore != null && (body.transform == ignore || body.transform.IsChildOf(ignore))) continue;
            if (ignoreShips && body.GetComponentInParent<ShipHull>() != null) continue;
            // A flat water collider is not an obstacle above the waterline.
            if (body.bounds.max.y <= SurfaceHeight + .05f) continue;
            reason = "Спуск занят кораблём, причалом или препятствием"; return false;
        }
        return true;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        var previous = Gizmos.matrix; Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, .1f, length)); Gizmos.matrix = previous;
    }
}
