using System;

/// <summary>Двухэтапная подготовка волны; отменённая волна никогда не выходит.</summary>
public sealed class RaidSchedule
{
    public enum Signal { None, Warning, Launch }
    public bool Cancelled { get; private set; }
    public float NextAt { get; private set; }
    private bool announced;
    public RaidSchedule(float firstLaunchAt) { NextAt = firstLaunchAt; }
    public void Cancel() { Cancelled = true; announced = false; }
    public Signal Tick(float now, float warningSeconds, float interval, bool canLaunch)
    {
        if (Cancelled) return Signal.None;
        if (announced && !canLaunch)
        { announced = false; NextAt = Math.Max(NextAt, now + Math.Max(0, warningSeconds)); return Signal.None; }
        if (!announced && canLaunch && now >= NextAt - Math.Max(0, warningSeconds))
        {
            // При блокировке лагеря до срока всё равно даём полное предупреждение.
            announced = true; NextAt = Math.Max(NextAt, now + Math.Max(0, warningSeconds));
            return Signal.Warning;
        }
        if (!announced || now < NextAt || !canLaunch) return Signal.None;
        announced = false; NextAt = now + Math.Max(1, interval);
        return Signal.Launch;
    }
}
