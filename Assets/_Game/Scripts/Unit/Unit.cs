using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public float unitHealth;
    public float unitMaxHealth;
    public HealthTracker heathTracker;

    [Header("Настройки выпадающих ресурсов")]
    public int woodReward;
    public int stoneReward;
    public int wheatReward;
    public int fishReward;

    void Start()
    {
        if (ManagerGO.Instance != null && ManagerGO.Instance.allUnitsList != null)
        {
            ManagerGO.Instance.allUnitsList.Add(gameObject);
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
