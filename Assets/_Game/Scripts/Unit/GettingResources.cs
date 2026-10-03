using _Game.Scripts.UI;
using UnityEngine;

public class GettingResources : MonoBehaviour
{
    [Header("Текущий рюкзак юнита")]
    public int Wood;
    public int Stone;
    public int Wheat;
    public int Fish;

    // Метод перекладывания ресурсов из объекта в рюкзак рабочего
    public void TakeResource(Unit targetUnit)
    {
        Wood = targetUnit.woodReward;
        Stone = targetUnit.stoneReward;
        Wheat = targetUnit.wheatReward;
        Fish = targetUnit.fishReward;
    }
}
