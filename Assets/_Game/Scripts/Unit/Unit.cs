using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    // Start is called before the first frame update
    public float unitHealth;
    public float unitMaxHealth;
    public HealthTracker heathTracker;
    private GettingResources _gettingResources;

    void Start()
    {
        if (_gettingResources == null) _gettingResources = GetComponent<GettingResources>();
        ManagerGO.Instance.allUnitsList.Add(gameObject);
        unitHealth = unitMaxHealth;
        UpdateHealth();
    }
    private void OnDestroy()
    {
        ManagerGO.Instance.allUnitsList.Remove(gameObject);
    }
    private void UpdateHealth()
    {
        heathTracker.UpdateSliderValue(unitHealth, unitMaxHealth);
        if (unitHealth <= 0)
        {
        
        }
    }
    public void Destroy()
    {
        Destroy(gameObject);
        if (_gettingResources != null) _gettingResources.TakeResource();
    }

    internal void TakeDamage(int damageToInflict)
    {
        unitHealth -= damageToInflict;
        UpdateHealth();
    }
}


