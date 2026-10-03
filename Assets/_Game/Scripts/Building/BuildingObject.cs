using System.Collections;
using System.Collections.Generic;
using _Game.Scripts.UI;
using UnityEngine;

public class BuildingObject : MonoBehaviour
{
    [Header("Настройки строительства")]
    public LayerMask GroundLayer;
    public float SpeedMove = 15f;

    [Header("Стоимость строительства")]
    public int woodCost = 50;
    public int stoneCost = 50;
    public int wheatCost = 0;
    public int fishCost;

    // Счётчик коллайдеров, внутри которых мы сейчас находимся
    private int collidersOverlapping = 0; 

    // Свойство возвращает true, если здание ни с чем не пересекается
    public bool IsPlaceable => collidersOverlapping == 0;

    void Update()
    {
        // 1. Движение за мышкой по земле
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, GroundLayer))
        {
            transform.position = Vector3.Lerp(transform.position, hit.point, SpeedMove * Time.deltaTime);
        }

        // 2. Поворот здания
        if (Input.GetKeyDown(KeyCode.R))
        {
            transform.rotation *= Quaternion.Euler(0f, 90f, 0f);
        }

        // 3. Попытка установить здание на ЛКМ
        if (Input.GetMouseButtonDown(0))
        {
            // Если мы внутри другого объекта — блокируем установку
            if (!IsPlaceable)
            {
                Debug.LogWarning("Нельзя установить здание здесь: Место занято!");
                return; 
            }

            // Проверяем и списываем ресурсы
            if (ResourcesManager.Instance != null && ResourcesManager.Instance.HasEnough(woodCost, stoneCost, wheatCost, fishCost))
            {
                ResourcesManager.Instance.Spend(woodCost, stoneCost, wheatCost,fishCost);
                
                // Делаем коллайдер твердым (не триггером), чтобы другие здания теперь натыкались на него
                Collider col = GetComponent<Collider>();
                if (col != null) col.isTrigger = false;

                // Отключаем этот скрипт, фиксируя здание на земле
                this.enabled = false;
                Debug.Log("Здание успешно построено!");
            }
            else
            {
                Debug.LogWarning("Недостаточно ресурсов для завершения строительства!");
            }
        }
    }

    // Срабатывает, когда призрачное здание наезжает на чужой коллайдер
    private void OnTriggerEnter(Collider other)
    {
        // ИСПРАВЛЕНИЕ: Игнорируем любые невидимые триггеры (например, большие круги атаки юнитов)
        if (other.isTrigger) 
        {
            return;
        }

        // Игнорируем землю (проверяем по слою), считаем только твердые объекты
        if (((1 << other.gameObject.layer) & GroundLayer) == 0)
        {
            collidersOverlapping++;
            Debug.Log($"Внутри объекта: {other.gameObject.name}. Всего пересечений: {collidersOverlapping}");
        }
    }

    // Срабатывает, когда призрачное здание съезжает с чужого коллайдера
    private void OnTriggerExit(Collider other)
    {
        // ИСПРАВЛЕНИЕ: Точно так же игнорируем триггеры при выходе
        if (other.isTrigger) 
        {
            return;
        }

        if (((1 << other.gameObject.layer) & GroundLayer) == 0)
        {
            collidersOverlapping = Mathf.Max(0, collidersOverlapping - 1);
            Debug.Log($"Покинули объект: {other.gameObject.name}. Осталось пересечений: {collidersOverlapping}");
        }
    }
}
