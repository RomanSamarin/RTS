using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Projects detected bandits into a user-supplied minimap and marker prefab.</summary>
public class WatchtowerMinimapMarkers : MonoBehaviour
{
    public Camera mapCamera;
    public RectTransform markerContainer;
    public RectTransform markerPrefab;

    private readonly Dictionary<Unit, RectTransform> markers = new Dictionary<Unit, RectTransform>();
    private readonly HashSet<Unit> visible = new HashSet<Unit>();
    private readonly List<Unit> expired = new List<Unit>();

    private void LateUpdate()
    {
        visible.Clear();
        if (!SaveGameManager.IsLoading && mapCamera != null && markerContainer != null && markerPrefab != null)
        {
            foreach (var tower in Watchtower.All)
            {
                if (tower == null) continue;
                foreach (var unit in tower.Contacts)
                {
                    if (unit == null || unit.unitHealth <= 0 || !unit.isActiveAndEnabled || visible.Contains(unit)) continue;
                    Vector3 point = mapCamera.WorldToViewportPoint(unit.transform.position);
                    if (point.z < 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) continue;
                    visible.Add(unit);
                    if (!markers.TryGetValue(unit, out var marker) || marker == null)
                    {
                        marker = Instantiate(markerPrefab, markerContainer, false);
                        foreach (var graphic in marker.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                        marker.gameObject.SetActive(true);
                        markers[unit] = marker;
                    }
                    marker.anchorMin = marker.anchorMax = new Vector2(point.x, point.y);
                    marker.anchoredPosition = Vector2.zero;
                }
            }
        }

        expired.Clear();
        foreach (var entry in markers)
            if (!visible.Contains(entry.Key)) expired.Add(entry.Key);
        foreach (var unit in expired)
        {
            if (markers[unit] != null) Destroy(markers[unit].gameObject);
            markers.Remove(unit);
        }
    }

    private void OnDisable()
    {
        foreach (var marker in markers.Values)
            if (marker != null) Destroy(marker.gameObject);
        markers.Clear();
    }
}
