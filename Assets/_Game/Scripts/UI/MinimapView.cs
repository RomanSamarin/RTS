using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MinimapView : MonoBehaviour, IPointerDownHandler, IDragHandler, IInitializePotentialDragHandler
{
    public Camera mapCamera;
    public CameraController cameraController;
    public bool fitToTerrain = true;
    public bool drawFrame;
    [Min(0f)] public float terrainPadding = 8f;
    [Tooltip("Ground and water used for placing the view center.")]
    public LayerMask groundLayers = (1 << 6) | (1 << 4);
    public float fallbackHeight;

    [SerializeField] private RawImage mapImage;
    [SerializeField] private MinimapViewportGraphic overlay;
    private readonly Plane[] planes = new Plane[6];
    private readonly List<Vector3> polygon = new List<Vector3>(12);
    private readonly List<Vector3> clipped = new List<Vector3>(12);
    private readonly List<Vector2> outline = new List<Vector2>(12);

    private void Awake()
    {
        if (mapImage == null) mapImage = GetComponent<RawImage>();
        if (overlay != null) overlay.raycastTarget = false;
    }

    private void Start()
    {
        if (cameraController == null && Camera.main != null)
            cameraController = Camera.main.GetComponentInParent<CameraController>();
        if (mapCamera != null && fitToTerrain) FitTerrain();
    }

    private void OnEnable()
    {
        if (overlay != null) overlay.enabled = true;
    }

    private void OnDisable()
    {
        if (overlay != null) overlay.enabled = false;
    }

    private void FitTerrain()
    {
        var terrains = Terrain.activeTerrains;
        if (terrains.Length == 0) return;
        var bounds = TerrainBounds(terrains[0]);
        for (int i = 1; i < terrains.Length; i++) bounds.Encapsulate(TerrainBounds(terrains[i]));
        float aspect = mapCamera.targetTexture != null
            ? (float)mapCamera.targetTexture.width / mapCamera.targetTexture.height : mapCamera.aspect;
        float padding = Mathf.Max(0f, terrainPadding);
        var center = bounds.center;
        mapCamera.orthographic = true;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = new Color32(9, 26, 39, 255);
        mapCamera.orthographicSize = Mathf.Max(bounds.extents.z + padding, (bounds.extents.x + padding) / aspect);
        mapCamera.transform.SetPositionAndRotation(new Vector3(center.x, bounds.max.y + 50f, center.z), Quaternion.Euler(90f, 0f, 0f));
        mapCamera.farClipPlane = Mathf.Max(mapCamera.farClipPlane, bounds.size.y + 100f);
        fallbackHeight = bounds.min.y;
    }

    private static Bounds TerrainBounds(Terrain terrain)
    {
        var bounds = terrain.terrainData.bounds;
        bounds.center += terrain.transform.position;
        return bounds;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;
    public void OnPointerDown(PointerEventData eventData) => MoveCamera(eventData);
    public void OnDrag(PointerEventData eventData) => MoveCamera(eventData);

    private void MoveCamera(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || mapImage == null || mapCamera == null || cameraController == null
            || SaveGameManager.IsLoading || SettingsMenu.BlocksWorldInput) return;
        var rectTransform = mapImage.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position,
            eventData.pressEventCamera, out Vector2 local)) return;
        var rect = rectTransform.rect;
        var uv = mapImage.uvRect;
        float x = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
        float y = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
        var ray = mapCamera.ViewportPointToRay(new Vector3(uv.x + uv.width * x, uv.y + uv.height * y));
        if (Physics.Raycast(ray, out var hit, mapCamera.farClipPlane, groundLayers, QueryTriggerInteraction.Ignore))
            cameraController.CenterOn(hit.point);
        else if (new Plane(Vector3.up, Vector3.up * fallbackHeight).Raycast(ray, out float distance))
            cameraController.CenterOn(ray.GetPoint(distance));
    }

    private void LateUpdate()
    {
        if (overlay == null || mapImage == null) return;
        outline.Clear();
        var view = cameraController != null ? cameraController.ViewCamera : Camera.main;
        if (view != null && mapCamera != null && !SaveGameManager.IsLoading)
        {
            var aim = view.ViewportPointToRay(new Vector3(.5f, .5f));
            float height = Physics.Raycast(aim, out var hit, view.farClipPlane, groundLayers, QueryTriggerInteraction.Ignore)
                ? hit.point.y : fallbackHeight;
            ProjectVisibleArea(view, height, mapImage.uvRect);
        }
        overlay.SetOutline(outline, drawFrame);
    }

    private void ProjectVisibleArea(Camera view, float height, Rect uv)
    {
        if (Mathf.Abs(uv.width) < .0001f || Mathf.Abs(uv.height) < .0001f) return;
        polygon.Clear();
        var ground = new Plane(Vector3.up, Vector3.up * height);
        for (int i = 0; i < 4; i++)
        {
            var ray = mapCamera.ViewportPointToRay(new Vector3(
                i == 0 || i == 3 ? uv.xMin : uv.xMax, i < 2 ? uv.yMin : uv.yMax));
            if (!ground.Raycast(ray, out float distance)) return;
            polygon.Add(ray.GetPoint(distance));
        }

        GeometryUtility.CalculateFrustumPlanes(view, planes);
        foreach (var plane in planes)
        {
            if (polygon.Count == 0) return;
            clipped.Clear();
            var previous = polygon[polygon.Count - 1];
            float previousDistance = plane.GetDistanceToPoint(previous);
            foreach (var point in polygon)
            {
                float distance = plane.GetDistanceToPoint(point);
                if ((previousDistance >= 0f) != (distance >= 0f))
                    clipped.Add(Vector3.Lerp(previous, point, previousDistance / (previousDistance - distance)));
                if (distance >= 0f) clipped.Add(point);
                previous = point;
                previousDistance = distance;
            }
            polygon.Clear();
            polygon.AddRange(clipped);
        }

        foreach (var point in polygon)
        {
            var mapPoint = mapCamera.WorldToViewportPoint(point);
            outline.Add(new Vector2((mapPoint.x - uv.x) / uv.width, (mapPoint.y - uv.y) / uv.height));
        }
    }
}
