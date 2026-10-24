using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HousesScript : MonoBehaviour
{
    public int UnitHouse;

    // Добавляем лимит ровно один раз и запоминаем это,
    // чтобы при уничтожении дома снять ровно столько же.
    private bool housingRegistered;

    void Start()
    {
        if (UnitManager.Instance == null) return;

        UnitManager.Instance.UnitMax += UnitHouse;
        housingRegistered = true;
    }

    void OnDestroy()
    {
        if (!housingRegistered) return;
        if (UnitManager.Instance == null) return;

        UnitManager.Instance.UnitMax -= UnitHouse;
    }
}
