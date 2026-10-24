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

            // ---- Add Buildable Benifits ---- //
            if (ob.benefits != null)
            {
                foreach (BuildBenefits bf in ob.benefits)
                {
                    CalculateAndAddBenefit(bf);
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

        Debug.Log($"[PlacementSystem] Построено \"{ob.Name}\" (ID = {ob.ID}). Списано: Wood = {wood}, Stone = {stone}, Wheat = {wheat}, Fish = {fish}");
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
      
        Vector3 mousePosition = inputManager.GetSelectedMapPosition();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        if (lastDetectedPosition != gridPosition)
        {
            buildingState.UpdateState(gridPosition);
            lastDetectedPosition = gridPosition;
        }

    }
}
