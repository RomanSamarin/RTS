using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class UnitFollowState : StateMachineBehaviour
{
    NavMeshAgent agent;
    AttackController attackController;
    GOScript goScript;
    
    public float stopFollowDistance = 2f;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        agent = animator.GetComponent<NavMeshAgent>();
        attackController = animator.GetComponent<AttackController>();
        
        if (animator != null)
        {
            goScript = animator.GetComponent<GOScript>();
            if (goScript == null) goScript = animator.GetComponentInParent<GOScript>();
        }
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Проверка ручного приказа на перемещение игроком
        if (goScript != null && goScript.isCommandToMove == true)
        {
            animator.SetBool("IsFollowing", false);
            return;
        }

        if (attackController != null && attackController.targetToAttack != null)
        {
            // Ведем агента к цели
            if (agent != null && agent.isOnNavMesh)
            {
                agent.SetDestination(attackController.targetToAttack.position);
            }

            // Вычисляем дистанцию
            float distanceFromTarget = Vector3.Distance(attackController.targetToAttack.position, animator.transform.position);

            // Проверяем, к какому объекту подошел юнит (Дерево/Враг ИЛИ Водоем)
            bool isEnemyOrTree = attackController.targetToAttack.CompareTag("Enemy");
            bool isFishingSpot = attackController.targetToAttack.CompareTag("FishingSpot");

            // Если цель имеет один из нужных тегов и мы подошли на дистанцию взаимодействия
            if ((isEnemyOrTree || isFishingSpot) && distanceFromTarget <= stopFollowDistance)
            {
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.ResetPath(); // Останавливаем навигацию
                }
                
                // Включаем переход в Attack State
                animator.SetBool("isAttacking", true); 
            }
        }
        else
        {
            // Если цель потеряна, сбрасываем следование
            animator.SetBool("IsFollowing", false);
        }
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Базовый выход из состояния
    }
}
