using UnityEngine;
using _Game.Scripts.UI;

public class ResourceProducerAuto : MonoBehaviour
{
    [Header("Production Settings")]
    public int WoodPerTick = 0;
    public int StonePerTick = 0;
    public int WheatPerTick = 5;
    public float ProductionTime = 5f; // секунды между начислениями

    [Header("Type")]
    public bool IsFarm = true; // true = ферма, false = шахта

    // Глобальный таймер для синхронизации всех зданий
    private static float globalTimer = 0f;
    private static float lastTickTime = 0f;

    private void Update()
    {
        // Защита, если ResourcesManager ещё не создан
        if (ResourcesManager.Instance == null) return;

        // Считаем глобальный таймер
        globalTimer += Time.deltaTime;

        // Если прошло время ProductionTime с последнего начисления
        if (globalTimer - lastTickTime >= ProductionTime)
        {
            lastTickTime = globalTimer;
            ProduceResources();
        }
    }

    private void ProduceResources()
    {
        // Создаём объект GettingResources и добавляем в ResourcesManager
        GettingResources res = new GettingResources
        {
            Wood = WoodPerTick,
            Stone = StonePerTick,
            Wheat = WheatPerTick
        };

        res.TakeResource();

        // Для отладки
        Debug.Log($"Produced resources: Wood={WoodPerTick}, Stone={StonePerTick}, Wheat={WheatPerTick}");
    }

    private void OnMouseDown()
    {
        // Можно оставить открытие меню для апгрейдов
        BuildingUIManager.Instance.ShowMenuForBuilding(this);
    }
}