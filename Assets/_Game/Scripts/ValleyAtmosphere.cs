using UnityEngine;

// Scene-owned settings make atmosphere available in Edit Mode and in builds.
[ExecuteAlways]
public sealed class ValleyAtmosphere : MonoBehaviour
{
    public Material sky;
    public Color haze = new Color(0.64f, 0.75f, 0.82f);
    [Min(0)] public float fogStart = 50f;
    [Min(1)] public float fogEnd = 260f;
    private Material previousSky;
    private bool previousFog;
    private FogMode previousMode;
    private Color previousColor;
    private float previousStart, previousEnd;

    private void OnEnable()
    {
        previousSky = RenderSettings.skybox;
        previousFog = RenderSettings.fog;
        previousMode = RenderSettings.fogMode;
        previousColor = RenderSettings.fogColor;
        previousStart = RenderSettings.fogStartDistance;
        previousEnd = RenderSettings.fogEndDistance;
        Apply();
    }

    private void OnValidate() { if (isActiveAndEnabled) Apply(); }

    public void Apply()
    {
        if (sky != null) RenderSettings.skybox = sky;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = haze;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = Mathf.Max(fogStart + 1, fogEnd);
    }

    private void OnDisable()
    {
        RenderSettings.skybox = previousSky;
        RenderSettings.fog = previousFog;
        RenderSettings.fogMode = previousMode;
        RenderSettings.fogColor = previousColor;
        RenderSettings.fogStartDistance = previousStart;
        RenderSettings.fogEndDistance = previousEnd;
    }
}
