using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingHealth))]
public class Constructable : MonoBehaviour
{

    NavMeshObstacle obstacle;
    public float Health;

    private void Awake()
    {
        // Страховка для префабов, созданных до появления BuildingHealth:
        // без него здание не попадёт в BuildingManager.AllBuildings.
        if (GetComponent<BuildingHealth>() == null)
            gameObject.AddComponent<BuildingHealth>();
    }

    public void ConstructableWasPlaced()
    {
         ActivateObstacle();
    }

    private void ActivateObstacle()
    {
        obstacle = GetComponentInChildren<NavMeshObstacle>();
        obstacle.enabled = true;
    }
    internal void TakeDamage(int damageToInflict, GameObject attacker)
    {
        Health -= damageToInflict;

        if (Health <= 0)
        {
            Destroy(attacker);
        }
    }
}
