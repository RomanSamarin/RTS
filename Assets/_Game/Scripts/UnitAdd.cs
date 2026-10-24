using UnityEngine;

public class UnitAdd : MonoBehaviour
{
    private void Start()
    {
        
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.RegisterUnit(this);
        }
    }

    private void OnDisable()
    {
       
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.UnregisterUnit(this);
        }
    }
}
