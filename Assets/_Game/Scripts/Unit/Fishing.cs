using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fishing : MonoBehaviour
{
    [Header("Запасы рыбы в рыболовне")]
    public int storeFish = 0; 

    // Метод для добавления рыбы (вызывает рыбак)
    public void AddFish(int amount)
    {
        storeFish += amount;
        Debug.Log($"В рыболовню поступила рыба. Всего тут: {storeFish}");
    }

    // Метод для извлечения рыбы (вызывает носильщик)
    public int ExtractFish(int maxAmount)
    {
        int amountToTake = Mathf.Min(storeFish, maxAmount);
        storeFish -= amountToTake;
        return amountToTake;
    }
}
