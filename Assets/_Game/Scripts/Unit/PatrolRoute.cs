using System;
using System.Collections.Generic;

/// <summary>Маршрут туда и обратно. Смена цели происходит только после прибытия, а не после боя.</summary>
public sealed class PatrolRoute<T>
{
    private readonly T[] points;
    private int direction = 1;
    public int Index { get; private set; }
    public int Count => points.Length;
    public T Point => points[Index];
    public PatrolRoute(IList<T> source)
    {
        if (source == null || source.Count < 2) throw new ArgumentException("Patrol needs at least two points.");
        points = new T[source.Count];
        source.CopyTo(points, 0);
    }
    public void Arrive()
    {
        if (Index == points.Length - 1) direction = -1;
        else if (Index == 0) direction = 1;
        Index += direction;
    }
}
