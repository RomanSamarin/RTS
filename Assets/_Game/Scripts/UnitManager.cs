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
        if(unit==null||!FactionMember.IsPlayer(unit.gameObject)||unit.GetComponent<CartHorse>()!=null||unit.GetComponent<FoodCart>()!=null||unit.GetComponent<SiegeLadderCart>()!=null||unit.GetComponent<Catapult>()!=null||unit.GetComponent<BatteringRam>()!=null)return;
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
            UnitAmount = allUnits.Count;
            FoodUnitEat = allUnits.Count;
        }
    }
    // Оставлено для совместимости с событиями сцены. Питание здесь отключено.
    public void EatUnitFood()
    {
    }


    public int UnitsCount => allUnits.Count;
}
