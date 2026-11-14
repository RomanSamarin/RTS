using UnityEngine;

/// <summary>Work is independent of combat. Missing parameters never prevent production.</summary>
public static class WorkerAnimation
{
    public static bool Controls(Component owner)
    {
        var worker = owner.GetComponentInParent<ResourcesMove>();
        return worker != null && worker.isWorking && worker.CanPerformWork;
    }
    public static void Bool(Animator animator, string name, bool value)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var parameter in animator.parameters)
            if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Bool)
            { animator.SetBool(name, value); return; }
    }
    public static void Apply(Animator animator, bool working)
    {
        Bool(animator, "isWorking", working);
        if (!working) return;
        Bool(animator, "isAttacking", false);
        Bool(animator, "IsFollowing", false);
        Bool(animator, "isMoving", false);
    }
}
