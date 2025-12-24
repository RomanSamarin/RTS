using _Game.Scripts.UI;
using UnityEngine;

public class GettingResources : MonoBehaviour
{
    public int Wood;
    public int Stone;
    public int Wheat;

    void Start() { }

    void Update() { }

    public void TakeResource()
    {
        ResourcesManager.Instance.TakeResource(this);
    }
}