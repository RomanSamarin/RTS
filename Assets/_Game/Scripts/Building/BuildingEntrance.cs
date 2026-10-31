using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Точка входа в здание («дверь»). Рабочие подходят к ней, чтобы сдать или
/// забрать ресурсы, вместо того чтобы сдавать их, коснувшись любой стены.
///
/// Компонент не обязателен. Достаточно добавить зданию дочерний объект
/// с именем Door / Entrance / Вход / Дверь и поставить его у входа — скрипт
/// найдёт его сам. Компонент нужен, если хочется задать точку вручную
/// (Door Point) или изменить отступ от двери.
///
/// Если двери нет вообще, рабочие ведут себя как раньше: подходят к
/// ближайшей стене склада.
/// </summary>
[DisallowMultipleComponent]
public class BuildingEntrance : MonoBehaviour
{
    [Tooltip("Точка у двери здания. Пусто — ищем дочерний объект Door / Entrance / Вход / Дверь")]
    public Transform doorPoint;

    [Tooltip("На сколько отойти от двери наружу, чтобы рабочий не влезал в стену")]
    [Min(0f)]
    public float stopOffset = 0.5f;

    // Имена дочерних объектов, которые считаются дверью (в нижнем регистре)
    private static readonly string[] DoorNames =
    {
        "door", "entrance", "вход", "дверь", "doorpoint", "entrancepoint"
    };

    private const float DefaultStopOffset = 0.5f;

    // Найденные автоматически двери (в том числе отсутствие двери — null)
    private static readonly Dictionary<GameObject, Transform> autoDoors =
        new Dictionary<GameObject, Transform>();

    private Transform resolvedDoor;
    private bool doorSearched;

    /// <summary>
    /// Мировая точка, к которой должен подойти рабочий: чуть снаружи двери,
    /// чтобы он не стоял внутри стены.
    /// </summary>
    public Vector3 WorldPoint
    {
        get
        {
            Transform point = ResolveDoor();
            if (point == null)
                return transform.position;

            return ComputeWorldPoint(transform, point, stopOffset);
        }
    }

    /// <summary>Есть ли у здания настроенная точка входа.</summary>
    public bool HasDoor => ResolveDoor() != null;

    /// <summary>
    /// Компонент входа у здания (ищется на объекте и его родителях).
    /// Возвращает null, если здание без BuildingEntrance.
    /// </summary>
    public static BuildingEntrance Get(GameObject building)
    {
        if (building == null)
            return null;

        BuildingEntrance entrance = building.GetComponent<BuildingEntrance>();
        if (entrance != null)
            return entrance;

        return building.GetComponentInParent<BuildingEntrance>();
    }

    /// <summary>
    /// Мировая точка входа. Компонент не обязателен: если его нет,
    /// дверь ищется среди дочерних объектов здания по имени.
    /// </summary>
    public static bool TryGetWorldPoint(GameObject building, out Vector3 point)
    {
        point = default;

        if (building == null)
            return false;

        BuildingEntrance entrance = Get(building);
        if (entrance != null)
        {
            if (!entrance.HasDoor)
                return false;

            point = entrance.WorldPoint;
            return true;
        }

        Transform door = FindAutoDoor(building);
        Transform searchRoot = building.transform;

        // Коллайдер-триггер может висеть на дочернем объекте, а дверь —
        // на корне здания: проверяем ещё один уровень вверх.
        if (door == null && building.transform.parent != null)
        {
            searchRoot = building.transform.parent;
            door = FindDoorInChildren(searchRoot);
        }

        if (door == null)
            return false;

        point = ComputeWorldPoint(searchRoot, door, DefaultStopOffset);
        return true;
    }

    private static Vector3 ComputeWorldPoint(Transform building, Transform door, float offset)
    {
        // Отступаем от двери наружу — от центра коллайдера здания,
        // а не от пивота (у моделей он бывает сбоку).
        Vector3 origin = building.position;
        Collider buildingCollider = building.GetComponent<Collider>();
        if (buildingCollider == null)
            buildingCollider = building.GetComponentInChildren<Collider>();
        if (buildingCollider != null)
            origin = buildingCollider.bounds.center;

        Vector3 direction = door.position - origin;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return door.position;

        return door.position + direction.normalized * offset;
    }

    private static Transform FindAutoDoor(GameObject building)
    {
        if (autoDoors.TryGetValue(building, out Transform cached))
            return cached;

        // Раз в N зданий вычищаем ссылки на уничтоженные объекты
        if (autoDoors.Count > 64)
            CleanupAutoDoors();

        Transform door = FindDoorInChildren(building.transform);
        autoDoors[building] = door;
        return door;
    }

    private static void CleanupAutoDoors()
    {
        List<GameObject> dead = null;

        foreach (KeyValuePair<GameObject, Transform> pair in autoDoors)
        {
            if (pair.Key != null)
                continue;

            if (dead == null)
                dead = new List<GameObject>();

            dead.Add(pair.Key);
        }

        if (dead == null)
            return;

        foreach (GameObject building in dead)
            autoDoors.Remove(building);
    }

    private Transform ResolveDoor()
    {
        if (doorPoint != null)
            return doorPoint;

        if (doorSearched)
            return resolvedDoor;

        doorSearched = true;
        resolvedDoor = FindDoorInChildren(transform);
        return resolvedDoor;
    }

    private static Transform FindDoorInChildren(Transform root)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child == root)
                continue;

            string childName = child.name.ToLowerInvariant();

            foreach (string doorName in DoorNames)
            {
                if (childName.Contains(doorName))
                    return child;
            }
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Transform door = ResolveDoor();

        Gizmos.color = door != null ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(WorldPoint, 0.35f);
        Gizmos.DrawLine(transform.position, WorldPoint);
    }
}
