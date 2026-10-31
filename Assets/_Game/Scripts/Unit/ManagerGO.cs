using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public class ManagerGO : MonoBehaviour
{
    public static ManagerGO Instance { get; set; }

    public List<GameObject> allUnitsList = new List<GameObject>();
    public List<GameObject> unitsSelected = new List<GameObject>();
    public LayerMask clickable;
    public LayerMask Ground;
    public GameObject groundMarker;
    public LayerMask attackable;
    public bool attackCursorVisible;
    public GOScript GOScript;

    [Header("Движение группы")]
    [Tooltip("Расстояние между точками, в которые встают юниты при движении группой")]
    public float groupSpacing = 1.7f;

    [Tooltip("Насколько далеко искать ближайшую точку NavMesh для позиции юнита")]
    public float navMeshSnapRadius = 2f;

    private Camera cam;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    void Start()
    {
        cam = Camera.main;
    }

    private void Update()
    {
        // Завершаем приказы на перемещение у всех юнитов (и выделенных, и нет),
        // чтобы по прибытии сбрасывался путь и маркер цели исчезал.
        UpdateMoveCommands();

        RaycastHit hit;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (groundMarker != null && groundMarker.activeSelf)
        {
            bool anyUnitStillMoving = false;
            foreach (GameObject unit in unitsSelected)
            {
                if (unit == null) continue;
                GOScript move = unit.GetComponent<GOScript>();
                if (move != null && move.isCommandToMove)
                {
                    anyUnitStillMoving = true;
                    break;
                }
            }
            // Если абсолютно все выделенные юниты остановились — выключаем маркер
            if (!anyUnitStillMoving)
            {
                groundMarker.SetActive(false);
            }
        }


        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // Пока включён режим построения строя и есть выделенные юниты,
        // ЛКМ/ПКМ обрабатывает SoldiersFormation. Если выделения нет — даём
        // сначала выбрать юнитов, иначе из режима было бы не выйти.
        if (SoldiersFormation.IsPlacing && unitsSelected.Count > 0)
        {
            CursorSelector();
            return;
        }

        // ЛКМ — выбор юнитов
        if (Input.GetMouseButtonDown(0) && !pointerOverUI)
        {
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickable))
            {
                if (Input.GetKey(KeyCode.LeftShift))
                    MultiSelect(hit.collider.gameObject);
                else
                    SelectByClicking(hit.collider.gameObject);
            }
            else
            {
                if (!Input.GetKey(KeyCode.LeftShift))
                    DeselectAll();
            }
        }

        // ПКМ — атака или движение
        if (Input.GetMouseButtonDown(1) && unitsSelected.Count > 0)
        {
            // Сначала проверяем врагов (но НЕ свои склады и ресурсные объекты — приказ по ним = обычное движение)
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, attackable) && !IsResourceTarget(hit.collider.gameObject))
            {
                attackCursorVisible = true;

                Transform target = hit.collider.transform;
                foreach (GameObject unit in unitsSelected)
                {
                    // ИСПРАВЛЕНИЕ: Проверяем, не уничтожен ли сам выделенный юнит
                    if (unit == null) continue;

                    AttackController ac = unit.GetComponent<AttackController>();
                    if (ac != null)
                        ac.targetToAttack = target;

                    GOScript move = unit.GetComponent<GOScript>();
                    if (move != null)
                        move.StopMoving();
                }
            }
            // Потом проверяем землю
            else if (Physics.Raycast(ray, out hit, Mathf.Infinity, Ground))
            {
                attackCursorVisible = false;

                if (groundMarker != null)
                {
                    groundMarker.transform.position = hit.point;
                    groundMarker.SetActive(false);
                    groundMarker.SetActive(true);
                }

                MoveSelectedUnitsTo(hit.point, hit);
            }
            else
            {
                attackCursorVisible = false;
            }
        }
            CursorSelector();
    }

    /// <summary>
    /// Проверяет у всех юнитов, дошёл ли каждый до своей точки приказа.
    /// Нужно, чтобы по прибытии сбрасывался NavMesh-путь и исчезал маркер,
    /// даже если юнит уже снят с выделения (его GOScript выключен).
    /// </summary>
    private void UpdateMoveCommands()
    {
        foreach (GameObject unit in allUnitsList)
        {
            if (unit == null) continue;

            GOScript move = unit.GetComponent<GOScript>();
            if (move != null)
                move.UpdateMoveCommand();
        }
    }

    /// <summary>
    /// Двигает выделенных юнитов к клику, но каждому выдаёт отдельную точку
    /// в сетке вокруг маркера. Если вести всех в одну точку, NavMeshAgent'ы
    /// начинают толкаться и не могут дойти.
    /// </summary>
    private void MoveSelectedUnitsTo(Vector3 point, RaycastHit groundHit)
    {
        List<GameObject> units = new List<GameObject>();

        foreach (GameObject unit in unitsSelected)
        {
            if (unit != null)
                units.Add(unit);
        }

        if (units.Count == 0)
            return;

        Vector3 groupCenter = Vector3.zero;
        foreach (GameObject unit in units)
            groupCenter += unit.transform.position;
        groupCenter /= units.Count;

        List<Vector3> destinations = BuildGroupDestinations(point, units.Count, groupCenter);

        if (destinations.Count == 0)
            return;

        AssignNearestDestinations(units, destinations, point, groundHit);
    }

    private struct UnitDestinationPair
    {
        public int UnitIndex;
        public int DestinationIndex;
        public float SqrDistance;
    }

    /// <summary>
    /// Каждому юниту — ближайшая точка назначения. Пары «юнит-точка»
    /// сортируются по дистанции, поэтому порядок выделения не влияет:
    /// никто не побежит через всю группу.
    /// </summary>
    private void AssignNearestDestinations(
        List<GameObject> units,
        List<Vector3> destinations,
        Vector3 fallback,
        RaycastHit groundHit)
    {
        List<UnitDestinationPair> pairs = new List<UnitDestinationPair>();

        for (int u = 0; u < units.Count; u++)
        {
            Vector3 unitPosition = units[u].transform.position;

            for (int d = 0; d < destinations.Count; d++)
            {
                pairs.Add(new UnitDestinationPair
                {
                    UnitIndex = u,
                    DestinationIndex = d,
                    SqrDistance = (destinations[d] - unitPosition).sqrMagnitude
                });
            }
        }

        if (pairs.Count == 0)
            return;

        pairs.Sort((a, b) => a.SqrDistance.CompareTo(b.SqrDistance));

        bool[] unitAssigned = new bool[units.Count];
        bool[] destinationAssigned = new bool[destinations.Count];

        foreach (UnitDestinationPair pair in pairs)
        {
            if (unitAssigned[pair.UnitIndex] || destinationAssigned[pair.DestinationIndex])
                continue;

            unitAssigned[pair.UnitIndex] = true;
            destinationAssigned[pair.DestinationIndex] = true;

            ApplyGroupMove(units[pair.UnitIndex], destinations[pair.DestinationIndex], groundHit);
        }

        // Юнитов больше, чем точек (например, из-за притягивания к NavMesh)
        for (int u = 0; u < units.Count; u++)
        {
            if (unitAssigned[u])
                continue;

            ApplyGroupMove(units[u], fallback, groundHit);
        }
    }

    private static void ApplyGroupMove(GameObject unit, Vector3 destination, RaycastHit groundHit)
    {
        GOScript move = unit.GetComponent<GOScript>();
        if (move != null)
            move.MoveTo(destination);

        AttackController ac = unit.GetComponent<AttackController>();
        if (ac != null)
            ac.targetToAttack = null;

        // Линия-указатель от каждого юнита к точке приказа (как было раньше)
        DirectionIndicator direction = unit.GetComponent<DirectionIndicator>();
        if (direction != null)
            direction.DrawLine(groundHit);
    }

    /// <summary>
    /// Строит сетку точек вокруг клика: count позиций с шагом groupSpacing.
    /// Точки притягиваются к NavMesh, чтобы юниты не пытались идти в стену.
    /// </summary>
    private List<Vector3> BuildGroupDestinations(Vector3 center, int count, Vector3 groupCenter)
    {
        float spacing = Mathf.Max(0.6f, groupSpacing);

        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt((float)count / columns);

        // Ряды сетки ставим перпендикулярно направлению подхода группы:
        // тогда передний ряд достаётся ближайшим юнитам и никому не нужно
        // продираться сквозь уже вставших товарищей.
        Vector3 approach = groupCenter - center;
        approach.y = 0f;

        if (approach.sqrMagnitude < 0.0001f)
            approach = Vector3.forward;
        else
            approach.Normalize();

        Vector3 side = new Vector3(approach.z, 0f, -approach.x);

        List<Vector3> result = new List<Vector3>(count);
        int index = 0;

        for (int row = 0; row < rows; row++)
        {
            int inRow = Mathf.Min(columns, count - index);
            float rowOffset = (inRow - 1) * 0.5f;
            float z = (row - (rows - 1) * 0.5f) * spacing;

            for (int col = 0; col < inRow; col++)
            {
                float x = (col - rowOffset) * spacing;
                Vector3 candidate = center + side * x + approach * z;

                NavMeshHit navHit;
                if (NavMesh.SamplePosition(candidate, out navHit, navMeshSnapRadius, NavMesh.AllAreas))
                    result.Add(navHit.position);
                else if (NavMesh.SamplePosition(center, out navHit, navMeshSnapRadius, NavMesh.AllAreas))
                    result.Add(navHit.position);
                else
                    result.Add(candidate);

                index++;
            }
        }

        return result;
    }

    private void CursorSelector()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickable))
        {
            CursorManager.Instance.SetMarkerType(CursorManager.CursorType.Selectable);
        }
        else if (Physics.Raycast(ray, out hit, Mathf.Infinity, attackable) && unitsSelected.Count > 0 && AtleastOneOffensiveUnit(unitsSelected))
        {
            CursorManager.Instance.SetMarkerType(CursorManager.CursorType.Attackable);
        }
        else if (Physics.Raycast(ray, out hit, Mathf.Infinity, Ground) && unitsSelected.Count > 0 )
        {
            CursorManager.Instance.SetMarkerType(CursorManager.CursorType.Walkable);
        }
        else
        {
            CursorManager.Instance.SetMarkerType(CursorManager.CursorType.None);
        }
        
    }

    private bool AtleastOneOffensiveUnit(List<GameObject> unitSelected)
    {
        foreach (GameObject unit in unitSelected)
        {
            // ИСПРАВЛЕНИЕ: Безопасная проверка
            if (unit != null && unit.GetComponent<AttackController>() != null)
            {
                return true;
            }
        }
        return false;
    }

    // Наши ресурсные объекты (жила, склады) — не боевые цели: ПКМ по ним = команда движения
    private bool IsResourceTarget(GameObject obj)
    {
        if (obj == null) return false;
        return obj.CompareTag("Sawmill") || obj.CompareTag("Warehouse")
               || obj.CompareTag("FishHouse") || obj.CompareTag("StoneHouse")
               || obj.CompareTag("StoneQuarry");
    }

    private bool IsBuilding(GameObject obj)
    {
        return obj != null &&
               (obj.GetComponentInParent<UiSelectBuilding>() != null ||
                obj.GetComponentInChildren<UiSelectBuilding>() != null);
    }

    private void SelectByClicking(GameObject unit)
    {
        DeselectAll();
        if (unit == null || IsBuilding(unit)) return; // Защита
        unitsSelected.Add(unit);
        TriggerSelectionIndicator(unit, true);
        EnableUnitMovement(unit, true);
    }

    private void MultiSelect(GameObject unit)
    {
        if (unit == null || IsBuilding(unit)) return; // Защита
        if (!unitsSelected.Contains(unit))
        {
            unitsSelected.Add(unit);
            TriggerSelectionIndicator(unit, true);
            EnableUnitMovement(unit, true);
        }
        else
        {
            EnableUnitMovement(unit, false);
            TriggerSelectionIndicator(unit, false);
            unitsSelected.Remove(unit);
        }
    }

    public void DeselectAll()
    {
        foreach (var unit in unitsSelected)
        {
            // ИСПРАВЛЕНИЕ: Не пытаемся отключить скрипты у уничтоженных объектов
            if (unit == null) continue;

            EnableUnitMovement(unit, false);
            TriggerSelectionIndicator(unit, false);
        }

        if (groundMarker != null)
            groundMarker.SetActive(false);
        unitsSelected.Clear();
    }

    private void EnableUnitMovement(GameObject unit, bool shouldMove)
    {
        if (unit == null) return; // Защита
        var script = unit.GetComponent<GOScript>();
        if (script != null)
            script.enabled = shouldMove;
    }

    private void TriggerSelectionIndicator(GameObject unit, bool IsVisible)
    {
        if (unit == null) return; // Защита

        // Не у всех юнитов есть дочерний объект Indicator — без проверки
        // на null выделение падало с NullReferenceException.
        Transform indicator = unit.transform.Find("Indicator");
        if (indicator != null)
            indicator.gameObject.SetActive(IsVisible);
    }

    public void DragSelect(GameObject unit)
    {
        if (unit == null || IsBuilding(unit)) return; // Защита
        if (!unitsSelected.Contains(unit))
        {
            unitsSelected.Add(unit);
            TriggerSelectionIndicator(unit, true);
            EnableUnitMovement(unit, true);
        }
    }
}
