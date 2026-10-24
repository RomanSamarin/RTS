using UnityEngine;

public class Sawmill : MonoBehaviour
{
    [Header("Запасы дерева на лесопилке")]
    public int storedWood = 0; 

    public void AddWood(int amount)
    {
        storedWood += amount;
        Debug.Log($"На лесопилку поступило дерево. Всего тут: {storedWood}");
    }

    public int ExtractWood(int maxAmount)
    {
        int amountToTake = Mathf.Min(storedWood, maxAmount);
        storedWood -= amountToTake;
        return amountToTake;
    }
}
