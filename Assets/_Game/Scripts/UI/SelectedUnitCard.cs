using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectedUnitCard : MonoBehaviour
{
    public Button selectButton;
    public Image healthFill;
    public Image customPortrait;
    public RawImage atlasPortrait;
    public TMP_Text unitNameText;

    private static Texture2D atlas;
    private Unit unit;

    private void Awake()
    {
        if (selectButton != null) selectButton.onClick.AddListener(Select);
    }

    public void Show(Unit selected)
    {
        if (unit != selected)
        {
            unit = selected;
            if (unitNameText != null) unitNameText.text = unit.name;
            BindPortrait();
        }

        if (healthFill != null)
            healthFill.fillAmount = unit.unitMaxHealth > 0f ? Mathf.Clamp01(unit.unitHealth / unit.unitMaxHealth) : 0f;
    }

    private void BindPortrait()
    {
        bool hasCustomPortrait = customPortrait != null && unit.selectionPortrait != null;
        if (customPortrait != null)
        {
            customPortrait.sprite = unit.selectionPortrait;
            customPortrait.enabled = hasCustomPortrait;
        }
        if (atlasPortrait == null) return;
        if (atlas == null) atlas = Resources.Load<Texture2D>("SelectedUnitPortraits");
        if (atlasPortrait.texture == null) atlasPortrait.texture = atlas;
        int index = PortraitIndex(unit.gameObject);
        atlasPortrait.uvRect = new Rect(index % 4 / 4f + .002f,
            (2 - index / 4) / 3f + .002f, .246f, 1f / 3f - .004f);
        atlasPortrait.enabled = !hasCustomPortrait && atlasPortrait.texture != null;
    }

    private static int PortraitIndex(GameObject unit)
    {
        if (unit.TryGetComponent<ShipHull>(out var hull))
            return hull.type == ShipType.Transport ? 9 : hull.type == ShipType.Warship ? 10 : 8;
        if (unit.GetComponent<Catapult>() != null) return 5;
        if (unit.GetComponent<FoodCart>() != null || unit.GetComponent<SiegeLadderCart>() != null) return 6;
        if (unit.GetComponent<CartHorse>() != null) return 7;
        return BattleCombatant.TypeOf(unit) switch
        {
            TroopType.Swordsman => 0,
            TroopType.Spearman => 1,
            TroopType.Archer => 2,
            TroopType.Cavalry => 3,
            _ => 4
        };
    }

    private void Select()
    {
        if (unit == null || SaveGameManager.IsLoading) return;
        bool remove = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        ManagerGO.Instance?.SelectFromCard(unit.gameObject, remove);
    }

    private void OnDestroy()
    {
        if (selectButton != null) selectButton.onClick.RemoveListener(Select);
    }
}
