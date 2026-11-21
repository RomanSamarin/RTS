using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Форма строя, которую строят юниты.</summary>
public enum FormationShape
{
    /// <summary>Шеренги: по одному ряду на тип войск.</summary>
    Line,

    /// <summary>Клин: 1 юнит в первом ряду, в каждом следующем на одного больше.</summary>
    Wedge
}

/// <summary>
/// Построение выделенных юнитов по готовым точкам строя.
///
/// Точки строя — заранее созданные объекты сцены (дети formationRoot).
/// Пока включён режим построения, вся группа точек следует за курсором.
/// По ЛКМ каждый выделенный юнит получает свою точку.
///
/// Включение:
///   кнопка «в ширину» / «клин» на Canvas (Formation Button / Wedge Button)
///   или клавиша F (строит текущую форму)
///   Q / E (удерживать) или колесо мыши — поворот строя
///   ЛКМ — построить выделенных юнитов
///   ПКМ / Esc — отменить режим
///
/// Должен быть в сцене ОДИН. Лишние компоненты отключаются автоматически.
/// </summary>
public class SoldiersFormation : MonoBehaviour
{
    [Header("Точки строя")]
    [Tooltip("Объект-родитель точек строя. Точки — его дочерние объекты")]
    public Transform formationRoot;

    [Tooltip("Точки строя вручную. Если список пуст — берутся дочерние объекты formationRoot. " +
             "Повторы и недостающие дети formationRoot добавляются автоматически")]
    public List<Transform> points = new List<Transform>();

    [Header("Автосоздание точек")]
    [Tooltip("Создавать точки автоматически — ровно столько, сколько выделено юнитов")]
    public bool autoGeneratePoints = true;

    [Tooltip("Префаб точки строя (маркер/пустышка). Если пусто — создаются пустые объекты")]
    public Transform pointPrefab;

    [Tooltip("Расстояние между юнитами в шеренге и в рядах клина, м")]
    public float pointSpacing = 1.5f;

    [Tooltip("Расстояние между рядами строя (глубина), м")]
    public float rowSpacing = 1.5f;

    [Header("Форма строя")]
    [Tooltip("Какая форма строится сейчас. Меняется кнопками на Canvas")]
    public FormationShape currentShape = FormationShape.Line;

    [Header("Управление")]
    [Tooltip("Кнопка на Canvas: построение в ширину (шеренгой). " +
             "Если назначена — OnClick подключается автоматически (не дублируй его в инспекторе)")]
    public Button formationButton;

    [Tooltip("Отдельная кнопка на Canvas для построения клином. " +
             "Если назначена — OnClick подключается автоматически (не дублируй его в инспекторе)")]
    public Button wedgeButton;

    [Tooltip("Объект-индикатор включённого режима (подсветка кнопки и т.п.), необязательно")]
    public GameObject activeIndicator;

    [Tooltip("Клавиша включения/выключения режима. None — управление только кнопкой")]
    public KeyCode toggleKey = KeyCode.F;

    [Tooltip("Строить юнитов по ЛКМ")]
    public bool placeOnLeftClick = true;

    [Tooltip("Выходить из режима после построения")]
    public bool exitAfterPlacement = true;

    [Header("Распределение точек")]
    [Tooltip("Наоборот: юнит, ближайший к строю, идёт к самой дальней точке, " +
             "а самый дальний юнит — к ближней. Вызывает пересечение путей — " +
             "для построения без столкновений лучше держать выключенным")]
    public bool reverseMatching = false;

    [Header("Чтобы юниты не сталкивались")]
    [Tooltip("Минимальное расстояние между точками назначения, м. " +
             "Ближе юниты ставить нельзя — они будут задевать друг друга")]
    public float minDestinationSpacing = 1.4f;

    [Tooltip("Насколько далеко искать NavMesh под точкой строя, м. " +
             "Если точка попала в воду/дерево, она подтянется к проходимому месту")]
    public float navMeshSnapDistance = 2.5f;

    [Tooltip("Пауза между приказами юнитам, сек. Юниты стартуют по одному, " +
             "а не всей толпой в одну дверь. 0 — отправлять всех сразу")]
    public float orderInterval = 0.05f;

    [Header("Вращение строя")]
    [Tooltip("Клавиша поворота строя против часовой стрелки (удерживать)")]
    public KeyCode rotateLeftKey = KeyCode.Q;

    [Tooltip("Клавиша поворота строя по часовой стрелке (удерживать)")]
    public KeyCode rotateRightKey = KeyCode.E;

    [Tooltip("Скорость поворота при удержании клавиши, градусов в секунду")]
    public float holdRotationSpeed = 90f;

    [Tooltip("Поворачивать строй колесом мыши")]
    public bool rotateWithScroll = true;

    [Tooltip("Угол поворота за один щелчок колеса, градусов")]
    public float scrollRotationStep = 15f;

    [Header("Земля")]
    [Tooltip("Слой земли для луча из камеры. Если пусто — берётся Ground из ManagerGO")]
    public LayerMask ground;

    [Header("Отображение")]
    [Tooltip("Скрывать точки строя, когда режим выключен")]
    public bool hideWhenInactive = true;

    [Tooltip("Ставить под курсор центр строя (а не пивот formationRoot)")]
    public bool centerOnCursor = true;

    /// <summary>Включён ли сейчас режим построения (используется ManagerGO).</summary>
    public static bool IsPlacing { get; private set; }

    private static SoldiersFormation activeInstance;

    private struct UnitOrder
    {
        public GameObject Unit;
        public Vector3 Destination;
    }

    private struct UnitDestinationPair
    {
        public int UnitIndex;
        public int DestinationIndex;
        public float SqrDistance;
    }

    private readonly List<Transform> activePoints = new List<Transform>();
    private readonly List<Transform> generatedPoints = new List<Transform>();
    private Camera cam;
    private bool placing;
    private bool exitRequested;
    private float nextScrollRotateTime;
    private Vector3 localCenter;
    private Coroutine placementRoutine;
    private FormationShape builtShape;
    private const int FormationRankCount = 5;
    private readonly int[] selectedRankCounts = new int[FormationRankCount];
    private readonly int[] builtRankCounts = new int[FormationRankCount];
    private bool pointsBuilt;

    // ------------------------------------------------------------------ Unity

    private void Awake()
    {
        // В сцене должен быть один такой компонент. Иначе каждый из них
        // обрабатывает один и тот же клик и раздаёт юнитам свои приказы —
        // юниты мечутся между точками и сталкиваются.
        if (activeInstance != null && activeInstance != this)
        {
            Debug.LogWarning("[SoldiersFormation] В сцене несколько компонентов SoldiersFormation. " +
                             "Лишний отключён — оставь один (лучше на отдельном объекте-менеджере).", this);
            enabled = false;
            return;
        }

        activeInstance = this;
    }

    private void Start()
    {
        cam = Camera.main;

        if (ground.value == 0 && ManagerGO.Instance != null)
            ground = ManagerGO.Instance.Ground;

        CollectPoints();
        SetPreviewVisible(false);
    }

    private void Update()
    {
        if (SettingsMenu.BlocksWorldInput || Barracks.BlocksWorldInput || RoadBuilder.BlocksWorldInput
            || RtsNetworkConstruction.BlocksWorldInput) return;
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
        {
            TogglePlacement();
            return;
        }

        if (!placing)
            return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            RequestExit();
            return;
        }

        // Поворот строя: Q/E (удерживать) или колесо мыши
        HandleRotation();

        // Без выделенных юнитов строить нечего
        if (ManagerGO.Instance == null || !ManagerGO.Instance.unitsSelected.Exists(CanPlaceUnit))
        {
            SetPreviewVisible(false);
            return;
        }

        SetPreviewVisible(true);
        EnsurePointsFor(ManagerGO.Instance.unitsSelected);
        bool hasGround = FollowCursor();

        if (placeOnLeftClick && Input.GetMouseButtonDown(0) && !IsPointerOverUI()
            && (!RtsNetworkSession.BlocksOfflineCommands || hasGround))
            PlaceFormation();
    }

    private void OnEnable()
    {
        if (formationButton != null)
            formationButton.onClick.AddListener(ToggleLinePlacement);

        if (wedgeButton != null)
            wedgeButton.onClick.AddListener(ToggleWedgePlacement);
    }

    private void OnDisable()
    {
        if (formationButton != null)
            formationButton.onClick.RemoveListener(ToggleLinePlacement);

        if (wedgeButton != null)
            wedgeButton.onClick.RemoveListener(ToggleWedgePlacement);

        if (activeInstance != this)
            return;

        placing = false;
        exitRequested = false;
        IsPlacing = false;
        SetPreviewVisible(false);

        if (activeIndicator != null)
            activeIndicator.SetActive(false);
    }

    // Выходим из режима в конце кадра: ManagerGO должен увидеть IsPlacing == true
    // в своём Update, иначе он обработает тот же клик ЛКМ/ПКМ как выбор/приказ.
    private void LateUpdate()
    {
        if (!exitRequested)
            return;

        exitRequested = false;
        SetPlacing(false);
    }

    // ------------------------------------------------------------------ API

    /// <summary>Переключить режим построения текущей формы (для кнопки на Canvas).</summary>
    public void TogglePlacement()
    {
        SetPlacing(!placing);
    }

    /// <summary>Кнопка «в ширину»: построить юнитов одной шеренгой.</summary>
    public void ToggleLinePlacement()
    {
        ToggleShapePlacement(FormationShape.Line);
    }

    /// <summary>Кнопка «клин»: строить треугольником.</summary>
    public void ToggleWedgePlacement()
    {
        if (!autoGeneratePoints)
        {
            Debug.LogWarning("[SoldiersFormation] Клин строится по автоматически созданным точкам. " +
                             "Включи Auto Generate Points — иначе форма не изменится.", this);
        }

        ToggleShapePlacement(FormationShape.Wedge);
    }

    /// <summary>Выбрать форму строя (без включения/выключения режима).</summary>
    public void SetShape(FormationShape shape)
    {
        if (currentShape == shape)
            return;

        currentShape = shape;
        RebuildPointsIfPlacing();
    }

    // Кнопки формы работают как переключатели:
    // клик по кнопке уже выбранной формы выключает режим,
    // клик по другой форме переключает строй на неё, не выходя из режима.
    private void ToggleShapePlacement(FormationShape shape)
    {
        bool sameShape = currentShape == shape;
        currentShape = shape;

        if (placing && sameShape)
        {
            SetPlacing(false);
            return;
        }

        if (!placing)
        {
            SetPlacing(true);
            return;
        }

        // Режим уже включён, но выбрана другая форма — перестраиваем точки.
        RebuildPointsIfPlacing();
    }

    private void RebuildPointsIfPlacing()
    {
        if (!placing || !autoGeneratePoints || ManagerGO.Instance == null)
            return;

        EnsurePointsFor(ManagerGO.Instance.unitsSelected);
    }

    /// <summary>Включить/выключить режим построения (можно вызывать из UI).</summary>
    public void SetPlacing(bool value)
    {
        if (value && RtsNetworkSession.BlocksOfflineCommands)
        {
            if (RtsNetworkSession.Instance?.Ready != true || RtsNetworkConstruction.BlocksWorldInput
                || RoadBuilder.BlocksWorldInput || SettingsMenu.BlocksWorldInput
                || ManagerGO.Instance?.unitsSelected.Exists(CanPlaceUnit) != true) return;
            ManagerGO.Instance.CancelMilitaryTargetSelection();
        }
        placing = value;
        IsPlacing = value;
        exitRequested = false;

        SetPreviewVisible(value);

        if (value)
            EnsurePointsForSelection();

        if (activeIndicator != null)
            activeIndicator.SetActive(value);
    }

    /// <summary>Отменить режим построения.</summary>
    public void CancelPlacement()
    {
        RequestExit();
    }

    /// <summary>Выключить режим в конце текущего кадра.</summary>
    private void RequestExit()
    {
        exitRequested = true;
    }

    // ------------------------------------------------------------------ Внутреннее

    private void CollectPoints()
    {
        if (points == null)
            points = new List<Transform>();

        // При автосоздании ручной список не нужен: точки строятся
        // под число выделенных юнитов при входе в режим.
        if (!autoGeneratePoints)
        {
            // Убираем дубликаты: два юнита на одну точку — это столкновение
            List<Transform> unique = new List<Transform>();

            foreach (Transform point in points)
            {
                if (point == null) continue;
                if (unique.Contains(point)) continue;

                unique.Add(point);
            }

            // Добираем детей formationRoot, которых нет в списке.
            // Так пропущенная в списке точка не остаётся неиспользованной.
            if (formationRoot != null)
            {
                foreach (Transform child in formationRoot)
                {
                    if (child == null) continue;
                    if (unique.Contains(child)) continue;

                    unique.Add(child);
                }
            }

            points.Clear();
            points.AddRange(unique);
        }

        activePoints.Clear();
        activePoints.AddRange(points);

        RefreshLocalCenter();
    }

    /// <summary>
    /// Центр строя в локальных координатах formationRoot,
    /// чтобы под курсором оказывался центр построения, а не пивот объекта.
    /// </summary>
    private void RefreshLocalCenter()
    {
        localCenter = Vector3.zero;

        if (formationRoot == null)
            return;

        int count = 0;

        foreach (Transform point in points)
        {
            if (point == null) continue;

            localCenter += formationRoot.InverseTransformPoint(point.position);
            count++;
        }

        if (count > 0)
            localCenter /= count;
    }

    private void EnsurePointsForSelection()
    {
        if (!autoGeneratePoints || ManagerGO.Instance == null)
            return;

        EnsurePointsFor(ManagerGO.Instance.unitsSelected);
    }

    /// <summary>
    /// Держит в formationRoot ровно count точек. Выделили больше юнитов —
    /// точки дорисовываются по префабу, меньше — лишние удаляются.
    /// Благодаря этому точек всегда столько же, сколько юнитов,
    /// и двоим не приходится идти в одну точку.
    /// </summary>
    private void EnsurePointsFor(List<GameObject> units)
    {
        if (!autoGeneratePoints || units.Count == 0 || formationRoot == null)
            return;

        System.Array.Clear(selectedRankCounts, 0, FormationRankCount);
        int count = 0;
        foreach (var unit in units)
        {
            if (!CanPlaceUnit(unit)) continue;
            selectedRankCounts[FormationRank(unit)]++;
            count++;
        }
        if (count == 0) return;

        if (pointsBuilt && points.Count == count && builtShape == currentShape &&
            builtRankCounts.SequenceEqual(selectedRankCounts))
            return;

        System.Array.Copy(selectedRankCounts, builtRankCounts, FormationRankCount);
        BuildPoints(count);
        RefreshLocalCenter();
    }

    private void BuildPoints(int count)
    {
        ClearGeneratedPoints();

        List<Vector3> layout = BuildRankLayout(count);

        foreach (Vector3 localPosition in layout)
        {
            Transform point = CreatePoint(localPosition);

            generatedPoints.Add(point);
            points.Add(point);
        }

        builtShape = currentShape;
        pointsBuilt = true;
    }

    private void ClearGeneratedPoints()
    {
        // Старые точки убираем, иначе в formationRoot накопятся лишние
        foreach (Transform point in generatedPoints)
        {
            if (point == null) continue;

            point.gameObject.SetActive(false);
            Destroy(point.gameObject);
        }

        generatedPoints.Clear();
        points.Clear();
        activePoints.Clear();

        pointsBuilt = false;
    }

    private static int FormationRank(GameObject unit)
    {
        switch (BattleCombatant.TypeOf(unit))
        {
            case TroopType.Spearman: return 0;
            case TroopType.Swordsman: return 1;
            case TroopType.Archer: return 2;
            case TroopType.Cavalry: return 3;
            default: return 4;
        }
    }

    private static bool CanPlaceUnit(GameObject unit)
        => unit != null && (!RtsNetworkSession.BlocksOfflineCommands
            || (unit.TryGetComponent<RtsNetworkUnit>(out var network) && network.IsSpawned
                && network.IsMeleeFighter && FactionMember.IsPlayer(unit)));

    private List<Vector3> BuildRankLayout(int count)
    {
        var rowSizes = new List<int>();
        foreach (int rankCount in builtRankCounts)
        {
            int remaining = rankCount;
            while (remaining > 0)
            {
                int width = currentShape == FormationShape.Wedge
                    ? Mathf.Min(rowSizes.Count + 1, remaining)
                    : remaining;
                rowSizes.Add(width);
                remaining -= width;
            }
        }

        var layout = new List<Vector3>(count);
        float spacing = Mathf.Max(.2f, Mathf.Max(pointSpacing, minDestinationSpacing));
        float depth = Mathf.Max(.2f, Mathf.Max(rowSpacing, minDestinationSpacing));
        for (int row = 0; row < rowSizes.Count; row++)
        {
            int width = rowSizes[row];
            float xStart = -(width - 1) * .5f * spacing;
            float z = ((rowSizes.Count - 1) * .5f - row) * depth;
            for (int column = 0; column < width; column++)
                layout.Add(new Vector3(xStart + column * spacing, 0f, z));
        }
        return layout;
    }

    private Transform CreatePoint(Vector3 localPosition)
    {
        Transform point;
        Quaternion localRotation = Quaternion.identity;

        if (pointPrefab != null)
        {
            // Сохраняем поворот префаба: маркер точки должен смотреть так же,
            // как повёрнут префаб, а не строго по осям. Поворот formationRoot
            // при этом добавляется сверху — точки крутятся вместе со строем.
            localRotation = pointPrefab.localRotation;

            point = Instantiate(pointPrefab, formationRoot, false);
        }
        else
        {
            GameObject go = new GameObject("FormationPoint");
            point = go.transform;
            point.SetParent(formationRoot, false);
        }

        point.localPosition = localPosition;
        point.localRotation = localRotation;
        point.gameObject.SetActive(true);

        return point;
    }

    /// <summary>Точки строя едут за курсором.</summary>
    private bool FollowCursor()
    {
        if (cam == null || formationRoot == null)
            return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, ground, QueryTriggerInteraction.Ignore))
            return false;

        Vector3 offset = centerOnCursor ? formationRoot.rotation * localCenter : Vector3.zero;
        formationRoot.position = hit.point - offset;
        return true;
    }

    /// <summary>Поворот строя: удержание Q/E и колесо мыши.</summary>
    private void HandleRotation()
    {
        float degrees = 0f;

        if (rotateLeftKey != KeyCode.None && Input.GetKey(rotateLeftKey))
            degrees -= holdRotationSpeed * Time.deltaTime;

        if (rotateRightKey != KeyCode.None && Input.GetKey(rotateRightKey))
            degrees += holdRotationSpeed * Time.deltaTime;

        if (rotateWithScroll && Time.time >= nextScrollRotateTime)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.02f)
            {
                degrees += Mathf.Sign(scroll) * scrollRotationStep;
                nextScrollRotateTime = Time.time + 0.08f;
            }
        }

        if (Mathf.Abs(degrees) < 0.001f)
            return;

        RotateFormation(degrees);
    }

    /// <summary>Повернуть строй вокруг его пивота (formationRoot).</summary>
    public void RotateFormation(float degrees)
    {
        if (formationRoot == null)
            return;

        formationRoot.Rotate(Vector3.up, degrees, Space.World);
    }

    /// <summary>Раскладывает выделенных юнитов по точкам строя.</summary>
    internal bool PlaceFormationAt(Vector3 center, float yaw)
    {
        if (!placing || formationRoot == null) return false;
        EnsurePointsForSelection();
        formationRoot.rotation = Quaternion.Euler(0, yaw, 0);
        formationRoot.position = center - (centerOnCursor ? formationRoot.rotation * localCenter : Vector3.zero);
        return PlaceFormation();
    }

    private bool PlaceFormation()
    {
        if (formationRoot == null)
        {
            Debug.LogWarning($"[SoldiersFormation] Не назначен formationRoot у \"{name}\".", this);
            return false;
        }

        List<GameObject> units = ManagerGO.Instance.unitsSelected;
        if (units == null || units.Count == 0)
            return false;

        RefreshActivePoints();

        if (activePoints.Count == 0)
        {
            Debug.LogWarning($"[SoldiersFormation] У \"{formationRoot.name}\" нет точек строя.", this);
            return false;
        }

        List<GameObject> validUnits = new List<GameObject>();

        foreach (GameObject unit in units)
        {
            if (CanPlaceUnit(unit) && !validUnits.Contains(unit))
                validUnits.Add(unit);
        }

        if (validUnits.Count == 0)
            return false;

        // Сначала готовим безопасные точки: на NavMesh и не ближе
        // minDestinationSpacing друг к другу.
        List<Vector3> destinations = BuildDestinations(activePoints, validUnits.Count);

        if (destinations.Count == 0)
        {
            Debug.LogWarning("[SoldiersFormation] Не удалось подобрать точки назначения.", this);
            return false;
        }

        List<UnitOrder> orders = BuildOrders(validUnits, destinations);

        if (orders.Count == 0) return false;
        if (RtsNetworkSession.BlocksOfflineCommands)
        {
            var session = RtsNetworkSession.Instance;
            if (!session.Ready || orders.Count > RtsNetworkSession.MaxFormationUnits) return false;
            session.RequestFormation(orders.Select(order => order.Unit.GetComponent<RtsNetworkUnit>().EntityId).ToArray(),
                orders.Select(order => order.Destination).ToArray(), formationRoot.forward);
        }
        else StartIssuingOrders(orders);

        if (exitAfterPlacement)
            RequestExit();
        return true;
    }

    private void RefreshActivePoints()
    {
        activePoints.Clear();

        foreach (Transform point in points)
        {
            if (point == null) continue;
            if (!point.gameObject.activeSelf) continue;

            activePoints.Add(point);
        }
    }

    // ---------------------------------------------------------- точки назначения

    /// <summary>
    /// Готовит точки назначения: подтягивает их к NavMesh, убирает слишком
    /// близкие и добавляет места рядом, если юнитов больше, чем точек.
    /// Благодаря этому два юнита никогда не идут в одну и ту же точку.
    /// </summary>
    private List<Vector3> BuildDestinations(List<Transform> pointList, int needed)
    {
        List<Vector3> result = new List<Vector3>();
        float spacing = Mathf.Max(0.2f, minDestinationSpacing);

        foreach (Transform point in pointList)
        {
            if (point == null) continue;

            Vector3 position = SnapToNavMesh(point.position);

            if (!IsFreeDestination(position, result, spacing))
            {
                position = FindFreeDestination(position, result, spacing);

                if (!IsFreeDestination(position, result, spacing))
                    continue;
            }

            result.Add(position);
        }

        if (result.Count == 0)
            return result;

        // Юнитов больше, чем точек — добавляем места кольцами вокруг точек,
        // чтобы они не вставали друг на друга.
        List<Vector3> basePoints = new List<Vector3>(result);

        for (int ring = 1; result.Count < needed && ring <= 3; ring++)
        {
            float radius = spacing * ring;

            foreach (Vector3 basePoint in basePoints)
            {
                for (int i = 0; i < 8 && result.Count < needed; i++)
                {
                    float angle = i * Mathf.PI * 0.25f + ring * 0.35f;
                    Vector3 candidate = basePoint +
                        new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                    candidate = SnapToNavMesh(candidate);

                    if (IsFreeDestination(candidate, result, spacing))
                        result.Add(candidate);
                }
            }
        }

        // Совсем не хватило проходимых мест — разрешаем встать на первую точку
        while (result.Count < needed)
            result.Add(result[0]);

        return result;
    }

    private Vector3 SnapToNavMesh(Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSnapDistance, NavMesh.AllAreas))
            return hit.position;

        return position;
    }

    private static bool IsFreeDestination(Vector3 candidate, List<Vector3> used, float minSpacing)
    {
        float sqrSpacing = minSpacing * minSpacing;

        foreach (Vector3 other in used)
        {
            if ((other - candidate).sqrMagnitude < sqrSpacing)
                return false;
        }

        return true;
    }

    private Vector3 FindFreeDestination(Vector3 origin, List<Vector3> used, float minSpacing)
    {
        for (int ring = 1; ring <= 3; ring++)
        {
            float radius = minSpacing * ring;

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                Vector3 candidate = origin +
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                candidate = SnapToNavMesh(candidate);

                if (IsFreeDestination(candidate, used, minSpacing))
                    return candidate;
            }
        }

        return origin;
    }

    // ------------------------------------------------------------ распределение

    private List<UnitOrder> BuildOrders(List<GameObject> units, List<Vector3> destinations)
    {
        var ranks = new List<GameObject>[FormationRankCount];
        foreach (var unit in units)
        {
            int rank = FormationRank(unit);
            (ranks[rank] ??= new List<GameObject>()).Add(unit);
        }

        if (ranks.Count(rank => rank != null) == 1)
            return MatchSlots(units, destinations);

        Vector3 forward = formationRoot.forward;
        Vector3 origin = formationRoot.position;
        destinations.Sort((a, b) => Vector3.Dot(b - origin, forward).CompareTo(Vector3.Dot(a - origin, forward)));
        var orders = new List<UnitOrder>(units.Count);
        int offset = 0;
        foreach (var rank in ranks)
        {
            if (rank == null) continue;
            var slots = destinations.GetRange(offset, rank.Count);
            orders.AddRange(MatchSlots(rank, slots));
            offset += rank.Count;
        }
        return orders;
    }

    private List<UnitOrder> MatchSlots(List<GameObject> units, List<Vector3> destinations)
    {
        return reverseMatching
            ? BuildMirroredOrders(units, destinations)
            : BuildNearestOrders(units, destinations);
    }

    /// <summary>
    /// Обычное распределение: сначала самая близкая пара «юнит-точка»,
    /// затем следующая. Пути не пересекаются — меньше столкновений.
    /// </summary>
    private List<UnitOrder> BuildNearestOrders(List<GameObject> units, List<Vector3> destinations)
    {
        List<UnitOrder> orders = new List<UnitOrder>();

        List<UnitDestinationPair> pairs = new List<UnitDestinationPair>(units.Count * destinations.Count);

        for (int u = 0; u < units.Count; u++)
        {
            if (units[u] == null) continue;

            Vector3 unitPosition = units[u].transform.position;

            for (int d = 0; d < destinations.Count; d++)
            {
                pairs.Add(new UnitDestinationPair
                {
                    UnitIndex = u,
                    DestinationIndex = d,
                    SqrDistance = (destinations[d] - unitPosition).sqrMagnitude
                });
            }
        }

        if (pairs.Count == 0)
            return orders;

        pairs.Sort((a, b) => a.SqrDistance.CompareTo(b.SqrDistance));

        bool[] unitAssigned = new bool[units.Count];
        bool[] destinationAssigned = new bool[destinations.Count];

        int needed = 0;
        foreach (GameObject unit in units)
            if (unit != null) needed++;

        foreach (UnitDestinationPair pair in pairs)
        {
            if (needed <= 0)
                break;

            if (unitAssigned[pair.UnitIndex] || destinationAssigned[pair.DestinationIndex])
                continue;

            unitAssigned[pair.UnitIndex] = true;
            destinationAssigned[pair.DestinationIndex] = true;
            needed--;

            orders.Add(new UnitOrder
            {
                Unit = units[pair.UnitIndex],
                Destination = destinations[pair.DestinationIndex]
            });
        }

        return orders;
    }

    /// <summary>
    /// Обратное распределение: юнит, ближайший к строю, идёт к самой дальней точке,
    /// самый дальний юнит — к самой ближней.
    /// </summary>
    private List<UnitOrder> BuildMirroredOrders(List<GameObject> units, List<Vector3> destinations)
    {
        List<UnitOrder> orders = new List<UnitOrder>();

        Vector3 center = GetCenter(destinations);

        List<GameObject> sortedUnits = new List<GameObject>(units);
        sortedUnits.Sort((a, b) =>
            (a.transform.position - center).sqrMagnitude
                .CompareTo((b.transform.position - center).sqrMagnitude));

        List<Vector3> sortedDestinations = new List<Vector3>(destinations);
        sortedDestinations.Sort((a, b) =>
            (b - center).sqrMagnitude
                .CompareTo((a - center).sqrMagnitude));

        int count = Mathf.Min(sortedUnits.Count, sortedDestinations.Count);

        for (int i = 0; i < count; i++)
        {
            if (sortedUnits[i] == null) continue;

            orders.Add(new UnitOrder
            {
                Unit = sortedUnits[i],
                Destination = sortedDestinations[i]
            });
        }

        return orders;
    }

    private static Vector3 GetCenter(List<Vector3> pointList)
    {
        if (pointList == null || pointList.Count == 0)
            return Vector3.zero;

        Vector3 center = Vector3.zero;

        foreach (Vector3 point in pointList)
            center += point;

        return center / pointList.Count;
    }

    // --------------------------------------------------------------- отправка

    /// <summary>
    /// Разносит приказы по времени: юниты выходят по одному и не толкаются
    /// в одной точке старта.
    /// </summary>
    private void StartIssuingOrders(List<UnitOrder> orders)
    {
        if (placementRoutine != null)
            StopCoroutine(placementRoutine);
        Vector3 facing = formationRoot != null ? formationRoot.forward : transform.forward;
        var squadUnits=new List<GameObject>();var squadSlots=new List<Vector3>();
        foreach(var order in orders){if(order.Unit!=null){squadUnits.Add(order.Unit);squadSlots.Add(order.Destination);}}
        if(SquadMovement.MoveSlots(squadUnits,squadSlots,facing,true))return;
        int group = FormationHold.BeginGroup();

        if (orderInterval <= 0f || orders.Count <= 1)
        {
            foreach (UnitOrder order in orders)
                MoveUnit(order.Unit, order.Destination, facing, group);

            return;
        }

        placementRoutine = StartCoroutine(IssueOrdersRoutine(orders, facing, group));
    }

    private IEnumerator IssueOrdersRoutine(List<UnitOrder> orders, Vector3 facing, int group)
    {
        WaitForSeconds delay = new WaitForSeconds(Mathf.Max(0.01f, orderInterval));

        for (int i = 0; i < orders.Count; i++)
        {
            MoveUnit(orders[i].Unit, orders[i].Destination, facing, group);
            yield return delay;
        }

        placementRoutine = null;
    }

    private static void MoveUnit(GameObject unit, Vector3 destination, Vector3 facing, int group)
    {
        if (unit == null)
            return;

        // Основной путь — через GOScript: он сам завершит приказ по прибытии
        GOScript move = unit.GetComponent<GOScript>();
        if (move != null)
            move.MoveTo(destination);
        else
        {
            // Запасной вариант, если на юните нет GOScript
            NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh)
                agent.SetDestination(destination);
        }

        // Сообщаем Animator-у (UnitHoldingBehaviour на состоянии Idle), что это
        // точка строя: по прибытии включится поза Holding.
        FormationHold.SetPoint(unit, destination, facing, group);
    }

    private void SetPreviewVisible(bool visible)
    {
        if (formationRoot == null)
            return;

        if (!visible && !hideWhenInactive)
            return;

        // Если скрипт лежит на самом объекте строя (или внутри него),
        // выключать этот объект нельзя — выключится и скрипт.
        bool scriptInsideRoot = transform == formationRoot || transform.IsChildOf(formationRoot);

        if (!scriptInsideRoot)
        {
            formationRoot.gameObject.SetActive(visible);
            return;
        }

        foreach (Transform point in points)
        {
            if (point != null)
                point.gameObject.SetActive(visible);
        }
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    // ------------------------------------------------------------------ Editor

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.7f);

        foreach (Transform point in points)
        {
            if (point != null)
                Gizmos.DrawWireSphere(point.position, 0.3f);
        }

        if (formationRoot != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(formationRoot.position, Vector3.one * 0.5f);
        }
    }
}
