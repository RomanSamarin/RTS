using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class UnitFollowState : StateMachineBehaviour
{
    NavMeshAgent agent;
    AttackController attackController;
    GOScript goScript;
    Archer archer;
    ResourcesMove resourcesMove;
    
    public float stopFollowDistance = 2f;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        agent = animator.GetComponent<NavMeshAgent>();
        attackController = animator.GetComponent<AttackController>();
        archer = animator.GetComponent<Archer>();
        resourcesMove = animator.GetComponent<ResourcesMove>();
        if (resourcesMove == null) resourcesMove = animator.GetComponentInParent<ResourcesMove>();
        
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
                Vector3 destination = attackController.targetToAttack.position;

                // Рабочий идёт к двери здания (BuildingEntrance), а не в его центр.
                // Для деревьев, врагов и водоёмов точка остаётся прежней.
                if (resourcesMove != null)
                    destination = resourcesMove.GetApproachPoint(attackController.targetToAttack);

                agent.SetDestination(destination);
            }

            // Вычисляем дистанцию
            float distanceFromTarget = Vector3.Distance(attackController.targetToAttack.position, animator.transform.position);

            // Лучник останавливается на своей дистанции стрельбы, а не вплотную к цели
            float stopDistance = stopFollowDistance;
            if (archer != null)
                stopDistance = archer.attackRange;

            // Проверяем, к какому объекту подошел юнит (Дерево/Враг, Водоем ИЛИ Жила камня)
            bool isEnemyOrTree = attackController.targetToAttack.CompareTag("Enemy");
            bool isFishingSpot = attackController.targetToAttack.CompareTag("FishingSpot");

            // Жила камня — рабочая цель ТОЛЬКО для каменщика.
            // Носильщик с той же целью (жила) должен пройти ВНУТРЬ её триггера, чтобы забрать камень из кучи.
            var resourcesMoveOnUnit = animator.GetComponent<ResourcesMove>();
            bool isStoneQuarryMine = attackController.targetToAttack.CompareTag("StoneQuarry")
                                     && resourcesMoveOnUnit != null
                                     && resourcesMoveOnUnit.role == ResourcesMove.UnitRole.StoneMason;

            // Если цель имеет один из нужных тегов и мы подошли на дистанцию взаимодействия
            if ((isEnemyOrTree || isFishingSpot || isStoneQuarryMine) && distanceFromTarget <= stopDistance)
            {
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.ResetPath(); // Останавливаем навигацию
                }

                // У лучника анимацией атаки управляет скрипт Archer (по каждому выстрелу),
                // иначе isAttacking включался бы постоянно и клип не совпадал со стрельбой.
                if (archer == null)
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
