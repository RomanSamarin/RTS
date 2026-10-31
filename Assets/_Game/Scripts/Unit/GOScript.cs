using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GOScript : MonoBehaviour
{
    public Camera camera;
    public NavMeshAgent agent;
    public LayerMask Ground;
    public bool isCommandToMove;

    private float baseSpeed;
    private float moveStartTime;
    private float savedStoppingDistance;
    private bool hasSavedStoppingDistance;

    [Tooltip("Насколько близко юнит должен подойти к точке приказа. " +
             "Меньше стандартного stoppingDistance, чтобы группа реально доходила до метки.")]
    public float moveStoppingDistance = 0.1f;

    [Tooltip("Сколько секунд юнит должен простоять на месте, чтобы приказ отправлялся заново")]
    public float stuckCheckTime = 0.7f;

    [Tooltip("Пауза между повторными отправками приказа застрявшему юниту")]
    public float stuckRepathInterval = 0.4f;

    private Vector3 currentDestination;
    private float lastProgressTime;
    private float nextRepathTime;
    private float lostPathTimer;
    private int lastCommandUpdateFrame = -1;

    // Скорость ниже ~0.1 м/с считается «стоим на месте»
    private const float StuckVelocitySqr = 0.01f;

    // Если пути нет так долго — точка недостижима, приказ снимаем
    private const float PathLostGiveUpTime = 2f;

    // Допуск по дистанции: агент останавливается примерно на stoppingDistance
    // от точки, и из-за покадровых колебаний remainingDistance может быть
    // чуть больше stoppingDistance. Без допуска приказ никогда не завершается.
    private const float ArrivalTolerance = 0.25f;

    // Если агент уже сам сбросил путь и стоит, но до точки осталось чуть-чуть —
    // считаем приказ выполненным. Иначе юнит бесконечно «доезжает» и дёргается.
    private const float CloseEnoughDistance = 0.6f;

    // Минимальное время с момента приказа — защита от ложного «дошёл»
    // в первом кадре, пока NavMesh ещё не посчитал путь.
    private const float MinMoveTime = 0.1f;

    private void Start()
    {
        camera = Camera.main;
        agent = GetComponent<NavMeshAgent>();
        
        if (agent != null)
        {
            baseSpeed = agent.speed;

            // Небольшой разброс приоритетов обхода. Когда у всех одинаковый
            // приоритет, юниты в колонне могут «запирать» друг друга:
            // оба уступают и оба стоят.
            int shift = (GetInstanceID() % 17) - 8;
            agent.avoidancePriority = Mathf.Clamp(agent.avoidancePriority + shift, 0, 99);
        }
    }

    private void Update()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            NavMeshHit navMeshHit; 
            
            if (agent.SamplePathPosition(NavMesh.AllAreas, 0.0f, out navMeshHit))
            {
                int roadAreaIndex = NavMesh.GetAreaFromName("Road");
                bool isOnRoad = (navMeshHit.mask & (1 << roadAreaIndex)) != 0;

                if (isOnRoad)
                {
                    agent.speed = baseSpeed * 1.5f; 
                }
                else
                {
                    agent.speed = baseSpeed; 
                }
            }
        }
        
        // Завершение приказа на перемещение: по прибытии очищаем путь,
        // иначе агент будет возвращаться на старую точку, если юнита сдвинули.
        UpdateMoveCommand();
    }

    /// <summary>
    /// Отдать приказ на перемещение в точку.
    /// </summary>
    public void MoveTo(Vector3 destination)
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent == null)
            return;

        if (!agent.isOnNavMesh)
            return;

        // Запоминаем штатную дистанцию остановки и подходим к точке приказа ближе,
        // иначе юнит останавливается за метр до метки и кажется, что не дошёл.
        if (!hasSavedStoppingDistance)
        {
            savedStoppingDistance = agent.stoppingDistance;
            hasSavedStoppingDistance = true;
        }

        agent.stoppingDistance = Mathf.Min(moveStoppingDistance, savedStoppingDistance);

        agent.SetDestination(destination);
        isCommandToMove = true;
        moveStartTime = Time.time;
        currentDestination = destination;

        lastProgressTime = Time.time;
        nextRepathTime = 0f;
        lostPathTimer = 0f;
    }

    /// <summary>
    /// Проверяет, дошёл ли юнит до точки приказа. По прибытии сбрасывает путь
    /// и снимает флаг isCommandToMove, чтобы юнит не возвращался к метке.
    /// Вызывается каждый кадр, в том числе из ManagerGO — чтобы работать
    /// и для невыделенных юнитов.
    /// </summary>
    public void UpdateMoveCommand()
    {
        // ManagerGO вызывает это для всех юнитов, а Update — ещё и у выделенных.
        // Считаем один раз за кадр, иначе таймеры «застревания» идут вдвое быстрее.
        if (lastCommandUpdateFrame == Time.frameCount)
            return;

        lastCommandUpdateFrame = Time.frameCount;

        if (!isCommandToMove)
            return;

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                isCommandToMove = false;
                return;
            }
        }

        if (!agent.isOnNavMesh)
            return;

        // Прибытие считаем по фактическому расстоянию до точки приказа,
        // а не по hasPath: при объезде других юнитов путь может на миг
        // исчезнуть, и раньше юнит останавливался посреди дороги.
        float distanceToDestination = Vector3.Distance(transform.position, currentDestination);
        float arrivalDistance = agent.stoppingDistance + ArrivalTolerance;

        // Второе условие — на случай недостижимой точки: NavMesh ведёт юнита
        // к ближайшей доступной позиции, и remainingDistance там равен нулю.
        // MinMoveTime обязателен: сразу после SetDestination remainingDistance
        // бывает 0, пока путь реально не построен.
        bool remainingArrived = Time.time - moveStartTime >= MinMoveTime
                                && agent.hasPath
                                && !agent.pathPending
                                && agent.remainingDistance <= arrivalDistance;

        // Третье: агент уже сам закончил путь и встал, но до метки остались
        // сантиметры (радиус агента, расталкивание). Не гоняем его по кругу.
        bool stoppedNearby = !agent.pathPending
                             && !agent.hasPath
                             && agent.velocity.sqrMagnitude <= StuckVelocitySqr
                             && distanceToDestination <= CloseEnoughDistance;

        bool arrived = distanceToDestination <= arrivalDistance
                       || remainingArrived
                       || stoppedNearby;

        if (arrived)
        {
            if (agent.hasPath)
                agent.ResetPath();

            RestoreStoppingDistance();
            isCommandToMove = false;
            return;
        }

        if (agent.pathPending)
            return;

        if (Time.time - moveStartTime < MinMoveTime)
            return;

        // Путь так и не построился (точка недостижима) — через пару секунд снимаем приказ
        if (agent.hasPath)
        {
            lostPathTimer = 0f;
        }
        else
        {
            lostPathTimer += Time.deltaTime;
            if (lostPathTimer >= PathLostGiveUpTime)
            {
                RestoreStoppingDistance();
                isCommandToMove = false;
                return;
            }
        }

        // Стоим на месте, хотя приказ ещё активен — повторяем его.
        // Это спасает от застревания, когда юниты идут колонной друг за другом.
        HandleStuckRepath();
    }

    /// <summary>
    /// Если юнит стоит, хотя должен идти, заново отправляет его к точке приказа.
    /// Простой повторный SetDestination заставляет NavMeshAgent перестроить путь
    /// и объехать затор.
    /// </summary>
    private void HandleStuckRepath()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        if (agent.velocity.sqrMagnitude > StuckVelocitySqr)
        {
            lastProgressTime = Time.time;
            return;
        }

        if (Time.time - lastProgressTime < stuckCheckTime)
            return;

        if (Time.time - nextRepathTime < stuckRepathInterval)
            return;

        nextRepathTime = Time.time + stuckRepathInterval;

        agent.isStopped = false;
        agent.SetDestination(currentDestination);
    }

    /// <summary>Возвращает агенту штатную дистанцию остановки.</summary>
    private void RestoreStoppingDistance()
    {
        if (!hasSavedStoppingDistance)
            return;

        if (agent != null)
            agent.stoppingDistance = savedStoppingDistance;

        hasSavedStoppingDistance = false;
    }

    /// <summary>
    /// Прервать приказ на перемещение (например, при приказе атаковать).
    /// </summary>
    public void StopMoving()
    {
        isCommandToMove = false;

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent != null && agent.isOnNavMesh && agent.hasPath)
            agent.ResetPath();

        RestoreStoppingDistance();
    }
}
