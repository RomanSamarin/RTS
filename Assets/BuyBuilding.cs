using _Game.Scripts.UI;
using UnityEngine;

public class BuyBuilding : MonoBehaviour
{
    public int databaseItemId;

    public void OnButtonPlacement()
    {
        Debug.Log($"[BuyBuilding] Нажата кнопка постройки. ID здания = {databaseItemId}");

        // Перед размещением здания сначала проверяем ресурсы
        if (!CheckResources())
        {
            Debug.LogWarning("[BuyBuilding] Невозможно построить: недостаточно ресурсов!");
            return;
        }

        // Ресурсов достаточно — запускаем режим размещения
        Debug.Log($"[BuyBuilding] Ресурсов достаточно — запускаем размещение (ID = {databaseItemId})");
        PlacementSystem.Instance.StartPlacement(databaseItemId);
    }

    // Ищем базу данных: сначала через DataBaseManager, затем через PlacementSystem
    private ObjectsDatabseSO GetDatabase()
    {
        if (DataBaseManager.Instance != null && DataBaseManager.Instance.databseSO != null)
            return DataBaseManager.Instance.databseSO;

        // В PlacementSystem база уже назначена в инспекторе (BuildingSystem.prefab)
        if (PlacementSystem.Instance != null)
            return PlacementSystem.Instance.Database;

        return null;
    }

    // Возвращает true, если ресурсов хватает на постройку здания databaseItemId
    private bool CheckResources()
    {
        ObjectsDatabseSO db = GetDatabase();

        if (db == null)
        {
            Debug.LogError("[BuyBuilding] База данных недоступна: нет DataBaseManager с назначенным databseSO и нет PlacementSystem с базой!");
            return false;
        }

        ObjectData objectData = db.GetObjectByID(databaseItemId);

        // GetObjectByID возвращает пустой ObjectData, если такого ID нет
        if (objectData == null || objectData.Prefab == null)
        {
            Debug.LogError($"[BuyBuilding] В базе нет здания с ID = {databaseItemId}!");
            return false;
        }

        Debug.Log($"[BuyBuilding] Проверка здания \"{objectData.Name}\" (ID = {databaseItemId})");

        // Здание без требований можно строить всегда
        if (objectData.requirements == null || objectData.requirements.Count == 0)
        {
            Debug.Log("[BuyBuilding] У здания нет требований — постройка разрешена.");
            return true;
        }

        if (ResourcesManager.Instance == null)
        {
            Debug.LogError("[BuyBuilding] ResourcesManager не найден!");
            return false;
        }

        bool enough = BuildCost.CanAfford(objectData);

        if (enough)
        {
            Debug.Log("[BuyBuilding] Проверка пройдена: ресурсов хватает.");
        }
        else
        {
            foreach (var material in BuildCost.GetMaterials(objectData))
                if (ResourcesManager.Instance.GetAmount(material.Key) < material.Value)
                    Debug.LogWarning($"[BuyBuilding] Не хватает {material.Key} (нужно {material.Value}).");
        }

        return enough;
    }
}
