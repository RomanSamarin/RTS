using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

public class Barracks : MonoBehaviour
{
    [Header("Unit")]
    public GameObject unitPrefab;

    [Header("Cost per Unit")]
    public int woodCost = 10;
    public int stoneCost = 5;
    public int wheatCost = 0;

    [Header("Training")]
    public float trainingTime = 3f; // ����� ���������� � ��������
    public float spacing = 1.5f;    // ���������� ����� ������� ��� ������
    public int unitsPerRow = 5;     // ���������� ������ � ���� ����� ��������� �� ��������� ������

    private bool isTraining = false;
    private Transform spawnPoint;

    // ����� ������ ���� ������, ��������� ����� ���������
    private static List<GameObject> allUnits = new List<GameObject>();

    private void Awake()
    {
        // ������� spawnPoint ����� ��������
        spawnPoint = new GameObject("SpawnPoint").transform;
        spawnPoint.SetParent(transform);

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            float offset = col.bounds.extents.z + 1f;
            spawnPoint.position = transform.position + transform.forward * offset;
        }
        else
        {
            spawnPoint.localPosition = Vector3.forward * 2f;
        }
    }

    private void OnMouseDown()
    {
        // ��������� ���� ������ ����� BuildingUIManager
        if (BuildingUIManager.Instance != null)
            BuildingUIManager.Instance.ShowMenuForBuilding(this);
    }

    // ����� �� UI-������ ��� �������
    public void TrainUnit()
    {
        Debug.Log("TrainUnit(); -- gameObject.activeInHierarchy:" + gameObject.activeInHierarchy);
        Debug.Log("TrainUnit(); -- isTraining:" + isTraining);
        if (!gameObject.activeInHierarchy || isTraining)
            return;

        // �������� ��������
        if (!ResourcesManager.Instance.HasEnough(woodCost, stoneCost, wheatCost))
        {
            Debug.Log("������������ �������� ��� ���������� �����!");
            return;
        }

        // �������� ��������
        ResourcesManager.Instance.Spend(woodCost, stoneCost, wheatCost);

        StartCoroutine(TrainCoroutine());
    }

    private IEnumerator TrainCoroutine()
    {
        isTraining = true;

        yield return new WaitForSeconds(trainingTime);

        // ��������� ������� ������ ����� � �����
        int index = allUnits.Count;
        int row = index / unitsPerRow;
        int col = index % unitsPerRow;

        Vector3 offset = new Vector3(col * spacing, 0, row * spacing);
        Vector3 pos = spawnPoint.position + offset;
        pos.y = spawnPoint.position.y;

        GameObject unit = Instantiate(unitPrefab, pos, spawnPoint.rotation);
        allUnits.Add(unit);

        isTraining = false;
    }
}