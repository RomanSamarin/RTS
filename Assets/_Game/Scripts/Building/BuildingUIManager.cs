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

        HideAllMenus();
    }

    public void ShowMenuForBuilding(MonoBehaviour building)
    {
        Debug.Log($"ShowMenuForBuilding вызван для: {building.gameObject.name}");
        
        HideCurrentMenu();
        currentBuilding = building;

        if (building is Barracks barracks)
        {
            currentMenu = barracksMenu;
            Debug.Log($"Показываем меню казармы для: {barracks.gameObject.name}");
        }
        else if (building is ResourceProducerAuto producer)
        {
            currentMenu = producer.IsFarm ? farmMenu : mineMenu;
        }
        else
        {
            Debug.LogWarning($"Неизвестный тип здания: {building.GetType()}");
            return;
        }

        if (currentMenu != null)
        {
            currentMenu.SetActive(true);
            
            // Позиционируем меню над зданием
            Vector3 screenPos = Camera.main.WorldToScreenPoint(building.transform.position);
            currentMenu.transform.position = screenPos + new Vector3(0, 100, 0);
        }
    }

    public void OnTrainUnitButton()
    {
        Debug.Log("Нажата кнопка Train Unit");
        
        if (currentBuilding is Barracks barracks)
        {
            Debug.Log($"Вызываем TrainUnit для казармы: {barracks.gameObject.name}");
            barracks.TrainUnit();
        }
        else
        {
            Debug.LogWarning("Текущее здание не является казармой!");
        }

        HideCurrentMenu();
    }

    public void HideCurrentMenu()
    {
        if (currentMenu != null)
        {
            currentMenu.SetActive(false);
            Debug.Log($"Меню скрыто: {currentMenu.name}");
        }
        currentMenu = null;
        currentBuilding = null;
    }

    private void HideAllMenus()
    {
        if (barracksMenu != null) barracksMenu.SetActive(false);
        if (farmMenu != null) farmMenu.SetActive(false);
        if (mineMenu != null) mineMenu.SetActive(false);
    }

    private void Update()
    {
        // Закрываем меню при клике вне UI
        if (Input.GetMouseButtonDown(0))
        {
            if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                HideCurrentMenu();
            }
        }
    }
}