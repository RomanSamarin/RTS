using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class HowManyUnitUi : MonoBehaviour
{
    // Start is called before the first frame update
    public TMP_Text UnitManyText;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        UnitManyText.text = UnitManager.Instance.UnitAmount.ToString();
    }
}
