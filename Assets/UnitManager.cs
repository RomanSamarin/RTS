using System.Collections;
using System.Collections.Generic;
using System.Resources;
using UnityEngine;

public class UnitManager : MonoBehaviour
{
    public static UnitManager Instance { get; private set; }
    public  int UnitAmount;
    public int FoodUnitEat;
    public int UnitMax;
    
    // ИСПРАВЛЕНО: Тип списка должен быть строго UnitAdd с обеих сторон
    public List<UnitAdd> allUnits = new List<UnitAdd>();
    


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); 
        }
    }

    public void RegisterUnit(UnitAdd unit)
    {
        if (!allUnits.Contains(unit))
        {
            allUnits.Add(unit);
            UnitAmount = allUnits.Count;
            FoodUnitEat = allUnits.Count;
        }
    }

    public void UnregisterUnit(UnitAdd unit)
    {
        if (allUnits.Contains(unit))
        {
            allUnits.Remove(unit);
            UnitAmount -= allUnits.Count;
            FoodUnitEat -= allUnits.Count;
        }
    }
    void Start()
    {
        StartCoroutine(RepeatEveryMinute());
        
    }
    IEnumerator RepeatEveryMinute()
    {
        while (true)
        {
            EatUnitFood();

            yield return new WaitForSeconds(60f); 
        }
    }
    public void EatUnitFood()
    {
        _Game.Scripts.UI.ResourcesManager.Instance.wheat -= FoodUnitEat;
        _Game.Scripts.UI.ResourcesManager.Instance.Spend(0, 0, FoodUnitEat, 0);
    }


    public int UnitsCount => allUnits.Count;
}
