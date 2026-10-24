using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TimeUiScript : MonoBehaviour
{
    // Start is called before the first frame updatesss
    public bool IsPause = false;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void OnButtonTime2x()
    {
        Time.timeScale= 2.0f;
    }
    public void OnButtonTime4x()
    {
        Time.timeScale= 4.0f;
    }public void OnButtonPause()
    {
        if (IsPause == false)
        {
            Time.timeScale= 0f;
            IsPause = true;
        }
        else
        {
            IsPause = false;
            Time.timeScale = 1f;
        }
    }
}
