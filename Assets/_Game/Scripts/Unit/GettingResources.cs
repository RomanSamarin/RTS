using _Game.Scripts.UI;
using UnityEngine;

public class GettingResources : MonoBehaviour
{
    // Start is called before the first frame update
    public int Wood;
    public int Stone;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TakeResource()
    {
        ResourcesManager.Instance.TakeResource(this);
    }
}
