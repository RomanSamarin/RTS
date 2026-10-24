using UnityEngine;

// Единый расчёт стоимости постройки из требований ObjectData.
// Используется и при проверке ресурсов (BuyBuilding), и при списании (PlacementSystem).
public static class BuildCost
{
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
