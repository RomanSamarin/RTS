using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Поза строя на Animator State Machine.
///
/// Вешается НА САМИ СОСТОЯНИЯ (Add Behaviour), компонент на юните не нужен.
///
/// Два применения:
///   1) Role = Wait In Idle — на состоянии Idle. Когда юнит пришёл в свою точку
///      строя (его туда отправил SoldiersFormation) и стоит, поведение
///      переводит Animator в состояние Holding.
///   2) Role = Hold Pose — на состоянии Holding. Пока юнит стоит, поза держится;
///      как только он получил приказ на движение, погнался за целью, атакует
///      или погиб — возвращает Animator в Idle.
///
/// Переходы и bool-параметры не нужны: состояния переключаются через CrossFade.
/// Состояния Holding и Idle должны лежать на одном слое.
/// </summary>
public class UnitHoldingBehaviour : StateMachineBehaviour
{
    public enum Role
    {
        /// <summary>На состоянии Idle: ждёт прихода в точку строя и включает Holding.</summary>
        WaitInIdle,

        /// <summary>На состоянии Holding: держит позу и выходит из неё при движении/атаке.</summary>
        HoldPose
    }

    [Header("Роль (разная для Idle и Holding)")]
    [Tooltip("На состоянии Idle — Wait In Idle, на состоянии Holding — Hold Pose")]
    public Role role = Role.WaitInIdle;

    [Header("Имена состояний в контроллере")]
    [Tooltip("Состояние позы строя (для обеих ролей)")]
    public string holdingState = "Holding";

    [Tooltip("Состояние ожидания, куда возвращаемся из Holding")]
    public string idleState = "Idle";

    [Tooltip("Bool-параметр атаки. Пока он включён, поза не держится")]
    public string attackParameter = "isAttacking";

    [Header("Настройки")]
    [Tooltip("Пауза после прихода в точку строя, сек")]
    public float holdDelay = 0.2f;

    [Tooltip("Насколько юнит может отойти от своей точки строя, м")]
    public float maxDistanceFromPoint = 1.5f;

    [Tooltip("Длительность перехода между состояниями, сек")]
    public float fadeDuration = 0.15f;

    private GOScript goScript;
    private NavMeshAgent agent;
    private Unit unit;
    private Animator cachedAnimator;

    private int holdingHash;
    private int idleHash;
    private int attackHash;
    private bool hasAttackParameter;

    private float stillTimer;

    // ------------------------------------------------------------------ Unity

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        Cache(animator);
        stillTimer = 0f;
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator == null)
            return;

        if (cachedAnimator != animator)
            Cache(animator);

        if (role == Role.WaitInIdle)
            UpdateWaitInIdle(animator, layerIndex);
        else
            UpdateHoldPose(animator, layerIndex);
    }

    // ------------------------------------------------------ Idle -> Holding

    private void UpdateWaitInIdle(Animator animator, int layerIndex)
    {
        if (unit != null && unit.unitHealth <= 0f)
            return;

        // Юнит ещё идёт (приказ игрока или погоня за целью) — не время для позы
        if ((goScript != null && goScript.isCommandToMove) || HasActivePath() || IsAttacking(animator))
        {
            stillTimer = 0f;
            return;
        }

        // Юнита в строй не отправляли — ничего не делаем
        GameObject unitObject = animator.gameObject;

        if (!FormationHold.TryGetPoint(unitObject, out Vector3 point))
            return;

        // Юнит ушёл со своей точки строя приказом — строй больше не держим
        if (Vector3.Distance(animator.transform.position, point) > maxDistanceFromPoint)
        {
            FormationHold.Clear(unitObject);
            stillTimer = 0f;
            return;
        }

        stillTimer += Time.deltaTime;

        if (stillTimer < holdDelay)
            return;

        stillTimer = 0f;

        if (!HasState(animator, layerIndex, holdingHash))
        {
            Debug.LogWarning($"[UnitHoldingBehaviour] В контроллере юнита \"{animator.name}\" " +
                             $"нет состояния \"{holdingState}\" на слое {layerIndex}.", this);
            return;
        }

        animator.CrossFadeInFixedTime(holdingHash, fadeDuration, layerIndex);
    }

    // ------------------------------------------------------ Holding -> Idle

    private void UpdateHoldPose(Animator animator, int layerIndex)
    {
        bool leavePose = unit != null && unit.unitHealth <= 0f;

        if (!leavePose && goScript != null && goScript.isCommandToMove)
            leavePose = true;

        if (!leavePose && HasActivePath())
            leavePose = true;

        if (!leavePose && IsAttacking(animator))
            leavePose = true;

        if (!leavePose)
            return;

        if (!HasState(animator, layerIndex, idleHash))
            return;

        animator.CrossFadeInFixedTime(idleHash, fadeDuration, layerIndex);
    }

    // ------------------------------------------------------------- Helpers

    private void Cache(Animator animator)
    {
        cachedAnimator = animator;

        GameObject unitObject = animator.gameObject;

        goScript = unitObject.GetComponent<GOScript>();
        if (goScript == null)
            goScript = unitObject.GetComponentInParent<GOScript>();

        agent = unitObject.GetComponent<NavMeshAgent>();

        unit = unitObject.GetComponent<Unit>();
        if (unit == null)
            unit = unitObject.GetComponentInParent<Unit>();

        holdingHash = Animator.StringToHash(holdingState);
        idleHash = Animator.StringToHash(idleState);
        attackHash = Animator.StringToHash(attackParameter);
        hasAttackParameter = HasBoolParameter(animator, attackParameter);
    }

    private bool HasActivePath()
    {
        if (agent == null || !agent.isOnNavMesh)
            return false;

        return agent.hasPath
               && !agent.pathPending
               && agent.remainingDistance > agent.stoppingDistance + 0.05f;
    }

    private bool IsAttacking(Animator animator)
    {
        return hasAttackParameter && animator.GetBool(attackHash);
    }

    private static bool HasState(Animator animator, int layerIndex, int stateHash)
    {
        if (animator == null || layerIndex < 0 || layerIndex >= animator.layerCount)
            return false;

        return animator.HasState(layerIndex, stateHash);
    }

    private static bool HasBoolParameter(Animator animator, string parameterName)
    {
        if (animator == null || animator.runtimeAnimatorController == null ||
            string.IsNullOrEmpty(parameterName))
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.name == parameterName)
                return true;
        }

        return false;
    }
}
