using UnityEngine;

/// <summary>One work tick at a time, with no deferred burst after walking or eating.</summary>
public sealed class ResourceWorkClock
{
    private float remaining;
    public void Reset() { remaining = 0; }
    public bool Tick(float deltaTime, float period)
    {
        if (deltaTime <= 0) return false;
        remaining -= deltaTime;
        if (remaining > 0) return false;
        remaining = Mathf.Max(.05f, period); return true;
    }
}
