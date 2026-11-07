using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class UnitEat : MonoBehaviour
{
    public enum FeedingMode { Settlement, Expedition }
    public FeedingMode mode;
    [Header("Автоматическое питание армии")]
    public bool automaticArmySupply = true;
    [Min(1f)] public float settlementRadius = 60f;
    [Min(0f)] public float settlementHysteresis = 10f;
    public string SupplyWarning { get; private set; } = "";
    public float SecondsUntilStarvation => WantEat ? hunger.Active ? hunger.Remaining(Time.time) : Mathf.Max(0, starvationGrace) : 0f;
    private bool IsSoldier
    {
        get { var worker = GetComponent<ResourcesMove>(); return worker != null && worker.role == ResourcesMove.UnitRole.Soldier; }
    }
    // Сохранено для совместимости со сценой. Поиск использует реестр FoodService.
    public List<FoodCart> FoodCarts = new List<FoodCart>();
    public bool WantEat;
    [Min(1)] public int TimeToEat = 1;
    [Min(1)] public int portionsPerMeal = 1;
    [Min(0.1f)] public float eatingSeconds = 5f;
    [Min(0.5f)] public float arrivalDistance = 1.5f;
    [Min(1f)] public float travelTimeout = 60f;
    [Header("Отсрочка и урон от голода")]
    [Tooltip("Секунды с начала голода до первого урона. Поиск еды начинается сразу.")]
    [Min(0f)] public float starvationGrace = 120f;
    [Min(1f)] public float starvationInterval = 20f;
    [Min(1)] public int starvationDamage = 10;
    public Animator animator;
    public Unit unit;
    public bool IsBusy { get; private set; }
    public bool IsEating { get; private set; }
    public string CurrentTask => IsEating ? "Ест" : IsBusy ? "Идёт за едой" : WantEat ? "Ищет еду" : "";
    private NavMeshAgent agent;
    private GOScript command;
    private AttackController attack;
    private FoodService target;
    private Transform previousTarget;
    private float nextMeal, nextSearch, tripStarted, eatingUntil;
    private readonly HungerTimer hunger = new HungerTimer();
    private bool mealIssued;
    private FeedingMode tripMode;
    private int movementToken;
    public struct NutritionState
    {
        public bool hungry;
        public float nextMeal, hungrySince, nextDamage;
        public bool hungerActive;
        public float damageBeginsAt;
    }
    public NutritionState CaptureNutrition()
    {
        if (WantEat) hunger.Begin(Time.time, starvationGrace);
        return new NutritionState { hungry = WantEat, nextMeal = nextMeal, hungrySince = hunger.StartedAt,
            nextDamage = hunger.NextDamageAt, hungerActive = WantEat && hunger.Active, damageBeginsAt = hunger.DamageBeginsAt };
    }
    public void RestoreNutrition(NutritionState state)
    {
        WantEat = state.hungry; nextMeal = state.nextMeal; hunger.Reset();
        if (state.hungry && state.hungerActive) hunger.Restore(state.hungrySince, state.damageBeginsAt, state.nextDamage);
    }

    public static bool ControlsMovement(Component component)
    {
        if(HospitalPatient.Controls(component))return true;
        var eating = component.GetComponentInParent<UnitEat>();
        return eating != null && eating.isActiveAndEnabled && eating.IsBusy
            && eating.command != null && eating.command.OwnsFoodOrder(eating.movementToken);
    }
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        command = GetComponent<GOScript>();
        if (command == null) command = gameObject.AddComponent<GOScript>();
        attack = GetComponent<AttackController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (unit == null) unit = GetComponent<Unit>();
    }
    private void OnEnable() { nextMeal = Time.time + Mathf.Max(1, TimeToEat) * 60f; }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        // Животные и повозки не получают человеческие команды питания.
        if (GetComponent<FoodCart>() != null || GetComponent<CartHorse>() != null||GetComponent<Catapult>()!=null)
        { if (IsBusy) CancelForCommand(false); return; }
        if (unit == null || !unit.enabled || unit.unitHealth <= 0f) return;
        RefreshFeedingMode();
        if (IsBusy && (command == null || !command.OwnsFoodOrder(movementToken))) CancelForCommand(false);
        // Проверяется и до начала похода, и во время движения/еды.
        bool inCombat = InCombat();
        bool playerOrder = command != null && command.HasPlayerOrder;
        if ((inCombat || playerOrder) && IsBusy) CancelForCommand(false);
        if (!WantEat && Time.time >= nextMeal)
        {
            WantEat = true;
        }
        if (!WantEat) { hunger.Reset(); UpdateSupplyWarning(); return; }
        if (!hunger.Active)
        {
            hunger.Begin(Time.time, starvationGrace);
            nextSearch = 0;
            Debug.LogWarning($"Юнит голоден: {name}. До урона: {starvationGrace:0} с.", this);
        }
        UpdateSupplyWarning();
        // Выданная порция уже у юнита: она доедается даже при потере здания.
        if (IsEating)
        {
            if (Time.time >= eatingUntil) FinishMeal();
            return;
        }
        // Сначала пробуем получить порцию: прибытие к еде в этот кадр спасает
        // от урона на самой границе отсрочки. Бой и приказ игрока имеют приоритет.
        if (!inCombat && !playerOrder) UpdateFoodTrip();
        if (WantEat && !IsEating && hunger.TryDamage(Time.time, starvationInterval))
        {
            unit.TakeDamage(Mathf.Max(1, starvationDamage), null);
            if (unit.unitHealth <= 0f) CancelForCommand();
        }
    }
    private void UpdateFoodTrip()
    {
        if (IsBusy && mode != tripMode) CancelForCommand();
        if (command != null && command.HasPlayerOrder) { if (IsBusy) CancelForCommand(false); return; }
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) { if (IsBusy) CancelForCommand(); return; }
        if (IsBusy && (target == null || !target.isActiveAndEnabled || target.AvailableMeals < portionsPerMeal || !ServiceAllowed(target)))
            CancelForCommand();
        if (!IsBusy)
        {
            if (Time.time < nextSearch) return;
            nextSearch = Time.time + 2f;
            target = FindFood();
            if (target == null) return;
            if (command == null || !command.TryBeginFoodMove(arrivalDistance, out movementToken))
            { target = null; return; }
            previousTarget = attack != null ? attack.targetToAttack : null;
            if (attack != null) attack.targetToAttack = null;
            tripMode = mode; tripStarted = Time.time; IsBusy = true;
            SetBool("isAttacking", false);
        }
        if (Time.time - tripStarted > Mathf.Max(1f, travelTimeout)) { CancelForCommand(); return; }
        if (Vector3.Distance(transform.position, target.ServicePoint) <= arrivalDistance)
        {
            if (!command.StopForFood(movementToken)) { CancelForCommand(false); return; }
            if (!target.TryServe(Mathf.Max(1, portionsPerMeal))) { CancelForCommand(); return; }
            mealIssued = true; IsEating = true;
            eatingUntil = Time.time + Mathf.Max(0.1f, eatingSeconds);
            SetBool("IsFollowing", false); SetBool("isMoving", false); SetBool("isAttacking", false);
            SetBool("isEating", true);
        }
        else
        {
            if (!command.MoveForFood(movementToken, target.ServicePoint))
            { CancelForCommand(); return; }
            SetBool("IsFollowing", true);
        }
    }
    public bool InCombat()
    {
        if (unit != null && unit.RecentlyAttacked) return true;
        var enemy = GetComponent<EnemyAi>();
        if (enemy != null && enemy.isActiveAndEnabled
            && ((enemy.UnitTarget != null && enemy.UnitTarget.unitHealth > 0)
                || (enemy.BuildingTarget != null && !enemy.BuildingTarget.IsDead))) return true;
        if (attack == null || attack.targetToAttack == null || !attack.targetToAttack.gameObject.activeInHierarchy) return false;
        var targetObject = attack.targetToAttack;
        // Добыча, охота и возврат в своё здание — обычная работа, её можно прервать.
        if (targetObject.GetComponentInParent<WildlifeAnimal>() != null || targetObject.GetComponentInParent<FoodResourceSource>() != null
            || targetObject.GetComponentInParent<OreDeposit>() != null || targetObject.GetComponentInParent<FishingSpot>() != null
            || targetObject.GetComponentInParent<StoneQuarry>() != null || targetObject.GetComponentInParent<FarmField>() != null) return false;
        var opponent = attack.targetToAttack.GetComponentInParent<Unit>();
        if (opponent != null && opponent.unitHealth > 0)
        {
            if (opponent.woodReward > 0 || opponent.stoneReward > 0 || opponent.wheatReward > 0 || opponent.fishReward > 0) return false;
            var opponentAttack = opponent.GetComponent<AttackController>();
            return FactionMember.AreEnemies(gameObject,opponent.gameObject);
        }
        var camp = targetObject.GetComponentInParent<BanditCamp>();
        return camp != null && !camp.IsDestroyed;
    }
    private FoodService FindFood()
    {
        FoodService best = null; float distance = float.PositiveInfinity;
        foreach (var service in FoodService.All)
        {
            if (service == null || service.AvailableMeals < Mathf.Max(1, portionsPerMeal)) continue;
            if (!ServiceAllowed(service)) continue;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(service.ServicePoint, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float length = 0;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i-1], path.corners[i]);
            if (length >= distance) continue;
            distance = length; best = service;
        }
        return best;
    }
    private void RefreshFeedingMode()
    {
        if (!automaticArmySupply || !IsSoldier) return;
        float nearest = float.PositiveInfinity;
        foreach (var service in FoodService.All)
            if (service is Tavern && !(service is Hospital) && service.isActiveAndEnabled&&FactionMember.CanUse(gameObject,service.gameObject)) nearest = Mathf.Min(nearest, HorizontalDistance(transform.position, service.ServicePoint));
        mode = ArmySupplyRules.UseExpedition(mode == FeedingMode.Expedition, nearest, settlementRadius, settlementHysteresis)
            ? FeedingMode.Expedition : FeedingMode.Settlement;
    }
    private bool ServiceAllowed(FoodService service)
    {
        if(service is Hospital)return false;
        if(service==null||!FactionMember.CanUse(gameObject,service.gameObject))return false;
        if (mode == FeedingMode.Expedition) return service is FoodCart cart && cart.CanSupply(this);
        if (!(service is Tavern)) return false;
        return !automaticArmySupply || !IsSoldier
            || HorizontalDistance(transform.position, service.ServicePoint) <= Mathf.Max(1f, settlementRadius) + Mathf.Max(0, settlementHysteresis);
    }
    private void UpdateSupplyWarning()
    {
        SupplyWarning = "";
        if (!WantEat || IsEating) return;
        if (mode == FeedingMode.Settlement) { SupplyWarning = "Голоден: нужна еда в таверне"; return; }
        var member = GetComponent<ArmyMember>(); var army = member != null ? member.Army : null;
        var cart = army != null ? army.SupplyCart : null;
        SupplyWarning = army == null ? "В походе нужны назначенная армия и повозка"
            : cart == null || !cart.isActiveAndEnabled ? "Голоден: нет снабженческой повозки"
            : !cart.CanSupply(this) ? "Голоден: повозка вне радиуса снабжения"
            : cart.AvailableMeals < Mathf.Max(1, portionsPerMeal) ? "Голоден: в повозке нет еды"
            : "Голоден: еда доступна у повозки";
    }
    private static float HorizontalDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
    public void FeedAtHospital()
    {
        WantEat=false;hunger.Reset();nextMeal=Time.time+Mathf.Max(1,TimeToEat)*60f;SupplyWarning="";
    }
    private void FinishMeal()
    {
        WantEat = false;
        hunger.Reset();
        nextMeal = Time.time + Mathf.Max(1, TimeToEat) * 60f;
        mealIssued = false;
        CancelForCommand();
    }
    public void CancelForCommand(bool restoreWorkTarget = true)
    {
        bool wasBusy = IsBusy;
        // Прерывание анимации после выдачи не списывает вторую порцию.
        if (mealIssued) { WantEat = false; hunger.Reset(); nextMeal = Time.time + Mathf.Max(1, TimeToEat) * 60f; }
        mealIssued = false; IsBusy = IsEating = false; target = null;
        SetBool("isEating", false);
        bool released = command != null && command.EndFoodMove(movementToken);
        movementToken = 0;
        if (wasBusy && released && restoreWorkTarget && attack != null && attack.targetToAttack == null
            && previousTarget != null && previousTarget.gameObject.activeInHierarchy) attack.targetToAttack = previousTarget;
        previousTarget = null;
    }
    private void SetBool(string parameter, bool value)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var item in animator.parameters)
            if (item.type == AnimatorControllerParameterType.Bool && item.name == parameter) { animator.SetBool(parameter, value); return; }
    }
    private void OnDisable() { CancelForCommand(); }
}
