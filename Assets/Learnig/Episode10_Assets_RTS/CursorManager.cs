using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }


    private GameObject markerInstance;  // The instantiated marker object
    bool isMarkerActive = false;  // Boolean to control marker activation
    float markerHeight = 0.0f;  // Height at which the marker should be placed (e.g., ground level)

    [Header("Marker Prefabs")]
    public GameObject walkableCursor;
    public GameObject selectableCursor;
    public GameObject attackableCursor;
    public GameObject unAvailableCursor;


    CursorType currentCursor;


    public enum CursorType
    {
        None,
        Walkable,
        UnAvailable,
        Selectable,
        Attackable
    }

    void Update()
    {
        if (SettingsMenu.BlocksWorldInput)
        { markerInstance?.SetActive(false); Cursor.visible = true; return; }
        if (RoadBuilder.BlocksWorldInput)
        { markerInstance?.SetActive(false); Cursor.visible=true; return; }
        // Во время выбора сборного пункта сам флажок заменяет курсор.
        // Не даём обычному менеджеру снова показать указатель или второй маркер.
        if (Barracks.IsChoosingRallyPoint)
        {
            markerInstance?.SetActive(false);
            Cursor.visible = false;
            return;
        }
        // Check if the marker should be active
        if (isMarkerActive)
        {
            // Show the marker and hide the cursor
            markerInstance.SetActive(true);
            Cursor.visible = false;

            // Position the marker at the mouse position
            Vector3 mousePosition = Input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(mousePosition);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, markerHeight, 0));
            float rayDistance;

            if (groundPlane.Raycast(ray, out rayDistance))
            {
                Vector3 worldPosition = ray.GetPoint(rayDistance);
                markerInstance.transform.position = worldPosition;
            }
        }
        else
        {
            // Hide the marker and show the cursor
            markerInstance?.SetActive(false);

            Cursor.visible = true;
        }
    }

    public void SetMarkerType(CursorType type)
    {
        if (type != currentCursor)
        {
            isMarkerActive = true;

            currentCursor = type;

            switch (type)
            {
                case CursorType.Walkable:
                    markerInstance?.SetActive(false);
                    markerInstance = walkableCursor;
                    return;
                case CursorType.Selectable:
                    markerInstance?.SetActive(false);
                    markerInstance = selectableCursor;
                    return;
                case CursorType.Attackable:
                    markerInstance?.SetActive(false);
                    markerInstance = attackableCursor;
                    return;
                case CursorType.UnAvailable:
                    markerInstance?.SetActive(false);
                    markerInstance = unAvailableCursor;
                    return;
                case CursorType.None:
                    markerInstance?.SetActive(false);
                    isMarkerActive = false;
                    return;
            }
        }
    }
}
