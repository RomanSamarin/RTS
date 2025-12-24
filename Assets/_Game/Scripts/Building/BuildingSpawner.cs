using UnityEngine;
using _Game.Scripts.UI;

public class BuildSpawner : MonoBehaviour
{
    [Header("Build Prefab")]
    [SerializeField] private GameObject buildPrefab;

    [Header("Build Cost")]
    [SerializeField] private int woodCost = 5;
    [SerializeField] private int stoneCost = 3;
    [SerializeField] private int wheatCost = 2;

    private GameObject currentBuild;

    public void SpawnBuild()
    {
        if (currentBuild != null)
            return;

        if (!ResourcesManager.Instance.HasEnough(woodCost, stoneCost, wheatCost))
        {
            Debug.Log("Not enough resources to build!");
            return;
        }

        currentBuild = Instantiate(buildPrefab);
    }

    public void OnBuildPlaced()
    {
        ResourcesManager.Instance.Spend(woodCost, stoneCost, wheatCost);
        currentBuild = null;
    }

    public void OnBuildCanceled()
    {
        currentBuild = null;
    }
}