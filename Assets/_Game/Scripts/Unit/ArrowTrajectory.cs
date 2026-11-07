using UnityEngine;

public static class ArrowTrajectory
{
    public static Vector3 Position(Vector3 start, Vector3 end, float t, float height)
    {
        t = Mathf.Clamp01(t);
        return start + (end - start) * t + new Vector3(0f, 4f * height * t * (1f - t), 0f);
    }
    public static Vector3 Tangent(Vector3 start, Vector3 end, float t, float height)
        => end - start + new Vector3(0f, 4f * height * (1f - 2f * Mathf.Clamp01(t)), 0f);
    // Первое пересечение отрезка со сферой, включая быстрые стрелы и низкий FPS.
    public static bool HitSphere(Vector3 start, Vector3 end, Vector3 center, float radius, out float fraction)
    {
        fraction = 0f;
        Vector3 offset = start - center;
        float c = Vector3.Dot(offset, offset) - radius * radius;
        if (c <= 0f) return true;
        Vector3 delta = end - start;
        float a = Vector3.Dot(delta, delta);
        if (a < 0.000001f) return false;
        float b = Vector3.Dot(offset, delta);
        float discriminant = b * b - a * c;
        if (discriminant < 0f) return false;
        fraction = (-b - Mathf.Sqrt(discriminant)) / a;
        return fraction >= 0f && fraction <= 1f;
    }
}
