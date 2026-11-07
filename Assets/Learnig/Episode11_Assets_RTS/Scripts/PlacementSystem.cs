using System;
using System.Collections;
using System.Collections.Generic;
using _Game.Scripts.UI;
using UnityEngine;

public class PlacementSystem : MonoBehaviour
{
     public static PlacementSystem Instance {get; set;}

    // База данных, назначенная в инспекторе (нужна другим скриптам, например BuyBuilding)
    public ObjectsDatabseSO Database => database;
    public void CancelPlacement() { StopPlacement(); }
    public GridData SavedFurnitureData=>furnitureData;
    public void ApplySavedBenefits(int id)
    {if(database!=null)foreach(var data in database.objectsData)if(data.ID==id){ApplyBenefits(data);break;}}
    public void RestoreSavedPlacementHooks()
    {furnitureData?.RestoreSavedDestructionHooks(objectPlacer);floorData?.RestoreSavedDestructionHooks(objectPlacer);}

    // true, пока игрок выбирает место для здания или сносит его
    public bool IsPlacing => buildingState != null;
    private int placementEndedFrame = -1;
    public bool BlocksUnitInput => IsPlacing || placementEndedFrame == Time.frameCount;

    [SerializeField] private InputManager inputManager;
    [SerializeField] private Grid grid;

    [SerializeField] private ObjectsDatabseSO database;

    [SerializeField] private GridData floorData, furnitureData; // floor things like roads, furniture change to "buildings"

    [SerializeField] private PreviewSystem previewSystem;

    private Vector3Int lastDetectedPosition = Vector3Int.zero;

    [SerializeField] private ObjectPlacer objectPlacer;

    [Header("Стройка зданий")]
    [Tooltip("Здание появляется не сразу, а как стройплощадка. " +
             "Строители (Builder) подходят и достраивают его")]
    public bool useConstructionSites = true;

    [Tooltip("Сколько секунд строит один строитель (если для ID не задано своё время)")]
    public float defaultBuildTime = 8f;

    [Tooltip("Необязательно: префаб каркаса/фундамента стройки. " +
             "Если пусто — каркас собирается кодом из простых фигур")]
    public GameObject constructionSitePrefab;

    [Tooltip("Своё время стройки для конкретных зданий (по ID из базы)")]
    public List<BuildTimeSetting> buildTimes = new List<BuildTimeSetting>();

    [System.Serializable]
    public class BuildTimeSetting
    {
        [Tooltip("ID здания в Objects Database")]
        public int id;

        [Tooltip("Секунд на постройку одним строителем")]
        public float seconds = 8f;
    }

    int selectedID;

    IBuildingState buildingState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("PlacementSystem: на сцене больше одного экземпляра — лишний отключён.");
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void Start()
    {

        floorData = new();
        furnitureData = new();
    }

    public void StartPlacement(int ID)
    {
        // Обычные кнопки BuyBuilding тоже должны включать рисование стены.
        var requested = database != null ? database.objectsData.Find(item => item.ID == ID) : null;
        if (requested != null && requested.Prefab != null && requested.Prefab.GetComponent<WallGate>() != null)
        { StartGatePlacement(ID); return; }
        if (requested != null && requested.Prefab != null && requested.Prefab.GetComponent<WallSegment>() != null)
        { StartWallPlacement(ID); return; }
        // Перед режимом строительства закрываем меню выбранного здания.
        if (BuildingSelectionManager.Instance != null)
            BuildingSelectionManager.Instance.DeselectCurrent();

        Debug.Log("Should Start Placement");

        selectedID = ID;

        Debug.Log("Placement ID: " + ID);

        Debug.Log("[PlacementSystem] R — повернуть здание на 90°, Shift+R — против часовой.");

        StopPlacement();

        buildingState = new PlacementState(ID, grid, previewSystem, database, floorData, furnitureData, objectPlacer);
        inputManager.SuppressWorldClickThisFrame();

        inputManager.OnClicked += PlaceStructure;
        inputManager.OnExit += StopPlacement;
    }

    // Кнопка UI передаёт ID секции стены из той же базы, что и здания.
    public void StartGatePlacement() { StartGatePlacement(68); }
    public void StartGatePlacement(int ID)
    {
        StopPlacement();
        var data = database != null ? database.objectsData.Find(item => item.ID == ID) : null;
        if (data == null || data.Prefab == null || data.Prefab.GetComponent<WallGate>() == null || data.Size != Vector2Int.one
            || grid == null || furnitureData == null || objectPlacer == null || inputManager == null)
        { Debug.LogWarning("[Ворота] Нужна запись базы 1×1 с префабом WallGate и настроенный PlacementSystem.", this); return; }
        BuildingSelectionManager.Instance?.DeselectCurrent();
        buildingState = new GatePlacementState(data, grid, furnitureData, objectPlacer, GetBuildTime(data), constructionSitePrefab);
        inputManager.SuppressWorldClickThisFrame();
        inputManager.OnClicked += PlaceStructure; inputManager.OnExit += StopPlacement;
        Debug.Log("[Ворота] Выберите достроенную секцию стены. Escape/ПКМ — отмена.", this);
    }

    public void StartWallPlacement(int ID)
    {
        StopPlacement();
        var data = database != null ? database.objectsData.Find(item => item.ID == ID) : null;
        if (data == null || data.Prefab == null || data.Prefab.GetComponent<WallSegment>() == null
            || data.Size != Vector2Int.one || PlacementState.IsFloorID(ID))
        { Debug.LogWarning("Для стены нужна запись базы размером 1×1 с WallSegment на префабе.", this); return; }
        if (grid == null || inputManager == null || objectPlacer == null || furnitureData == null)
        { Debug.LogWarning("PlacementSystem ещё не настроен или не запущен.", this); return; }
        BuildingSelectionManager.Instance?.DeselectCurrent();
        selectedID = ID;
        buildingState = new WallPlacementState(data, grid, furnitureData, objectPlacer, inputManager,
            GetBuildTime(data), constructionSitePrefab);
        Debug.Log("[Стены] Режим включён. Первый клик по земле — начало, второй — конец участка.", this);
        inputManager.SuppressWorldClickThisFrame();
        inputManager.OnClicked += PlaceStructure;
        inputManager.OnExit += StopPlacement;
    }

    public void StartRemoving()
    {
        // Перед режимом сноса закрываем меню выбранного здания.
        if (BuildingSelectionManager.Instance != null)
            BuildingSelectionManager.Instance.DeselectCurrent();

        StopPlacement();

        buildingState = new RemovingState(grid, previewSystem, floorData, furnitureData, objectPlacer);
        inputManager.SuppressWorldClickThisFrame();

        inputManager.OnClicked += PlaceStructure;
        inputManager.OnExit += StopPlacement;
    }

    private void PlaceStructure()
    {
        if (buildingState == null || inputManager.IsPointerOverUI()) return;

        Vector3 mousePosition = inputManager.GetSelectedMapPosition();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        if (buildingState is WallPlacementState walls)
        {
            if (inputManager.TryGetSelectedMapPosition(out mousePosition))
                walls.OnAction(grid.WorldToCell(mousePosition));
            else Debug.LogWarning("[Стены] Курсор не попал в землю. Проверьте Scene Camera, коллайдер земли и Placement Layer Mask у InputManager.", this);
            return;
        }
        if (buildingState is GatePlacementState gates)
        {
            if (inputManager.TryGetSelectedMapPosition(out mousePosition) && gates.OnAction(grid.WorldToCell(mousePosition))) StopPlacement();
            return;
        }

        ObjectData ob = database.GetObjectByID(selectedID);

        // Режим удаления: ресурсы не проверяем и не списываем
        if (!(buildingState is PlacementState))
        {
            buildingState.OnAction(gridPosition);
            StopPlacement();
            return;
        }

        // Страховка: ресурсы могли потратить, пока игрок выбирал место
        BuildCost.Get(ob, out int wood, out int stone, out int wheat, out int fish);

        if (!BuildCost.CanAfford(ob))
        {
            Debug.LogWarning($"[PlacementSystem] Недостаточно ресурсов для постройки \"{ob.Name}\" — размещение отменено.");
            StopPlacement();
            return;
        }

        bool physicalConstruction = useConstructionSites && !PlacementState.IsFloorID(ob.ID);
        ConstructionMaterials materials = null;
        if (physicalConstruction)
        {
            materials = new GameObject("ConstructionMaterials").AddComponent<ConstructionMaterials>();
            if (!materials.Initialize(BuildCost.GetMaterials(ob)))
            {
                Destroy(materials.gameObject);
                StopPlacement();
                return;
            }
        }
        bool placed;
        try { placed = buildingState.OnAction(gridPosition); }
        catch
        {
            if (materials != null) { materials.ReleaseAll(); Destroy(materials.gameObject); }
            throw;
        }

        if (placed)
        {
            // ---- Списываем ресурсы только если здание реально поставлено ---- //
            if (!physicalConstruction) SpendResources(ob, wood, stone, wheat, fish);

            // ---- Ставим стройплощадку вместо готового здания ---- //
            PlacementState placement = buildingState as PlacementState;
            GameObject building = placement != null ? placement.LastPlacedObject : null;

            if (useConstructionSites && building != null && !PlacementState.IsFloorID(ob.ID))
            {
                float buildTime = GetBuildTime(ob);
                Vector2Int siteSize = placement != null ? placement.PlacedSize : ob.Size;
                ConstructionSite site = ConstructionSite.Begin(building, buildTime, siteSize, constructionSitePrefab);

                if (site != null)
                {
                    site.Materials = materials;
                    materials.transform.SetParent(site.transform, false);
                    // Бонусы (например, жильё) начисляем только после окончания стройки.
                    site.CompletionObjectId=ob.ID;
                    Debug.Log($"[PlacementSystem] Начата стройка \"{ob.Name}\": {buildTime:0.#} с одним строителем. " +
                              $"Строителей можно назначить: до {site.MaxBuilders}.");

                    if (Builder.ActiveCount == 0)
                    {
                        Debug.LogWarning("[PlacementSystem] На сцене нет строителей (Builder). " +
                                         "Поставь рабочему роль Builder или добавь компонент Builder — " +
                                         "иначе стройка не начнётся.");
                    }
                }
                else
                {
                    materials.ReleaseAll();
                    Destroy(materials.gameObject);
                    Destroy(building);
                }
            }
            else
            {
                ApplyBenefits(ob);

                if (building != null)
                {
                    Constructable constructable = building.GetComponent<Constructable>();
                    if (constructable != null)
                        constructable.ConstructableWasPlaced();
                }
            }
        }
        else
        {
            if (materials != null) { materials.ReleaseAll(); Destroy(materials.gameObject); }
            Debug.Log("[PlacementSystem] Клетка занята — здание не построено, ресурсы не списаны.");
        }

        // ---- Stop the placement after every build ---- //
        StopPlacement();
    }

    private void SpendResources(ObjectData ob, int wood, int stone, int wheat, int fish)
    {
        if (ResourcesManager.Instance == null)
        {
            Debug.LogError("[PlacementSystem] ResourcesManager не найден — ресурсы не списаны!");
            return;
        }

        ResourcesManager.Instance.Spend(wood, stone, wheat, fish);

        Debug.Log($"[PlacementSystem] Размещено \"{ob.Name}\" (ID = {ob.ID}). Списано: Wood = {wood}, Stone = {stone}, Wheat = {wheat}, Fish = {fish}");
    }

    /// <summary>Время стройки для конкретного здания (по ID) или общее.</summary>
    private float GetBuildTime(ObjectData ob)
    {
        if (ob != null && buildTimes != null)
        {
            foreach (BuildTimeSetting setting in buildTimes)
            {
                if (setting != null && setting.id == ob.ID && setting.seconds > 0f)
                    return setting.seconds;
            }
        }

        return defaultBuildTime > 0f ? Mathf.Max(0.5f, defaultBuildTime) : 8f;
    }

    /// <summary>Начисляет бонусы здания (жильё и т.п.).</summary>
    private void ApplyBenefits(ObjectData ob)
    {
        if (ob == null || ob.benefits == null)
            return;

        foreach (BuildBenefits bf in ob.benefits)
            CalculateAndAddBenefit(bf);
    }

    private void CalculateAndAddBenefit(BuildBenefits bf)
    {
        switch (bf.benefitType)
        {
            case BuildBenefits.BenefitType.Housing:
                if (UnitManager.Instance == null)
                {
                    Debug.LogWarning("[PlacementSystem] UnitManager не найден — жильё не добавлено.");
                    break;
                }

                UnitManager.Instance.UnitMax += bf.benefitAmount;
                Debug.Log($"[PlacementSystem] Жильё +{bf.benefitAmount}. Лимит юнитов: {UnitManager.Instance.UnitMax}");
                break;
        }
    }

    private void StopPlacement()
    {
        if (buildingState == null)
            return;

        buildingState.EndState();
        placementEndedFrame = Time.frameCount;

        inputManager.OnClicked -= PlaceStructure;
        inputManager.OnExit -= StopPlacement;

        lastDetectedPosition = Vector3Int.zero;

        buildingState = null;
    }

    private void Update()
    {
        // We return because we did not selected an item to place (not in placement mode)
        // So there is no need to show cell indicator
        if (buildingState == null)
            return;

        if (buildingState is GatePlacementState gates)
        {
            if (Input.GetMouseButtonDown(1)) { StopPlacement(); return; }
            if (!inputManager.IsPointerOverUI() && inputManager.TryGetSelectedMapPosition(out var point))
                gates.UpdateState(grid.WorldToCell(point));
            else gates.HidePreview();
            return;
        }
        if (buildingState is WallPlacementState walls)
        {
            if (Input.GetMouseButtonDown(1)) { StopPlacement(); return; }
            if (Input.GetKeyDown(KeyCode.R))
                walls.Rotate(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
            if (!inputManager.IsPointerOverUI() && inputManager.TryGetSelectedMapPosition(out var point))
                walls.UpdateState(grid.WorldToCell(point));
            else walls.HidePreview();
            return;
        }

        bool rotationChanged = HandleRotationInput();

        Vector3 mousePosition = inputManager.GetSelectedMapPosition();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        if (rotationChanged || lastDetectedPosition != gridPosition)
        {
            buildingState.UpdateState(gridPosition);
            lastDetectedPosition = gridPosition;
        }

    }

    // R — повернуть на 90° по часовой, Shift+R — против часовой.
    // Возвращает true, если поворот изменился.
    private bool HandleRotationInput()
    {
        if (!(buildingState is PlacementState placementState))
            return false;

        if (!Input.GetKeyDown(KeyCode.R))
            return false;

        bool counterClockwise = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        placementState.Rotate(counterClockwise ? -1 : 1);

        Debug.Log($"[PlacementSystem] Поворот здания: {Mathf.RoundToInt(placementState.Rotation.eulerAngles.y)}°");
        return true;
    }

    private void OnDisable() { StopPlacement(); }
}
