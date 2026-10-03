using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FishingSpot : MonoBehaviour
{
    [Header("Настройки рыбалки")]
    public int totalFishCount = 5;         // Общее количество рыб в этом водоеме
    public float timeToCatchOneFish = 3f; // Сколько СЕКУНД ловится ОДНА рыба

    [HideInInspector] public float fishHealth; // Текущее "здоровье" (оставшееся время в секундах)
    private int fishCaughtByUnit = 0;          // Сколько рыбы уже поймал текущий рыбак

    void Start()
    {
        if (ManagerGO.Instance != null && ManagerGO.Instance.allUnitsList != null)
        {
            ManagerGO.Instance.allUnitsList.Add(gameObject);
        }

        // Общее "здоровье" водоема — это общее время в секундах, нужное на вылов всей рыбы
        fishHealth = totalFishCount * timeToCatchOneFish;
    }

    private void OnDestroy()
    {
        if (ManagerGO.Instance != null && ManagerGO.Instance.allUnitsList != null)
        {
            ManagerGO.Instance.allUnitsList.Remove(gameObject);
        }
    }

    // Метод вызывается при каждом замахе удочки из UnitAttackState
    public void TakeDamage(int damageToInflict, GameObject attacker)
    {
        // Юнит наносит "урон", который равен секундам, проведенным за ловлей
        // (Обычно damageToInflict равен 1, то есть снимает 1 секунду за один тик атаки)
        fishHealth -= damageToInflict;

        // Вычисляем, сколько рыб ДОЛЖНО быть поймано на данный момент
        int expectedCaught = totalFishCount - Mathf.CeilToInt(fishHealth / timeToCatchOneFish);
        
        // Если выловилась новая рыбина
        if (expectedCaught > fishCaughtByUnit && fishCaughtByUnit < totalFishCount)
        {
            fishCaughtByUnit++;
            
            GettingResources backpack = attacker.GetComponent<GettingResources>();
            if (backpack != null)
            {
                backpack.Fish++; // Добавляем ровно 1 рыбу в рюкзак рыбака прямо во время ловли!
                Debug.Log($"Рыбак {attacker.name} выудил рыбу! В рюкзаке: {backpack.Fish}. В озере осталось рыб: {totalFishCount - fishCaughtByUnit}");
            }
        }

        // Если водоем полностью опустел
        if (fishHealth <= 0 || fishCaughtByUnit >= totalFishCount)
        {
            FinishFishing(attacker);
        }
    }

    private void FinishFishing(GameObject killer)
    {
        if (killer != null)
        {
            ResourcesMove targetMove = killer.GetComponent<ResourcesMove>();
            if (targetMove != null)
            {
                // Отправляем рыбака разгружаться в рыболовню, так как рыба закончилась
                targetMove.SetInventoryFull(); 
            }
        }

        Debug.Log("Водоем полностью истощен!");
        Destroy(gameObject);
    }
}
