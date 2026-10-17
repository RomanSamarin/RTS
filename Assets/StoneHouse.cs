using UnityEngine;

// ВРЕМЕННЫЙ СКЛАД КАМНЯ: каменщик сдает сюда добытый камень,
// носильщик забирает его отсюда и относит на главный склад (Warehouse).
public class StoneHouse : MonoBehaviour
{
    [Header("Запасы камня на временном складе")]
    public int storedStone = 0;

    // Метод для КАМЕНЩИКА: сдает добытый камень на склад
    public void AddStone(int amount)
    {
        storedStone += amount;
        Debug.Log($"На склад камня поступило {amount} шт. Всего на складе: {storedStone}");
    }

    // Метод для НОСИЛЬЩИКА: забирает камень (до maxAmount шт.) и несет на главный склад
    public int ExtractStone(int maxAmount)
    {
        int amountToTake = Mathf.Min(storedStone, maxAmount);
        storedStone -= amountToTake;
        return amountToTake;
    }
}