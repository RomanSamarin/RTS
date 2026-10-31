using UnityEngine;

/// <summary>
/// Поведение состояния AttackBuilding в Animator.
/// Вешается на состояние AttackBuilding (Add Behaviour).
/// Само наносит урон зданию, если на объекте нет EnemyAi,
/// либо вызывает EnemyAi.AttackBuildingTarget(), чтобы соблюдался общий интервал ударов.
/// </summary>
public class EnemyAttackBuilding : StateMachineBehaviour
{
    [Tooltip("Интервал между ударами, если на объекте нет EnemyAi.")]
    public float attackInterval = 1f;

    private AttackController attackController;
    private EnemyAi enemyAi;
    private float nextAttackTime;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        attackController = animator.GetComponent<AttackController>();
        enemyAi = animator.GetComponent<EnemyAi>();
        nextAttackTime = 0f;
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (attackController == null || attackController.targetToAttack == null)
            return;

        LookAtTarget(animator);

        // Основной вариант: урон считает EnemyAi, чтобы не было двойных ударов.
        if (enemyAi != null)
        {
            // Не бьём здание, если прямо сейчас враг дерётся с юнитом.
            if (enemyAi.BuildingTarget != null && enemyAi.UnitTarget == null)
                enemyAi.AttackBuildingTarget();

            return;
        }

        // Запасной вариант, если EnemyAi на объекте нет.
        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + Mathf.Max(0.05f, attackInterval);

        BuildingHealth building = attackController.targetToAttack.GetComponentInParent<BuildingHealth>();

        if (building != null)
        {
            building.TakeDamage(attackController.unitDamage);
            return;
        }

        Unit unit = attackController.targetToAttack.GetComponentInParent<Unit>();

        if (unit != null)
            unit.TakeDamage(attackController.unitDamage, attackController.gameObject);
    }

    private void LookAtTarget(Animator animator)
    {
        if (animator == null || attackController == null || attackController.targetToAttack == null)
            return;

        Vector3 direction = attackController.targetToAttack.position - animator.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
            animator.transform.rotation = Quaternion.LookRotation(direction);
    }
}
