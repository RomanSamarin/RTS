using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Универсальный найм для любого здания.
///
/// Вешается на здание. В инспекторе выбирается профессия, на которую здание
/// нанимает безработных жителей (роль Villager), и лимит рабочих.
///
/// Кнопки «Нанять безработного» / «Уволить текущего» и текст счётчика
/// назначаются вручную в полях ниже (например, кнопки из меню этого здания).
///
/// Найм — это смена роли Villager → выбранная профессия. Увольнение —
/// обратно в Villager. Каждый нанятый рабочий привязывается к этому зданию
/// и возвращается сюда после добычи, а строитель — когда нет работы.
///
/// Смена модели: в списке «Profession Models» задаётся префаб юнита для
/// каждой профессии, а в «Villager Prefab» — префаб обычного жителя.
/// При найме/увольнении юнит заменяется нужным префабом на том же месте
/// (дом, место работы, точка строя и здоровье переносятся).
/// Если префаб для профессии не задан — меняется только роль.
/// </summary>
[DisallowMultipleComponent]
public class WorkplaceHiring : MonoBehaviour
{
    /// <summary>Связка «профессия → префаб юнита».</summary>
    [System.Serializable]
    public class ProfessionModel
    {
        [Tooltip("Профессия, для которой используется префаб")]
        public ResourcesMove.UnitRole role = ResourcesMove.UnitRole.Lumberjack;

        [Tooltip("Префаб юнита этой профессии")]
        public GameObject prefab;
    }

    [Header("Профессия")]
    [Tooltip("На какую профессию здание нанимает безработных жителей")]
    public ResourcesMove.UnitRole profession = ResourcesMove.UnitRole.Lumberjack;

    [Tooltip("Сколько рабочих может быть нанято в это здание. 0 — нанимать нельзя")]
    [Min(0)]
    public int maxWorkers = 2;

    [Tooltip("Радиус, в котором ищутся безработные жители. 0 — искать по всей карте")]
    [Min(0f)]
    public float hireRadius = 0f;

    [Header("Модели юнитов")]
    [Tooltip("Префаб юнита для каждой профессии этого здания. " +
             "Не задан префаб — меняется только роль, модель остаётся прежней")]
    public List<ProfessionModel> professionModels = new List<ProfessionModel>();

    [Tooltip("Префаб безработного жителя — на него юнит меняется при увольнении")]
    public GameObject villagerPrefab;

    [Header("Кнопки (назначьте вручную)")]
    [Tooltip("Кнопка «Нанять безработного». Можно также повесить WorkplaceHiring.Hire() на OnClick")]
    public Button hireButton;

    [Tooltip("Кнопка «Уволить текущего». Можно также повесить WorkplaceHiring.Fire() на OnClick")]
    public Button fireButton;

    [Tooltip("Текст счётчика «Рабочие: 1/2». Необязательно")]
    public TMP_Text workersText;

    [Header("Отладка")]
    [Tooltip("Писать в Console, кого наняли/уволили и почему не получилось")]
    public bool logActions = true;

    // Нанятые этим зданием рабочие
    private readonly List<ResourcesMove> workers = new List<ResourcesMove>();

    private float nextRefreshTime;

    /// <summary>Сколько рабочих сейчас нанято (с учётом уволенных/погибших).</summary>
    public int WorkerCount
    {
        get
        {
            CleanupWorkers();
            return workers.Count;
        }
    }

    /// <summary>Лимит рабочих этого здания.</summary>
    public int MaxWorkers => Mathf.Max(0, maxWorkers);
    public IReadOnlyList<ResourcesMove> Workers { get { CleanupWorkers(); return workers; } }

    // ------------------------------------------------------------------ Жизненный цикл

    private void Start()
    {
        var production = GetComponent<RecipeBuilding>();
        if (production != null) profession = production.Profession;
        var gathering = GetComponent<GatheringHouse>();
        if (gathering != null) profession = gathering.Profession;
        BindButtons();
        UpdateCounter();
    }

    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        // Раз в полсекунды обновляем счётчик: рабочий мог погибнуть,
        // уволиться через другой скрипт или сменить профессию вручную.
        if (Time.time < nextRefreshTime)
            return;

        nextRefreshTime = Time.time + 0.5f;
        UpdateCounter();
    }

    // ------------------------------------------------------------------ Публичное API

    /// <summary>Кнопка «Нанять безработного».</summary>
    public void Hire()
    {
        TryHire();
    }

    /// <summary>Кнопка «Уволить текущего».</summary>
    public void Fire()
    {
        TryFire();
    }

    /// <summary>
    /// Нанять ближайшего безработного жителя. Возвращает false,
    /// если достигнут лимит или свободных жителей нет.
    /// </summary>
    public bool TryHire()
    {
        if(!BuildingRoadAccess.Allows(this)){Log("нет дороги к зданию");return false;}
        var production = GetComponent<RecipeBuilding>();
        if (production != null) profession = production.Profession;
        var gathering = GetComponent<GatheringHouse>();
        if (gathering != null) profession = gathering.Profession;
        CleanupWorkers();

        if (profession == ResourcesMove.UnitRole.Villager)
        {
            Log("профессия не выбрана: в инспекторе указан Villager (житель) — нанимать некуда");
            return false;
        }

        if (MaxWorkers <= 0)
        {
            Log("лимит рабочих равен 0 — нанимать нельзя (Max Workers)");
            UpdateCounter();
            return false;
        }

        if (workers.Count >= MaxWorkers)
        {
            Log($"лимит рабочих достигнут ({workers.Count}/{MaxWorkers})");
            UpdateCounter();
            return false;
        }

        ResourcesMove villager = FindVillager();
        if (villager == null)
        {
            Log(hireRadius > 0f
                ? $"в радиусе {hireRadius:0.#} м нет безработных жителей"
                : "на карте нет безработных жителей");
            return false;
        }

        ResourcesMove worker = ReplaceUnitModel(
            villager, profession, gameObject);

        if (worker == null)
        {
            Log("не удалось заменить модель юнита — найм отменён");
            return false;
        }

        workers.Add(worker);

        Log($"нанят {worker.name} → {RoleName(profession)} ({workers.Count}/{MaxWorkers})");
        UpdateCounter();
        return true;
    }

    /// <summary>
    /// Уволить последнего нанятого рабочего (он снова становится жителем).
    /// </summary>
    public bool TryFire()
    {
        CleanupWorkers();

        if (workers.Count == 0)
        {
            Log("нет нанятых рабочих — увольнять некого");
            UpdateCounter();
            return false;
        }

        int last = workers.Count - 1;
        ResourcesMove worker = workers[last];
        if (worker != null && !worker.CanChangeRole)
        {
            Log("сначала разгрузите рабочего — увольнение отменено");
            return false;
        }
        workers.RemoveAt(last);

        string workerName = worker != null ? worker.name : "рабочий";
        ReplaceUnitModel(worker, ResourcesMove.UnitRole.Villager, null);

        Log($"уволен {workerName} — снова безработный житель ({workers.Count}/{MaxWorkers})");
        UpdateCounter();
        return true;
    }

    /// <summary>Принудительно обновить текст счётчика.</summary>
    public void UpdateCounter()
    {
        CleanupWorkers();

        if (workersText != null)
            workersText.text = $"Рабочие: {workers.Count}/{MaxWorkers}";
    }

    // ------------------------------------------------------------------ Смена модели

    /// <summary>
    /// Меняет роль юнита и, если для этой роли задан префаб, заменяет юнита
    /// его префабом на том же месте. Возвращает компонент ResourcesMove
    /// актуального юнита (исходный, если замена не делалась).
    /// </summary>
    private ResourcesMove ReplaceUnitModel(
        ResourcesMove current, ResourcesMove.UnitRole newRole, GameObject workplace)
    {
        if (current == null || !current.CanChangeRole)
            return null;

        GameObject prefab = FindModelPrefab(newRole);

        if (prefab == null)
        {
            // Префаб не задан — просто меняем профессию, модель остаётся прежней.
            current.SetRole(newRole, workplace);
            return current;
        }

        Transform oldTransform = current.transform;
        Vector3 position = oldTransform.position;
        Quaternion rotation = oldTransform.rotation;
        Transform parent = oldTransform.parent;

        // Дом, точка строя и здоровье переносятся на новый префаб,
        // чтобы смена профессии не выселяла юнита и не лечила его.
        HousesScript house = HousesScript.FindHouseOf(current.gameObject);
        if (house != null)
            house.Evict(current.gameObject);

        bool hasFormationPoint = FormationHold.TryGetPoint(current.gameObject, out Vector3 formationPoint);
        if (hasFormationPoint)
            FormationHold.Clear(current.gameObject);

        Unit oldUnit = current.GetComponent<Unit>();
        float healthRatio = 1f;
        if (oldUnit != null && oldUnit.unitMaxHealth > 0f)
            healthRatio = Mathf.Clamp01(oldUnit.unitHealth / oldUnit.unitMaxHealth);

        GameObject instance = Instantiate(prefab, position, rotation, parent);
        instance.name = prefab.name;

        ResourcesMove replacement = instance.GetComponent<ResourcesMove>();
        if (replacement == null)
            replacement = instance.GetComponentInChildren<ResourcesMove>();

        if (replacement == null)
        {
            Debug.LogWarning(
                $"[Hiring] {name}: в префабе «{prefab.name}» нет ResourcesMove — " +
                "замена модели отменена, меняю только роль", this);

            Destroy(instance);
            current.SetRole(newRole, workplace);
            return current;
        }

        replacement.SetRole(newRole, workplace);

        if (house != null)
        {
            house.TrySettle(instance);
            if (!house.IsResident(instance))
                HousesScript.TrySettleAutomatically(instance);
        }

        if (hasFormationPoint)
            FormationHold.SetPoint(instance, formationPoint);

        if (healthRatio < 0.999f)
            StartCoroutine(RestoreHealthNextFrame(instance.GetComponent<Unit>(), healthRatio));

        TransferSelection(current.gameObject, instance);

        Destroy(current.gameObject);
        return replacement;
    }

    /// <summary>Префаб юнита для роли (для Villager — отдельное поле).</summary>
    private GameObject FindModelPrefab(ResourcesMove.UnitRole role)
    {
        if (role == ResourcesMove.UnitRole.Villager)
            return villagerPrefab;

        if (professionModels == null)
            return null;

        foreach (ProfessionModel model in professionModels)
        {
            if (model == null || model.prefab == null || model.role != role)
                continue;

            return model.prefab;
        }

        return null;
    }

    /// <summary>
    /// Unit.Start() при появлении выставляет полное здоровье — возвращаем
    /// прежний запас на следующем кадре, после Start.
    /// </summary>
    private IEnumerator RestoreHealthNextFrame(Unit unit, float healthRatio)
    {
        yield return null;

        if (unit == null)
            yield break;

        unit.unitHealth = unit.unitMaxHealth * healthRatio;

        if (unit.heathTracker != null)
            unit.heathTracker.UpdateSliderValue(unit.unitHealth, unit.unitMaxHealth);
    }

    /// <summary>Сохраняет выделение: старый объект удаляется, новый остаётся выбранным.</summary>
    private static void TransferSelection(GameObject from, GameObject to)
    {
        if (to == null || ManagerGO.Instance == null)
            return;

        List<GameObject> selected = ManagerGO.Instance.unitsSelected;
        if (selected == null)
            return;

        int index = selected.IndexOf(from);
        if (index < 0)
            return;

        selected[index] = to;

        Transform indicator = to.transform.Find("Indicator");
        if (indicator != null)
            indicator.gameObject.SetActive(true);

        GOScript script = to.GetComponent<GOScript>();
        if (script != null)
            script.enabled = true;
    }

    // ------------------------------------------------------------------ Поиск и учёт

    private ResourcesMove FindVillager()
    {
        ResourcesMove[] all = FindObjectsOfType<ResourcesMove>();

        ResourcesMove best = null;
        float bestDistance = Mathf.Infinity;
        Vector3 origin = transform.position;

        foreach (ResourcesMove candidate in all)
        {
            if(candidate!=null&&!FactionMember.CanUse(candidate.gameObject,gameObject))continue;
            if (candidate == null || !candidate.IsUnemployed || !candidate.CanChangeRole)
                continue;

            float distance = Vector3.Distance(origin, candidate.transform.position);

            if (hireRadius > 0f && distance > hireRadius)
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Убирает из списка погибших, уволенных и переведённых на другую
    /// профессию/в другое здание рабочих.
    /// </summary>
    private void CleanupWorkers()
    {
        for (int i = workers.Count - 1; i >= 0; i--)
        {
            ResourcesMove worker = workers[i];

            if (worker == null)
            {
                workers.RemoveAt(i);
                continue;
            }

            if (worker.Role != profession||!FactionMember.CanUse(worker.gameObject,gameObject))
            {
                workers.RemoveAt(i);
                continue;
            }

            if (worker.AssignedWorkplace != gameObject)
                workers.RemoveAt(i);
        }
    }

    // ------------------------------------------------------------------ UI

    private void BindButtons()
    {
        if (hireButton != null)
        {
            hireButton.onClick.RemoveListener(Hire);
            hireButton.onClick.AddListener(Hire);
        }

        if (fireButton != null)
        {
            fireButton.onClick.RemoveListener(Fire);
            fireButton.onClick.AddListener(Fire);
        }
    }

    // ------------------------------------------------------------------ Логи

    private void Log(string message)
    {
        if (!logActions)
            return;

        Debug.Log($"[Hiring] {name}: {message}", this);
    }

    private static string RoleName(ResourcesMove.UnitRole role)
    {
        switch (role)
        {
            case ResourcesMove.UnitRole.Lumberjack: return "лесоруб";
            case ResourcesMove.UnitRole.Carrier: return "носильщик";
            case ResourcesMove.UnitRole.Fisher: return "рыбак";
            case ResourcesMove.UnitRole.StoneMason: return "каменщик";
            case ResourcesMove.UnitRole.Builder: return "строитель";
            case ResourcesMove.UnitRole.Villager: return "житель";
            case ResourcesMove.UnitRole.Miner: return "шахтёр";
            case ResourcesMove.UnitRole.Farmer: return "фермер";
            case ResourcesMove.UnitRole.Miller: return "мельник";
            case ResourcesMove.UnitRole.Baker: return "пекарь";
            case ResourcesMove.UnitRole.Smoker: return "коптильщик";
            case ResourcesMove.UnitRole.Blacksmith: return "кузнец";
            case ResourcesMove.UnitRole.Craftsman: return "мастер";
            case ResourcesMove.UnitRole.Shipwright: return "корабельщик";
            case ResourcesMove.UnitRole.PortWorker: return "портовый рабочий";
            case ResourcesMove.UnitRole.Watchman: return "дозорный";
            case ResourcesMove.UnitRole.Hunter: return "охотник";
            case ResourcesMove.UnitRole.BerryGatherer: return "сборщик ягод";
            default: return role.ToString();
        }
    }
}
