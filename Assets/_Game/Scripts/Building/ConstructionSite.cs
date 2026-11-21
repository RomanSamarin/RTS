using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// Строительная площадка. Создаётся автоматически, когда игрок ставит здание
/// (PlacementSystem), и заменяет собой готовое здание до окончания стройки.
///
/// Пока идёт стройка:
///   • само здание выключено (SetActive(false)) и не работает;
///   • на его месте стоит каркас/фундамент и шкала прогресса;
///   • строители (Builder) подходят к площадке и увеличивают Progress01.
///
/// Когда прогресс доходит до 1:
///   • здание включается на том же месте и с тем же поворотом;
///   • вызывается Constructable.ConstructableWasPlaced();
///   • срабатывает событие Completed (PlacementSystem начисляет бонусы, например жильё);
///   • площадка удаляется.
///
/// Вручную на сцену вешать не нужно — компонент добавляется кодом.
/// </summary>
[DisallowMultipleComponent]
public class ConstructionSite : MonoBehaviour
{
    // ------------------------------------------------------------------ Статика

    private static readonly List<ConstructionSite> allSites = new List<ConstructionSite>();

    /// <summary>Все площадки, на которых ещё идёт стройка.</summary>
    public static IReadOnlyList<ConstructionSite> AllSites => allSites;

    /// <summary>
    /// Ближайшая площадка, на которую ещё есть свободное место для строителя.
    /// </summary>
    public static ConstructionSite FindNearest(Vector3 position, float maxDistance = 200f,Faction? owner=null)
    {
        CleanupList();

        ConstructionSite nearest = null;
        float nearestSqr = maxDistance * maxDistance;

        foreach (ConstructionSite site in allSites)
        {
            if (site == null || !site.IsAvailable || !site.HasFreeSlot)
                continue;
            if(owner.HasValue&&!FactionRules.AreAllies(owner.Value,FactionMember.Of(site.gameObject)))continue;

            float sqr = (site.transform.position - position).sqrMagnitude;
            if (sqr >= nearestSqr)
                continue;

            nearestSqr = sqr;
            nearest = site;
        }

        return nearest;
    }

    private static void CleanupList()
    {
        for (int i = allSites.Count - 1; i >= 0; i--)
        {
            if (allSites[i] == null)
                allSites.RemoveAt(i);
        }
    }

    // ------------------------------------------------------------------ Поля

    [Header("Стройка")]
    [Tooltip("Сколько секунд строит ОДИН строитель")]
    [SerializeField] private float buildTime = 8f;

    [Tooltip("Максимум строителей одновременно на одной площадке")]
    [SerializeField] private int maxBuilders = 4;

    [Tooltip("Размер здания в клетках (задаётся автоматически из базы построек)")]
    [SerializeField] private Vector2Int size = Vector2Int.one;

    [Header("Внешний вид площадки")]
    [Tooltip("Необязательно: свой префаб каркаса/фундамента. " +
             "Если пусто — каркас собирается из простых кубов кодом")]
    [SerializeField] private GameObject visualPrefab;

    [Tooltip("Перекрывать NavMesh под стройплощадкой, чтобы юниты не проходили " +
             "сквозь будущее здание. На готовом здании obstacle включается сам")]
    [SerializeField] private bool blockSurface = true;

    // Здание, которое появится после окончания стройки
    private GameObject building;
    public GameObject Building=>building;
    public bool IsNearWorkArea(Vector3 point)
    {
        Vector3 delta=point-transform.TransformPoint(footprintCenter);delta.y=0;
        float radius=Mathf.Max(footprintSize.x,footprintSize.y)*.5f+3f;
        return delta.sqrMagnitude<=radius*radius;
    }

    // Фактический центр и размер меша здания в локальных координатах.
    // Нужны, потому что pivot префаба часто стоит не в центре постройки.
    private Vector3 footprintCenter;
    private Vector2 footprintSize = Vector2.one;

    // Строители, которые сейчас реально работают на площадке
    private readonly List<Builder> builders = new List<Builder>();

    private float barHeight = 2.6f;

    private bool completed;
    public ConstructionMaterials Materials { get; set; }
    public int CompletionObjectId=-1;
    public bool MaterialsReady => Materials == null || Materials.Ready;

    // ------------------------------------------------------------------ Свойства

    /// <summary>Готовность стройки от 0 до 1.</summary>
    public float Progress01 { get; private set; }

    /// <summary>Стройка ещё идёт и здание существует.</summary>
    public bool IsAvailable => !completed && building != null;
    public bool OverlapsRoadCell(Vector3 world,float halfWidth)
    {
        Vector3 point=transform.InverseTransformPoint(world)-footprintCenter;
        return Mathf.Abs(point.x)<footprintSize.x*.5f+halfWidth-.05f && Mathf.Abs(point.z)<footprintSize.y*.5f+halfWidth-.05f;
    }

    /// <summary>Есть место для ещё одного строителя.</summary>
    public bool HasFreeSlot => builders.Count < Mathf.Max(1, maxBuilders);

    /// <summary>Сколько строителей работает прямо сейчас.</summary>
    public int BuilderCount => builders.Count;

    /// <summary>Сколько строителей помещается на площадке.</summary>
    public int MaxBuilders => Mathf.Max(1, maxBuilders);

    /// <summary>Событие окончания стройки (подписывается PlacementSystem).</summary>
    public event Action<ConstructionSite> Completed;

    // ------------------------------------------------------------------ Создание

    /// <summary>
    /// Начать стройку: скрыть готовое здание и поставить на его место площадку.
    /// Возвращает null, если начать стройку невозможно.
    /// </summary>
    public static ConstructionSite Begin(GameObject building, float buildTime, Vector2Int size, GameObject visualPrefab, bool createVisual = true)
    {
        if (building == null)
            return null;

        GameObject root = new GameObject($"ConstructionSite_{building.name}");
        FactionMember.Set(root,FactionMember.Of(building));
        root.transform.SetPositionAndRotation(building.transform.position, building.transform.rotation);

        ConstructionSite site = root.AddComponent<ConstructionSite>();
        site.building = building;
        site.buildTime = Mathf.Max(0.5f, buildTime);
        site.size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        site.visualPrefab = visualPrefab;

        // Границы меша считаем ДО выключения здания: pivot у префаба может
        // быть смещён, и площадка должна встать вокруг самой модели, а не вокруг pivot.
        site.MeasureFootprint(building);

        // Высоту здания запоминаем ДО выключения: шкала должна висеть над крышей.
        site.barHeight = MeasureHeight(building) + 0.8f;

        // Здание не должно работать и не должно быть видно до конца стройки.
        building.SetActive(false);

        if (createVisual)
        {
            site.CreateVisual();
            site.CreateSelectionCollider();
        }

        return site;
    }
    private void CreateSelectionCollider()
    {
        // У каркаса удалены физические коллайдеры. Триггер позволяет выбрать его для приказа.
        var selection=GetComponent<BoxCollider>();if(selection==null)selection=gameObject.AddComponent<BoxCollider>();
        float height=Mathf.Max(1f,barHeight-.8f);
        selection.isTrigger=true;
        selection.center=new Vector3(footprintCenter.x,height*.5f,footprintCenter.z);
        selection.size=new Vector3(Mathf.Max(.2f,footprintSize.x),height,Mathf.Max(.2f,footprintSize.y));
    }

    // ------------------------------------------------------------------ Unity

    private void Awake()
    {
        if (!allSites.Contains(this))
            allSites.Add(this);
    }

    private void OnDestroy()
    {
        if (Materials != null) Materials.ReleaseAll();
        allSites.Remove(this);

        // Площадку снесли до окончания стройки — освобождаем строителей.
        for (int i = builders.Count - 1; i >= 0; i--)
        {
            if (builders[i] != null)
                builders[i].OnSiteRemoved(this);
        }

        builders.Clear();
    }

    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        // Здание снесли через режим удаления — площадка больше не нужна.
        if (building == null)
        {
            Destroy(gameObject);
            return;
        }

        if (completed)
            return;

        RemoveInvalidBuilders();

        float speed = 0f;
        for (int i = 0; i < builders.Count; i++)
        {
            if (builders[i] != null)
                speed += Mathf.Max(0f, builders[i].WorkSpeed);
        }

        if (speed > 0f && MaterialsReady)
        {
            Progress01 += Time.deltaTime * speed / Mathf.Max(0.1f, buildTime);

            if (Progress01 >= 1f)
            {
                Progress01 = 1f;
                FinishConstruction();
                return;
            }
        }

    }

    // ------------------------------------------------------------------ Строители

    /// <summary>
    /// Строитель дошёл до площадки и начинает работать.
    /// Возвращает false, если мест уже нет.
    /// </summary>
    public bool RegisterBuilder(Builder builder)
    {
        if (builder == null || !IsAvailable || !MaterialsReady)
            return false;

        if (builders.Contains(builder))
            return true;

        if (builders.Count >= Mathf.Max(1, maxBuilders))
            return false;

        builders.Add(builder);
        Debug.Log($"[ConstructionSite] {builder.name} начал строить {name} " +
                  $"({builders.Count}/{MaxBuilders}).", this);

        return true;
    }

    /// <summary>Строитель ушёл или прервал работу.</summary>
    public void UnregisterBuilder(Builder builder)
    {
        if (builder == null)
            return;

        builders.Remove(builder);
    }

    /// <summary>
    /// Точка, к которой строителю нужно подойти.
    /// Считается по направлению от площадки к строителю — поэтому
    /// несколько строителей встают вокруг здания, а не в одну точку.
    /// </summary>
    public Vector3 GetWorkPoint(Vector3 fromPosition)
    {
        Vector3 center = transform.TransformPoint(footprintCenter);

        Vector3 direction = fromPosition - center;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            direction = transform.forward;

        direction.Normalize();

        float radius = Mathf.Max(footprintSize.x, footprintSize.y) * 0.5f + 0.9f;
        Vector3 candidate = center + direction * radius;

        // 1) Точка снаружи здания — самый естественный подход.
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            return hit.position;

        // 2) Рядом с центром площадки (на случай, если вокруг здания нет NavMesh).
        if (NavMesh.SamplePosition(center, out hit, 8f, NavMesh.AllAreas))
            return hit.position;

        // 3) Последний шанс — остаёмся рядом с самим строителем.
        if (NavMesh.SamplePosition(fromPosition, out hit, 4f, NavMesh.AllAreas))
            return hit.position;

        return candidate;
    }

    private void RemoveInvalidBuilders()
    {
        for (int i = builders.Count - 1; i >= 0; i--)
        {
            Builder builder = builders[i];

            if (builder == null || !builder.IsWorkingOn(this))
                builders.RemoveAt(i);
        }
    }

    // ------------------------------------------------------------------ Окончание

    private void FinishConstruction()
    {
        if (completed)
            return;

        completed = true;

        List<Builder> finishedBuilders = new List<Builder>(builders);
        builders.Clear();

        for (int i = 0; i < finishedBuilders.Count; i++)
        {
            if (finishedBuilders[i] != null)
                finishedBuilders[i].OnSiteCompleted(this);
        }

        if (building != null)
        {
            building.transform.SetPositionAndRotation(transform.position, transform.rotation);
            building.SetActive(true);

            Constructable constructable = building.GetComponent<Constructable>();
            if (constructable != null)
                constructable.ConstructableWasPlaced();
        }

        if(CompletionObjectId>=0)PlacementSystem.Instance?.ApplySavedBenefits(CompletionObjectId);
        Completed?.Invoke(this);

        Debug.Log($"[ConstructionSite] Стройка {name} завершена.", this);

        Destroy(gameObject);
    }

    // ------------------------------------------------------------------ Визуал

    private void CreateVisual()
    {
        var visual = new GameObject("ConstructionStages");
        visual.transform.SetParent(transform, false);
        visual.AddComponent<ConstructionStageView>().Initialize(this, building, footprintCenter, footprintSize, barHeight - .8f, visualPrefab);

        // Пока здание выключено, его собственный NavMeshObstacle не работает.
        // Поэтому перекрываем поверхность самой площадкой.
        if (blockSurface)
            CreateSurfaceObstacle();

        var placement = PlacementSystem.Instance;
        if (placement != null && placement.ConstructionProgressPrefab != null)
        {
            var progressView = Instantiate(placement.ConstructionProgressPrefab, transform, false);
            progressView.transform.localPosition = new Vector3(footprintCenter.x, barHeight, footprintCenter.z);
            progressView.Bind(this);
        }
    }

    /// <summary>
    /// NavMeshObstacle по размеру здания. Не даёт юнитам проходить сквозь
    /// стройплощадку и заставляет их обходить её по NavMesh.
    /// </summary>
    private void CreateSurfaceObstacle()
    {
        GameObject obstacleObject = new GameObject("SurfaceObstacle");
        obstacleObject.transform.SetParent(transform, false);
        obstacleObject.transform.localPosition = Vector3.zero;

        NavMeshObstacle obstacle = obstacleObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(
            Mathf.Max(1f, footprintSize.x) * 0.98f,
            4f,
            Mathf.Max(1f, footprintSize.y) * 0.98f);
        obstacle.center = new Vector3(footprintCenter.x, 2f, footprintCenter.z);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
    }

    /// <summary>
    /// Простой каркас из кубов: фундамент + четыре угловые стойки.
    /// Нужен, чтобы стройку было видно даже без отдельного префаба.
    /// </summary>
    private void BuildDefaultVisual()
    {
        float footprintFactor = building != null && building.GetComponent<WallSegment>() != null ? 1f : 0.95f;
        float width = footprintSize.x * footprintFactor;
        float depth = footprintSize.y * footprintFactor;
        Vector3 center = new Vector3(footprintCenter.x, 0f, footprintCenter.z);

        CreatePart("Foundation", PrimitiveType.Cube,
            center + new Vector3(0f, 0.2f, 0f),
            new Vector3(width, 0.4f, depth),
            new Color(0.55f, 0.47f, 0.33f));

        float postHeight = 1.8f;
        float postX = Mathf.Max(0.35f, width * 0.5f - 0.15f);
        float postZ = Mathf.Max(0.35f, depth * 0.5f - 0.15f);

        Vector3[] corners =
        {
            center + new Vector3(-postX, postHeight * 0.5f + 0.4f, -postZ),
            center + new Vector3( postX, postHeight * 0.5f + 0.4f, -postZ),
            center + new Vector3(-postX, postHeight * 0.5f + 0.4f,  postZ),
            center + new Vector3( postX, postHeight * 0.5f + 0.4f,  postZ)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            CreatePart($"Post_{i}", PrimitiveType.Cube,
                corners[i],
                new Vector3(0.12f, postHeight, 0.12f),
                new Color(0.42f, 0.29f, 0.15f));
        }
    }

    private GameObject CreatePart(string partName, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            SetMaterialColor(renderer, color);

            // Каркас не должен отбрасывать тяжёлые тени на всю карту.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        return part;
    }

    /// <summary>
    /// Центр и размер меша здания в локальных координатах относительно pivot.
    /// Учитывает смещение pivot и поворот здания.
    /// </summary>
    private void MeasureFootprint(GameObject target)
    {
        footprintCenter = Vector3.zero;
        footprintSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));

        if (target == null)
            return;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        bool hasBounds = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        foreach (Renderer renderer in renderers)
        {
            // Частицы/трейлы/линии могут быть прикреплены к зданию и сильно
            // раздувать границы — для площадки важны только меши.
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                continue;

            Bounds bounds = renderer.bounds;
            Vector3 extents = bounds.extents;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + new Vector3(
                    ((i & 1) == 0 ? -1f : 1f) * extents.x,
                    ((i & 2) == 0 ? -1f : 1f) * extents.y,
                    ((i & 4) == 0 ? -1f : 1f) * extents.z);

                Vector3 local = target.transform.InverseTransformPoint(corner);

                if (!hasBounds)
                {
                    min = local;
                    max = local;
                    hasBounds = true;
                }
                else
                {
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
        }

        if (!hasBounds)
            return;

        footprintCenter = new Vector3(
            (min.x + max.x) * 0.5f,
            0f,
            (min.z + max.z) * 0.5f);

        footprintSize = new Vector2(
            Mathf.Max(0.5f, max.x - min.x),
            Mathf.Max(0.5f, max.z - min.z));
    }

    /// <summary>
    /// Высота здания от его основания до верхней точки мешей.
    /// Считается по всем Renderer'ам, включая выключенные дочерние объекты.
    /// </summary>
    private static float MeasureHeight(GameObject target)
    {
        if (target == null)
            return 2f;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return 2f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float height = bounds.max.y - target.transform.position.y;
        return Mathf.Clamp(height, 1.2f, 20f);
    }

    /// <summary>
    /// Красит материал с учётом URP (там основной цвет — _BaseColor)
    /// и встроенного рендера (_Color).
    /// </summary>
    private static void SetMaterialColor(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        Material material = renderer.material;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }
}
