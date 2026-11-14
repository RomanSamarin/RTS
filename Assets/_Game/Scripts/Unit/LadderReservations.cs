using System.Collections.Generic;

/// <summary>One physical ladder per owner; reservation never removes inventory.</summary>
public sealed class LadderReservations<T> where T : class
{
    private readonly List<T> owners = new List<T>();
    public int Available(int stock) => System.Math.Max(0, stock - owners.Count);
    public bool Reserve(T owner, int stock)
    {
        if (owner == null) return false;
        if (owners.Contains(owner)) return true;
        if (Available(stock) <= 0) return false;
        owners.Add(owner); return true;
    }
    public bool Contains(T owner) => owner != null && owners.Contains(owner);
    public bool Release(T owner) => owner != null && owners.Remove(owner);
    public void Clear() => owners.Clear();
}
