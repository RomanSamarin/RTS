using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ЖИЛА КАМНЯ (объект добычи): каменщик копает её киркой
// (каждый "удар" через UnitAttackState.TakeDamage = 1 секунда добычи).
// Нагрузка камня идёт сразу в рюкзак рабочего; сдать он его должен на
// ВРЕМЕННЫЙ СКЛАД КАМНЯ (скрипт StoneHouse). Носильщик через камень уже не ходит.
public class StoneQuarry : MonoBehaviour
{
    [Header("Настройки жилы")]
    public int totalStoneCount = 5;       // Изначальное количество камней в этой жиле (для расчета здоровья)
    public float timeToMineOneStone = 3f; // Сколько СЕКУНД добывается ОДИН камень

    [Header("Прогресс добычи")]
    public int currentStoneLeft;          // Сколько камней ЕЩЕ МОЖНО ДОБЫТЬ (невыработанная порода)

    [HideInInspector] public float stoneHealth; // Текущее "здоровье" жилы (оставшееся время добычи в секундах)
    private int stoneMinedByUnit = 0;           // Сколько камня уже добыл рабочий в эту сессию

    void Start()
    {
        if (ManagerGO.Instance != null && ManagerGO.Instance.allUnitsList != null)
        {
            ManagerGO.Instance.allUnitsList.Add(gameObject);
        }

        // Заполняем жилу невыработанной породой на старте
        currentStoneLeft = totalStoneCount;

        // Общее "здоровье" жилы — это общее время в секундах, нужное на добычу всего камня
        stoneHealth = totalStoneCount * timeToMineOneStone;
    }

    private void OnDestroy()
    {
        if (ManagerGO.Instance != null && ManagerGO.Instance.allUnitsList != null)
        {
            ManagerGO.Instance.allUnitsList.Remove(gameObject);
        }
    }

    // Метод вызывается при каждом ударе киркой из UnitAttackState
    public void TakeDamage(int damageToInflict, GameObject attacker)
    {
        // Если в породе ничего не осталось, добывать больше нечего
        if (currentStoneLeft <= 0) return;

        // Юнит наносит "урон", который равен секундам, проведенным за работой
        stoneHealth -= damageToInflict;

        // Вычисляем, сколько камней ДОЛЖНО быть добыто на данный момент с начала работы
        int expectedMined = totalStoneCount - Mathf.CeilToInt(stoneHealth / timeToMineOneStone);

        // Если добыт новый камень
        if (expectedMined > stoneMinedByUnit && currentStoneLeft > 0)
        {
            stoneMinedByUnit++;
            currentStoneLeft--; // Уменьшаем количество оставшейся породы в жиле

            GettingResources backpack = attacker.GetComponent<GettingResources>();
            if (backpack != null)
            {
                backpack.Stone++; // Добавляем ровно 1 камень в рюкзак рабочего
                Debug.Log($"Рабочий {attacker.name} добыл камень! В рюкзаке: {backpack.Stone}. Осталось в жиле: {currentStoneLeft}");
            }
        }

        // Если порода в жиле полностью закончилась
        if (stoneHealth <= 0 || currentStoneLeft <= 0)
        {
            FinishMining(attacker);
        }
    }

    private void FinishMining(GameObject attacker)
    {
        if (attacker != null)
        {
            ResourcesMove targetMove = attacker.GetComponent<ResourcesMove>();
            if (targetMove != null)
            {
                // Отправляем рабочего разгружаться на склад камня (StoneHouse),
                // даже если он нёс неполный рюкзак
                targetMove.SetInventoryFull();
            }
        }

        Debug.Log("Жила полностью истощена! Каменщик унес оставшийся камень на склад камня.");

        // Жила больше ничего не хранит — просто исчезает
        Destroy(gameObject);
    }
}