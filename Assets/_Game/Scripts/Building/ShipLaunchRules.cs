using UnityEngine;

public static class ShipLaunchRules
{
    public static bool Contains(float x, float z, float width, float length)
        => width > 0 && length > 0 && Mathf.Abs(x) <= width * .5f && Mathf.Abs(z) <= length * .5f;
    public static bool EnoughDepth(float surface, float seabed, float minimum)
        => minimum > 0 && surface - seabed >= minimum;
}
