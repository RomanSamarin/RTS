using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using _Game.Scripts.UI;

/// <summary>Состояние строя и временные эффекты. Не меняет население или снаряжение.</summary>
[DisallowMultipleComponent]
public class BattleCombatant : MonoBehaviour
{
    public static readonly List<BattleCombatant> All = new List<BattleCombatant>();
    [Min(0.1f)] public float braceSeconds = 1.5f;
    [Min(0.5f)] public float neighborRadius = 3.5f;
    [Min(1)] public int minimumNeighbors = 2;
    [Min(0.1f)] public float pointTolerance = 1.2f;
    public bool regroupAfterImpact = true;
    private Unit unit;
    private NavMeshAgent agent;
    private GOScript command;
    private AttackController attack;
    private Animator animator;
    private float stillSince, nextNeighbors, nextRegroupPath;
    private bool wasStill, coherent, ownsStop, savedStopped, ownsRotation, savedRotation, regrouping;
    private float regroupStopping, fallbackSpeed;
    private Vector3 pushVelocity;
    private float pushUntil;
    private readonly BattleShock effects = new BattleShock();
    public TroopType Kind => TypeOf(gameObject);
    public bool Alive => isActiveAndEnabled && unit != null && unit.unitHealth > 0&&!HospitalPatient.Inside(gameObject)&&!ShipPassenger.Aboard(gameObject);
    public Faction Side => FactionMember.Of(gameObject);
    public bool IsStunned => effects.Stunned(Time.time);
    public bool IsDisordered => effects.Disordered(Time.time);
    public bool IsFatigued => effects.Fatigued(Time.time);
    public bool Holding => CandidateHolding() && wasStill && Time.time - stillSince >= braceSeconds;
    public bool DefendsPosition => Alive && Kind != TroopType.Cavalry && FormationHold.TryGetPoint(gameObject, out _)
        && (command == null || !command.HasPlayerOrder) && !UnitEat.ControlsMovement(this);
    public bool Cohesive => Holding && coherent;
    public float SpeedMultiplier => effects.SpeedFactor(Time.time);
    internal float NaturalSpeed => fallbackSpeed * SpeedMultiplier;
    public float AttackRateMultiplier => effects.AttackRateFactor(Time.time);
    public float DamageMultiplier => effects.DamageFactor(Time.time);
    public string Status => IsStunned ? "Оглушён" : IsDisordered ? "Строй расстроен" : IsFatigued ? "Усталость после натиска"
        : Cohesive && Kind == TroopType.Spearman ? "Копья выставлены" : Cohesive && Kind == TroopType.Swordsman ? "Щитовой строй"
        : Holding ? "Удерживает строй" : "";
    public Vector3 Facing => FormationHold.TryGetFacing(gameObject, out var facing) ? facing : transform.forward;
    public void SetBaseSpeed(float speed) { fallbackSpeed = speed; }
    public static TroopType TypeOf(GameObject obj)
    {
        if (obj == null) return TroopType.Auto;
        var profile = obj.GetComponent<TroopCombat>();
        if (profile != null && profile.troopType != TroopType.Auto) return profile.troopType;
        if (obj.TryGetComponent<RtsNetworkUnit>(out var network) && network.IsCombatUnit) return network.MilitaryType;
        if (obj.GetComponent<Archer>() != null) return TroopType.Archer;
        var worker = obj.GetComponent<ResourcesMove>();
        if (worker == null || worker.role != ResourcesMove.UnitRole.Soldier) return TroopType.Auto;
        var cargo = obj.GetComponent<GettingResources>();
        return cargo != null && cargo.GetStoredAmount(ResourceType.Spear) > 0 ? TroopType.Spearman : TroopType.Swordsman;
    }
    public static BattleCombatant Ensure(GameObject obj)
    {
        if (obj == null || TypeOf(obj) == TroopType.Auto || obj.GetComponent<Unit>() == null || obj.GetComponent<NavMeshAgent>() == null) return null;
        var result = obj.GetComponent<BattleCombatant>();
        return result != null ? result : obj.AddComponent<BattleCombatant>();
    }
    private void Awake()
    {
        unit = GetComponent<Unit>(); agent = GetComponent<NavMeshAgent>(); command = GetComponent<GOScript>();
        attack = GetComponent<AttackController>(); animator = GetComponentInChildren<Animator>();
        fallbackSpeed = agent != null ? agent.speed : 0;
    }
    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() { All.Remove(this); RestoreStop(); PrepareForOrder(); }
    private bool CandidateHolding()
    {
        if (!Alive || IsDisordered || IsStunned || Kind == TroopType.Cavalry || UnitEat.ControlsMovement(this)
            || (command != null && command.HasPlayerOrder) || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        return FormationHold.TryGetPoint(gameObject, out var point) && (point - transform.position).sqrMagnitude <= pointTolerance * pointTolerance
            && !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.15f)
            && agent.velocity.sqrMagnitude < 0.09f;
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        bool still = CandidateHolding();
        if (still && !wasStill) stillSince = Time.time;
        wasStill = still;
        if (!still) { coherent = false; nextNeighbors = Time.time; }
        else if (Time.time >= nextNeighbors)
        {
            nextNeighbors = Time.time + 0.2f; int neighbors = 0;
            foreach (var other in All)
            {
                if (other == this || !other.Alive || !FactionRules.AreAllies(other.Side,Side) || !other.Holding || other.Kind != Kind
                    || FormationHold.GetGroup(other.gameObject) != FormationHold.GetGroup(gameObject)) continue;
                var difference = other.transform.position - transform.position;
                if (Mathf.Abs(difference.y) <= 1 && difference.sqrMagnitude <= neighborRadius * neighborRadius
                    && Vector3.Dot(Facing, other.Facing) > 0.7f) neighbors++;
            }
            coherent = neighbors >= minimumNeighbors;
        }
        SetBool("isStunned", IsStunned); SetBool("isDisrupted", IsDisordered); SetBool("isBraced", Cohesive);
    }
    private void LateUpdate()
    {
        if(SaveGameManager.IsLoading)return;
        if (!Alive || agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        if (command == null) agent.speed = SquadMember.Speed(gameObject, NaturalSpeed);
        if (IsStunned || Time.time < pushUntil)
        {
            if (!ownsStop) { ownsStop = true; savedStopped = agent.isStopped; }
            agent.isStopped = true; SetBool("isAttacking", false);
            if (Time.time < pushUntil)
            {
                Vector3 next = transform.position + pushVelocity * Time.deltaTime;
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                if (NavMesh.Raycast(transform.position, next, out var edge, filter)) next = edge.position;
                if (Mathf.Abs(next.y - transform.position.y) < 0.5f) agent.Move(next - transform.position);
            }
            return;
        }
        RestoreStop();
        if ((command != null && command.HasPlayerOrder) || UnitEat.ControlsMovement(this)) { PrepareForOrder(); return; }
        if (!FormationHold.TryGetPoint(gameObject, out var point)) { PrepareForOrder(); return; }
        if (regroupAfterImpact && (Kind != TroopType.Cavalry || IsDisordered) && (point - transform.position).sqrMagnitude > 0.4f * 0.4f)
        {
            ReleaseRotation();
            if (!regrouping) { regrouping = true; regroupStopping = agent.stoppingDistance; }
            agent.stoppingDistance = 0.1f; agent.isStopped = false;
            if (Time.time >= nextRegroupPath)
            {
                nextRegroupPath = Time.time + 0.35f;
                var path = new NavMeshPath();
                if (agent.CalculatePath(point, path) && path.status == NavMeshPathStatus.PathComplete) agent.SetPath(path);
            }
        }
        else
        {
            RestoreRegroupDistance();
            if (Kind != TroopType.Cavalry) agent.ResetPath();
            if (!Holding && GetComponent<RtsNetworkUnit>()?.IsHoldingPosition != true) { ReleaseRotation(); return; }
            agent.ResetPath();
            if (Kind != TroopType.Archer)
            {
                if (!ownsRotation) { ownsRotation = true; savedRotation = agent.updateRotation; }
                agent.updateRotation = false;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(Facing), 540 * Time.deltaTime);
            }
            var archer = GetComponent<Archer>();
            if (archer == null && attack != null && attack.targetToAttack != null)
            {
                bool close = Vector3.Distance(transform.position, AttackController.CombatPoint(attack.targetToAttack,transform.position)) <= attack.meleeRange;
                SetBool("isAttacking", close); SetBool("IsFollowing", close);
            }
        }
    }
    public void PrepareForOrder() { RestoreRegroupDistance(); ReleaseRotation(); }
    public void FormationChanged(){wasStill=false;coherent=false;stillSince=Time.time;}
    private void RestoreRegroupDistance()
    {
        if (!regrouping) return;
        if (agent != null) agent.stoppingDistance = regroupStopping;
        regrouping = false;
    }
    private void ReleaseRotation()
    {
        if (!ownsRotation) return;
        if (agent != null) agent.updateRotation = savedRotation;
        ownsRotation = false;
    }
    private void RestoreStop()
    {
        if (!ownsStop) return;
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = savedStopped;
        ownsStop = false;
    }
    public bool Frontal(Vector3 attackerPosition)
    {
        Vector3 incoming = attackerPosition - transform.position; incoming.y = 0;
        return incoming.sqrMagnitude > 0.001f && Vector3.Dot(Facing, incoming.normalized) >= 0.5f;
    }
    public bool Rear(Vector3 attackerPosition)
    {
        Vector3 incoming = attackerPosition - transform.position; incoming.y = 0;
        return incoming.sqrMagnitude > 0.001f && Vector3.Dot(Facing, incoming.normalized) < -0.5f;
    }
    public void Impact(Vector3 direction, float push, float stun, float disorder)
    {
        FormationAnchor.Release(gameObject);
        effects.Impact(Time.time, stun, disorder); wasStill = false; coherent = false;
        if (push > 0 && agent != null && agent.enabled && agent.isOnNavMesh)
        { direction.y = 0; pushVelocity = direction.normalized * (push / 0.25f); pushUntil = Time.time + 0.25f; }
        GetComponent<UnitEat>()?.CancelForCommand(false);
    }
    public void Exhaust(float seconds) { effects.Fatigue(Time.time, seconds); }
    public static bool BlocksActions(Component component)
    { if(ShipPassenger.Aboard(component.gameObject))return true;var battle = component.GetComponentInParent<BattleCombatant>(); return battle != null && battle.IsStunned; }
    public static bool Holds(Component component)
    { var battle = component.GetComponentInParent<BattleCombatant>(); return battle != null && battle.Holding; }
    public static bool Defends(Component component)
    { var battle = component.GetComponentInParent<BattleCombatant>(); return battle != null && battle.DefendsPosition; }
    private void SetBool(string parameter, bool value)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters) if (p.type == AnimatorControllerParameterType.Bool && p.name == parameter)
        { animator.SetBool(parameter, value); return; }
    }
}
