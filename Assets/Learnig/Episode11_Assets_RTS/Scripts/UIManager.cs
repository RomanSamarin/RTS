using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public Button buildButton;
    public PlacementSystem placement;

    private void Start()
    {
        if (buildButton != null && placement != null)
            buildButton.onClick.AddListener(() => Construct(0));
    }

    private void Construct(int id)
    {
        Debug.Log("clicked");
        placement.StartPlacement(id);
    }
}
