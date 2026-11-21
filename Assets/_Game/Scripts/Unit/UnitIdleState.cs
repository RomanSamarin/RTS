using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitIdleState : StateMachineBehaviour
{
    AttackController attackController;
    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        attackController = animator.GetComponentInParent<AttackController>();
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.GetComponentInParent<RtsNetworkUnit>()?.IsCombatUnit == true) return;
        if(SaveGameManager.IsLoading)return;
        if(SiegeLadderAnimation.Controls(animator))return;
        if(WorkerAnimation.Controls(animator))return;
        if(animator.GetComponentInParent<BatteringRam>()!=null)return;
        var worker = animator.GetComponentInParent<ResourcesMove>();
        if(ResourceWorker.OwnsTarget(worker,attackController!=null?attackController.targetToAttack:null))return;
        if (UnitEat.ControlsMovement(animator)) return;
        if(Builder.ControlsAnimation(animator))return;
        if (BattleCombatant.BlocksActions(animator)) return;
        if (attackController != null && attackController.targetToAttack != null)
        {
            animator.SetBool("IsFollowing", true);
        }

    }

   
}
