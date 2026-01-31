using UnityEngine;
using UnityEngine.AI;

public class UnitFollowState : StateMachineBehaviour
{
    AttackController attackController;
    NavMeshAgent agent;
    public float attackingDistance = 1f;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        attackController = animator.transform.GetComponent<AttackController>();
        agent = animator.transform.GetComponent<NavMeshAgent>();
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        GOScript moveScript = animator.transform.GetComponent<GOScript>();

        if (moveScript != null && moveScript.isCommandToMove)
        {
            if (attackController != null)
                attackController.targetToAttack = null;

            animator.SetBool("IsFollowing", false);
            return;
        }

        if (attackController == null || attackController.targetToAttack == null)
        {
            animator.SetBool("IsFollowing", false);
            return;
        }

        agent.SetDestination(attackController.targetToAttack.position);
        animator.transform.LookAt(attackController.targetToAttack);

        float distanceFromTarget = Vector3.Distance(attackController.targetToAttack.position, animator.transform.position);

        if (distanceFromTarget < attackingDistance)
        {
            agent.SetDestination(animator.transform.position);
            animator.SetBool("isAttacking", true);
        }
        else
        {
            animator.SetBool("isAttacking", false);
        }
    }
}