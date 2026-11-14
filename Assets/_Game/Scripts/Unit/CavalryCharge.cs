using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Разгон → один удар по ближайшему ряду → восстановление, без повторного урона от контакта.</summary>
[DisallowMultipleComponent]
public class CavalryCharge : MonoBehaviour
{
    [Min(1f)] public float minimumSpeed = 4.5f;
    [Min(1f)] public float runDistance = 6f;
    [Min(1f)] public float impactRange = 2.7f;
    [Min(0f)] public float pushDistance = 2.5f;
    [Min(1)] public int impactDamage = 32;
    [Min(1)] public int spearCounterDamage = 16;
    [Min(1)] public int maximumTargets = 3;
    [Min(1f)] public float cooldown = 8f;
    [Min(0f)] public float fatigueSeconds = 4f;
    public bool ChargeReady => run.Ready(Time.time);
    public float RunDistance => run.Distance;
    public void ResetTransportMotion()
    {previousPosition=transform.position;previousDirection=Vector3.zero;run.Tick(Time.time,0,0,0,minimumSpeed,runDistance,true);}
    private readonly ChargeRun run = new ChargeRun();
    private BattleCombatant battle;
    private NavMeshAgent agent;
    private Unit unit;
    private Vector3 previousPosition, previousDirection;
    private readonly List<BattleCombatant> targets = new List<BattleCombatant>();
    private void Awake()
    { battle = BattleCombatant.Ensure(gameObject); agent = GetComponent<NavMeshAgent>(); unit = GetComponent<Unit>(); }
    private void OnEnable() { previousPosition = transform.position; previousDirection = Vector3.zero; }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (battle == null || agent == null || unit == null || unit.unitHealth <= 0 || battle.Kind != TroopType.Cavalry
            || !agent.enabled || !agent.isOnNavMesh) return;
        Vector3 delta = transform.position - previousPosition; delta.y = 0;
        previousPosition = transform.position;
        Vector3 direction = delta.sqrMagnitude > 0.00001f ? delta.normalized : previousDirection;
        float turn = previousDirection.sqrMagnitude < 0.001f || direction.sqrMagnitude < 0.001f ? 1 : Vector3.Dot(previousDirection, direction);
        float speed = agent.velocity.magnitude;
        // Телепорт/замена модели не считается разгоном.
        float travelled = delta.magnitude <= Mathf.Max(1, agent.speed * Time.deltaTime * 3) ? delta.magnitude : 0;
        bool retreating = GetComponent<GOScript>()?.IsRetreating ?? false;
        run.Tick(Time.time, speed, travelled, turn, minimumSpeed, runDistance, retreating || battle.IsStunned || UnitEat.ControlsMovement(this) || agent.isOnOffMeshLink);
        if (direction.sqrMagnitude > 0.001f) previousDirection = direction;
        if (retreating || !ChargeReady || direction.sqrMagnitude < 0.001f) return;
        targets.Clear();
        foreach (var other in BattleCombatant.All)
        {
            if (other == null || other == battle || !other.Alive || !FactionRules.AreEnemies(other.Side,battle.Side)) continue;
            Vector3 offset = other.transform.position - transform.position;
            if (Mathf.Abs(offset.y) > 1.2f) continue;
            offset.y = 0;
            if (offset.sqrMagnitude > impactRange * impactRange || offset.sqrMagnitude < 0.01f
                || Vector3.Dot(offset.normalized, direction) < 0.65f) continue;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (NavMesh.Raycast(transform.position, other.transform.position, out _, filter)) continue;
            targets.Add(other);
        }
        if (targets.Count == 0) return;
        targets.Sort((a, b) => (a.transform.position - transform.position).sqrMagnitude.CompareTo((b.transform.position - transform.position).sqrMagnitude));
        bool stopped = false;
        float momentum = Mathf.Clamp(run.PeakSpeed / Mathf.Max(1, minimumSpeed), 1f, 1.5f);
        run.Consume(Time.time, cooldown);
        int count = Mathf.Min(maximumTargets, targets.Count);
        for (int i = 0; i < count; i++)
        {
            var target = targets[i];
            if (target == null || !target.Alive || unit.unitHealth <= 0) break;
            var result = BattleRules.Charge(target.Kind, target.Holding, target.Cohesive,
                target.Frontal(transform.position), target.Rear(transform.position));
            var defender = target.GetComponent<Unit>();
            defender.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(impactDamage * momentum * result.damage)), gameObject, CombatHitKind.Charge);
            if (target.Alive) target.Impact(direction, pushDistance * result.push, result.stun, result.disorder);
            if (result.counter > 0)
                unit.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(spearCounterDamage * momentum * result.counter)), target.gameObject, CombatHitKind.Counter);
            if (result.stopped)
            {
                stopped = true;
                // Первый ряд копий не позволяет поражать стоящих за ним тем же натиском.
                break;
            }
        }
        if (unit.unitHealth > 0)
        {
            battle.Exhaust(fatigueSeconds);
            if (stopped) battle.Impact(-direction, 0.4f, 0.7f, 2f);
        }
    }
}
