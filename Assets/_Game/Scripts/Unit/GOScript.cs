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
    private readonly MovementOrderState orders = new MovementOrderState();
    private int playerToken, foodToken, combatToken;
    private Transform combatTarget;
    private float foodStoppingDistance, nextFoodRepath;
    private Vector3 foodDestination;
    public MovementOrderState.Order CurrentOrder => orders.Current;
    public bool IsRetreating => orders.Current == MovementOrderState.Order.Retreat;
    public bool HasPlayerOrder
    {
        get { RefreshCombatOrder(); return isCommandToMove || orders.Current == MovementOrderState.Order.PlayerMove
            || IsRetreating || orders.Current == MovementOrderState.Order.Combat || (GetComponent<MilitaryOrders>()?.BlocksFood ?? false)
            || (GetComponent<HospitalPatient>()?.IsBusy??false); }
    }
    public event System.Action ManualOrderIssued;
    public void NotifyMilitaryOrder()=>ManualOrderIssued?.Invoke();
    private int supplyToken;
    private float supplyStoppingDistance;
    public bool MovementAllowed
    {
        get
        {
            if(HospitalPatient.Inside(gameObject))return false;
            var cart = GetComponent<FoodCart>();
            if (cart != null) return cart.CanMove;
            var horse = GetComponent<CartHorse>();
            return horse == null || !horse.IsHitched;
        }
    }
    public bool OwnsSupplyOrder(int token) => token == supplyToken && orders.Current == MovementOrderState.Order.Supply && orders.Owns(token);
    public bool TryBeginSupplyMove(out int token)
    {
        token = 0; RefreshCombatOrder();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (!MovementAllowed || isCommandToMove || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        if (!orders.TryBeginSupply(out token)) return false;
        supplyToken = token; supplyStoppingDistance = agent.stoppingDistance; return true;
    }
    public bool MoveForSupply(int token, Vector3 point, float stoppingDistance = 0.5f)
    {
        if (!OwnsSupplyOrder(token) || !MovementAllowed || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        var path = new NavMeshPath();
        if (!agent.CalculatePath(point, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        agent.stoppingDistance = Mathf.Max(0.1f, stoppingDistance); agent.isStopped = false;
        return agent.SetPath(path);
    }
    public bool EndSupplyMove(int token)
    {
        if (!OwnsSupplyOrder(token)) return false;
        orders.End(token); supplyToken = 0;
        if (agent != null)
        {
            agent.stoppingDistance = supplyStoppingDistance;
            if (agent.enabled && agent.isOnNavMesh) { agent.isStopped = false; agent.ResetPath(); }
        }
        return true;
    }
    public bool OwnsFoodOrder(int token) => token == foodToken && orders.Owns(token);
    public bool TryBeginFoodMove(float arrivalDistance, out int token)
    {
        MeleeApproach.Release(gameObject);
        token = 0;
        RefreshCombatOrder();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (!MovementAllowed || agent == null || !agent.enabled || !agent.isOnNavMesh || isCommandToMove) return false;
        if (orders.Current == MovementOrderState.Order.Supply) EndSupplyMove(supplyToken);
        if (!orders.TryBeginFood(out token)) return false;
        GetComponent<BattleCombatant>()?.PrepareForOrder();
        foodToken = token; foodStoppingDistance = agent.stoppingDistance;
        agent.stoppingDistance = Mathf.Min(foodStoppingDistance, Mathf.Max(0.1f, arrivalDistance * 0.5f));
        nextFoodRepath = 0f; return true;
    }
    public bool MoveForFood(int token, Vector3 point)
    {
        if (!OwnsFoodOrder(token) || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        if ((agent.pathPending || (Time.time < nextFoodRepath && agent.hasPath))
            && (point - foodDestination).sqrMagnitude < 0.0625f) return true;
        var path = new NavMeshPath();
        if (!agent.CalculatePath(point, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        agent.isStopped = false;
        if (!agent.SetPath(path)) return false;
        foodDestination = point; nextFoodRepath = Time.time + 0.5f; return true;
    }
    public bool StopForFood(int token)
    {
        if (!OwnsFoodOrder(token) || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        agent.ResetPath(); agent.isStopped = true; return true;
    }
    public bool EndFoodMove(int token)
    {
        if (!OwnsFoodOrder(token)) return false;
        orders.End(token); foodToken = 0;
        if (agent != null)
        {
            agent.stoppingDistance = foodStoppingDistance;
            if (agent.enabled && agent.isOnNavMesh) { agent.isStopped = false; agent.ResetPath(); }
        }
        return true;
    }
    public void AttackTarget(Transform target)
    {
        if (target == null) return;
        if(!FactionMember.AreEnemies(gameObject,target.gameObject))return;
        var targetOwner=FactionMember.Find(target.gameObject);
        if(targetOwner!=null)target=targetOwner.transform;
        var camp = target.GetComponentInParent<BanditCamp>();
        if (camp != null) { if (camp.IsDestroyed) return; target = camp.transform; }
        if (GetComponent<FoodCart>() != null || GetComponent<CartHorse>() != null) return;
        GetComponent<BattleCombatant>()?.PrepareForOrder();
        FormationHold.Clear(gameObject);
        GetComponent<UnitEat>()?.CancelForCommand(false);
        StopMoving();
        combatTarget = target; combatToken = orders.Begin(MovementOrderState.Order.Combat);
        var attack = GetComponent<AttackController>();
        if (attack != null) attack.targetToAttack = target;
        ManualOrderIssued?.Invoke();
    }
    private void RefreshCombatOrder()
    {
        if (orders.Current != MovementOrderState.Order.Combat) return;
        var attack = GetComponent<AttackController>();
        bool valid = combatTarget != null && combatTarget.gameObject.activeInHierarchy
            && attack != null && attack.targetToAttack == combatTarget;
        if (valid)
        {
            var opponent = combatTarget.GetComponentInParent<Unit>();
            var building = combatTarget.GetComponentInParent<BuildingHealth>();
            valid = (opponent == null || opponent.unitHealth > 0) && (building == null || !building.IsDead);
            valid = valid && FactionMember.AreEnemies(gameObject,combatTarget.gameObject);
        }
        if (!valid) { orders.End(combatToken); combatToken = 0; combatTarget = null; }
    }

    private float baseSpeed;
    public float NaturalSpeed
    {
        get
        {
            if(agent==null)agent=GetComponent<NavMeshAgent>();
            if(baseSpeed<=0)baseSpeed=agent!=null?agent.speed:3f;
            var battle=GetComponent<BattleCombatant>();
            return baseSpeed*(battle!=null?battle.SpeedMultiplier:1f);
        }
    }
    public bool SetSquadWaypoint(Vector3 point)
    {
        if(agent==null||!agent.enabled||!agent.isOnNavMesh||!MovementAllowed)return false;
        if(agent.pathPending)return false;
        if(agent.hasPath&&agent.pathStatus==NavMeshPathStatus.PathComplete&&(agent.destination-point).sqrMagnitude<.0625f)return true;
        var path=new NavMeshPath();
        if(!agent.CalculatePath(point,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
        agent.isStopped=false;return agent.SetDestination(point);
    }
    public void SetBaseSpeed(float value)
    {
        baseSpeed = Mathf.Max(0.1f, value);
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = baseSpeed;
    }
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

    private const float StuckVelocitySqr = 0.01f;

    private const float PathLostGiveUpTime = 2f;

    private const float ArrivalTolerance = 0.25f;

        private const float CloseEnoughDistance = 0.6f;



    private const float MinMoveTime = 0.1f;

    private void Start()
    {
        camera = Camera.main;
        agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            if(baseSpeed<=0)baseSpeed = agent.speed;
            RoadNavigation.ConfigureAgent(agent);
            if (!WallStation.AllowsWalkway(gameObject)) agent.areaMask &= ~WallNavigation.AreaMask;
            else agent.autoTraverseOffMeshLink = true;


            int shift = (GetInstanceID() % 17) - 8;
            agent.avoidancePriority = Mathf.Clamp(agent.avoidancePriority + shift, 0, 99);
        }
    }

    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (agent != null && agent.isOnNavMesh)
        {
            var battle = GetComponent<BattleCombatant>();
            agent.speed = SquadMember.Speed(gameObject,NaturalSpeed);
        }

        UpdateMoveCommand();
    }

    /// <summary>Недоступная точка не отменяет текущий приказ. До прибытия запрещён автоматический бой.</summary>
    public bool RetreatTo(Vector3 destination)
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (!MovementAllowed || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        var path = new NavMeshPath();
        if (!agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        int previousToken = playerToken;
        MoveTo(destination);
        if (!isCommandToMove || playerToken == previousToken) return false;
        playerToken = orders.Begin(MovementOrderState.Order.Retreat);
        return true;
    }

    public void MoveTo(Vector3 destination)
    {
        if (!MovementAllowed) return;
        FormationAnchor.Release(gameObject);
        MeleeApproach.Release(gameObject);
        GetComponent<BattleCombatant>()?.PrepareForOrder();
        EndSupplyMove(supplyToken);
        GetComponent<UnitEat>()?.CancelForCommand(false);
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent == null)
            return;

        if (!agent.enabled || !agent.isOnNavMesh)
            return;
        var attack = GetComponent<AttackController>();
        if (attack != null) attack.targetToAttack = null;
        combatTarget = null; combatToken = 0;

        if (!hasSavedStoppingDistance)
        {
            savedStoppingDistance = agent.stoppingDistance;
            hasSavedStoppingDistance = true;
        }

        agent.stoppingDistance = Mathf.Min(moveStoppingDistance, savedStoppingDistance);

        agent.isStopped = false;
        if (!agent.SetDestination(destination)) { RestoreStoppingDistance(); return; }
        FormationHold.Clear(gameObject);
        playerToken = orders.Begin(MovementOrderState.Order.PlayerMove);
        isCommandToMove = true;
        moveStartTime = Time.time;
        currentDestination = destination;

        lastProgressTime = Time.time;
        nextRepathTime = 0f;
        lostPathTimer = 0f;
        ManualOrderIssued?.Invoke();
    }
    public void UpdateMoveCommand()
    {
        if (!MovementAllowed) { if (isCommandToMove) StopMoving(); return; }
        RefreshCombatOrder();
        if(SquadMember.Travelling(gameObject))return;
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
                orders.End(playerToken);
                return;
            }
        }

        if (!agent.isOnNavMesh)
            return;
        if (BattleCombatant.BlocksActions(this)) return;


        if (agent.isOnOffMeshLink)
        { lastProgressTime = Time.time; lostPathTimer = 0f; return; }


        float distanceToDestination = Vector3.Distance(transform.position, currentDestination);
        float arrivalDistance = agent.stoppingDistance + ArrivalTolerance;


        bool remainingArrived = Time.time - moveStartTime >= MinMoveTime
                                && agent.hasPath
                                && !agent.pathPending
                                && (!IsRetreating || agent.pathStatus == NavMeshPathStatus.PathComplete)
                                && agent.remainingDistance <= arrivalDistance;


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
            orders.End(playerToken);
            return;
        }

        if (agent.pathPending)
            return;

        if (Time.time - moveStartTime < MinMoveTime)
            return;


        if (agent.hasPath)
        {
            lostPathTimer = 0f;
        }
        else
        {
            lostPathTimer += Time.deltaTime;
            if (lostPathTimer >= PathLostGiveUpTime)
            {
                // Временно перекрытый путь не разрешает развернуться обратно к врагу.
                if (IsRetreating)
                {
                    lostPathTimer = 0f;
                    agent.SetDestination(currentDestination);
                    return;
                }
                RestoreStoppingDistance();
                isCommandToMove = false;
                orders.End(playerToken);
                return;
            }
        }


        HandleStuckRepath();
    }
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


    private void RestoreStoppingDistance()
    {
        if (!hasSavedStoppingDistance)
            return;

        if (agent != null)
            agent.stoppingDistance = savedStoppingDistance;

        hasSavedStoppingDistance = false;
    }


    public void StopMoving()
    {
        var squadMember=GetComponent<SquadMember>();
        bool wasSquad=squadMember!=null&&squadMember.Squad!=null;
        squadMember?.Leave();
        if(wasSquad)FormationHold.Clear(gameObject);
        MeleeApproach.Release(gameObject);
        EndSupplyMove(supplyToken);
        GetComponent<UnitEat>()?.CancelForCommand(false);
        isCommandToMove = false;
        orders.End(playerToken);
        if (orders.Current == MovementOrderState.Order.Combat) orders.End(combatToken);
        combatTarget = null; combatToken = 0;

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent != null && agent.isOnNavMesh && agent.hasPath)
            agent.ResetPath();

        RestoreStoppingDistance();
    }
}
