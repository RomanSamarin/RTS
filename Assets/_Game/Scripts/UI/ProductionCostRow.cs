using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Game.Scripts.UI
{
    public class ProductionCostRow : MonoBehaviour
    {
        public Image icon;
        public TMP_Text amountText;
        public void Bind(EquipmentProductionPanel panel, ResourceType type, int amount)
        {
            if (icon != null) { icon.sprite = panel.GetSprite(type); icon.enabled = icon.sprite != null; }
            if (amountText != null) amountText.text = panel.CostLabel(type, amount);
        }
    }
}
