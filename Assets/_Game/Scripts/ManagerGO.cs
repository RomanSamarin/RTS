using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        RaycastHit hit;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // ЛКМ — выбор юнитов
        if (Input.GetMouseButtonDown(0))
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
            // Сначала проверяем врагов
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, attackable))
            {
                attackCursorVisible = true;

                Transform target = hit.collider.transform;
                foreach (GameObject unit in unitsSelected)
                {
                    AttackController ac = unit.GetComponent<AttackController>();
                    if (ac != null)
                        ac.targetToAttack = target;

                    GOScript move = unit.GetComponent<GOScript>();
                    if (move != null)
                        move.isCommandToMove = false;
                }
            }
            // Потом проверяем землю
            else if (Physics.Raycast(ray, out hit, Mathf.Infinity, Ground))
            {
                attackCursorVisible = false;

                groundMarker.transform.position = hit.point;
                groundMarker.SetActive(false);
                groundMarker.SetActive(true);

                foreach (GameObject unit in unitsSelected)
                {
                    GOScript move = unit.GetComponent<GOScript>();
                    if (move != null)
                    {
                        move.isCommandToMove = true;
                        move.agent.SetDestination(hit.point);
                    }

                    AttackController ac = unit.GetComponent<AttackController>();
                    if (ac != null)
                        ac.targetToAttack = null;
                }
            }
            else
            {
                attackCursorVisible = false;
            }
        }
    }


    private bool AtleastOneOffensiveUnit(List<GameObject> unitSelected)
    {
        foreach (GameObject unit in unitSelected)
        {
            if (unit.GetComponent<AttackController>() != null)
            {
                return true;
            }
        }
        return false;
    }

    private void SelectByClicking(GameObject unit)
    {
        DeselectAll();
        unitsSelected.Add(unit);
        TriggerSelectionIndicator(unit, true);
        EnableUnitMovement(unit, true);
    }

    private void MultiSelect(GameObject unit)
    {
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
            EnableUnitMovement(unit, false);
            TriggerSelectionIndicator(unit, false);
        }

        groundMarker.SetActive(false);
        unitsSelected.Clear();
    }

    private void EnableUnitMovement(GameObject unit, bool shouldMove)
    {
        var script = unit.GetComponent<GOScript>();
        if (script != null)
            script.enabled = shouldMove;
    }

    private void TriggerSelectionIndicator(GameObject unit, bool IsVisible)
    {
        if (unit.transform.childCount > 0)
        {
            unit.transform.GetChild(0).gameObject.SetActive(IsVisible);
        }
    }

    public void DragSelect(GameObject unit)
    {
        if (!unitsSelected.Contains(unit))
        {
            unitsSelected.Add(unit);
            TriggerSelectionIndicator(unit, true);
            EnableUnitMovement(unit, true);
        }
    }
}