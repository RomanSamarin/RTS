using UnityEngine;
using UnityEngine.Splines;

#if UNITY_AI_NAVIGATION
using Unity.AI.Navigation;
#endif

public class RoadBuilder : MonoBehaviour
{
    [Header("Layer Options")]
    public LayerMask groundLayer;

    [Header("Distance Options")]
    public float minNodeDistance = 2f;
    public float snapDistance = 3.0f;

    [Header("Road Template Prefab")]
    public GameObject roadTemplatePrefab; // Сюда перетащи созданный RoadTemplate из Project

    private SplineContainer currentSplineContainer;
    private MeshCollider roadCollider;
    
    private bool isBuilding = false;
    private Vector3 lastPlacedPosition;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            StartNewRoad();
        }

        if (!isBuilding) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            Vector3 targetPosition = hit.point;
            targetPosition = CheckSnap(targetPosition);

            UpdatePreviewNode(targetPosition);

            if (Input.GetMouseButtonDown(0))
            {
                TryPlaceNode(targetPosition);
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            FinishRoad();
        }
    }

    void StartNewRoad()
    {
        if (roadTemplatePrefab == null)
        {
            Debug.LogError("Пожалуйста, назначьте Road Template Prefab в инспекторе скрипта RoadBuilder!");
            return;
        }

        isBuilding = true;
        
        // Спавним уже полностью настроенный в редакторе шаблон дороги
        GameObject roadObject = Instantiate(roadTemplatePrefab);
        roadObject.name = "DynamicRoad";
        roadObject.tag = "EditorOnly"; 

        currentSplineContainer = roadObject.GetComponent<SplineContainer>();
        roadCollider = roadObject.GetComponent<MeshCollider>();
        
        lastPlacedPosition = Vector3.zero;
    }

    void UpdatePreviewNode(Vector3 mousePosition)
    {
        if (currentSplineContainer == null) return;
        
        Spline spline = currentSplineContainer.Spline;

        if (spline.Count < 2)
        {
            if (spline.Count == 0)
            {
                spline.Add(new BezierKnot(mousePosition));
            }
            spline.Add(new BezierKnot(mousePosition));
        }
        else
        {
            int lastIndex = spline.Count - 1;
            BezierKnot knot = spline[lastIndex];
            knot.Position = mousePosition;
            spline[lastIndex] = knot;
        }

        AutoSmoothSpline();
        RefreshColliderAndMesh();
    }

    void TryPlaceNode(Vector3 position)
    {
        if (currentSplineContainer == null) return;
        
        Spline spline = currentSplineContainer.Spline;

        if (lastPlacedPosition != Vector3.zero && Vector3.Distance(lastPlacedPosition, position) < minNodeDistance)
        {
            return;
        }

        int lastIndex = spline.Count - 1;
        BezierKnot knot = spline[lastIndex];
        knot.Position = position;
        spline[lastIndex] = knot;

        spline.Add(new BezierKnot(position));

        lastPlacedPosition = position;
        RefreshColliderAndMesh();
    }

    void AutoSmoothSpline()
    {
        if (currentSplineContainer == null) return;
        
        Spline spline = currentSplineContainer.Spline;
        if (spline.Count < 3) return;

        spline.SetTangentMode(TangentMode.AutoSmooth);
    }

    void RefreshColliderAndMesh()
    {
        if (currentSplineContainer == null) return;

        // ИСПРАВЛЕНО: Так как встроенный в префаб компонент сам обновляет визуальный меш,
        // мы просто пинаем MeshCollider, чтобы он обновил свои физические границы
        MeshFilter mf = currentSplineContainer.gameObject.GetComponentInChildren<MeshFilter>();
        if (roadCollider != null && mf != null && mf.sharedMesh != null)
        {
            roadCollider.sharedMesh = null; // сбрасываем старый
            roadCollider.sharedMesh = mf.sharedMesh; // накатываем новый
        }
    }

    Vector3 CheckSnap(Vector3 currentMousePos)
    {
        SplineContainer[] allRoads = FindObjectsByType<SplineContainer>(FindObjectsSortMode.None);
        Vector3 bestSnapPoint = currentMousePos;
        float closestDistance = snapDistance;

        foreach (var road in allRoads)
        {
            if (road == currentSplineContainer) continue;

            Spline spline = road.Spline;
            for (int i = 0; i < spline.Count; i++)
            {
                Vector3 nodeWorldPos = road.transform.TransformPoint(spline[i].Position);
                float dist = Vector3.Distance(currentMousePos, nodeWorldPos);

                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    bestSnapPoint = nodeWorldPos;
                }
            }
        }

        return bestSnapPoint;
    }

    void FinishRoad()
    {
        if (currentSplineContainer == null) return;
        
        Spline spline = currentSplineContainer.Spline;
        if (spline.Count > 1)
        {
            spline.RemoveAt(spline.Count - 1);
        }

        RefreshColliderAndMesh();

        GameObject roadObj = currentSplineContainer.gameObject;

#if UNITY_AI_NAVIGATION
        NavMeshModifier modifier = roadObj.GetComponent<NavMeshModifier>();
        if (modifier == null) modifier = roadObj.AddComponent<NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = 3;
#endif

        roadObj.tag = "Untagged";

        var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
        if (surface != null) surface.UpdateNavMesh(surface.navMeshData);

        isBuilding = false;
        currentSplineContainer = null;
        roadCollider = null;
    }
}
