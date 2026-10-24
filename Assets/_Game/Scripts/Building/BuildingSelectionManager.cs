using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Один менеджер на сцену. По ЛКМ открывает меню выбранного здания
/// и закрывает меню предыдущего. Какое меню показывать — задаётся
/// в компоненте UiSelectBuilding на самом здании.
/// </summary>
[DisallowMultipleComponent]
public class BuildingSelectionManager : MonoBehaviour
{
    public static BuildingSelectionManager Instance { get; private set; }

    [Header("Настройки")]
    [Tooltip("Слои с коллайдерами зданий. По умолчанию — все слои. " +
             "Можно ограничить слоями Building/Kazarma.")]
    public LayerMask buildingLayers = ~0;

    [Tooltip("Клик по пустому месту или по юниту закрывает меню здания.")]
    public bool deselectOnEmptyClick = true;

    [Tooltip("Повторный клик по тому же зданию закрывает его меню.")]
    public bool toggleOnSameClick = false;

    private Camera cam;
    private UiSelectBuilding current;

    public UiSelectBuilding Current => current;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("BuildingSelectionManager: на сцене больше одного экземпляра — лишний отключён.");
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        cam = Camera.main;
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        // Клик по UI (например, по кнопкам внутри открытого меню)
        // не должен закрывать или переключать меню здания.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Не мешаем режиму размещения или сноса зданий.
        if (PlacementSystem.Instance != null && PlacementSystem.Instance.IsPlacing)
            return;

        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            Mathf.Infinity,
            buildingLayers,
            QueryTriggerInteraction.Collide);

        if (hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                UiSelectBuilding building = FindBuilding(hit.collider);
                if (building != null)
                {
                    SelectBuilding(building);
                    return;
                }

                // Ближайший объект — не здание (юнит, дерево, земля).
                // Сквозь него не смотрим, иначе можно открыть меню здания,
                // которое стоит за юнитом.
                break;
            }
        }

        if (deselectOnEmptyClick)
            DeselectCurrent();
    }

    private static UiSelectBuilding FindBuilding(Collider collider)
    {
        UiSelectBuilding building = collider.GetComponentInParent<UiSelectBuilding>();
        if (building != null) return building;

        // Если компонент висит на дочернем объекте, а коллайдер — на корне.
        return collider.GetComponentInChildren<UiSelectBuilding>();
    }

    public void SelectBuilding(UiSelectBuilding building)
    {
        if (building == null) return;

        if (current == building)
        {
            if (toggleOnSameClick)
                DeselectCurrent();
            else if (!building.IsSelected)
                building.Select(); // панель могли закрыть снаружи

            return;
        }

        DeselectCurrent();

        current = building;
        current.Select();
    }

    public void DeselectCurrent()
    {
        if (current == null)
        {
            current = null;
            return;
        }

        current.Deselect();
        current = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Менеджер не нужно вручную добавлять в сцену: он создастся сам при запуске игры.
    // Если на сцене уже есть свой экземпляр (например, с настроенными слоями),
    // автоматический не создаётся.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfMissing()
    {
        if (Instance != null) return;
        if (FindObjectOfType<BuildingSelectionManager>() != null) return;

        GameObject managerObject = new GameObject("BuildingSelectionManager");
        managerObject.AddComponent<BuildingSelectionManager>();
        DontDestroyOnLoad(managerObject);
    }
}
