using UnityEngine;

/// <summary>Optional Animator parameters; absence of a clip never blocks the gameplay task.</summary>
[DisallowMultipleComponent]
public class SiegeLadderAnimation : MonoBehaviour
{
    public Animator animator;
    [Tooltip("Clips for Tools/RTS/Setup Ladder Animations For Selected Unit.")]
    public AnimationClip pickupClip, installationClip, climbClip;
    [Tooltip("Optional child model shown in the soldier's hands after receiving a ladder.")]
    public GameObject carriedLadderVisual;
    public string pickupParameter = "isTakingLadder";
    public string carryParameter = "isCarryingLadder";
    public string installParameter = "isPlacingLadder";
    public string climbParameter = "isClimbingLadder";
    public string animationLayer = "Siege Ladder";
    private bool ownsRootMotion, previousRootMotion;
    public static SiegeLadderAnimation Ensure(GameObject unit)
        => unit.GetComponent<SiegeLadderAnimation>() ?? unit.AddComponent<SiegeLadderAnimation>();
    public void Set(bool pickup = false, bool carry = false, bool install = false, bool climb = false)
    {
        if (carriedLadderVisual != null && carriedLadderVisual != gameObject && carriedLadderVisual.transform.IsChildOf(transform))
            carriedLadderVisual.SetActive(carry || install);
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) return;
        Bool(pickupParameter, pickup); Bool(carryParameter, carry);
        Bool(installParameter, install); Bool(climbParameter, climb);
        int layer = animator.GetLayerIndex(animationLayer);
        if (layer >= 0)
        {
            string state = pickup ? "SiegeLadder_Pickup" : install ? "SiegeLadder_Install" : climb ? "SiegeLadder_Climb" : "";
            bool hasMotion = state.Length > 0 && animator.HasState(layer, Animator.StringToHash(animationLayer + "." + state));
            animator.SetLayerWeight(layer, hasMotion ? 1 : 0);
        }
        bool stationary = pickup || install || climb;
        if (stationary)
        {
            Bool("IsFollowing", false); Bool("isMoving", false); Bool("isAttacking", false); Bool("isEating", false);
        }
        // The agent/state machine moves the root; animation must not move it a second time.
        if (pickup || carry || install || climb)
        {
            if (!ownsRootMotion) { previousRootMotion = animator.applyRootMotion; ownsRootMotion = true; }
            animator.applyRootMotion = false;
        }
        else RestoreRootMotion();
    }
    private void Bool(string name, bool value)
    {
        if (string.IsNullOrEmpty(name)) return;
        foreach (var parameter in animator.parameters)
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == name)
            { animator.SetBool(name, value); return; }
    }
    private void RestoreRootMotion()
    { if (ownsRootMotion && animator != null) animator.applyRootMotion = previousRootMotion; ownsRootMotion = false; }
    private void LateUpdate()
    {
        if (SaveGameManager.IsLoading) return;
        var climber = GetComponent<SiegeLadderClimber>();
        if (climber != null && climber.IsBusy) { Set(climb: climber.IsClimbing); return; }
        var installer = GetComponent<SiegeLadderInstaller>();
        if (installer != null && installer.IsBusy)
        { Set(installer.IsPickingUp, installer.IsCarrying, installer.IsInstalling); return; }
        Set();
    }
    public static bool Controls(Component owner)
    {
        var installer = owner.GetComponentInParent<SiegeLadderInstaller>();
        var climber = owner.GetComponentInParent<SiegeLadderClimber>();
        return (installer != null && installer.IsBusy) || (climber != null && climber.IsBusy);
    }
    private void OnDisable() { Set(); RestoreRootMotion(); }
}
