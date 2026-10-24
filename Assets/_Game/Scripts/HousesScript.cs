using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HousesScript : MonoBehaviour
{
    // Start is called before the first frame update
    public int UnitHouse;
    void Start()
    {
        UnitManager.Instance.UnitMax +=UnitHouse;
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void Awake()
    {
        UnitManager.Instance.UnitMax +=UnitHouse;
    }
    void OnDestroy()
    {
        UnitManager.Instance.UnitMax -=UnitHouse;
    }
}
