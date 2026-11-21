using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

/// <summary>The host simulates the unit. Its faction determines who can issue orders.</summary>
[RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(NavMeshAgent))]
[RequireComponent(typeof(Unit), typeof(FactionMember))]
public sealed class RtsNetworkUnit : NetworkBehaviour
{
    internal const int MaxPatrolPoints = 16;
    internal enum TravelOrder { Move, AttackMove, Retreat }
    public enum UnitKind { Prototype, Villager, Swordsman, Builder, Lumberjack, Carrier, StoneMason, Producer, Farmer, Miner, Spearman, Archer, Horse, Cavalry }
    [SerializeField] private UnitKind kind;
    public bool IsVillager => kind == UnitKind.Villager;
    public bool IsSwordsman => kind == UnitKind.Swordsman;
    public bool IsSpearman => kind == UnitKind.Spearman;
    public bool IsCavalry => kind == UnitKind.Cavalry;
    public bool IsMeleeFighter => IsSwordsman || IsSpearman || IsCavalry;
    public bool IsArcher => kind == UnitKind.Archer;
    public bool IsHorse => kind == UnitKind.Horse;
    public bool IsCombatUnit => IsMeleeFighter || IsArcher;
    internal TroopType MilitaryType => IsSpearman ? TroopType.Spearman : IsSwordsman ? TroopType.Swordsman
        : IsArcher ? TroopType.Archer : IsCavalry ? TroopType.Cavalry : TroopType.Auto;
    public bool IsBuilder => kind == UnitKind.Builder;
    public bool IsLumberjack => kind == UnitKind.Lumberjack;
    public bool IsCarrier => kind == UnitKind.Carrier;
    public bool IsStoneMason => kind == UnitKind.StoneMason;
    public bool IsProducer => kind == UnitKind.Producer;
    public bool IsFarmer => kind == UnitKind.Farmer;
    public bool IsMiner => kind == UnitKind.Miner;
    public bool IsSupplyWorker => IsLumberjack || IsCarrier || IsStoneMason || IsProducer || IsFarmer || IsMiner;
    private bool NeedsWorkerRole => IsVillager || IsSupplyWorker;
    private ResourcesMove.UnitRole WorkerRole => IsLumberjack ? ResourcesMove.UnitRole.Lumberjack
        : IsStoneMason ? ResourcesMove.UnitRole.StoneMason : IsCarrier ? ResourcesMove.UnitRole.Carrier
        : IsProducer ? ResourcesMove.UnitRole.Miller : IsFarmer ? ResourcesMove.UnitRole.Farmer
        : IsMiner ? ResourcesMove.UnitRole.Miner : ResourcesMove.UnitRole.Villager;
    private readonly NetworkVariable<int> owner = new NetworkVariable<int>();
    private readonly NetworkVariable<bool> moving = new NetworkVariable<bool>();
    private readonly NetworkVariable<bool> attacking = new NetworkVariable<bool>();
    private readonly NetworkVariable<int> heldFormationGroup = new NetworkVariable<int>();
    private readonly NetworkVariable<bool> attackMoving = new NetworkVariable<bool>();
    private readonly NetworkVariable<bool> patrolling = new NetworkVariable<bool>();
    private readonly NetworkVariable<bool> retreating = new NetworkVariable<bool>();
    private readonly NetworkVariable<Vector2> health = new NetworkVariable<Vector2>();
    private readonly NetworkVariable<bool> horseReserved = new NetworkVariable<bool>();
    private readonly NetworkVariable<int> carriedWood = new NetworkVariable<int>();
    private readonly NetworkVariable<int> carriedStone = new NetworkVariable<int>();
    private readonly NetworkVariable<int> carriedWheat = new NetworkVariable<int>();
    private readonly NetworkVariable<int> carriedFlour = new NetworkVariable<int>();
    private readonly NetworkVariable<int> carriedBread = new NetworkVariable<int>();
    private readonly NetworkVariable<int> carriedIron = new NetworkVariable<int>();
    private readonly NetworkVariable<int> carriedGold = new NetworkVariable<int>();
    private readonly NetworkVariable<int> carriedFood = new NetworkVariable<int>();
    private readonly NetworkVariable<int> carriedSword = new NetworkVariable<int>();
    private readonly NetworkVariable<int> carriedSpear = new NetworkVariable<int>();
    private readonly NetworkVariable<int> carriedBow = new NetworkVariable<int>();
    private readonly NetworkVariable<int> carriedShield = new NetworkVariable<int>();
    private NavMeshAgent agent;
    private FactionMember faction;
    private Unit body;
    private AttackController attack;
    private Archer archer;
    private CartHorse horse;
    private BattleCombatant battle;
    private RtsNetworkUnit attackTarget;
    private RtsNetworkBuildSite attackBuilding;
    private float nextBuildingApproach;
    private Vector3? automaticAttackOrigin;
    private Vector3 travelDestination;
    private Vector3 formationFacing;
    private int formationGroup;
    private PatrolRoute<Vector3> patrolRoute;
    private RtsNetworkUnit skippedTravelTarget;
    private float nextTravelSearch;
    private float nextTravelPath;
    private readonly MeleeStrikeClock strikeClock = new MeleeStrikeClock();
    private float nextStrike;
    private float nextHoldSearch;
    private float nextOrderTime;
    private Animator animator;
    private bool hasMoving, hasFollowing, hasAttacking, hasActionType;
    public ulong EntityId => NetworkObjectId;
    public int PlayerId => owner.Value;
    public float Health => health.Value.x;
    public float MaxHealth => health.Value.y;
    internal float NavigationRadius => agent.radius;
    internal bool CanUseFormation => IsSpawned && IsServer && IsMeleeFighter && body.unitHealth > 0
        && agent.enabled && agent.isOnNavMesh;
    public bool IsHoldingPosition => HeldFormationGroup != 0;
    public int HeldFormationGroup => heldFormationGroup.Value;
    internal float FormationSpeed => battle != null ? battle.NaturalSpeed : agent.speed;
    public bool IsAttackMoving => attackMoving.Value;
    public bool IsPatrolling => patrolling.Value;
    public bool IsRetreating => retreating.Value;
    public bool IsHorseReserved => horseReserved.Value;
    public int CarriedWood => carriedWood.Value;
    public int CarriedStone => carriedStone.Value;
    public int CarriedWheat => carriedWheat.Value;
    public int CarriedFlour => carriedFlour.Value;
    public int CarriedBread => carriedBread.Value;
    public int CarriedIron => carriedIron.Value;
    public int CarriedGold => carriedGold.Value;
    public int CarriedFood => carriedFood.Value;
    public int CarriedSword => carriedSword.Value;
    public int CarriedSpear => carriedSpear.Value;
    public int CarriedBow => carriedBow.Value;
    public int CarriedShield => carriedShield.Value;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        faction = GetComponent<FactionMember>();
        body = GetComponent<Unit>();
        // No local simulation before the server has spawned this object.
        agent.enabled = false;
        if (TryGetComponent<ResourcesMove>(out var worker)) worker.enabled = false;
        if (IsHorse)
        {
            horse = GetComponent<CartHorse>();
            horse.enabled = false;
            GetComponent<GOScript>().enabled = false;
            GetComponent<AttackController>().enabled = false;
        }
        if (IsArcher)
        {
            archer = GetComponent<Archer>();
            archer.enabled = false;
            GetComponent<AttackController>().enabled = false;
        }
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            foreach (var parameter in animator.parameters)
            {
                hasActionType |= parameter.name == "ActionType" && parameter.type == AnimatorControllerParameterType.Int;
                if (parameter.type != AnimatorControllerParameterType.Bool) continue;
                hasMoving |= parameter.name == "isMoving";
                hasFollowing |= parameter.name == "IsFollowing";
                hasAttacking |= parameter.name == "isAttacking";
            }
        }
    }
    public bool ValidatePrototype(out string error)
    {
        error = null;
        if (!TryGetComponent<FactionMember>(out var member) || !FactionRules.IsPlayerSide(member.Side))
            error = name + ": назначьте владельца Player–Player4";
        if (NeedsWorkerRole != TryGetComponent<ResourcesMove>(out _))
            error = name + ": проверьте профессию сетевого рабочего";
        foreach (var script in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script == null || script is RtsNetworkUnit || script is NetworkTransform || script is NetworkObject || script is Unit || script is FactionMember
                || (NeedsWorkerRole && script is ResourcesMove worker && worker.gameObject == gameObject && worker.role == WorkerRole)
                || (IsArcher && script.gameObject == gameObject && (script is Archer || script is AttackController))
                || (IsHorse && script.gameObject == gameObject && (script is CartHorse || script is GOScript || script is AttackController))) continue;
            error = name + ": тестовый юнит должен содержать только Unit, FactionMember и сетевые компоненты; найден " + script.GetType().Name;
            break;
        }
        return error == null;
    }
    public override void OnNetworkSpawn()
    {
        owner.OnValueChanged += OwnerChanged;
        if (IsServer)
        {
            owner.Value = faction.PlayerId;
            agent.enabled = true;
            if (IsHorse) horse.enabled = true;
            if (IsCombatUnit)
            {
                attack = IsArcher ? GetComponent<AttackController>() : gameObject.AddComponent<AttackController>();
                attack.autoAcquireTargets = false;
                attack.damageHandledExternally = true;
                attack.enabled = true;
                gameObject.AddComponent<TroopCombat>().Configure(MilitaryType);
                battle = GetComponent<BattleCombatant>();
                if (IsArcher)
                {
                    archer.autoAcquireTarget = false;
                    archer.moveIntoRange = true;
                    archer.enabled = true;
                }
            }
            health.Value = new Vector2(body.InitialHealth, body.unitMaxHealth);
        }
        else
        {
            agent.enabled = false;
            health.OnValueChanged += ApplyHealth;
            ApplyHealth(Vector2.zero, health.Value);
        }
        if (IsServer && IsSupplyWorker && !IsProducer)
        {
            gameObject.AddComponent<GettingResources>();
            if (IsLumberjack || IsStoneMason)
            {
                var attack = gameObject.AddComponent<AttackController>();
                attack.autoAcquireTargets = false;
                attack.unitDamage = 10;
            }
        }
        if (IsServer && NeedsWorkerRole && TryGetComponent<ResourcesMove>(out var worker)) worker.enabled = true;
        if (IsServer && IsBuilder)
        {
            var builder = gameObject.AddComponent<Builder>();
            builder.autoRepair = false;
            builder.logState = false;
        }
        OwnerChanged(0, owner.Value);
    }
    private void OwnerChanged(int previous, int current) => faction.SetFaction(NetworkPlayerSlots.Side(current));

    private void ApplyHealth(Vector2 previous, Vector2 current)
    {
        if (current.y <= 0) return;
        body.unitMaxHealth = current.y;
        body.SetInitialHealthRatio(current.x / current.y);
    }

    public void RequestAttack(RtsNetworkUnit target)
    {
        var session = RtsNetworkSession.Instance;
        if (!IsSpawned || !IsCombatUnit || session == null || !session.Ready || !FactionMember.IsPlayer(gameObject)
            || !CanAttack(target)) return;
        AttackServerRpc(target.NetworkObjectId);
    }

    public void RequestBuildingAttack(RtsNetworkBuildSite target)
    {
        if (IsSpawned && IsMeleeFighter && RtsNetworkSession.Instance?.Ready == true
            && FactionMember.IsPlayer(gameObject) && CanAttackBuilding(target))
            AttackServerRpc(target.NetworkObjectId);
    }

    public void RequestHold() => RequestStance(true);
    public void RequestStop() => RequestStance(false);

    private void RequestStance(bool hold)
    {
        var session = RtsNetworkSession.Instance;
        if (IsSpawned && IsMeleeFighter && session != null && session.Ready && FactionMember.IsPlayer(gameObject))
            SetStanceServerRpc(hold);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetStanceServerRpc(bool hold, ServerRpcParams parameters = default)
    {
        if (!IsMeleeFighter || !CanReceiveOrder(parameters.Receive.SenderClientId)) return;
        if (hold && IsHoldingPosition) return;
        if (hold) SetDefenseFacing(transform.forward, FormationHold.BeginGroup());
        else StopForFormation();
    }

    [ServerRpc(RequireOwnership = false)]
    private void AttackServerRpc(ulong targetId, ServerRpcParams parameters = default)
    {
        if (attack == null || !CanReceiveOrder(parameters.Receive.SenderClientId)) return;
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var entity)) return;
        var target = entity.GetComponent<RtsNetworkUnit>();
        var building = entity.GetComponent<RtsNetworkBuildSite>();
        if (building != null ? !IsMeleeFighter || !CanAttackBuilding(building) : !CanAttack(target)) return;
        ReleaseHold();
        ClearTravelOrder();
        LeaveMarch();
        if (building != null)
        {
            if (building != attackBuilding)
            {
                CancelAttack();
                agent.ResetPath();
                attackBuilding = building;
                attack.targetToAttack = building.transform;
                nextBuildingApproach = 0;
            }
        }
        else if (target != attackTarget) BeginAttack(target);
        automaticAttackOrigin = null;
    }

    private bool CanReceiveOrder(ulong sender)
    {
        var session = RtsNetworkSession.Instance;
        if (session == null || !session.CanCommand(sender, NetworkPlayerSlots.Side(PlayerId))
            || body.unitHealth <= 0 || !agent.enabled || !agent.isOnNavMesh || Time.unscaledTime < nextOrderTime) return false;
        nextOrderTime = Time.unscaledTime + .05f;
        return true;
    }

    internal void RespondToHit(GameObject attacker)
    {
        if (!IsSpawned || !IsServer || !IsMeleeFighter || attack == null || !agent.enabled || !agent.isOnNavMesh
            || IsHoldingPosition || IsAttackMoving || IsPatrolling || IsRetreating || formationGroup != 0 || attackTarget != null || IsTraveling
            || attackBuilding != null || SquadMember.Travelling(gameObject)
            || !attacker.TryGetComponent<RtsNetworkUnit>(out var target)
            || !CanAttack(target)) return;
        BeginAttack(target);
        automaticAttackOrigin = transform.position;
    }

    private bool IsTraveling => agent.enabled && agent.isOnNavMesh && !agent.isStopped
        && (agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + .05f));

    private void BeginAttack(RtsNetworkUnit target)
    {
        CancelAttack();
        agent.ResetPath();
        attackTarget = target;
        attack.targetToAttack = target.transform;
    }

    private bool CanAttack(RtsNetworkUnit target)
        => target != null && target.IsSpawned && target.body.HasStarted && target.body.unitHealth > 0
            && FactionMember.AreEnemies(gameObject, target.gameObject);

    private bool CanAttackBuilding(RtsNetworkBuildSite target)
        => target != null && target.IsSpawned && target.IsComplete && target.Health > 0
            && FactionMember.AreEnemies(gameObject, target.gameObject);

    internal void EndCombatOrders()
    {
        CancelAttack();
        ReleaseHold();
        ClearTravelOrder();
        LeaveMarch();
    }

    private void ReleaseHold()
    {
        if (!IsHoldingPosition) return;
        FormationHold.Clear(gameObject);
        battle?.PrepareForOrder();
        heldFormationGroup.Value = 0;
    }

    private void HoldPosition(Vector3 facing, int group, Vector3? slot = null)
    {
        transform.rotation = Quaternion.LookRotation(facing);
        FormationHold.SetPoint(gameObject, slot ?? transform.position, facing, group);
        heldFormationGroup.Value = group;
        nextHoldSearch = 0;
    }

    internal void SetDefenseFacing(Vector3 facing, int group)
    {
        StopForFormation();
        HoldPosition(facing, group);
    }

    private void StopForFormation()
    {
        EndCombatOrders();
        agent.ResetPath();
        agent.isStopped = false;
    }

    internal bool TryPlanFormation(Vector3 point, out Vector3 destination, out NavMeshPath path)
    {
        destination = default;
        path = null;
        if (!CanUseFormation || !NetworkPlayerSlots.ValidPoint(point.x, point.y, point.z)) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(point, out var hit, 2f, filter) || !TryTravelPath(hit.position, out path)) return false;
        destination = hit.position;
        return true;
    }

    internal void BeginFormation(Vector3 destination, Vector3 facing, int group, NavMeshPath path)
    {
        EndCombatOrders();
        agent.isStopped = false;
        if (!agent.SetPath(path)) return;
        travelDestination = destination;
        formationFacing = facing;
        formationGroup = group;
        nextTravelPath = 0;
    }

    internal void BeginMarch(Vector3 firstGoal)
    {
        EndCombatOrders();
        SetMarchWaypoint(firstGoal);
    }

    internal bool SetMarchWaypoint(Vector3 point)
    {
        if (!agent.enabled || !agent.isOnNavMesh || agent.pathPending) return false;
        if (agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete
            && (agent.destination - point).sqrMagnitude < .0625f) return true;
        if (!TryTravelPath(point, out var path)) return false;
        agent.isStopped = false;
        return agent.SetPath(path);
    }

    internal void FinishMarch(Vector3 slot, Vector3 facing, int group)
    {
        StopMarch();
        HoldPosition(facing, group, slot);
    }

    internal void StopMarch()
    {
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
    }

    private void LeaveMarch()
    {
        if (!TryGetComponent<SquadMember>(out var member) || member.Squad == null) return;
        member.Leave();
        StopMarch();
    }

    private void CancelAttack()
    {
        if (attack == null || (ReferenceEquals(attackTarget, null) && ReferenceEquals(attackBuilding, null))) return;
        attackTarget = null;
        attackBuilding = null;
        automaticAttackOrigin = null;
        attack.targetToAttack = null;
        strikeClock.Reset();
        MeleeApproach.Release(gameObject);
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        attacking.Value = false;
    }

    private void UpdateAttack()
    {
        if (battle != null && battle.IsStunned)
        {
            attacking.Value = false;
            strikeClock.Reset();
            return;
        }
        if (!ReferenceEquals(attackBuilding, null))
        {
            UpdateBuildingAttack();
            return;
        }
        if (IsHoldingPosition) UpdateHeldTarget();
        if (ReferenceEquals(attackTarget, null)) return;
        if (!CanAttack(attackTarget) || OutsidePursuitRange())
        {
            CancelAttack();
            return;
        }
        bool inRange = IsHoldingPosition || (MeleeApproach.Drive(attack, attack.meleeRange, out bool ready) && ready);
        attacking.Value = inRange;
        if (inRange && !IsHoldingPosition) FaceAttackPoint(attackTarget.transform.position);
        if (CanStrikeNow(inRange)) attackTarget.body.TakeDamage(attack.unitDamage, gameObject);
    }

    private void UpdateBuildingAttack()
    {
        if (!CanAttackBuilding(attackBuilding))
        {
            CancelAttack();
            return;
        }
        bool ready = agent.enabled && agent.isOnNavMesh && !agent.isOnOffMeshLink;
        var feet = transform.position - Vector3.up * agent.baseOffset;
        bool inRange = ready && attackBuilding.CanBeStruckFrom(feet, agent, attack.meleeRange);
        attacking.Value = inRange;
        if (inRange)
        {
            agent.ResetPath();
            FaceAttackPoint(attackBuilding.MeleeContact(transform.position));
        }
        else if (ready && !IsTraveling && Time.time >= nextBuildingApproach)
        {
            nextBuildingApproach = Time.time + .5f;
            if (attackBuilding.TryApproachForMelee(agent, attack.meleeRange, out var path))
            {
                agent.isStopped = false;
                agent.SetPath(path);
            }
        }
        if (CanStrikeNow(inRange)) attackBuilding.ReceiveMeleeHit(gameObject, attack.unitDamage);
    }

    private void FaceAttackPoint(Vector3 point)
    {
        var direction = point - transform.position;
        direction.y = 0;
        if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction);
    }

    private bool CanStrikeNow(bool inRange)
    {
        if (!strikeClock.Tick(Time.deltaTime, inRange, 1f, attack.strikeWindup) || Time.time < nextStrike) return false;
        nextStrike = Time.time + .95f;
        return true;
    }

    private void UpdateHeldTarget()
    {
        if (CanAttack(attackTarget) && MeleeApproach.CanStrike(gameObject, attackTarget.transform, attack.meleeRange)) return;
        CancelAttack();
        if (Time.time < nextHoldSearch) return;
        nextHoldSearch = Time.time + .25f;
        var closest = FindCombatTarget(float.PositiveInfinity, hold: true);
        if (closest != null) BeginAttack(closest);
    }

    private RtsNetworkUnit FindCombatTarget(float range, bool hold)
    {
        RtsNetworkUnit closest = null;
        float nearest = range * range;
        foreach (var entity in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            if (!entity.TryGetComponent<RtsNetworkUnit>(out var target) || !CanAttack(target) || target == skippedTravelTarget) continue;
            float distance = (target.transform.position - transform.position).sqrMagnitude;
            if (distance >= nearest) continue;
            if (hold ? !MeleeApproach.CanStrike(gameObject, target.transform, attack.meleeRange)
                : !MeleeApproach.CanTarget(gameObject, target.transform)) continue;
            nearest = distance;
            closest = target;
        }
        return closest;
    }

    private bool OutsidePursuitRange()
    {
        if (!automaticAttackOrigin.HasValue) return false;
        var origin = automaticAttackOrigin.Value;
        const float range = MilitaryOrders.DefaultPursuitDistance;
        return (attackTarget.transform.position - origin).sqrMagnitude > range * range
            || (transform.position - origin).sqrMagnitude > range * range;
    }

    public void RequestMove(Vector3 point) => RequestTravel(point, TravelOrder.Move);
    public void RequestAttackMove(Vector3 point) => RequestTravel(point, TravelOrder.AttackMove);

    public void RequestPatrol(Vector3[] points)
    {
        var session = RtsNetworkSession.Instance;
        if (IsSpawned && IsMeleeFighter && session?.Ready == true && FactionMember.IsPlayer(gameObject)
            && points != null && points.Length >= 2 && points.Length <= MaxPatrolPoints) PatrolServerRpc(points);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PatrolServerRpc(Vector3[] points, ServerRpcParams parameters = default)
    {
        if (!IsMeleeFighter || !CanReceiveOrder(parameters.Receive.SenderClientId)
            || points == null || points.Length < 2 || points.Length > MaxPatrolPoints) return;
        var route = new Vector3[points.Length];
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        var leg = new NavMeshPath();
        for (int i = 0; i < points.Length; i++)
        {
            var point = points[i];
            if (!NetworkPlayerSlots.ValidPoint(point.x, point.y, point.z) || !NavMesh.SamplePosition(point, out var hit, 2f, filter)) return;
            route[i] = hit.position;
            if (i == 0) continue;
            var previous = route[i - 1];
            if ((previous - route[i]).sqrMagnitude < 1f
                || !NavMesh.CalculatePath(previous, route[i], filter, leg) || leg.status != NavMeshPathStatus.PathComplete
                || !NavMesh.CalculatePath(route[i], previous, filter, leg) || leg.status != NavMeshPathStatus.PathComplete) return;
        }
        if (!TryTravelPath(route[0], out var approach)) return;
        EndCombatOrders();
        agent.isStopped = false;
        if (!agent.SetPath(approach)) return;
        patrolRoute = new PatrolRoute<Vector3>(route);
        patrolling.Value = true;
        nextTravelSearch = nextTravelPath = 0;
    }

    internal void RequestTravel(Vector3 point, TravelOrder order)
    {
        var session = RtsNetworkSession.Instance;
        if (!IsSpawned || session == null || !session.Ready || !FactionMember.IsPlayer(gameObject)
            || (order != TravelOrder.Move && !IsMeleeFighter)) return;
        MoveServerRpc(point, order);
    }

    [ServerRpc(RequireOwnership = false)]
    private void MoveServerRpc(Vector3 point, TravelOrder order, ServerRpcParams parameters = default)
    {
        if (order != TravelOrder.Move && order != TravelOrder.AttackMove && order != TravelOrder.Retreat) return;
        if (IsSupplyWorker || (order != TravelOrder.Move && !IsMeleeFighter) || !CanReceiveOrder(parameters.Receive.SenderClientId)) return;
        if (TryGetComponent<TrainingRecruit>(out var recruit) && recruit.IsReserved) return;
        if (TryGetComponent<Builder>(out var builder) && builder.HasConstructionTask) return;
        if (!NetworkPlayerSlots.ValidPoint(point.x, point.y, point.z)) return;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(point, out var hit, 2f, filter) || !TryTravelPath(hit.position, out var path)) return;
        EndCombatOrders();
        agent.isStopped = false;
        if (!agent.SetPath(path) || order == TravelOrder.Move) return;
        travelDestination = hit.position;
        attackMoving.Value = order == TravelOrder.AttackMove;
        retreating.Value = order == TravelOrder.Retreat;
        nextTravelSearch = nextTravelPath = 0;
    }

    private bool TryTravelPath(Vector3 point, out NavMeshPath path)
    {
        path = new NavMeshPath();
        return agent.CalculatePath(point, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    private void ClearTravelOrder()
    {
        if (!IsAttackMoving && !IsPatrolling && !IsRetreating && formationGroup == 0) return;
        attackMoving.Value = false;
        patrolling.Value = false;
        retreating.Value = false;
        formationGroup = 0;
        patrolRoute = null;
        skippedTravelTarget = null;
        if (attackTarget == null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
    }

    private void UpdateCombatTravel()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        if (attackTarget != null && CanAttack(attackTarget) && !OutsidePursuitRange())
        {
            if (Time.time < nextTravelSearch) return;
            nextTravelSearch = Time.time + .5f;
            if (MeleeApproach.CanTarget(gameObject, attackTarget.transform)) return;
        }
        if (!ReferenceEquals(attackTarget, null))
        {
            skippedTravelTarget = attackTarget;
            CancelAttack();
            nextTravelSearch = Time.time + .35f;
            nextTravelPath = 0;
        }

        var destination = patrolRoute != null ? patrolRoute.Point : travelDestination;
        if (Vector3.Distance(transform.position, destination) <= .7f)
        {
            if (patrolRoute == null)
            {
                ClearTravelOrder();
                return;
            }
            patrolRoute.Arrive();
            destination = patrolRoute.Point;
            agent.ResetPath();
            nextTravelPath = 0;
        }
        if (Time.time >= nextTravelSearch)
        {
            nextTravelSearch = Time.time + .35f;
            const float range = MilitaryOrders.DefaultAcquisitionRadius;
            if (skippedTravelTarget != null && (skippedTravelTarget.transform.position - transform.position).sqrMagnitude >= range * range)
                skippedTravelTarget = null;
            var target = FindCombatTarget(range, hold: false);
            if (target != null)
            {
                BeginAttack(target);
                automaticAttackOrigin = transform.position;
                return;
            }
        }
        ContinueTravel(destination);
    }

    private void UpdateRetreat()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        if (!agent.pathPending && Vector3.Distance(transform.position, travelDestination) <= Mathf.Max(.6f, agent.stoppingDistance + .25f))
        {
            ClearTravelOrder();
            return;
        }
        ContinueTravel(travelDestination);
    }

    private void UpdateFormation()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        var feet = transform.position - Vector3.up * agent.baseOffset;
        if (!agent.pathPending && (feet - travelDestination).sqrMagnitude <= .25f * .25f)
        {
            int group = formationGroup;
            ClearTravelOrder();
            agent.isStopped = false;
            HoldPosition(formationFacing, group);
            return;
        }
        ContinueTravel(travelDestination);
    }

    private void ContinueTravel(Vector3 destination)
    {
        if (IsTraveling && (!IsRetreating || agent.pathPending || agent.pathStatus == NavMeshPathStatus.PathComplete)) return;
        if (Time.time < nextTravelPath) return;
        nextTravelPath = Time.time + 1f;
        if (!TryTravelPath(destination, out var resumedPath))
        {
            if (IsRetreating && agent.hasPath) agent.ResetPath();
            return;
        }
        agent.isStopped = false;
        agent.SetPath(resumedPath);
    }
    private void Update()
    {
        if (!IsSpawned) return;
        if (IsServer)
        {
            if (formationGroup != 0) UpdateFormation();
            else if (IsRetreating) UpdateRetreat();
            else if (IsMeleeFighter)
            {
                if (IsAttackMoving || IsPatrolling) UpdateCombatTravel();
                UpdateAttack();
            }
            else if (IsArcher)
            {
                if (!ReferenceEquals(attackTarget, null) && !CanAttack(attackTarget)) CancelAttack();
                attacking.Value = attackTarget != null && archer.IsShowingAttack;
            }
            moving.Value = IsTraveling;
            if (IsSupplyWorker && TryGetComponent<GettingResources>(out var cargo))
            {
                carriedWood.Value = cargo.Wood;
                carriedStone.Value = cargo.Stone;
                carriedWheat.Value = cargo.Wheat;
                carriedFlour.Value = cargo.Flour;
                carriedBread.Value = cargo.Bread;
                carriedIron.Value = cargo.Iron;
                carriedGold.Value = cargo.Gold;
                carriedFood.Value = cargo.Food;
                carriedSword.Value = cargo.GetStoredAmount(_Game.Scripts.UI.ResourceType.Sword);
                carriedSpear.Value = cargo.Spear;
                carriedBow.Value = cargo.Bow;
                carriedShield.Value = cargo.GetStoredAmount(_Game.Scripts.UI.ResourceType.Shield);
            }
        }
        if (animator == null) return;
        if (hasMoving) animator.SetBool("isMoving", moving.Value);
        if (hasFollowing) animator.SetBool("IsFollowing", moving.Value);
        if (IsCombatUnit)
        {
            if (hasActionType && attacking.Value) animator.SetInteger("ActionType", 1);
            if (hasAttacking) animator.SetBool("isAttacking", attacking.Value);
        }
    }

    private void LateUpdate()
    {
        if (!IsSpawned || !IsServer || !body.HasStarted) return;
        health.Value = new Vector2(body.unitHealth, body.unitMaxHealth);
        if (IsHorse) horseReserved.Value = horse.TrainingOwner != null;
    }

    public override void OnNetworkDespawn()
    {
        owner.OnValueChanged -= OwnerChanged;
        health.OnValueChanged -= ApplyHealth;
        if (IsServer) LeaveMarch();
        if (IsServer && IsHoldingPosition) FormationHold.Clear(gameObject);
        agent.enabled = false;
        if (TryGetComponent<ResourcesMove>(out var worker)) worker.enabled = false;
        if (TryGetComponent<Builder>(out var builder)) builder.enabled = false;
        if (archer != null) archer.enabled = false;
        if (horse != null) horse.enabled = false;
        faction.SetFaction(Faction.Neutral);
        base.OnNetworkDespawn();
    }
}
