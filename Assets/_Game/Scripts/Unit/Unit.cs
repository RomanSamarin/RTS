using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
public class Unit : MonoBehaviour
{
    public bool HasStarted { get; private set; }
    public float unitHealth;
    public float unitMaxHealth = 100f;
    [Tooltip("Optional portrait for the selection card. Uses the default portrait by unit type if empty.")]
    public Sprite selectionPortrait;
    private float initialHealthRatio = 1f;
    internal float InitialHealth => unitMaxHealth * initialHealthRatio;
    public void SetInitialHealthRatio(float ratio)
    { initialHealthRatio = Mathf.Clamp01(ratio); unitHealth = unitMaxHealth * initialHealthRatio; UpdateHealth(); }
    private float lastHostileDamage = float.NegativeInfinity;
    public bool RecentlyAttacked => Time.time - lastHostileDamage < 5f;
    public HealthTracker heathTracker;
    public Animator animator;
    public NavMeshAgent navMeshAgent;

    [Header("Настройки выпадающих ресурсов")]
    public int woodReward;
    public int stoneReward;
    public int wheatReward;
    public int fishReward;

    // Кэш, чтобы не искать компоненты каждый кадр.
    private AttackController attackController;
    private bool hasFollowParameter;
    [NonSerialized] private bool hasCatapultAnimation;

    private void Awake()
    {
        FactionMember.Ensure(gameObject);
        CacheReferences();
    }

    void Update()
    {
        if (TryGetComponent<RtsNetworkUnit>(out _)) return;
        if(SaveGameManager.IsLoading || hasCatapultAnimation)return;
        if (navMeshAgent == null || animator == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            return;

        // hasPath проверяем обязательно: без пути remainingDistance == Infinity,
        // и после ResetPath() по прибытии юнит «шёл» бы на месте.
        bool moving = !navMeshAgent.isStopped && !BattleCombatant.BlocksActions(this) && navMeshAgent.hasPath
                      && !navMeshAgent.pathPending
                      && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance;

        animator.SetBool("isMoving", moving);

        if (!hasFollowParameter)
            return;

        if (attackController == null)
            attackController = GetComponent<AttackController>();

        bool hasTarget = attackController != null && attackController.targetToAttack != null;

        // Анимация ходьбы на Base Layer включается только по IsFollowing.
        // Раньше этот флаг выставляли лишь скрипты рабочих, когда у юнита есть
        // цель (дерево, склад, жила). Из-за этого житель — у него цели нет —
        // и юнит, которого ведёт игрок через GOScript, «скользили» без анимации.
        // Здесь включаем ходьбу для любого реального движения, если юнит
        // ни за кем не идёт (иначе следующие состояния управляют флагом сами).
        if (!hasTarget)
            animator.SetBool("IsFollowing", moving);
    }

    void Start()
    {
        CacheReferences();

        unitHealth = unitMaxHealth * initialHealthRatio;
        HasStarted=true;
        FactionMember.Ensure(gameObject).RefreshControlRegistration();
        UpdateHealth();
        // The network unit wires up the simulation components on the host.
        if (TryGetComponent<RtsNetworkUnit>(out _)) return;
        BattleCombatant.Ensure(gameObject);
        MilitaryOrders.Ensure(gameObject);
        HospitalPatient.Ensure(gameObject);
        
    }

    /// <summary>
    /// Ищет компоненты, если они не назначены в инспекторе.
    /// Важно: не затираем animator, назначенный вручную или снаружи —
    /// Animator у моделей часто лежит на дочернем объекте, и
    /// GetComponent<Animator>() возвращал null, из-за чего юнит
    /// вообще не получал анимаций (не переключался в Moving/ходьбу).
    /// </summary>
    private void CacheReferences()
    {
        hasCatapultAnimation = GetComponent<Catapult>() != null || GetComponent<BatteringRam>() != null;
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (navMeshAgent == null)
            navMeshAgent = GetComponent<NavMeshAgent>();

        if (attackController == null)
            attackController = GetComponent<AttackController>();

        hasFollowParameter = HasBoolParameter(animator, "IsFollowing");
    }

    private static bool HasBoolParameter(Animator target, string parameterName)
    {
        if (target == null || target.runtimeAnimatorController == null)
            return false;

        foreach (AnimatorControllerParameter parameter in target.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.name == parameterName)
                return true;
        }

        return false;
    }

    private void OnDestroy()
    {
        FormationHold.Clear(gameObject);
        if (ManagerGO.Instance != null && ManagerGO.Instance.allUnitsList != null)
        {
            ManagerGO.Instance.allUnitsList.Remove(gameObject);
        }
    }

    private void UpdateHealth()
    {
        if (heathTracker != null)
        {
            heathTracker.UpdateSliderValue(unitHealth, unitMaxHealth);
        }
    }

    // Метод смерти: принимает объект юнита, который нанес последний удар
    public void Destroy(GameObject killer)
    {
        if (deathHandled) return;
        if (TryGetComponent<RtsNetworkUnit>(out var network) && network.IsSpawned)
        {
            if (!network.IsServer) return;
            deathHandled = true;
            network.NetworkObject.Despawn();
            return;
        }
        deathHandled = true;
        if (TrySinkShip()) return;
        var bandit = GetComponent<BanditRaider>();
        if (bandit != null) bandit.DropLoot();
        else if (killer != null&&!FactionMember.AreAllies(gameObject,killer))
        {
            GettingResources backpack = killer.GetComponent<GettingResources>();
            ResourcesMove targetMove = killer.GetComponent<ResourcesMove>();

            // Напрямую передаем награду в рюкзак юнита
            if (backpack != null)
            {
                backpack.TakeResource(this);
                Debug.Log($"Ресурсы дерева переданы юниту {killer.name}. Дерево: {woodReward}");
            }

            // Переключаем юнита в режим переноски
            if (targetMove != null)
            {
                targetMove.SetInventoryFull();
            }
        }

        UnityEngine.Object.Destroy(gameObject); 
    }

    // Изменили метод: теперь он обязательно требует указать, кто атакует (attacker)
    internal void TakeDamage(int damageToInflict, GameObject attacker, CombatHitKind hit = CombatHitKind.Melee)
    {
        if (deathHandled) return;
        if (TryGetComponent<RtsNetworkUnit>(out var network) && network.IsSpawned && !network.IsServer) return;
        if(attacker!=null&&(HospitalPatient.Inside(gameObject)||ShipCrewMember.Aboard(gameObject)||ShipPassenger.Aboard(gameObject)))return;
        if(attacker!=null&&FactionMember.AreAllies(attacker,gameObject))return;
        damageToInflict = TroopCombat.ResolveDamage(damageToInflict, attacker, this, hit);
        unitHealth -= damageToInflict;
        if (attacker != null && damageToInflict > 0)
        {
            lastHostileDamage = Time.time;
            var eating = GetComponent<UnitEat>();
            if (eating != null && eating.IsBusy) eating.CancelForCommand(false);
        }
        UpdateHealth();

        if (unitHealth <= 0)
        {
            Destroy(attacker);
            return;
        }
        if (damageToInflict > 0 && attacker != null && network != null)
            network.RespondToHit(attacker);
    }

    // Оставили старый метод для совместимости
    public void Destroy()
    {
        if (deathHandled) return;
        if (GetComponent<ShipHull>() != null || (TryGetComponent<RtsNetworkUnit>(out var network) && network.IsSpawned))
        {
            Destroy((GameObject)null);
            return;
        }
        UnityEngine.Object.Destroy(gameObject);
    }
    private bool TrySinkShip()
    {
        var ship = GetComponent<ShipHull>();
        if (ship == null) return false;
        SetInitialHealthRatio(0);
        ship.BeginSinking();
        return true;
    }
    private bool deathHandled;
    public void Heal(float amount)
    {
        if(deathHandled||unitHealth<=0||amount<=0)return;
        unitHealth=Mathf.Min(unitMaxHealth,unitHealth+amount);UpdateHealth();
    }
}
