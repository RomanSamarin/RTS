using UnityEngine;

// Единый расчёт стоимости постройки из требований ObjectData.
// Используется и при проверке ресурсов (BuyBuilding), и при списании (PlacementSystem).
public static class BuildCost
{
    public static System.Collections.Generic.Dictionary<_Game.Scripts.UI.ResourceType,int> GetMaterials(ObjectData data)
    {
        var result = new System.Collections.Generic.Dictionary<_Game.Scripts.UI.ResourceType,int>();
        if (data?.requirements == null) return result;
        foreach (var req in data.requirements)
        {
            if (!System.Enum.TryParse(req.resource, true, out _Game.Scripts.UI.ResourceType type)
                || !System.Enum.IsDefined(typeof(_Game.Scripts.UI.ResourceType), type) || req.amount < 0)
                throw new System.ArgumentException("Некорректная стоимость здания: " + req.resource);
            result.TryGetValue(type, out int previous);
            result[type] = checked(previous + req.amount);
        }
        return result;
    }
    public static bool CanAfford(ObjectData data)
    {
        foreach (var item in GetMaterials(data))
        {
            int total = 0;
            foreach (var store in _Game.Scripts.UI.WarehouseStorage.All) if(FactionMember.IsPlayer(store.gameObject)&&BuildingRoadAccess.Allows(store))total += store.AvailableMaterial(item.Key);
            if (total < item.Value) return false;
        }
        return true;
    }
    public static void Get(ObjectData data, out int wood, out int stone, out int wheat, out int fish)
    {
        wood = 0;
        stone = 0;
        wheat = 0;
        fish = 0;

        // Здание без требований — бесплатное
        if (data == null || data.requirements == null)
            return;

        foreach (var req in data.requirements)
        {
            switch (req.resource)
            {
                case "Wood":  wood  = req.amount; break;
                case "Stone": stone = req.amount; break;
                case "Wheat": wheat = req.amount; break;
                case "Fish":  fish  = req.amount; break;
                default:
                    Debug.LogWarning($"[BuildCost] Неизвестный ресурс \"{req.resource}\" в требованиях здания \"{data.Name}\" (ID = {data.ID}) — требование проигнорировано.");
                    break;
            }
        }
    }
}
