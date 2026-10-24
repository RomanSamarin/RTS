using UnityEngine;

/// <summary>
/// Вешается на здание. Хранит ссылку на меню именно этого здания
/// и показывает/скрывает его при выборе.
/// </summary>
[DisallowMultipleComponent]
public class UiSelectBuilding : MonoBehaviour, ISelectable
{
    [Header("Меню этого здания")]
    [Tooltip("Панель, которая откроется при клике по зданию. У каждого здания может быть своя.")]
    public GameObject UIPanel;

    [Header("Необязательно")]
    [Tooltip("Объект-подсветка, который включается вместе с меню.")]
    public GameObject selectionIndicator;

    public bool IsSelected { get; private set; }

    public GameObject Panel
    {
        get
        {
            if (UIPanel != null) return UIPanel;

            // Совместимость со старой казармой: у неё панель лежит в Barracks.UIkazarma.
            Barracks barracks = GetComponent<Barracks>();
            if (barracks == null) barracks = GetComponentInParent<Barracks>();
            if (barracks == null) barracks = GetComponentInChildren<Barracks>();

            return barracks != null ? barracks.UIkazarma : null;
        }
    }

    private void Awake()
    {
        if (Panel != null) Panel.SetActive(false);
        if (selectionIndicator != null) selectionIndicator.SetActive(false);
    }

    public void Select()
    {
        if (IsSelected) return;

        IsSelected = true;

        GameObject panel = Panel;
        if (panel != null)
            panel.SetActive(true);
        else
            Debug.LogWarning($"[UiSelectBuilding] У \"{name}\" не назначена панель меню (UIPanel).", this);

        if (selectionIndicator != null)
            selectionIndicator.SetActive(true);
    }

    public void Deselect()
    {
        if (!IsSelected) return;

        IsSelected = false;

        GameObject panel = Panel;
        if (panel != null) panel.SetActive(false);
        if (selectionIndicator != null) selectionIndicator.SetActive(false);
    }

    private void OnDisable()
    {
        Deselect();
    }
}
