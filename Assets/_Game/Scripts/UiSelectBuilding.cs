using UnityEngine;

/// <summary>
/// Вешается на здание. Хранит ссылку на меню именно этого здания
/// и показывает/скрывает его при выборе.
///
/// Здания на сцене появляются из префабов во время игры, поэтому в префабе
/// нельзя указать ссылку на панель из сцены. В таком случае панель ищется
/// по имени (поле «Panel Name»), в том числе среди выключенных объектов.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingHealth))]
public class UiSelectBuilding : MonoBehaviour, ISelectable
{
    [Header("Меню этого здания")]
    [Tooltip("Панель, которая откроется при клике по зданию. У каждого здания может быть своя.")]
    public GameObject UIPanel;

    [Tooltip("Имя панели в сцене (например, SawMillPanel). Используется, если поле UIPanel " +
             "пустое — например, у зданий, которые создаются из префаба во время игры")]
    public string panelName;

    [Header("Необязательно")]
    [Tooltip("Объект-подсветка, который включается вместе с меню.")]
    public GameObject selectionIndicator;

    public bool IsSelected { get; private set; }

    // Найденная по имени панель (кэш, чтобы не искать на каждый клик).
    private GameObject resolvedPanel;

    public GameObject Panel
    {
        get
        {
            if (UIPanel != null)
                return UIPanel;

            if (resolvedPanel == null && !string.IsNullOrEmpty(panelName))
                resolvedPanel = FindPanelByName(panelName);

            return resolvedPanel;
        }
    }

    private void Awake()
    {
        // Страховка для зданий, созданных до появления BuildingHealth:
        // RequireComponent сам добавляет компонент только при ручном добавлении.
        if (GetComponent<BuildingHealth>() == null)
            gameObject.AddComponent<BuildingHealth>();

        if (Panel != null) Panel.SetActive(false);
        if (selectionIndicator != null) selectionIndicator.SetActive(false);
    }

    public void Select()
    {
        if (IsSelected||!FactionMember.IsPlayer(gameObject)) return;

        IsSelected = true;

        GameObject panel = Panel;
        if (panel != null)
        {
            var warehouseUI = panel.GetComponentInChildren<_Game.Scripts.UI.WarehouseUI>(true);
            var productionUI = panel.GetComponentInChildren<_Game.Scripts.UI.EquipmentProductionPanel>(true);
            var barracksUI = panel.GetComponentInChildren<_Game.Scripts.UI.BarracksActionsPanel>(true);
            var stableUI = panel.GetComponentInChildren<_Game.Scripts.UI.StablePanel>(true);
            var hospitalUI=panel.GetComponentInChildren<_Game.Scripts.UI.HospitalPanel>(true);
            if(hospitalUI!=null)hospitalUI.Bind(GetComponentInParent<Hospital>()??GetComponentInChildren<Hospital>());
            if (stableUI != null)
            {
                var stable = GetComponentInParent<Stable>();
                if (stable == null) stable = GetComponentInChildren<Stable>();
                stableUI.Bind(stable);
            }
            if (barracksUI != null)
            {
                var barracks = GetComponentInParent<Barracks>();
                if (barracks == null) barracks = GetComponentInChildren<Barracks>();
                barracksUI.Bind(barracks);
            }
            if (productionUI != null)
            {
                var production = GetComponentInParent<EquipmentProductionBuilding>();
                if (production == null) production = GetComponentInChildren<EquipmentProductionBuilding>();
                productionUI.Bind(production);
            }
            if (warehouseUI != null && CompareTag("Warehouse"))
            {
                var storage = GetComponent<_Game.Scripts.UI.WarehouseStorage>();
                if (storage == null) storage = gameObject.AddComponent<_Game.Scripts.UI.WarehouseStorage>();
                warehouseUI.Bind(storage);
            }
            panel.SetActive(true);
        }
        else
            Debug.LogWarning($"[UiSelectBuilding] У \"{name}\" не назначена панель меню " +
                             $"(UIPanel пустая, Panel Name = \"{panelName}\").", this);

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

    /// <summary>
    /// Ищет объект по имени среди всех загруженных объектов сцены,
    /// включая выключенные (панели меню обычно лежат выключенными).
    /// </summary>
    private static GameObject FindPanelByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        foreach (RectTransform rect in FindObjectsOfType<RectTransform>(true))
        {
            if (rect != null && rect.name == name)
                return rect.gameObject;
        }

        return null;
    }
}
