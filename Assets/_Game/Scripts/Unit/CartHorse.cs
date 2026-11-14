using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(GOScript))]
[RequireComponent(typeof(Unit), typeof(AttackController))]
public class CartHorse : MonoBehaviour
{
    private static readonly List<CartHorse> horses = new List<CartHorse>();
    public static IReadOnlyList<CartHorse> All => horses;
    [Min(0.5f)] public float hitchDistance = 3f;
    public FoodCart Cart { get; private set; }
    public SiegeLadderCart SiegeCart { get; private set; }
    public Barracks TrainingOwner { get; set; }
    public bool IsHitched => Cart != null || SiegeCart != null;
    public bool IsAlive => isActiveAndEnabled && (unit == null || unit.unitHealth > 0);
    private NavMeshAgent agent;
    private Unit unit;
    private Animator animator;
    private bool wasAgentEnabled;
    private bool restoreAgentOnEnable;
    private void Cache()
    { if (agent == null) agent = GetComponent<NavMeshAgent>(); if (unit == null) unit = GetComponent<Unit>(); if (animator == null) animator = GetComponentInChildren<Animator>(); }
    private void Awake()
    {
        Cache(); var move = GetComponent<ResourcesMove>(); if (move != null) { move.role = ResourcesMove.UnitRole.Horse; move.ClearWorkplace(); }
        if (unit == null) unit = gameObject.AddComponent<Unit>();
        var attack = GetComponent<AttackController>(); if (attack == null) attack = gameObject.AddComponent<AttackController>();
        attack.autoAcquireTargets = false; attack.unitDamage = 0;
        FactionMember.Ensure(gameObject);
        var population = GetComponent<UnitAdd>(); if (population != null) population.enabled = false;
        var house = HousesScript.FindHouseOf(gameObject); if (house != null) house.Evict(gameObject);
    }
    private void OnEnable() { if (!horses.Contains(this)) horses.Add(this); Cache(); if (restoreAgentOnEnable) RestoreAgent(); }
    public bool TryHitch(FoodCart target)
    {
        Cache();
        if (target != null && Cart == target && target.AttachedHorse == this && IsAlive) return true;
        if (!IsAlive || TrainingOwner != null || target == null || !target.isActiveAndEnabled || target.AttachedHorse != null || IsHitched) return false;
        if(!FactionMember.AreAllies(gameObject,target.gameObject))return false;
        if (Vector3.Distance(transform.position, target.transform.position) > hitchDistance) return false;
        GetComponent<GOScript>()?.StopMoving();
        wasAgentEnabled = agent != null && agent.enabled;
        if (agent != null)
        { if (agent.enabled && agent.isOnNavMesh) agent.ResetPath(); agent.enabled = false; }
        Cart = target; target.SetHorse(this); return true;
    }
    public bool TryHitch(SiegeLadderCart target)
    {
        Cache();
        if (target != null && SiegeCart == target && target.AttachedHorse == this && IsAlive) return true;
        if (!IsAlive || TrainingOwner != null || target == null || !target.isActiveAndEnabled || target.AttachedHorse != null || IsHitched) return false;
        if (!FactionMember.AreAllies(gameObject, target.gameObject)) return false;
        if (Vector3.Distance(transform.position, target.transform.position) > hitchDistance) return false;
        GetComponent<GOScript>()?.StopMoving();
        wasAgentEnabled = agent != null && agent.enabled;
        if (agent != null)
        { if (agent.enabled && agent.isOnNavMesh) agent.ResetPath(); agent.enabled = false; }
        SiegeCart = target; target.SetHorse(this); return true;
    }
    public void Unhitch()
    {
        var previous = Cart; Cart = null;
        var previousSiege = SiegeCart; SiegeCart = null;
        if (previous != null) previous.ClearHorse(this);
        if (previousSiege != null) previousSiege.ClearHorse(this);
        Cache();
        if (wasAgentEnabled && (unit == null || unit.unitHealth > 0)) RestoreAgent();
        SetMoving(false);
    }
    private void RestoreAgent()
    {
        restoreAgentOnEnable = true;
        if (agent != null && IsAlive)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (NavMesh.SamplePosition(transform.position, out var hit, 3f, filter))
            { transform.position = hit.position; agent.enabled = true; restoreAgentOnEnable = false; }
        }
    }
    private void LateUpdate()
    {
        if(SaveGameManager.IsLoading)return;
        if (SiegeCart != null)
        {
            if (!IsAlive || !SiegeCart.isActiveAndEnabled || !FactionMember.AreAllies(gameObject, SiegeCart.gameObject)) { Unhitch(); return; }
            transform.SetPositionAndRotation(SiegeCart.HorsePosition, SiegeCart.transform.rotation);
            SetMoving(SiegeCart.IsMoving); return;
        }
        if (Cart == null) return;
        if (!IsAlive || !Cart.isActiveAndEnabled) { Unhitch(); return; }
        transform.SetPositionAndRotation(Cart.HorsePosition, Cart.transform.rotation);
        SetMoving(Cart.IsMoving);
    }
    private void SetMoving(bool moving)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var parameter in animator.parameters)
            if (parameter.name == "isMoving" && parameter.type == AnimatorControllerParameterType.Bool) animator.SetBool(parameter.name, moving);
    }
    private void OnDisable() { horses.Remove(this); Unhitch(); }
}
