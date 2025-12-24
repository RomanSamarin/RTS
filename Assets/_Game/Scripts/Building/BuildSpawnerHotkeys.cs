using UnityEngine;
using _Game.Scripts.UI;

public class BuildSpawnerHotkeys : MonoBehaviour
{
    [Header("Building Prefabs")]
    public GameObject barracksPrefab;
    public GameObject farmPrefab;
    public GameObject minePrefab;

    [Header("Cost")]
    public int barracksWood = 10;
    public int barracksStone = 5;
    public int barracksWheat = 0;

    public int farmWood = 5;
    public int farmStone = 2;
    public int farmWheat = 0;

    public int mineWood = 5;
    public int mineStone = 5;
    public int mineWheat = 0;

    [Header("Spawn Settings")]
    public LayerMask groundLayer;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            TrySpawnBuilding(barracksPrefab, barracksWood, barracksStone, barracksWheat);

        if (Input.GetKeyDown(KeyCode.Alpha2))
            TrySpawnBuilding(farmPrefab, farmWood, farmStone, farmWheat);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            TrySpawnBuilding(minePrefab, mineWood, mineStone, mineWheat);
    }

    private void TrySpawnBuilding(GameObject prefab, int woodCost, int stoneCost, int wheatCost)
    {
        if (!ResourcesManager.Instance.HasEnough(woodCost, stoneCost, wheatCost))
        {
            Debug.Log("Недостаточно ресурсов!");
            return;
        }

        // Списание ресурсов
        ResourcesManager.Instance.Spend(woodCost, stoneCost, wheatCost);

        // Определяем позицию спавна по мышке
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            Instantiate(prefab, hit.point, Quaternion.identity);
        }
    }
}