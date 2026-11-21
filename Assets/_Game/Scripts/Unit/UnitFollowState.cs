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
        agent = animator.GetComponentInParent<NavMeshAgent>();
        attackController = animator.GetComponentInParent<AttackController>();
        archer = animator.GetComponentInParent<Archer>();
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
        if (animator.GetComponentInParent<RtsNetworkUnit>()?.IsCombatUnit == true) return;
        if(SaveGameManager.IsLoading)return;
        if(SiegeLadderAnimation.Controls(animator)||RamCrewMember.Controls(animator))return;
        if(WorkerAnimation.Controls(animator))return;
        if(animator.GetComponentInParent<Catapult>()!=null || animator.GetComponentInParent<BatteringRam>()!=null)return;
        if (UnitEat.ControlsMovement(animator)) return;
        if(Builder.ControlsAnimation(animator))return;
        if (MilitaryOrders.BlocksChase(animator)) return;
        if (BattleCombatant.BlocksActions(animator) || BattleCombatant.Defends(animator)) return;
        if (WallStation.Holding(animator)) return;
        if (animator.GetComponentInParent<FoodCart>() != null || animator.GetComponentInParent<SiegeLadderCart>() != null || animator.GetComponentInParent<CartHorse>() != null) return;
        if (resourcesMove != null && resourcesMove.IsTrainingRecruit) return;
        if (ResourceWorker.OwnsTarget(resourcesMove, attackController != null ? attackController.targetToAttack : null))
        { WorkerAnimation.Bool(animator, "isAttacking", false); return; }
        if (resourcesMove != null && (resourcesMove.IsFoodGatherer || resourcesMove.IsProductionWorker || resourcesMove.role == ResourcesMove.UnitRole.Miner || resourcesMove.role == ResourcesMove.UnitRole.Farmer || resourcesMove.role == ResourcesMove.UnitRole.Carrier)
            && !resourcesMove.IsReturningToWorkplace) return;
        // Проверка ручного приказа на перемещение игроком.
        // Флаг IsFollowing не сбрасываем: иначе во время приказа пропадает
        // анимация ходьбы и юнит «скользит». Маршрут агента тоже не трогаем —
        // им управляет GOScript.
        if (goScript != null && goScript.isCommandToMove == true)
            return;

        if(MeleeApproach.Drive(animator,stopFollowDistance,out bool ready))
        {animator.SetBool("isAttacking",ready);return;}

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
                var camp = attackController.targetToAttack.GetComponent<BanditCamp>();
                if (camp != null) destination = camp.CombatPoint(animator.transform.position);
                if(FactionMember.AreEnemies(attackController.gameObject,attackController.targetToAttack.gameObject))destination=AttackController.CombatPoint(attackController.targetToAttack,animator.transform.position);

                agent.SetDestination(destination);
            }

            // Вычисляем дистанцию
            float distanceFromTarget = Vector3.Distance(attackController.targetToAttack.position, animator.transform.position);
            var targetCamp = attackController.targetToAttack.GetComponent<BanditCamp>();
            if (targetCamp != null && archer == null) distanceFromTarget = Vector3.Distance(targetCamp.CombatPoint(animator.transform.position), animator.transform.position);
            if(archer==null&&FactionMember.AreEnemies(attackController.gameObject,attackController.targetToAttack.gameObject))distanceFromTarget=Vector3.Distance(AttackController.CombatPoint(attackController.targetToAttack,animator.transform.position),animator.transform.position);

            // Лучник останавливается на своей дистанции стрельбы, а не вплотную к цели
            float stopDistance = stopFollowDistance;
            if(archer==null&&FactionMember.AreEnemies(attackController.gameObject,attackController.targetToAttack.gameObject))stopDistance=attackController.meleeRange;
            if (archer != null)
                stopDistance = archer.attackRange;

            // Проверяем, к какому объекту подошел юнит (Дерево/Враг, Водоем ИЛИ Жила камня)
            bool isEnemyOrTree = FactionMember.AreEnemies(attackController.gameObject,attackController.targetToAttack.gameObject);

            // Если цель имеет один из нужных тегов и мы подошли на дистанцию взаимодействия
            if (isEnemyOrTree && distanceFromTarget <= stopDistance)
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
            // Цель потеряна. IsFollowing здесь не сбрасываем: флагом владеет
            // Unit.Update — он снимает его, только когда юнит реально остановился.
            // Иначе прерывалась бы анимация ходьбы по приказу игрока
            // и у безработного жителя, у которого цели нет вообще.
        }
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Базовый выход из состояния
    }
}
