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
    public GameObject roadTemplatePrefab; 

    private SplineContainer currentSplineContainer;
    private MeshCollider roadCollider;
    
    private bool isBuilding = false;
    private Vector3 lastPlacedWorldPosition;

    void Update()
    {
        // Кнопка R запускает строительство
        if (Input.GetKeyDown(KeyCode.R) && !isBuilding)
        {
            StartNewRoad();
        }

        if (!isBuilding) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            Vector3 targetWorldPosition = hit.point;
            
            // 1. Проверяем привязку к узлам других дорог (в мировых координатах)
            targetWorldPosition = CheckSnap(targetWorldPosition);

            // 2. Обновляем временную точку, за которой тянется превью
            UpdatePreviewNode(targetWorldPosition);

            // ЛКМ — зафиксировать узел
            if (Input.GetMouseButtonDown(0))
            {
                TryPlaceNode(targetWorldPosition);
            }
        }

        // ПКМ — завершить строительство дороги
        if (Input.GetMouseButtonDown(1))
        {
            FinishRoad();
        }
    }

    void StartNewRoad()
    {
        if (roadTemplatePrefab == null)
        {
            Debug.LogError("Пожалуйста, назначьте Road Template Prefab в инспекторе!");
            return;
        }

        isBuilding = true;
        
        // Спавним префаб дороги
        GameObject roadObject = Instantiate(roadTemplatePrefab);
        roadObject.name = "DynamicRoad";
        roadObject.tag = "EditorOnly"; 

        currentSplineContainer = roadObject.GetComponent<SplineContainer>();
        roadCollider = roadObject.GetComponent<MeshCollider>();
        
        lastPlacedWorldPosition = Vector3.zero;
    }

    void UpdatePreviewNode(Vector3 mouseWorldPosition)
    {
        if (currentSplineContainer == null) return;
        
        Spline spline = currentSplineContainer.Spline;
        
        // Переводим мировую позицию мыши в ЛОКАЛЬНЫЕ координаты контейнера сплайна
        Vector3 localPos = currentSplineContainer.transform.InverseTransformPoint(mouseWorldPosition);

        if (spline.Count == 0)
        {
            // Самая первая точка при создании
            spline.Add(new BezierKnot(localPos));
            spline.Add(new BezierKnot(localPos)); // Вторая точка становится "превью"
        }
        else
        {
            // Изменяем положение только последнего (превью) узла
            int lastIndex = spline.Count - 1;
            BezierKnot knot = spline[lastIndex];
            knot.Position = localPos;
            spline[lastIndex] = knot;
        }

        AutoSmoothSpline();
        RefreshColliderAndMesh();
    }

    void TryPlaceNode(Vector3 worldPosition)
    {
        if (currentSplineContainer == null) return;
        
        Spline spline = currentSplineContainer.Spline;

        // Проверяем дистанцию в мировых координатах, чтобы избежать "кучкования" точек
        if (lastPlacedWorldPosition != Vector3.zero && Vector3.Distance(lastPlacedWorldPosition, worldPosition) < minNodeDistance)
        {
            return;
        }

        Vector3 localPos = currentSplineContainer.transform.InverseTransformPoint(worldPosition);

        // Фиксируем текущую превью-ноду в этой точке
        int lastIndex = spline.Count - 1;
        BezierKnot knot = spline[lastIndex];
        knot.Position = localPos;
        spline[lastIndex] = knot;

        // Добавляем СЛЕДУЮЩУЮ превью-ноду в эту же позицию (она начнет двигаться с мышью на следующем кадре)
        spline.Add(new BezierKnot(localPos));

        lastPlacedWorldPosition = worldPosition;
        RefreshColliderAndMesh();
    }

    void AutoSmoothSpline()
    {
        if (currentSplineContainer == null) return;
        
        Spline spline = currentSplineContainer.Spline;
        if (spline.Count < 3) return;

        // Рекомендуется применять ко всему сплайну для гладкости симулятора
        spline.SetTangentMode(TangentMode.AutoSmooth);
    }

    void RefreshColliderAndMesh()
    {
        if (currentSplineContainer == null) return;

        // Ждем один кадр или пинаем генератор меша, если он не обновляется мгновенно.
        // Обычно встроенные компоненты (Loft/Extrude) обновляются в LateUpdate или по событию Spline.Changed.
        MeshFilter mf = currentSplineContainer.gameObject.GetComponentInChildren<MeshFilter>();
        if (roadCollider != null && mf != null && mf.sharedMesh != null)
        {
            roadCollider.sharedMesh = null; 
            roadCollider.sharedMesh = mf.sharedMesh; 
        }
    }

    Vector3 CheckSnap(Vector3 currentMouseWorldPos)
    {
        // Находим все дороги на сцене
        SplineContainer[] allRoads = FindObjectsByType<SplineContainer>(FindObjectsSortMode.None);
        Vector3 bestSnapPoint = currentMouseWorldPos;
        float closestDistance = snapDistance;

        foreach (var road in allRoads)
        {
            // Пропускаем дорогу, которую строим прямо сейчас
            if (road == currentSplineContainer) continue;

            Spline spline = road.Spline;
            for (int i = 0; i < spline.Count; i++)
            {
                // Переводим точку чужого сплайна в мировые координаты для честного расчета расстояния
                Vector3 nodeWorldPos = road.transform.TransformPoint(spline[i].Position);
                float dist = Vector3.Distance(currentMouseWorldPos, nodeWorldPos);

                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    bestSnapPoint = nodeWorldPos; // Возвращаем мировую координату точки привязки
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

        // Полностью изолируем весь код навигации для безопасной компиляции
#if UNITY_AI_NAVIGATION
        NavMeshModifier modifier = roadObj.GetComponent<NavMeshModifier>();
        if (modifier == null) modifier = roadObj.AddComponent<NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = 3; 

        roadObj.tag = "Untagged"; 

        // Теперь NavMeshSurface не вызовет ошибку, даже если пакета нет в проекте
        var surface = Object.FindFirstObjectByType<NavMeshSurface>();
        if (surface != null) 
        {
            surface.UpdateNavMesh(surface.navMeshData);
        }
#else
        // Лог на случай, если вы забыли поставить пакет навигации
        roadObj.tag = "Untagged";
        Debug.LogWarning("Пакет Unity AI Navigation не найден. Обновление NavMesh пропущено.");
#endif

        isBuilding = false;
        currentSplineContainer = null;
        roadCollider = null;
    }

}
