using UnityEngine;

public static class LadderClimbMotion
{
    public static float Duration(float distance, float speed) => Mathf.Max(.2f, distance / Mathf.Max(.1f, speed));
    public static Vector3 Position(Vector3 start, Vector3 crest, Vector3 landing, float progress)
    {
        progress = Mathf.Clamp01(progress);
        return progress < .85f ? start + (crest - start) * (progress / .85f)
            : crest + (landing - crest) * ((progress - .85f) / .15f);
    }
}
