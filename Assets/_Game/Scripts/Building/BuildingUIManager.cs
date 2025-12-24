using UnityEngine;

public class BuildingUIManager : MonoBehaviour
{
    public static BuildingUIManager Instance;

    public GameObject barracksMenu;
    public GameObject farmMenu;
    public GameObject mineMenu;

    private GameObject currentMenu;
    private MonoBehaviour currentBuilding;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        HideCurrentMenu();
    }

    public void ShowMenuForBuilding(MonoBehaviour building)
    {
        HideCurrentMenu();
        currentBuilding = building;

        if (building is Barracks)
            currentMenu = barracksMenu;
        else if (building is ResourceProducerAuto producer)
            currentMenu = producer.IsFarm ? farmMenu : mineMenu;
        else
            return;

        currentMenu.SetActive(true);
    }

    public void HideCurrentMenu()
    {
        if (currentMenu != null)
            currentMenu.SetActive(false);
        currentMenu = null;
        currentBuilding = null;
    }

    public void OnTrainUnitButton()
    {
        if (currentBuilding is Barracks barracks)
            barracks.TrainUnit();

        HideCurrentMenu();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                HideCurrentMenu();
        }
    }
}