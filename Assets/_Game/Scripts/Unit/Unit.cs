using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
public class Unit : MonoBehaviour
{
    public float unitHealth;
    public float unitMaxHealth = 100f;
    public HealthTracker heathTracker;
    public Animator animator;
    public NavMeshAgent navMeshAgent;

    [Header("Настройки выпадающих ресурсов")]
    public int woodReward;
    public int stoneReward;
    public int wheatReward;
    public int fishReward;
    void Update()
    {
        if (navMeshAgent != null && animator != null)
        {
            // hasPath проверяем обязательно: без пути remainingDistance == Infinity,
            // и после ResetPath() по прибытии юнит «шёл» бы на месте.
            bool moving = navMeshAgent.hasPath
                          && !navMeshAgent.pathPending
                          && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance;

            animator.SetBool("isMoving", moving);
        }
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        // В список для рамки выделения добавляем только СВОИ юниты
        // (у дерева/врага AttackController есть, но IsPlayer == false, либо его вообще нет)
        if (ManagerGO.Instance != null && ManagerGO.Instance.allUnitsList != null)
        {
            AttackController attack = GetComponent<AttackController>();
            // Строитель отключает авто-захват целей (AttackController.IsPlayer = false),
            // поэтому добавляем его в список выделения отдельно — иначе он
            // не попадал бы в рамку выделения.
            bool isBuilder = GetComponent<Builder>() != null;
            if ((attack != null && attack.IsPlayer) || isBuilder)
            {
                ManagerGO.Instance.allUnitsList.Add(gameObject);
            }
        }
        unitHealth = unitMaxHealth;
        UpdateHealth();
        
    }

    private void OnDestroy()
    {
        if (ManagerGO.Instance != null && ManagerGO.Instance.allUnitsList != null)
        {
            ManagerGO.Instance.allUnitsList.Remove(gameObject);
        }
    }

    private void UpdateHealth()
    {
        if (heathTracker != null)
        {
            heathTracker.UpdateSliderValue(unitHealth, unitMaxHealth);
        }
    }

    // Метод смерти: принимает объект юнита, который нанес последний удар
    public void Destroy(GameObject killer)
    {
        if (killer != null)
        {
            GettingResources backpack = killer.GetComponent<GettingResources>();
            ResourcesMove targetMove = killer.GetComponent<ResourcesMove>();

            // Напрямую передаем награду в рюкзак юнита
            if (backpack != null)
            {
                backpack.TakeResource(this);
                Debug.Log($"Ресурсы дерева переданы юниту {killer.name}. Дерево: {woodReward}");
            }

            // Переключаем юнита в режим переноски
            if (targetMove != null)
            {
                targetMove.SetInventoryFull();
            }
        }

        UnityEngine.Object.Destroy(gameObject); 
    }

    // Изменили метод: теперь он обязательно требует указать, кто атакует (attacker)
    internal void TakeDamage(int damageToInflict, GameObject attacker)
    {
        unitHealth -= damageToInflict;
        UpdateHealth();

        if (unitHealth <= 0)
        {
            Destroy(attacker);
        }
    }

    // Оставили старый метод для совместимости
    public void Destroy()
    {
        UnityEngine.Object.Destroy(gameObject);
    }
}
