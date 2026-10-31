using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Профессия «Строитель».
///
/// Вешается на юнита-рабочего (можно на копию префаба фермера).
/// Строитель сам ищет ближайшую стройплощадку (ConstructionSite),
/// подходит к ней, проигрывает анимацию работы и ускоряет стройку.
/// Когда площадка достроена — ищет следующую.
///
/// Что делает компонент:
///   • переключает ResourcesMove в роль Builder (чтобы рабочий не бегал за ресурсами);
///   • отключает авто-захват целей у AttackController (строитель не отвлекается на деревья);
///   • останавливает работу, если игрок отдал приказ идти или атаковать.
///
/// Ничего вызывать вручную не нужно. Достаточно добавить компонент
/// на префаб рабочего и назначить этот префаб в казарму (Object To Spawn).
/// </summary>
[DisallowMultipleComponent]
public class Builder : MonoBehaviour
{
    [Header("Работа")]
    [Tooltip("С какой дистанции строитель начинает работать")]
    public float workDistance = 1.6f;

    [Tooltip("Скорость работы. 1 — как один обычный строитель. " +
             "Прогресс площадки суммируется по всем строителям")]
    public float workSpeed = 1f;

    [Tooltip("Максимальная дистанция поиска стройки")]
    public float searchRadius = 250f;

    [Tooltip("Как часто искать новую стройку, если текущей нет, сек")]
    public float retargetInterval = 0.8f;

    [Tooltip("Как часто обновлять путь к площадке, сек")]
    public float repathInterval = 0.5f;

    [Tooltip("Автоматически искать стройку, когда строитель свободен")]
    public bool autoFindWork = true;

    [Header("Анимация")]
    [Tooltip("Bool-параметр Animator, который включается во время работы. " +
             "По умолчанию isAttacking — он запускает клип удара/рубки")]
    public string workParameter = "isAttacking";

    [Tooltip("Скорость поворота к стройке")]
    public float turnSpeed = 8f;

    [Header("Отладка")]
    [Tooltip("Писать в Console, какую стройку нашёл строитель и почему не может идти")]
    public bool logState = true;

    private NavMeshAgent agent;
    private GOScript goScript;
    private AttackController attackController;
    private ResourcesMove resourcesMove;
    private Unit unit;
    private Animator animator;

    private int workHash;
    private bool hasWorkParameter;

    private ConstructionSite currentSite;
    private bool working;

    private float nextSearchTime;
    private float nextRepathTime;
    private float nextProblemLogTime;

    // Все живые строители на сцене — нужны только для подсказки игроку,
    // если он поставил здание, а строить некому.
    private static readonly List<Builder> allBuilders = new List<Builder>();

    /// <summary>Сколько строителей сейчас существует в сцене.</summary>
    public static int ActiveCount
    {
        get
        {
            for (int i = allBuilders.Count - 1; i >= 0; i--)
            {
                if (allBuilders[i] == null)
                    allBuilders.RemoveAt(i);
            }

            return allBuilders.Count;
        }
    }

    // ------------------------------------------------------------------ Свойства

    /// <summary>Скорость этого строителя (используется площадкой).</summary>
    public float WorkSpeed => Mathf.Max(0.05f, workSpeed);

    /// <summary>Строитель сейчас работает именно на этой площадке.</summary>
    public bool IsWorkingOn(ConstructionSite site)
    {
        return working && currentSite == site && site != null;
    }

    // ------------------------------------------------------------------ Unity

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        goScript = GetComponent<GOScript>();
        attackController = GetComponent<AttackController>();
        resourcesMove = GetComponent<ResourcesMove>();
        unit = GetComponent<Unit>();

        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Рабочий с этим компонентом — строитель, а не добытчик.
        if (resourcesMove != null)
            resourcesMove.role = ResourcesMove.UnitRole.Builder;

        // Строитель не должен сам цепляться к деревьям/врагам, иначе
        // будет уходить со стройки. Приказ игрока всё равно работает:
        // ManagerGO выставляет targetToAttack напрямую.
        if (attackController != null)
        {
            attackController.IsPlayer = false;
            attackController.targetToAttack = null;
        }

        workHash = Animator.StringToHash(workParameter);
        hasWorkParameter = HasBoolParameter(animator, workParameter);

        if (!allBuilders.Contains(this))
            allBuilders.Add(this);
    }

    private void OnDisable()
    {
        ReleaseSite();
    }

    private void Start()
    {
        // Строитель может быть добавлен позже Unit.Start (через роль в ResourcesMove),
        // поэтому на всякий случай гарантируем, что он попадёт в список выделения.
        if (ManagerGO.Instance != null && ManagerGO.Instance.allUnitsList != null &&
            !ManagerGO.Instance.allUnitsList.Contains(gameObject))
        {
            ManagerGO.Instance.allUnitsList.Add(gameObject);
        }
    }

    private void OnDestroy()
    {
        ReleaseSite();
        allBuilders.Remove(this);
    }

    private void Update()
    {
        // Мёртвый юнит ничего не строит.
        // Проверяем максимум здоровья: у рабочих он может быть не настроен (0),
        // и тогда unitHealth тоже 0 — но юнит при этом живой и должен строить.
        if (unit != null && unit.unitMaxHealth > 0f && unit.unitHealth <= 0f)
        {
            ReleaseSite();
            return;
        }

        // Игрок приказал идти — стройка прерывается.
        if (goScript != null && goScript.isCommandToMove)
        {
            ReleaseSite();
            return;
        }

        // Игрок приказал атаковать — стройка прерывается.
        if (attackController != null && attackController.targetToAttack != null)
        {
            ReleaseSite();
            return;
        }

        if (currentSite != null && !currentSite.IsAvailable)
            ReleaseSite();

        // Свободен — ищем ближайшую стройку.
        if (currentSite == null)
        {
            StopWorking();

            if (!autoFindWork)
                return;

            if (Time.time < nextSearchTime)
                return;

            nextSearchTime = Time.time + Mathf.Max(0.1f, retargetInterval);

            currentSite = ConstructionSite.FindNearest(transform.position, searchRadius);
            if (currentSite == null)
            {
                LogNoSite();
                return;
            }

            nextRepathTime = 0f;
            LogState($"нашёл стройку {currentSite.name} — иду строить");
        }

        if (agent == null || !agent.isOnNavMesh)
        {
            LogProblem(agent == null
                ? "не может идти: на юните нет NavMeshAgent"
                : "не может идти: NavMeshAgent не стоит на NavMesh");
            return;
        }

        Vector3 workPoint = currentSite.GetWorkPoint(transform.position);

        float distance = Vector3.Distance(transform.position, workPoint);
        if (distance > workDistance)
        {
            StopWorking();

            if (Time.time >= nextRepathTime)
            {
                nextRepathTime = Time.time + Mathf.Max(0.1f, repathInterval);
                MoveTowards(workPoint);
            }

            return;
        }

        // Дошёл — встаём и работаем.
        StopAgent();
        FaceTarget(currentSite.transform.position);

        if (!working)
        {
            if (!currentSite.RegisterBuilder(this))
            {
                // На площадке уже максимум строителей — ищем другую.
                ReleaseSite();
                nextSearchTime = Time.time + Mathf.Max(0.1f, retargetInterval);
                return;
            }

            working = true;
            SetWorkParameter(true);

            LogState($"начал строить {currentSite.name} " +
                     $"({currentSite.BuilderCount}/{currentSite.MaxBuilders} строителей)");
        }
    }

    // ------------------------------------------------------------------ Стройка

    /// <summary>Вызывается площадкой, когда стройка завершена.</summary>
    public void OnSiteCompleted(ConstructionSite site)
    {
        if (currentSite != site)
            return;

        working = false;
        currentSite = null;
        SetWorkParameter(false);

        nextSearchTime = Time.time + Mathf.Max(0.1f, retargetInterval);
    }

    /// <summary>Вызывается площадкой, если её снесли до окончания стройки.</summary>
    public void OnSiteRemoved(ConstructionSite site)
    {
        if (currentSite != site)
            return;

        working = false;
        currentSite = null;
        SetWorkParameter(false);

        nextSearchTime = Time.time + Mathf.Max(0.1f, retargetInterval);
    }

    private void ReleaseSite()
    {
        StopWorking();
        currentSite = null;
    }

    private void StopWorking()
    {
        if (working)
        {
            working = false;

            if (currentSite != null)
                currentSite.UnregisterBuilder(this);
        }

        SetWorkParameter(false);
    }

    // ------------------------------------------------------------------ Движение

    private void MoveTowards(Vector3 point)
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        agent.isStopped = false;
        agent.SetDestination(point);
    }

    private void StopAgent()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        if (agent.hasPath)
            agent.ResetPath();
    }

    // ------------------------------------------------------------------ Логи

    private void LogState(string message)
    {
        if (logState)
            Debug.Log($"[Builder] {name}: {message}", this);
    }

    private void LogProblem(string message)
    {
        if (!logState || Time.time < nextProblemLogTime)
            return;

        nextProblemLogTime = Time.time + 5f;
        Debug.LogWarning($"[Builder] {name}: {message}", this);
    }

    private void LogNoSite()
    {
        if (!logState || Time.time < nextProblemLogTime)
            return;

        int siteCount = ConstructionSite.AllSites.Count;
        if (siteCount == 0)
            return; // строек нет — это норма, не засоряем Console

        nextProblemLogTime = Time.time + 5f;
        Debug.LogWarning($"[Builder] {name}: на сцене есть стройки ({siteCount}), но ни одна не подходит — " +
                         "все заняты максимумом строителей или находятся дальше searchRadius.", this);
    }

    private void FaceTarget(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * Mathf.Max(0.1f, turnSpeed));
    }

    // ------------------------------------------------------------------ Анимация

    private void SetWorkParameter(bool value)
    {
        if (!hasWorkParameter || animator == null)
            return;

        animator.SetBool(workHash, value);
    }

    private static bool HasBoolParameter(Animator animator, string parameterName)
    {
        if (animator == null || animator.runtimeAnimatorController == null ||
            string.IsNullOrEmpty(parameterName))
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.name == parameterName)
                return true;
        }

        return false;
    }
}
