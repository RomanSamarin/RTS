using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class UnitAttackState : StateMachineBehaviour
{
    NavMeshAgent agent;
    AttackController attackController;
    GOScript goScript; 
    Archer archer;
    
    public float stopAttackDistance = 2f;
    public float attackRate = 1f; 
    private readonly MeleeStrikeClock strikeClock=new MeleeStrikeClock();
    private Transform strikeTarget;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        strikeClock.Reset();strikeTarget=null;
        agent = animator.GetComponentInParent<NavMeshAgent>();
        attackController = animator.GetComponentInParent<AttackController>();
        archer = animator.GetComponentInParent<Archer>();
        
        if (animator != null)
        {
            goScript = animator.GetComponent<GOScript>();
            if (goScript == null) goScript = animator.GetComponentInParent<GOScript>();

            if (attackController != null && attackController.targetToAttack != null)
            {
                if (FactionMember.AreEnemies(attackController.gameObject, attackController.targetToAttack.gameObject))
                    animator.SetInteger("ActionType", 1);
            }
        }
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.GetComponentInParent<RtsNetworkUnit>()?.IsCombatUnit == true) return;
        if(SaveGameManager.IsLoading)return;
        if(SiegeLadderAnimation.Controls(animator)||RamCrewMember.Controls(animator)){strikeClock.Reset();strikeTarget=null;return;}
        if(animator.GetComponentInParent<Catapult>()!=null || animator.GetComponentInParent<BatteringRam>()!=null)return;
        if (UnitEat.ControlsMovement(animator)) {strikeClock.Reset();return;}
        if (BattleCombatant.BlocksActions(animator)) { strikeClock.Reset();animator.SetBool("isAttacking", false); return; }
        if(Builder.ControlsAnimation(animator)){strikeClock.Reset();strikeTarget=null;return;}
        if (animator.GetComponentInParent<FoodCart>() != null || animator.GetComponentInParent<SiegeLadderCart>() != null || animator.GetComponentInParent<CartHorse>() != null) return;
        var worker = animator.GetComponentInParent<ResourcesMove>();
        if (worker != null && ResourceWorker.OwnsTarget(worker, attackController != null ? attackController.targetToAttack : null))
        { strikeClock.Reset(); WorkerAnimation.Bool(animator, "isAttacking", false); return; }
        if (worker != null && worker.IsTrainingRecruit) return;
        if (worker != null && (worker.IsFoodGatherer || worker.IsProductionWorker || worker.role == ResourcesMove.UnitRole.Miner || worker.role == ResourcesMove.UnitRole.Farmer || worker.role == ResourcesMove.UnitRole.Carrier)) return;
        if (attackController != null && attackController.targetToAttack != null)
        {
            if (goScript != null && goScript.isCommandToMove == true)
            {
                strikeClock.Reset();
                animator.SetBool("isAttacking", false);
                return;
            }

            // Урон лучника наносит стрела, а анимацией выстрела управляет скрипт Archer:
            // не трогаем isAttacking здесь, чтобы клип не сбрасывался между выстрелами.
            if (archer != null)
            {
                LookAtTarget();
                return;
            }

            // This state handles combat only. Work runs in profession components.
            Unit targetUnit = attackController.targetToAttack.GetComponent<Unit>();
            BanditCamp targetCamp = attackController.targetToAttack.GetComponent<BanditCamp>();
            Vector3 combatPoint = AttackController.CombatPoint(attackController.targetToAttack,animator.transform.position);

            bool hostile=FactionMember.AreEnemies(attackController.gameObject,attackController.targetToAttack.gameObject);
            bool isAttackableTarget = hostile;

            // СКЛАДЫ И ДРУГИЕ ЗДАНИЯ (Sawmill, StoneHouse, Warehouse, FishHouse) — НЕ боевые цели!
            // Юнит не должен бить склад: он просто продолжает ИДТИ к нему, чтобы сдать/забрать ресурс.
            if (!isAttackableTarget)
            {
                animator.SetBool("isAttacking", false);
                return;
            }

            LookAtTarget();

            if(MeleeApproach.Drive(animator,stopAttackDistance,out bool inPlace)&&!inPlace)
            {strikeClock.Reset();animator.SetBool("isAttacking",false);return;}

            float meleeDistance=hostile?attackController.meleeRange:stopAttackDistance;
            if(strikeTarget!=attackController.targetToAttack){strikeClock.Reset();strikeTarget=attackController.targetToAttack;}
            if(hostile)
            {
                var battle=animator.GetComponentInParent<BattleCombatant>();
                float period=1f/Mathf.Max(.05f,attackRate*(battle!=null?battle.AttackRateMultiplier:1f));
                bool inRange=Vector3.Distance(combatPoint,animator.transform.position)<=meleeDistance&&MeleeApproach.CanStrike(attackController.gameObject,attackController.targetToAttack);
                if(strikeClock.Tick(Time.deltaTime,inRange,period,attackController.strikeWindup))Attack();
            }
            
            if (attackController.targetToAttack == null)
            {
                animator.SetBool("isAttacking", false);
                return;
            }

            bool isTargetDead = (targetUnit != null && targetUnit.unitHealth <= 0) || 
                                (targetCamp != null && targetCamp.IsDestroyed);

            if (isTargetDead)
            {
                if (targetCamp != null) attackController.targetToAttack = null;
                animator.SetBool("isAttacking", false);
                return;
            }

            float distanceFromTarget = Vector3.Distance(combatPoint, animator.transform.position);

            // Лучник держится на дистанции стрельбы (урон наносит Arrow, а не ближний бой)
            float stopDistance = archer != null ? archer.attackRange : meleeDistance;

            if ((WallStation.Holding(animator) || BattleCombatant.Defends(animator)) && distanceFromTarget > stopDistance)
            { animator.SetBool("isAttacking", false); return; }

            // Если подошли достаточно близко — включаем атаку (рыбалку)
            if (distanceFromTarget > stopDistance)
            {
                if (agent != null && agent.isOnNavMesh&&!MilitaryOrders.BlocksChase(animator)) agent.SetDestination(combatPoint);
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
            strikeClock.Reset();strikeTarget=null;
            animator.SetBool("isAttacking", false);
        }
    }

    private void Attack()
    {
        // Враг сам считает урон в EnemyAi; здесь только анимация,
        // иначе удар по юниту наносился бы дважды.
        if (attackController != null && attackController.damageHandledExternally)
            return;

        // У лучника урон наносит стрела (Arrow), ближний бой ему не нужен
        if (archer != null)
            return;

        if (attackController != null && attackController.targetToAttack != null)
        {
            if (!FactionMember.AreEnemies(attackController.gameObject, attackController.targetToAttack.gameObject)) return;
            var building=attackController.targetToAttack.GetComponentInParent<BuildingHealth>();
            if(building!=null){building.TakeDamage(attackController.unitDamage,attackController.gameObject);return;}
            Unit targetUnit = attackController.targetToAttack.GetComponent<Unit>();
            if (targetUnit != null)
            {
                var damageToInflict = attackController.unitDamage;
                targetUnit.TakeDamage(damageToInflict, attackController.gameObject);
                return;
            }

        }
    }

    private void LookAtTarget()
    {
        if (attackController != null && (BattleCombatant.Holds(attackController)||BattleCombatant.Defends(attackController))) return;
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
