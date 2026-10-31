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

    // true, пока игрок выбирает место для здания или сносит его
    public bool IsPlacing => buildingState != null;

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
        // Перед режимом строительства закрываем меню выбранного здания.
        if (BuildingSelectionManager.Instance != null)
            BuildingSelectionManager.Instance.DeselectCurrent();

        Debug.Log("Should Start Placement");

        selectedID = ID;

        Debug.Log("Placement ID: " + ID);

        Debug.Log("[PlacementSystem] R — повернуть здание на 90°, Shift+R — против часовой.");

        StopPlacement();

        buildingState = new PlacementState(ID, grid, previewSystem, database, floorData, furnitureData, objectPlacer);

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

        inputManager.OnClicked += PlaceStructure;
        inputManager.OnExit += StopPlacement;
    }

    private void PlaceStructure()
    {
        if(inputManager.IsPointerOverUI()){
            Debug.Log("Pointer was over UI - Returned");
            return;
        }

        Vector3 mousePosition = inputManager.GetSelectedMapPosition();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

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

        if (ResourcesManager.Instance != null &&
            !ResourcesManager.Instance.HasEnough(wood, stone, wheat, fish))
        {
            Debug.LogWarning($"[PlacementSystem] Недостаточно ресурсов для постройки \"{ob.Name}\" — размещение отменено.");
            StopPlacement();
            return;
        }

        bool placed = buildingState.OnAction(gridPosition);

        if (placed)
        {
            // ---- Списываем ресурсы только если здание реально поставлено ---- //
            SpendResources(ob, wood, stone, wheat, fish);

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
                    // Бонусы (например, жильё) начисляем только после окончания стройки.
                    site.Completed += _ => ApplyBenefits(ob);
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
                    ApplyBenefits(ob);
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
}
