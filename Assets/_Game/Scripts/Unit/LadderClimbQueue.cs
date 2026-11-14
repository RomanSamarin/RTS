using System.Collections.Generic;

/// <summary>FIFO entry and one climber on a physical ladder.</summary>
public sealed class LadderClimbQueue<T> where T : class
{
    private readonly List<T> waiting = new List<T>();
    private T active;
    public int Join(T owner)
    {
        if (owner == null) return -1;
        if (!waiting.Contains(owner)) waiting.Add(owner);
        return waiting.IndexOf(owner);
    }
    public int IndexOf(T owner) => waiting.IndexOf(owner);
    public bool TryBegin(T owner)
    {
        if (owner == null || waiting.Count == 0 || !ReferenceEquals(waiting[0], owner)) return false;
        if (active != null && !ReferenceEquals(active, owner)) return false;
        active = owner; return true;
    }
    public void Release(T owner)
    {
        waiting.Remove(owner);
        if (ReferenceEquals(active, owner)) active = null;
    }
    public List<T> Snapshot() => new List<T>(waiting);
}
