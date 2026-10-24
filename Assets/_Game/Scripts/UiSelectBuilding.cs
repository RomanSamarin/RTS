using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class UiSelectBuilding : MonoBehaviour
{
    public GameObject UIPanel;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, 10f))
            {
                // Сравниваем transform объекта, в который попали, с transform этого скрипта
                if (hit.transform == this.transform)
                {
                    Debug.Log("Лучом я попал в собственное тело!");
                }
            }

                }
}
