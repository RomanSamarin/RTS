using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPlacer : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> placedGameObjects = new();

    public int PlaceObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        // We instantiate the prefab into the cell
        GameObject newObject = Instantiate(prefab, position, rotation);

        // Constructable включается позже: либо сразу в PlacementSystem
        // (если стройка отключена / это пол), либо после окончания стройки
        // в ConstructionSite. Иначе NavMeshObstacle занял бы место заранее.

        // Storing the positions that are now occupied
        placedGameObjects.Add(newObject);

        return placedGameObjects.Count - 1;
    }

    // Здание по индексу, который вернул PlaceObject.
    public GameObject GetPlacedObject(int gameObjectIndex)
    {
        if (gameObjectIndex < 0 || gameObjectIndex >= placedGameObjects.Count)
            return null;

        return placedGameObjects[gameObjectIndex];
    }

    internal void RemoveObjectAt(int gameObjectIndex)
    {
        if(placedGameObjects.Count <= gameObjectIndex 
            || placedGameObjects[gameObjectIndex] == null)
             return;
        Destroy(placedGameObjects[gameObjectIndex]);
        placedGameObjects[gameObjectIndex] = null;
    }
}
