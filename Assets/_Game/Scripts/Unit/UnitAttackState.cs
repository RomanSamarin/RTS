using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class UnitAttackState : StateMachineBehaviour
{
    NavMeshAgent agent;
    AttackController attackController;
    GOScript goScript; 
    
    public float stopAttackDistance = 2f;
    public float attackRate = 1f; 
    private float attackTimer;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        agent = animator.GetComponent<NavMeshAgent>();
        attackController = animator.GetComponent<AttackController>();
        
        if (animator != null)
        {
            goScript = animator.GetComponent<GOScript>();
            if (goScript == null) goScript = animator.GetComponentInParent<GOScript>();

            if (attackController != null && attackController.targetToAttack != null)
            {
                // Проверяем тег или наличие компонента FishingSpot
                if (attackController.targetToAttack.CompareTag("FishingSpot") || attackController.targetToAttack.GetComponent<FishingSpot>() != null)
                {
                    animator.SetInteger("ActionType", 2); // Анимация рыбалки
                }
                else
                {
                    animator.SetInteger("ActionType", 1); // Анимация топора/меча
                }
            }
        }
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (attackController != null && attackController.targetToAttack != null)
        {
            if (goScript != null && goScript.isCommandToMove == true)
            {
                animator.SetBool("isAttacking", false);
                return;
            }

            LookAtTarget();
            
            if (attackTimer <= 0)
            {
                Attack();
                attackTimer = 1f / attackRate;
            }
            else
            {
                attackTimer -= Time.deltaTime;
            }

            if (attackController.targetToAttack == null)
            {
                animator.SetBool("isAttacking", false);
                return;
            }

            Unit targetUnit = attackController.targetToAttack.GetComponent<Unit>();
            FishingSpot targetWater = attackController.targetToAttack.GetComponent<FishingSpot>();
            
            bool isTargetDead = (targetUnit != null && targetUnit.unitHealth <= 0) || 
                                (targetWater != null && targetWater.fishHealth <= 0);

            if (isTargetDead)
            {
                animator.SetBool("isAttacking", false);
                return;
            }

            float distanceFromTarget = Vector3.Distance(attackController.targetToAttack.position, animator.transform.position);

            // Если подошли достаточно близко — включаем атаку (рыбалку)
            if (distanceFromTarget > stopAttackDistance)
            {
                if (agent != null && agent.isOnNavMesh) agent.SetDestination(attackController.targetToAttack.position);
                animator.SetBool("isAttacking", false);
            }
            else
            {
                if (agent != null && agent.isOnNavMesh) agent.ResetPath(); // Останавливаемся, чтобы начать ловить
                animator.SetBool("isAttacking", true); 
            }
        }
        else
        {
            animator.SetBool("isAttacking", false);
        }
    }

    private void Attack()
    {
        if (attackController != null && attackController.targetToAttack != null)
        {
            Unit targetUnit = attackController.targetToAttack.GetComponent<Unit>();
            if (targetUnit != null)
            {
                var damageToInflict = attackController.unitDamage;
                targetUnit.TakeDamage(damageToInflict, attackController.gameObject);
                return;
            }

            FishingSpot targetWater = attackController.targetToAttack.GetComponent<FishingSpot>();
            if (targetWater != null)
            {
                targetWater.TakeDamage(1, attackController.gameObject); 
                return;
            }
        }
    }

    private void LookAtTarget()
    {
        if (attackController != null && attackController.targetToAttack != null && agent != null)
        {
            Vector3 direction = attackController.targetToAttack.position - agent.transform.position;
            if (direction != Vector3.zero)
            {
                agent.transform.rotation = Quaternion.LookRotation(direction);
                var yRotation = agent.transform.eulerAngles.y;
                agent.transform.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }
    }
}
