using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{

    [SerializeField] private Camera sceneCamera;
    [SerializeField] private LayerMask placementLayerMask;

    [SerializeField]  private Vector3 lastPosition;

    public event Action OnClicked, OnExit;
    private int suppressedClickFrame = -1;
    public void SuppressWorldClickThisFrame() { suppressedClickFrame = Time.frameCount; }

    // EventSystem обрабатывает UI в Update. Проверяем клик после него, чтобы
    // кнопка и закрывающаяся панель не передавали то же нажатие в мир.
    private void LateUpdate()
    {
        if (SettingsMenu.BlocksWorldInput) return;
        if (Input.GetMouseButtonDown(0) && suppressedClickFrame != Time.frameCount && !IsPointerOverUI())
             OnClicked?.Invoke();
        if (Input.GetKeyDown(KeyCode.Escape))
             OnExit?.Invoke();
    }

    public bool IsPointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    public bool TryGetSelectedMapPosition(out Vector3 position)
    {
        position = default;
        var camera = sceneCamera != null ? sceneCamera : Camera.main;
        if (camera == null || !Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition), out var hit,
                Mathf.Infinity, placementLayerMask, QueryTriggerInteraction.Ignore)) return false;
        position = lastPosition = hit.point; return true;
    }

    public bool TryProjectWallGround(Vector3 position, out RaycastHit hit)
    {
        return Physics.Raycast(position + Vector3.up * 1000f, Vector3.down, out hit, 2000f,
            placementLayerMask, QueryTriggerInteraction.Ignore);
    }


    public Vector3 GetSelectedMapPosition()
    {
        TryGetSelectedMapPosition(out _);
        return lastPosition;
    }
}
