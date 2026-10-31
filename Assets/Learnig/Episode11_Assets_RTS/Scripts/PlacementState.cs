using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlacementState : IBuildingState
{
    private int selectedObjectIndex = -1;
    int ID;
    Grid grid;
    PreviewSystem previewSystem;
    ObjectsDatabseSO database;
    GridData floorData;
    GridData furnitureData;
    ObjectPlacer objectPlacer;

    // Текущий поворот здания (всегда кратен 90°)
    private Quaternion rotation = Quaternion.identity;

    // Последнее реально поставленное здание (нужно PlacementSystem,
    // чтобы заменить его на строительную площадку)
    public GameObject LastPlacedObject { get; private set; }

    public PlacementState(int iD,
                          Grid grid,
                          PreviewSystem previewSystem,
                          ObjectsDatabseSO database,
                          GridData floorData,
                          GridData furnitureData,
                          ObjectPlacer objectPlacer)
    {
        ID = iD;
        this.grid = grid;
        this.previewSystem = previewSystem;
        this.database = database;
        this.floorData = floorData;
        this.furnitureData = furnitureData;
        this.objectPlacer = objectPlacer;

        selectedObjectIndex = database.objectsData.FindIndex(data => data.ID == ID);
        if (selectedObjectIndex > -1)
        {
            previewSystem.StartShowingPlacementPreview(database.objectsData[selectedObjectIndex].Prefab,
                database.objectsData[selectedObjectIndex].Size);
        }
        else
        {
            throw new System.Exception($"No object with ID {iD}");
        }
    }

    public void EndState()
    {
        previewSystem.StopShowingPreview();
    }

    // Текущий поворот здания (кратен 90°)
    public Quaternion Rotation => rotation;

    // Размер здания с учётом поворота — нужен для стройплощадки
    public Vector2Int PlacedSize => GetRotatedSize();

    // Поворачивает здание на 90° * steps (1 — по часовой, -1 — против часовой)
    public void Rotate(int steps)
    {
        float angle = rotation.eulerAngles.y + 90f * steps;
        rotation = Quaternion.Euler(0f, Mathf.Repeat(angle, 360f), 0f);
        previewSystem.RotatePreview(rotation);
    }

    // Размер здания с учётом поворота: при 90°/270° стороны меняются местами
    private Vector2Int GetRotatedSize()
    {
        Vector2Int size = database.objectsData[selectedObjectIndex].Size;
        int angle = Mathf.RoundToInt(Mathf.Repeat(rotation.eulerAngles.y, 360f));
        return angle % 180 == 90 ? new Vector2Int(size.y, size.x) : size;
    }

    public bool OnAction(Vector3Int gridPosition)
    {
        // Checking if we can place this item (position not occupied)
        bool placementValidity = CheckPlacementValidity(gridPosition, selectedObjectIndex);
        if (placementValidity == false)
        {
            return false;
        }

        int index = objectPlacer.PlaceObject(database.objectsData[selectedObjectIndex].Prefab, grid.CellToWorld(gridPosition), rotation);
        LastPlacedObject = objectPlacer.GetPlacedObject(index);

        // If this id is a floor id, then its a floor data, else its a furniture data
        GridData selectedData = GetAllFloorIDs().Contains(database.objectsData[selectedObjectIndex].ID) ? floorData : furnitureData;
       
        selectedData.AddObjectAt(gridPosition,
            GetRotatedSize(),
            database.objectsData[selectedObjectIndex].ID,
            index);

        previewSystem.UpdatePosition(grid.CellToWorld(gridPosition), false);

        return true;
    }

    // When you have more floor objects add their id here
    private static List<int> GetAllFloorIDs()
    {
        return new List<int> { 11 }; // These are all the ids of floor items - For now its only the grass
    }

    /// <summary>Пол/дорога: такие объекты ставятся сразу, без стройки.</summary>
    public static bool IsFloorID(int id)
    {
        return GetAllFloorIDs().Contains(id);
    }

    private bool CheckPlacementValidity(Vector3Int gridPosition, int selectedObjectIndex)
    {
      

        GridData selectedData = GetAllFloorIDs().Contains(database.objectsData[selectedObjectIndex].ID) ? floorData : furnitureData;

        return selectedData.CanPlaceObjectAt(gridPosition, GetRotatedSize());
    }

    public void UpdateState(Vector3Int gridPosition)
    {
        // Show the player if he can place the item
        bool placementValidity = CheckPlacementValidity(gridPosition, selectedObjectIndex);

        previewSystem.UpdatePosition(grid.CellToWorld(gridPosition), placementValidity);
    }
}
