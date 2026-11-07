using UnityEngine;
using UnityEngine.AI;

/// <summary>Выбранная охрана следует за повозкой, в том числе во время рейса за едой.</summary>
public class ArmyEscort : MonoBehaviour
{
    [Min(1f)] public float guardDistance = 6f;
    private GOScript command;
    private int routeToken;
    private float nextMove;
    private void Awake() { command = GetComponent<GOScript>(); if (command == null) command = gameObject.AddComponent<GOScript>(); }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        var member = GetComponent<ArmyMember>(); var food = GetComponent<UnitEat>();
        var unit = GetComponent<Unit>(); var attack = GetComponent<AttackController>();
        var army = member != null ? member.Army : null; var cart = army != null ? army.SupplyCart : null;
        if (cart == null || !cart.isActiveAndEnabled || unit == null || unit.unitHealth <= 0 || UnitEat.ControlsMovement(this)
            || command.HasPlayerOrder || ArmyRejoin.Returning(gameObject) || GetComponent<MilitaryOrders>()?.Current==MilitaryOrders.Order.Hold
            || (GetComponent<Catapult>()?.HoldingPosition??false)
            || (food != null && food.InCombat()) || (attack != null && attack.targetToAttack != null))
        { StopRoute(); return; }
        if (Vector3.Distance(transform.position, cart.transform.position) <= guardDistance) { StopRoute(); return; }
        if (Time.time < nextMove) return; nextMove = Time.time + 0.75f;
        if (!command.OwnsSupplyOrder(routeToken) && !command.TryBeginSupplyMove(out routeToken)) return;
        var agent = GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) { StopRoute(); return; }
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        float angle = Mathf.Abs(GetInstanceID() % 8) * Mathf.PI / 4f;
        Vector3 point = cart.transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Mathf.Max(1f, guardDistance * 0.5f);
        if (!NavMesh.SamplePosition(point, out var hit, 3f, filter) || !command.MoveForSupply(routeToken, hit.position)) StopRoute();
    }
    private void StopRoute() { if (command != null) command.EndSupplyMove(routeToken); routeToken = 0; }
    private void OnDisable() { StopRoute(); }
}
