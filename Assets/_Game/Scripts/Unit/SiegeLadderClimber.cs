using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class SiegeLadderClimber : MonoBehaviour
{
    [Min(.1f)] public float climbingSpeed = 1f;
    [Min(1)] public float approachTimeout = 45f;
    public bool IsBusy => ladder != null;
    public bool IsClimbing => climbing;
    public string Status { get; private set; } = "";
    private SiegeLadderSite ladder;
    private NavMeshAgent agent;
    private GOScript command;
    private Unit unit;
    private Rigidbody body;
    private SiegeLadderAnimation animationDriver;
    private int token;
    private bool climbing, savedAgentEnabled, savedKinematic, savedGravity;
    private Vector3 start, crest, landing, previousPosition;
    private float progress, duration, nextPath, lastProgressAt;
    public static bool CanClimb(GameObject soldier) => SiegeLadderInstaller.CanInstall(soldier);
    private void Awake() { Cache(); if (command != null) command.ManualOrderIssued += Cancel; }
    private void Cache()
    {
        agent = GetComponent<NavMeshAgent>(); command = GetComponent<GOScript>(); unit = GetComponent<Unit>();
        body = GetComponent<Rigidbody>(); animationDriver = SiegeLadderAnimation.Ensure(gameObject);
    }
    public void RestoreSavedHooks()
    {
        Cache();
        if (command != null) { command.ManualOrderIssued -= Cancel; command.ManualOrderIssued += Cancel; }
        if (climbing && agent != null) agent.enabled = false;
        animationDriver.Set(climb: climbing);
    }
    public bool TryAssign(SiegeLadderSite target)
    {
        if (!CanClimb(gameObject) || target == null || !target.Installed || !target.IsValid
            || !target.CanUse(gameObject) || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        var path = new NavMeshPath();
        if (!agent.CalculatePath(target.transform.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        command.StopMoving(); command.NotifyMilitaryOrder();
        FormationHold.Clear(gameObject); GetComponent<WallStation>()?.Release();
        GetComponent<BattleCombatant>()?.PrepareForOrder();
        var attack = GetComponent<AttackController>(); if (attack != null) attack.targetToAttack = null;
        if (!command.TryBeginSupplyMove(out token)) return false;
        ladder = target; target.Join(this); progress = 0; nextPath = 0;
        lastProgressAt = Time.time; previousPosition = transform.position;
        Status = "Идёт к лестнице"; return true;
    }
    private void Update()
    {
        if (SaveGameManager.IsLoading || ladder == null) { if (ladder == null && token != 0) Cancel(); return; }
        if (unit == null || unit.unitHealth <= 0 || !ladder.IsValid || !ladder.Installed || !ladder.CanUse(gameObject))
        { AbortFromLadder(); return; }
        if (climbing)
        {
            float advance = Mathf.Min(1, progress + Time.deltaTime / duration);
            bool blockedExit = advance > .85f && !ladder.LandingClear(this, landing);
            progress = blockedExit ? Mathf.Min(progress, .85f) : advance;
            transform.position = LadderClimbMotion.Position(start, crest, landing, progress);
            animationDriver.Set(climb: true);
            if (blockedExit) { Status = "Ждёт свободный выход"; return; }
            if (progress >= 1)
            {
                Finish();
            }
            return;
        }
        if (!agent.enabled || !agent.isOnNavMesh || !command.OwnsSupplyOrder(token)) { Cancel(); return; }
        var attack = GetComponent<AttackController>(); if (attack != null) attack.targetToAttack = null;
        if (BattleCombatant.BlocksActions(this)) { Cancel(); return; }
        int index = ladder.QueueIndex(this);
        if (index < 0) { Cancel(); return; }
        Vector3 away = Vector3.ProjectOnPlane(ladder.transform.position - ladder.Top, Vector3.up).normalized;
        Vector3 goal = ladder.transform.position + away * (index * Mathf.Max(1.2f, agent.radius * 2 + .3f));
        if (Vector3.Distance(transform.position, previousPosition) > .2f)
        { lastProgressAt = Time.time; previousPosition = transform.position; }
        if (index == 0 && Vector3.Distance(transform.position, ladder.transform.position) <= .6f)
        {
            agent.ResetPath(); agent.isStopped = true; Status = "Ждёт свободную лестницу и выход";
            if (ladder.TryBeginClimb(this, agent, out landing)) Begin();
            // Waiting for an occupied ladder is intentional, not a failed approach.
            lastProgressAt = Time.time; return;
        }
        if (Vector3.Distance(transform.position, goal) <= .6f)
        { agent.ResetPath(); agent.isStopped = true; Status = "Ждёт очереди"; lastProgressAt = Time.time; return; }
        if (Time.time - lastProgressAt > approachTimeout) { Cancel(); Status = "Путь к лестнице заблокирован"; return; }
        if (Time.time < nextPath) return;
        nextPath = Time.time + .5f;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask & ~WallNavigation.AreaMask };
        if (!NavMesh.SamplePosition(goal, out var hit, .75f, filter) || !command.MoveForSupply(token, hit.position, .15f))
        { Cancel(); Status = "Нет пути к лестнице"; }
    }
    private void Begin()
    {
        start = transform.position; crest = ladder.Top; progress = 0;
        duration = LadderClimbMotion.Duration(Vector3.Distance(start, crest) + Vector3.Distance(crest, landing), climbingSpeed);
        Vector3 forward = Vector3.ProjectOnPlane(ladder.Top - start, Vector3.up);
        if (forward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(forward);
        climbing = true; savedAgentEnabled = agent.enabled; agent.ResetPath(); agent.enabled = false;
        if (body != null) { savedKinematic = body.isKinematic; savedGravity = body.useGravity; body.isKinematic = true; body.useGravity = false; }
        animationDriver.Set(climb: true); Status = "Поднимается по лестнице";
    }
    private bool RestoreAt(Vector3 point)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        return RestoreAt(point, filter, false);
    }

    private bool RestoreAt(Vector3 point, NavMeshQueryFilter filter, bool clearGround)
    {
        if (unit == null || unit.unitHealth <= 0) return false;
        if (!NavMesh.SamplePosition(point, out var hit, .5f, filter) || Mathf.Abs(hit.position.y - point.y) > .4f ||
            (clearGround && ladder != null && !ladder.LandingClear(this, hit.position))) return false;
        transform.position = hit.position;
        agent.enabled = savedAgentEnabled;
        if (!savedAgentEnabled || agent.Warp(hit.position)) return true;
        agent.enabled = false;
        return false;
    }

    private bool RestoreGround()
    {
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask & ~WallNavigation.AreaMask };
        if (RestoreAt(start, filter, true)) return true;
        if (ladder == null || ladder.Wall == null) return false;

        Vector3 foot = ladder.transform.position;
        Vector3 outward = Vector3.ProjectOnPlane(foot - ladder.Top, Vector3.up).normalized;
        Vector3 alongWall = ladder.Wall.transform.right;
        float spacing = Mathf.Max(1f, agent.radius * 2 + .25f);
        for (int row = 0; row <= 2; row++)
            for (int column = -2; column <= 2; column++)
                if (RestoreAt(foot + outward * (spacing * row) + alongWall * (spacing * column), filter, true))
                    return true;
        return false;
    }
    private void Finish()
    {
        if (!RestoreAt(landing)) { AbortFromLadder(); return; }
        bool clearExit = ladder.TryDisembark(this, landing, out var destination);
        ResetTask(); Status = "На стене";
        if (clearExit) command.MoveTo(destination);
    }
    public void AbortFromLadder() => AbortFromLadder("Подъём прерван");

    internal void AbortFromLadder(string status)
    {
        bool fell = climbing;
        bool landed = !fell || RestoreGround();
        ResetTask();
        if (fell && unit != null && unit.unitHealth > 0)
            unit.TakeDamage(Mathf.CeilToInt(landed ? unit.unitMaxHealth * .5f : unit.unitHealth), null);
        Status = status;
    }
    public void Cancel()
    {
        bool lostGround = climbing && unit != null && unit.unitHealth > 0 && !RestoreGround();
        ResetTask();
        if (lostGround && unit != null && unit.unitHealth > 0)
            unit.TakeDamage(Mathf.CeilToInt(unit.unitHealth), null);
    }
    private void ResetTask()
    {
        if (ladder != null) ladder.Leave(this);
        ladder = null;
        if (climbing && body != null) { body.isKinematic = savedKinematic; body.useGravity = savedGravity; }
        climbing = false; progress = 0;
        if (command != null) command.EndSupplyMove(token);
        token = 0; if (animationDriver != null) animationDriver.Set();
    }
    public bool ReservesLanding(Vector3 point, float distance) => climbing && Vector3.Distance(landing, point) < distance;
    private void OnDisable() { Cancel(); }
    private void OnDestroy() { Cancel(); if (command != null) command.ManualOrderIssued -= Cancel; }
}
