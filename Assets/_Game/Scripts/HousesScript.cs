using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Дом: даёт лимит юнитов (UnitHouse) и хранит список заселённых жителей.
///
/// Заселение автоматическое: все юниты игрока (рабочие и бойцы) сами
/// распределяются по ближайшим домам, где есть свободное место.
/// Список Residents виден в инспекторе; для ручного управления есть
/// TrySettle() / Evict() / ClearResidents().
/// Лимит дома — UnitHouse: больше этого числа юнитов в дом не заселится.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingHealth))]
public class HousesScript : MonoBehaviour
{
    [Header("Вместимость")]
    [Tooltip("Сколько юнитов может жить в этом доме")]
    public int UnitHouse;

    [Tooltip("Автоматически заселять юнитов игрока в этот дом")]
    public bool autoSettle = true;

    [Header("Жители")]
    [Tooltip("Юниты, заселённые в дом. Заполняется автоматически, но можно править и вручную")]
    [SerializeField] private List<GameObject> residents = new List<GameObject>();

    // Все дома на сцене — чтобы юнит не заселился в два дома сразу
    // и чтобы новый дом мог принять уже существующих юнитов.
    private static readonly List<HousesScript> allHouses = new List<HousesScript>();
    private static float nextAutoSettleTime;

    // Добавляем лимит ровно один раз и запоминаем это,
    // чтобы при уничтожении дома снять ровно столько же.
    private bool housingRegistered;

    private float nextCleanupTime;

    private void Awake()
    {
        // Дом — тоже здание: без BuildingHealth он не попадёт
        // в список BuildingManager.AllBuildings и его нельзя будет разрушить.
        if (GetComponent<BuildingHealth>() == null)
            gameObject.AddComponent<BuildingHealth>();
    }

    /// <summary>Список заселённых юнитов (только для чтения).</summary>
    public IReadOnlyList<GameObject> Residents => residents;

    /// <summary>Сколько юнитов сейчас заселено.</summary>
    public int ResidentCount => residents.Count;

    /// <summary>Максимум жителей по настройке дома.</summary>
    public int MaxResidents => Mathf.Max(0, UnitHouse);

    /// <summary>Сколько ещё можно заселить.</summary>
    public int FreePlaces => Mathf.Max(0, MaxResidents - residents.Count);

    /// <summary>Есть ли свободное место.</summary>
    public bool HasFreePlace => residents.Count < MaxResidents;

    void OnEnable()
    {
        if (!allHouses.Contains(this))
            allHouses.Add(this);
    }

    void Start()
    {
        if (UnitManager.Instance == null) return;
        if(!FactionMember.IsPlayer(gameObject))return;

        UnitManager.Instance.UnitMax += UnitHouse;
        housingRegistered = true;
    }

    void Update()
    {
        if(SaveGameManager.IsLoading)return;
        // Раз в секунду убираем уничтоженных юнитов и заселяем новых.
        if (Time.time < nextCleanupTime)
            return;

        nextCleanupTime = Time.time + 1f;
        RemoveMissingResidents();

        // Статический таймер нужен, чтобы при нескольких домах
        // проход по юнитам выполнялся только один раз в секунду.
        if (Time.time >= nextAutoSettleTime)
        {
            nextAutoSettleTime = Time.time + 1f;
            AutoSettlePlayerUnits();
        }
    }

    void OnDestroy()
    {
        allHouses.Remove(this);

        if (!housingRegistered) return;
        if (UnitManager.Instance == null) return;

        UnitManager.Instance.UnitMax -= UnitHouse;
    }

    // ------------------------------------------------------------------ Автозаселение

    /// <summary>
    /// Заселить юнита в ближайший дом со свободным местом.
    /// Враги, деревья и юниты без признаков принадлежности игроку игнорируются.
    /// </summary>
    public static bool TrySettleAutomatically(GameObject unit)
    {
        if (unit == null || !IsPlayerUnit(unit))
            return false;

        if (IsSettled(unit))
            return false;

        HousesScript house = FindHouseWithFreePlace(unit.transform.position);
        if (house == null)
            return false;

        house.residents.Add(unit);
        return true;
    }

    /// <summary>Живёт ли юнит хотя бы в одном доме.</summary>
    public static bool IsSettled(GameObject unit)
    {
        return FindHouseOf(unit) != null;
    }

    /// <summary>Дом, в котором живёт юнит, или null, если он не заселён.</summary>
    public static HousesScript FindHouseOf(GameObject unit)
    {
        if (unit == null)
            return null;

        foreach (HousesScript house in allHouses)
        {
            if (house == null)
                continue;

            if (house.residents.Contains(unit))
                return house;
        }

        return null;
    }

    private static void AutoSettlePlayerUnits()
    {
        // Список выделения ManagerGO уже содержит только своих юнитов.
        if (ManagerGO.Instance != null && ManagerGO.Instance.allUnitsList != null)
        {
            List<GameObject> units = ManagerGO.Instance.allUnitsList;

            for (int i = 0; i < units.Count; i++)
                TrySettleAutomatically(units[i]);

            return;
        }

        // Запасной вариант, если менеджера юнитов на сцене нет.
        Unit[] sceneUnits = FindObjectsOfType<Unit>();
        foreach (Unit unit in sceneUnits)
        {
            if (unit != null)
                TrySettleAutomatically(unit.gameObject);
        }
    }

    /// <summary>
    /// Ближайший дом, который принимает юнитов и имеет свободное место.
    /// Возвращает null, если подходящих домов нет.
    /// </summary>
    public static HousesScript FindHouseWithFreePlace(Vector3 position)
    {
        HousesScript best = null;
        float bestDistance = Mathf.Infinity;

        foreach (HousesScript house in allHouses)
        {
            if (house == null || !house.isActiveAndEnabled)
                continue;

            if (!house.autoSettle || !house.HasFreePlace || !BuildingRoadAccess.Allows(house) || !FactionMember.IsPlayer(house.gameObject))
                continue;

            float distance = (house.transform.position - position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = house;
            }
        }

        return best;
    }

    /// <summary>Житель или боец местного игрока.</summary>
    private static bool IsPlayerUnit(GameObject unit)
    {
        if (unit == null)
            return false;
        if (unit.GetComponent<FoodCart>() != null || unit.GetComponent<SiegeLadderCart>() != null || unit.GetComponent<CartHorse>() != null) return false;

        return FactionMember.IsPlayer(unit);
    }

    // ------------------------------------------------------------------ Жители

    /// <summary>
    /// Заселить юнита в этот дом вручную. Если он жил в другом доме —
    /// автоматически выселяется оттуда. Возвращает false, если юнит пустой,
    /// уже живёт здесь или нет свободных мест.
    /// </summary>
    public bool TrySettle(GameObject unit)
    {
        if (unit == null)
            return false;
        if(!FactionMember.AreAllies(unit,gameObject))return false;
        if (unit.GetComponent<FoodCart>() != null || unit.GetComponent<SiegeLadderCart>() != null || unit.GetComponent<CartHorse>() != null) return false;

        RemoveMissingResidents();

        if (residents.Contains(unit))
            return false;

        if (!HasFreePlace)
            return false;

        RemoveFromOtherHouses(unit);
        residents.Add(unit);
        return true;
    }

    /// <summary>Выселить юнита из дома.</summary>
    public bool Evict(GameObject unit)
    {
        if (unit == null)
            return false;

        return residents.Remove(unit);
    }

    /// <summary>Выселить всех.</summary>
    public void ClearResidents()
    {
        residents.Clear();
    }

    /// <summary>Живёт ли юнит в этом доме.</summary>
    public bool IsResident(GameObject unit)
    {
        if (unit == null)
            return false;

        return residents.Contains(unit);
    }

    private static void RemoveFromOtherHouses(GameObject unit)
    {
        foreach (HousesScript house in allHouses)
        {
            if (house == null)
                continue;

            house.residents.Remove(unit);
        }
    }

    private void RemoveMissingResidents()
    {
        for (int i = residents.Count - 1; i >= 0; i--)
        {
            if (residents[i] == null)
                residents.RemoveAt(i);
        }
    }
}
