using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridData
{
    Dictionary<Vector3Int, PlacementData> placedObjects = new();

    public void AddObjectAt(Vector3Int gridPosition, Vector2Int objectSize, int Id, int placedObjectIndex)
    {
        List<Vector3Int> positionToOccuply = CalculatePositions(gridPosition, objectSize);
        PlacementData data = new PlacementData(positionToOccuply, Id, placedObjectIndex);
        foreach (var pos in positionToOccuply)
        {
            if (placedObjects.ContainsKey(pos))
            {
                throw new Exception("Dictionary already contains this cell position");
            }
            placedObjects[pos] = data;
        }
    }

    private List<Vector3Int> CalculatePositions(Vector3Int gridPosition, Vector2Int objectSize)
    {
        List<Vector3Int> returnVal1 = new();
        for (int x = 0; x < objectSize.x; x++)
        {
            for (int y = 0; y < objectSize.y; y++)
            {
                returnVal1.Add(gridPosition + new Vector3Int(x,0,y));
            }
        }
        return returnVal1;
    }

    public bool CanPlaceObjectAt(Vector3Int gridPosition, Vector2Int objectSize)
    {
        List<Vector3Int> positionToOccupy = CalculatePositions(gridPosition, objectSize);
        foreach (var pos in positionToOccupy)
        {
            if (placedObjects.ContainsKey(pos))
            {
                return false;
            }
        }
        return true;
    }

    internal void RemoveObjectAt(Vector3Int gridPosition)
    {
        if (!placedObjects.ContainsKey(gridPosition)) return;
        foreach (var pos in placedObjects[gridPosition].occupiedPositions)
        {
            placedObjects.Remove(pos);
        }
    }

    internal int GetRepresentationIndex(Vector3Int gridPosition)
    {
        if (placedObjects.ContainsKey(gridPosition) == false)
            return -1;
        return placedObjects[gridPosition].PlacedObjectIndex;
    }
    public int GetObjectId(Vector3Int position) => placedObjects.TryGetValue(position, out var data) ? data.ID : -1;
    public void RestoreSavedDestructionHooks(ObjectPlacer placer)
    {
        if(placer==null)return;
        var indices=new HashSet<int>();
        foreach(var pair in placedObjects)
        {
            int index=pair.Value.PlacedObjectIndex;if(!indices.Add(index))continue;
            var obj=placer.GetPlacedObject(index);if(obj==null)continue;
            var health=obj.GetComponent<BuildingHealth>();if(health==null)continue;
            var cell=pair.Key;
            health.Destroyed+=_=>{if(GetRepresentationIndex(cell)==index)RemoveObjectAt(cell);};
        }
    }
}


public class PlacementData
{
    public List<Vector3Int> occupiedPositions;
    public int ID { get; private set; }
    public int PlacedObjectIndex { get; private set; }
    public static object Instance { get; internal set; }

    public PlacementData(List<Vector3Int> occupiedPositions, int iD, int placedObjectIndex)
    {
        this.occupiedPositions = occupiedPositions;
        ID = iD;
        PlacedObjectIndex = placedObjectIndex;
    }
}
