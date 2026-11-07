/// <summary>Одна отсрочка на один эпизод голода; поиск еды и приказы её не перезапускают.</summary>
public sealed class HungerTimer
{
    public bool Active { get; private set; }
    public float StartedAt { get; private set; }
    public float DamageBeginsAt { get; private set; }
    public float NextDamageAt { get; private set; }
    public void Begin(float now, float grace)
    {
        if (Active) return;
        Restore(now, now + System.Math.Max(0, grace), now + System.Math.Max(0, grace));
    }
    public void Restore(float startedAt, float damageBeginsAt, float nextDamageAt)
    { Active = true; StartedAt = startedAt; DamageBeginsAt = damageBeginsAt; NextDamageAt = nextDamageAt; }
    public void Reset() { Active = false; }
    public float Remaining(float now) => Active ? System.Math.Max(0, DamageBeginsAt - now) : 0;
    public bool TryDamage(float now, float interval)
    {
        if (!Active || now < NextDamageAt) return false;
        NextDamageAt = now + System.Math.Max(1, interval); return true;
    }
}
