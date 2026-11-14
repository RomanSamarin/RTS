using System;
using System.Collections.Generic;
using UnityEngine;

public static class WorkerAnimationChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("Work animation: " + message); }
    public static void Run()
    {
        var animator = new Animator { parameters = new[]
        {
            new AnimatorControllerParameter { name = "isWorking", type = AnimatorControllerParameterType.Bool },
            new AnimatorControllerParameter { name = "isAttacking", type = AnimatorControllerParameterType.Bool },
            new AnimatorControllerParameter { name = "IsFollowing", type = AnimatorControllerParameterType.Bool },
            new AnimatorControllerParameter { name = "isMoving", type = AnimatorControllerParameterType.Bool }
        }};
        animator.values["isAttacking"] = true; animator.values["IsFollowing"] = true;
        WorkerAnimation.Apply(animator, true);
        Check(animator.values["isWorking"] && !animator.values["isAttacking"] && !animator.values["IsFollowing"]
            && !animator.values["isMoving"], "work uses its own flag and suppresses movement/combat poses");
        animator.values["isAttacking"] = true; animator.values["IsFollowing"] = true;
        WorkerAnimation.Apply(animator, false);
        Check(!animator.values["isWorking"] && animator.values["isAttacking"] && animator.values["IsFollowing"],
            "ending work does not clear a new combat or movement animation");
        WorkerAnimation.Apply(null, true);
        var missing = new Animator(); WorkerAnimation.Apply(missing, true);
        Check(missing.values.Count == 0, "missing work parameters do not throw or invent unrelated parameters");
        missing.runtimeAnimatorController = null; WorkerAnimation.Apply(missing, false);
        var owner = new ResourcesMove { isWorking = true }; animator.owner = owner;
        Check(WorkerAnimation.Controls(animator), "active worker owns work animation");
        owner.CanPerformWork = false; Check(!WorkerAnimation.Controls(animator), "food/orders/other interruption relinquish animation ownership");

        var clock = new ResourceWorkClock();
        Check(!clock.Tick(0, 1), "paused game does not harvest");
        Check(clock.Tick(.01f, 1) && !clock.Tick(.4f, 1) && !clock.Tick(.59f, 1) && clock.Tick(.02f, 1),
            "work timer yields one resource action per period without Animator");
        Check(clock.Tick(10, 1) && !clock.Tick(.01f, 1), "long frame does not cause repeated work through an animation state");
        clock.Reset(); Check(clock.Tick(.01f, 1), "new source starts its own work interval");
        clock.Reset(); Check(!clock.Tick(-1, 1), "invalid time does not harvest");
        Console.WriteLine("PASS: separate work/combat parameters, missing Animator parameters, interruption ownership and independent resource timer");
    }
}

namespace UnityEngine
{
    public class Component : Object
    {
        public ResourcesMove owner;
        public T GetComponentInParent<T>() => owner is T value ? value : default;
    }
    public enum AnimatorControllerParameterType { Bool, Int, Float, Trigger }
    public class AnimatorControllerParameter { public string name; public AnimatorControllerParameterType type; }
    public class Animator : Component
    {
        public object runtimeAnimatorController = new object();
        public AnimatorControllerParameter[] parameters = Array.Empty<AnimatorControllerParameter>();
        public readonly Dictionary<string, bool> values = new Dictionary<string, bool>();
        public void SetBool(string name, bool value)
        {
            if (!Array.Exists(parameters, p => p.name == name && p.type == AnimatorControllerParameterType.Bool))
                throw new Exception("Attempted to write a missing Animator bool: " + name);
            values[name] = value;
        }
    }
}
