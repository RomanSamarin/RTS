using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Менеджер построек. Повесь этот компонент на любой объект в сцене —
/// в инспекторе будет виден список всех зданий (BuildingHealth).
/// Список заполняется автоматически.
/// </summary>
[DisallowMultipleComponent]
public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    private static readonly List<BuildingHealth> registry = new List<BuildingHealth>();

    [Header("Все здания")]
    [Tooltip("Заполняется автоматически: сюда попадают все BuildingHealth на сцене.")]
    public List<BuildingHealth> allBuildings = new List<BuildingHealth>();

    [Header("Настройки")]
    [Tooltip("При запуске добавить BuildingHealth зданиям, у которых его ещё нет " +
             "(например, старым префабам без этого компонента).")]
    [SerializeField] private bool addMissingHealthOnStart = true;

    /// <summary>Все здания. Доступно из любого скрипта.</summary>
    public static IReadOnlyList<BuildingHealth> AllBuildings => registry;

    /// <summary>Список зданий, который виден в инспекторе.</summary>
    public IReadOnlyList<BuildingHealth> Buildings => allBuildings;

    public static int Count => registry.Count;

    /// <summary>Здание добавлено в список.</summary>
    public static event Action<BuildingHealth> BuildingRegistered;

    /// <summary>Здание убрано из списка (уничтожено или выключено).</summary>
    public static event Action<BuildingHealth> BuildingUnregistered;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("BuildingManager: на сцене больше одного экземпляра — лишний отключён.");
            enabled = false;
            return;
        }

        Instance = this;
        SyncInspectorList();
    }

    private void Start()
    {
        if (addMissingHealthOnStart)
            FindAllBuildings();
        else
            SyncInspectorList();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Register(BuildingHealth building)
    {
        Cleanup();

        if (building == null || registry.Contains(building))
            return;

        registry.Add(building);
        Instance?.SyncInspectorList();
        BuildingRegistered?.Invoke(building);
    }

    public static void Unregister(BuildingHealth building)
    {
        if (building == null)
            return;

        if (registry.Remove(building))
        {
            Instance?.SyncInspectorList();
            BuildingUnregistered?.Invoke(building);
        }
    }

    /// <summary>
    /// Находит все здания на сцене, добавляет им BuildingHealth (если нужно)
    /// и заполняет список в инспекторе. Можно вызвать из контекстного меню компонента.
    /// </summary>
    [ContextMenu("Найти все здания на сцене")]
    public void FindAllBuildings()
    {
        EnsureHealthOnBuildings();

        BuildingHealth[] found = FindObjectsOfType<BuildingHealth>(true);

        foreach (BuildingHealth building in found)
            Register(building);

        Cleanup();
        SyncInspectorList();
    }

    /// <summary>Ближайшее к точке здание из списка (или null).</summary>
    public static BuildingHealth GetNearest(Vector3 position)
    {
        BuildingHealth nearest = null;
        float bestDistance = float.MaxValue;

        foreach (BuildingHealth building in registry)
        {
            if (building == null)
                continue;

            float distance = (building.transform.position - position).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = building;
            }
        }

        return nearest;
    }

    private static void Cleanup()
    {
        registry.RemoveAll(building => building == null);
    }

    /// <summary>Переносит актуальный список в сериализованное поле для инспектора.</summary>
    private void SyncInspectorList()
    {
        allBuildings.Clear();

        foreach (BuildingHealth building in registry)
        {
            if (building != null)
                allBuildings.Add(building);
        }
    }

    /// <summary>
    /// Добавляет BuildingHealth зданиям, у которых его нет:
    /// UiSelectBuilding и Constructable есть на всех постройках.
    /// </summary>
    private static void EnsureHealthOnBuildings()
    {
        UiSelectBuilding[] selectable =
            FindObjectsOfType<UiSelectBuilding>(true);

        foreach (UiSelectBuilding building in selectable)
        {
            if (building.GetComponent<BuildingHealth>() == null)
                building.gameObject.AddComponent<BuildingHealth>();
        }

        Constructable[] constructables =
            FindObjectsOfType<Constructable>(true);

        foreach (Constructable constructable in constructables)
        {
            if (constructable.GetComponent<BuildingHealth>() == null)
                constructable.gameObject.AddComponent<BuildingHealth>();
        }

        // Дома на сцене могут быть обычными объектами без Constructable.
        HousesScript[] houses = FindObjectsOfType<HousesScript>(true);

        foreach (HousesScript house in houses)
        {
            if (house.GetComponent<BuildingHealth>() == null)
                house.gameObject.AddComponent<BuildingHealth>();
        }
    }
}
