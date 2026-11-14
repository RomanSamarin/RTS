using UnityEngine;
using UnityEngine.AI;
using _Game.Scripts.UI;

[DisallowMultipleComponent]
public class SiegeLadderInstaller : MonoBehaviour
{
    public const float InstallationSeconds = 10f;
    [Min(1)] public float maxOrderDistance = 40f;
    [Min(1)] public float maxCartDistance = 30f;
    [Min(.1f)] public float pickupSeconds = 1.2f;
    public bool climbAfterInstalling = true;
    public bool IsBusy => site != null;
    public string Status { get; private set; } = "";
    public float Progress01 => Mathf.Clamp01(work / InstallationSeconds);
    public bool IsPickingUp => IsBusy && pickingUp;
    public bool IsInstalling => IsBusy && work > 0;
    public bool IsCarrying => IsBusy && carrying && !IsInstalling;
    private SiegeLadderSite site;
    private SiegeLadderCart cart;
    private GOScript command;
    private NavMeshAgent agent;
    private GettingResources cargo;
    private bool carrying;
    private bool pickingUp;
    private float pickupProgress;
    private SiegeLadderAnimation animationDriver;
    private int token;
    private float work, nextPath, lastProgress;
    private Vector3 previousPosition;

    private void Awake()
    {
        command = GetComponent<GOScript>(); agent = GetComponent<NavMeshAgent>();
        cargo = GetComponent<GettingResources>();
        animationDriver = SiegeLadderAnimation.Ensure(gameObject);
        if (command != null) command.ManualOrderIssued += Cancel;
    }
    public void RestoreSavedHooks()
    {
        command = GetComponent<GOScript>(); agent = GetComponent<NavMeshAgent>(); cargo = GetComponent<GettingResources>();
        animationDriver = SiegeLadderAnimation.Ensure(gameObject);
        if (command != null) { command.ManualOrderIssued -= Cancel; command.ManualOrderIssued += Cancel; }
    }
    public static bool CanInstall(GameObject soldier) => soldier != null && FactionMember.IsPlayer(soldier)
        && WallStation.CanUse(soldier) && soldier.GetComponent<ResourcesMove>()?.role == ResourcesMove.UnitRole.Soldier
        && soldier.GetComponent<GOScript>() != null;
    private NavMeshQueryFilter Filter => new NavMeshQueryFilter
        { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask & ~WallNavigation.AreaMask };
    private bool Route(Vector3 from, Vector3 to)
    {
        var path = new NavMeshPath();
        return NavMesh.CalculatePath(from, to, Filter, path) && path.status == NavMeshPathStatus.PathComplete;
    }
    private bool CartPoint(SiegeLadderCart wagon, out Vector3 point)
    {
        point = default;
        if (wagon == null || !wagon.isActiveAndEnabled || wagon.GetComponent<Unit>().unitHealth <= 0
            || !FactionMember.AreAllies(gameObject, wagon.gameObject)) return false;
        if (!NavMesh.SamplePosition(wagon.transform.position, out var hit, wagon.loadingDistance, Filter)
            || Vector3.Distance(hit.position, wagon.transform.position) > wagon.loadingDistance) return false;
        point = hit.position; return Route(transform.position, point);
    }
    public bool TryAssign(WallSegment wall, Vector3 clicked)
    {
        if (!CanInstall(gameObject) || agent == null || !agent.enabled || !agent.isOnNavMesh
            || Vector3.Distance(transform.position, clicked) > maxOrderDistance
            || !SiegeLadderSite.TryPoint(wall, clicked, transform.position, agent, out var foot, out var top)
            || !Route(transform.position, foot)) return false;
        bool hasLadder = cargo != null && cargo.GetStoredAmount(ResourceType.SiegeLadder) > 0;
        SiegeLadderCart best = null; float nearest = float.PositiveInfinity;
        if (!hasLadder)
        {
            foreach (var wagon in FindObjectsOfType<SiegeLadderCart>())
            {
                if (wagon.AvailableLadders <= 0 || Vector3.Distance(wagon.transform.position, foot) > maxCartDistance
                    || !CartPoint(wagon, out var pickup) || !Route(pickup, foot)) continue;
                float distance = Vector3.Distance(transform.position, pickup);
                if (distance < nearest) { nearest = distance; best = wagon; }
            }
            if (best == null) { Status = "Нет доступной повозки со свободной лестницей"; return false; }
        }
        // All checks precede cancellation of the previous command.
        command.StopMoving(); command.NotifyMilitaryOrder();
        FormationHold.Clear(gameObject); GetComponent<BattleCombatant>()?.PrepareForOrder();
        var attack = GetComponent<AttackController>(); if (attack != null) attack.targetToAttack = null;
        if (!command.TryBeginSupplyMove(out token)) return false;
        if (!hasLadder && !best.ReserveLadder(this)) { command.EndSupplyMove(token); return false; }
        if (cargo == null) cargo = gameObject.AddComponent<GettingResources>();
        cart = best; carrying = hasLadder;
        site = SiegeLadderSite.Reserve(wall, foot, top, FactionMember.Of(gameObject));
        pickingUp = false; pickupProgress = 0;
        work = 0; nextPath = 0; lastProgress = Time.time; previousPosition = transform.position;
        Status = carrying ? "Несёт лестницу к стене" : "Идёт за лестницей";
        return true;
    }
    private void Update()
    {
        if (SaveGameManager.IsLoading) return;
        if (site == null) { if (token != 0) Cancel(); return; }
        if (!CanInstall(gameObject) || !site.IsValid || !agent.enabled || !agent.isOnNavMesh
            || !command.OwnsSupplyOrder(token) || BattleCombatant.BlocksActions(this))
        { Fail("Установка отменена"); return; }
        Vector3 target = site.transform.position;
        if (!carrying)
        {
            if (!CartPoint(cart, out target) || Vector3.Distance(cart.transform.position, site.transform.position) > maxCartDistance)
            { Fail("Повозка недоступна"); return; }
            if (Vector3.Distance(transform.position, cart.transform.position) <= cart.loadingDistance)
            {
                agent.ResetPath(); agent.isStopped = true;
                pickingUp = true; pickupProgress += Time.deltaTime;
                lastProgress = Time.time; Status = "Берёт лестницу";
                var direction = Vector3.ProjectOnPlane(cart.transform.position - transform.position, Vector3.up);
                if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction);
                animationDriver.Set(pickup: true);
                if (pickupProgress < Mathf.Max(.1f, pickupSeconds)) return;
                if (!cart.PickupLadder(this, cargo)) { Fail("Лестница недоступна"); return; }
                cart = null; carrying = true; pickingUp = false; pickupProgress = 0; nextPath = 0; lastProgress = Time.time;
                Status = "Несёт лестницу к стене"; target = site.transform.position;
            }
            else { pickingUp = false; pickupProgress = 0; }
        }
        if (carrying && cargo.GetStoredAmount(ResourceType.SiegeLadder) <= 0) { Fail("Лестница потеряна"); return; }
        if (carrying && Vector3.Distance(transform.position, target) <= .6f)
        {
            if (!site.ClearSpace(agent)) { Fail("Место установки занято"); return; }
            agent.ResetPath(); agent.isStopped = true;
            transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(site.Top - target, Vector3.up));
            work += Time.deltaTime; Status = $"Устанавливает лестницу: {Mathf.CeilToInt(InstallationSeconds - work)} с";
            if (work >= InstallationSeconds)
            {
                cargo.RemoveStoredAmount(ResourceType.SiegeLadder, 1);
                var installed = site;
                site.Complete(); site = null; carrying = false;
                command.EndSupplyMove(token); token = 0; Status = "Лестница установлена";
                animationDriver.Set();
                if (climbAfterInstalling)
                {
                    var climber = GetComponent<SiegeLadderClimber>() ?? gameObject.AddComponent<SiegeLadderClimber>();
                    climber.TryAssign(installed);
                }
            }
            return;
        }
        work = 0;
        if (Vector3.Distance(previousPosition, transform.position) > .2f)
        { previousPosition = transform.position; lastProgress = Time.time; }
        if (Time.time - lastProgress > 30) { Fail("Путь к лестнице или стене заблокирован"); return; }
        if (Time.time < nextPath) return;
        nextPath = Time.time + .5f;
        if (!Route(transform.position, target) || !command.MoveForSupply(token, target, .2f)) Fail("Нет пути к месту установки");
    }
    private void Fail(string reason) { Cancel(); Status = reason; Debug.Log(reason, this); }
    public void Cancel()
    {
        if (cart != null) cart.ReleaseLadder(this);
        cart = null;
        if (site != null) { site.gameObject.SetActive(false); Destroy(site.gameObject); }
        site = null; carrying = false; pickingUp = false; pickupProgress = 0; work = 0;
        if (animationDriver != null) animationDriver.Set();
        if (command != null) command.EndSupplyMove(token);
        token = 0;
        // A picked-up ladder stays in the soldier's real cargo for retry/death loot.
    }
    private void OnDisable() => Cancel();
    private void OnDestroy() { Cancel(); if (command != null) command.ManualOrderIssued -= Cancel; }
}
