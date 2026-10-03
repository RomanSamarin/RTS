using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

// Добавили реализацию интерфейса ISelectable
public class Barracks : MonoBehaviour 
{
    [Header("Настройки")]
    public GameObject objectToSpawn; 
    public GameObject SpawnPoint;
    public float spawnDelay = 3f;

    [Header("Стоимость юнита")]
    public int woodCost = 50;
    public int stoneCost = 0;
    public int wheatCost = 20;
    public int fishCost = 20;

    [Header("UI")]
    public GameObject UIkazarma; // Уникальное UI для конкретно этой казармы

    private Queue<GameObject> unitQueue = new Queue<GameObject>();
    private bool isTraining = false;

    void Start()
    {
        Debug.Log("Barracks::Start(); -- objectToSpawn:" + objectToSpawn);
        Debug.Log("Barracks::Start(); -- SpawnPoint:" + SpawnPoint);
    }

    public void OnButtonSpawnUnit()
    {
        Debug.Log("Нажата кнопка");
        if (objectToSpawn == null) return;

        if (ResourcesManager.Instance != null && ResourcesManager.Instance.HasEnough(woodCost, stoneCost, wheatCost, fishCost))
        {
            ResourcesManager.Instance.Spend(woodCost, stoneCost, wheatCost, fishCost);
            unitQueue.Enqueue(objectToSpawn);
            

            if (!isTraining)
            {
                StartCoroutine(ProcessQueueRoutine());
            }
        }
    }

    IEnumerator ProcessQueueRoutine()
    {
        isTraining = true;
        while (unitQueue.Count > 0)
        {
            GameObject nextUnit = unitQueue.Peek();
            yield return new WaitForSeconds(spawnDelay);

            if (SpawnPoint != null)
            {
                Instantiate(nextUnit, SpawnPoint.transform.position, SpawnPoint.transform.rotation);
            }
            unitQueue.Dequeue();
        }
        isTraining = false;
    }

    
    public void Select()
    {
        if (UIkazarma != null) UIkazarma.SetActive(true);
    }

    public void Deselect()
    {
        if (UIkazarma != null) UIkazarma.SetActive(false);
    }
}