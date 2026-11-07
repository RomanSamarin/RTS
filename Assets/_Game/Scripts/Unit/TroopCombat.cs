using UnityEngine;
using UnityEngine.AI;

/// <summary>Единые характеристики и контртипы. Auto не меняет характеристики рабочих.</summary>
[DisallowMultipleComponent]
public class TroopCombat : MonoBehaviour
{
    public TroopType troopType = TroopType.Auto;
    public CartHorse MountedHorse { get; private set; }
    private bool configured;
    private void Start() { if (!configured && troopType != TroopType.Auto) Configure(troopType); }
    public void Configure(TroopType kind)
    {
        troopType = kind; configured = true;
        var unit = GetComponent<Unit>();
        float ratio = unit.unitMaxHealth > 0 && unit.unitHealth > 0 ? unit.unitHealth / unit.unitMaxHealth : 1f;
        float health = kind == TroopType.Swordsman ? 140 : kind == TroopType.Spearman ? 100 : kind == TroopType.Cavalry ? 160 : 70;
        unit.unitMaxHealth = health; unit.SetInitialHealthRatio(Mathf.Clamp01(ratio));
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = kind == TroopType.Cavalry ? 6f : kind == TroopType.Swordsman ? 3f : 3.5f;
        if (agent != null) GetComponent<GOScript>()?.SetBaseSpeed(agent.speed);
        if (agent != null && kind == TroopType.Cavalry) agent.areaMask &= ~WallNavigation.AreaMask;
        var attack = GetComponent<AttackController>();
        if (attack != null) attack.unitDamage = kind == TroopType.Cavalry ? 22 : kind == TroopType.Swordsman ? 18 : kind == TroopType.Spearman ? 12 : 6;
        var archer = GetComponent<Archer>();
        if (archer != null && kind == TroopType.Archer)
        { archer.damage = 16; archer.attackRange = 14; archer.attackCooldown = 1.5f; archer.firstShotDelay = 0.5f; }
        var battle = BattleCombatant.Ensure(gameObject);
        if (battle != null && agent != null) battle.SetBaseSpeed(agent.speed);
        if (kind == TroopType.Cavalry && GetComponent<CavalryCharge>() == null) gameObject.AddComponent<CavalryCharge>();
    }
    public void Mount(CartHorse horse)
    {
        MountedHorse = horse;
        horse.TrainingOwner = null;
        horse.GetComponent<GOScript>()?.StopMoving();
        horse.gameObject.SetActive(false);
    }
    public bool CanReleaseHorse(out Vector3 position)
    {
        position = transform.position;
        if (MountedHorse == null) return true;
        var agent = MountedHorse.GetComponent<NavMeshAgent>();
        if (agent == null) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(position, out var hit, 3f, filter)) return false;
        position = hit.position; return true;
    }
    public void ReleaseHorse()
    {
        if (MountedHorse == null) return;
        if (!CanReleaseHorse(out var position)) return;
        var horse = MountedHorse; MountedHorse = null;
        horse.transform.position = position;
        horse.gameObject.SetActive(true);
        var agent = horse.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled) agent.Warp(position);
    }
    private void OnDestroy() { if (MountedHorse != null) Destroy(MountedHorse.gameObject); }
    public static int ResolveDamage(int damage, GameObject attacker, Unit defender, CombatHitKind hit = CombatHitKind.Melee)
    {
        if (damage <= 0) return 0;
        // Голод и падение без атакующего не получают боевых бонусов/штрафов.
        if (attacker == null) return damage;
        TroopType from = BattleCombatant.TypeOf(attacker), to = BattleCombatant.TypeOf(defender.gameObject);
        var source = BattleCombatant.Ensure(attacker);
        var target = BattleCombatant.Ensure(defender.gameObject);
        float multiplier = TroopBalance.Multiplier(from, to) * (source != null ? source.DamageMultiplier : 1);
        if (target != null)
        {
            bool front = target.Frontal(attacker.transform.position);
            multiplier *= BattleRules.Incoming(hit, to, target.Holding, target.Cohesive, target.IsDisordered, front);
            if (hit == CombatHitKind.Ranged && to == TroopType.Swordsman && !front) multiplier *= 1.7f;
        }
        return Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
    }
}
