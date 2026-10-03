using UnityEngine;
using _Game.Scripts.UI;

public class BuildManager : MonoBehaviour
{
    public LayerMask groundLayer;

    public void TryToBuild(GameObject buildingPrefab)
    {
        BuildingObject buildingScript = buildingPrefab.GetComponent<BuildingObject>();

        if (buildingScript == null)
        {
            Debug.LogError("BuildManager::TryToBuild() -- На префабе отсутствует скрипт BuildingObject!");
            return;
        }

        // Проверяем ресурсы ТОЛЬКО для старта строительства (перед тем как дать объект в руку)
        if (ResourcesManager.Instance != null && 
            ResourcesManager.Instance.HasEnough(buildingScript.woodCost, buildingScript.stoneCost, buildingScript.wheatCost, buildingScript.fishCost))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Просто спавним здание в точке мыши. Дальше оно само будет следовать за курсором.
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer))
            {
                Instantiate(buildingPrefab, hit.point, Quaternion.identity);
                Debug.Log("Режим строительства активирован. Объект в руке.");
            }
        }
        else
        {
            Debug.Log("Недостаточно ресурсов для начала строительства!");
        }
    }
}
