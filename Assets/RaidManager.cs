using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RaidManager : MonoBehaviour
{
    public GameObject PrefabEnemy;
    public List<GameObject> points = new List<GameObject>();

    [Header("Рейд")]
    public float raidIntervalMinutes = 2f;
    public int minEnemies = 1;
    public int maxEnemies = 5;

    private void Start()
    {
        StartCoroutine(RaidLoop());
    }

    private IEnumerator RaidLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(raidIntervalMinutes * 60f);
            SpawnWave();
        }
    }

    private void SpawnWave()
    {
        if (!PrefabEnemy || points.Count == 0)
            return;

        for (int i = Random.Range(minEnemies, maxEnemies + 1); i > 0; i--)
        {
            Vector3 position = points[Random.Range(0, points.Count)].transform.position;

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                position = hit.position;

            GameObject enemy = Instantiate(PrefabEnemy, position, PrefabEnemy.transform.rotation);

            if (enemy.TryGetComponent(out EnemyAi ai))
                ai.Raid();
        }
    }
}
