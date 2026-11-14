using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UnitSelectionBox : MonoBehaviour
{
    private Camera myCam;

    [SerializeField]
    private RectTransform boxVisual;

    private Rect selectionBox;
    private Vector2 startPosition;
    private Vector2 endPosition;
    private bool selecting;

    private void Start()
    {
        myCam = Camera.main;
        startPosition = Vector2.zero;
        endPosition = Vector2.zero;

        if (boxVisual != null)
        {
            boxVisual.sizeDelta = Vector2.zero;
            boxVisual.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (SettingsMenu.BlocksWorldInput) { CancelSelection(); return; }
        if(ManagerGO.Instance!=null&&(ManagerGO.Instance.BlocksMilitarySelection||Input.GetKeyDown(ManagerGO.Instance.attackMoveKey)||Input.GetKeyDown(ManagerGO.Instance.defenseFacingKey)||Input.GetKeyDown(ManagerGO.Instance.retreatKey)||Input.GetKeyDown(ManagerGO.Instance.patrolKey)))
        {CancelSelection();return;}
        if (Barracks.BlocksWorldInput || RoadBuilder.BlocksWorldInput) { CancelSelection(); return; }
        if (boxVisual == null)
            return;
        if (PlacementSystem.Instance != null && PlacementSystem.Instance.BlocksUnitInput)
        { CancelSelection(); return; }

        // Пока идёт расстановка строя, рамка выделения не должна перехватывать ЛКМ
        if (SoldiersFormation.IsPlacing &&
            ManagerGO.Instance != null &&
            ManagerGO.Instance.unitsSelected.Count > 0)
        {
            CancelSelection();
            return;
        }

        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (Input.GetMouseButtonDown(0))
        {
            // Клик по UI (например, кнопка на канвасе) не должен запускать
            // рамку выделения и снимать выделение с юнитов
            if (pointerOverUI)
            {
                CancelSelection();
                return;
            }

            selecting = true;
            startPosition = Input.mousePosition;
            endPosition = startPosition;
            selectionBox = new Rect();

            // Сбрасываем старый размер рамки, иначе на первом кадре
            // boxVisual.rect.width > 0 и ManagerGO.DeselectAll() сработает сразу
            boxVisual.sizeDelta = Vector2.zero;
            boxVisual.gameObject.SetActive(true);
        }

        if (!selecting)
            return;

        // Тянем рамку
        if (Input.GetMouseButton(0))
        {
            if (boxVisual.rect.width > 0f || boxVisual.rect.height > 0f)
            {
                ManagerGO.Instance.DeselectAll();
                SelectUnits();
            }

            endPosition = Input.mousePosition;
            DrawVisual();
            DrawSelection();
        }

        // Отпустили ЛКМ
        if (Input.GetMouseButtonUp(0))
        {
            SelectUnits();
            CancelSelection();
        }
    }

    private void CancelSelection()
    {
        selecting = false;
        startPosition = Vector2.zero;
        endPosition = Vector2.zero;
        selectionBox = new Rect();

        if (boxVisual != null)
        {
            boxVisual.sizeDelta = Vector2.zero;
            boxVisual.gameObject.SetActive(false);
        }
    }

    private void DrawVisual()
    {
        Vector2 boxStart = startPosition;
        Vector2 boxEnd = endPosition;

        Vector2 boxCenter = (boxStart + boxEnd) / 2;
        boxVisual.position = boxCenter;

        Vector2 boxSize = new Vector2(Mathf.Abs(boxStart.x - boxEnd.x), Mathf.Abs(boxStart.y - boxEnd.y));
        boxVisual.sizeDelta = boxSize;
    }

    private void DrawSelection()
    {
        if (Input.mousePosition.x < startPosition.x)
        {
            selectionBox.xMin = Input.mousePosition.x;
            selectionBox.xMax = startPosition.x;
        }
        else
        {
            selectionBox.xMin = startPosition.x;
            selectionBox.xMax = Input.mousePosition.x;
        }

        if (Input.mousePosition.y < startPosition.y)
        {
            selectionBox.yMin = Input.mousePosition.y;
            selectionBox.yMax = startPosition.y;
        }
        else
        {
            selectionBox.yMin = startPosition.y;
            selectionBox.yMax = Input.mousePosition.y;
        }
    }

    private void SelectUnits()
    {
        if (ManagerGO.Instance == null || myCam == null)
            return;

        foreach (var unit in ManagerGO.Instance.allUnitsList)
        {
            if (unit == null) continue;

            if (selectionBox.Contains(myCam.WorldToScreenPoint(unit.transform.position)))
            {
                ManagerGO.Instance.DragSelect(unit);
            }
        }
    }
}
