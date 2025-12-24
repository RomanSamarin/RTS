using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

public class Barracks : MonoBehaviour
{
    [Header("Unit")]
    public GameObject unitPrefab;

    [Header("Cost per Unit")]
    public int woodCost = 10;
    public int stoneCost = 5;
    public int wheatCost = 0;

    [Header("Training")]
    public float trainingTime = 3f; // время тренировки в секундах
    public float spacing = 1.5f;    // расстояние между юнитами при спавне
    public int unitsPerRow = 5;     // количество юнитов в ряду перед переходом на следующую строку

    private bool isTraining = false;
    private Transform spawnPoint;

    // Общий список всех юнитов, созданных всеми казармами
    private static List<GameObject> allUnits = new List<GameObject>();

    private void Awake()
    {
        // Создаем spawnPoint перед казармой
        spawnPoint = new GameObject("SpawnPoint").transform;
        spawnPoint.SetParent(transform);

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            float offset = col.bounds.extents.z + 1f;
            spawnPoint.position = transform.position + transform.forward * offset;
        }
        else
        {
            spawnPoint.localPosition = Vector3.forward * 2f;
        }
    }

    private void OnMouseDown()
    {
        // Открываем меню здания через BuildingUIManager
        if (BuildingUIManager.Instance != null)
            BuildingUIManager.Instance.ShowMenuForBuilding(this);
    }

    // Вызов из UI-кнопки или клавиши
    public void TrainUnit()
    {
        if (!gameObject.activeInHierarchy || isTraining)
            return;

        // Проверка ресурсов
        if (!ResourcesManager.Instance.HasEnough(woodCost, stoneCost, wheatCost))
        {
            Debug.Log("Недостаточно ресурсов для тренировки юнита!");
            return;
        }

        // Списание ресурсов
        ResourcesManager.Instance.Spend(woodCost, stoneCost, wheatCost);

        StartCoroutine(TrainCoroutine());
    }

    private IEnumerator TrainCoroutine()
    {
        isTraining = true;

        yield return new WaitForSeconds(trainingTime);

        // Вычисляем позицию нового юнита в сетке
        int index = allUnits.Count;
        int row = index / unitsPerRow;
        int col = index % unitsPerRow;

        Vector3 offset = new Vector3(col * spacing, 0, row * spacing);
        Vector3 pos = spawnPoint.position + offset;
        pos.y = spawnPoint.position.y;

        GameObject unit = Instantiate(unitPrefab, pos, spawnPoint.rotation);
        allUnits.Add(unit);

        isTraining = false;
    }
}