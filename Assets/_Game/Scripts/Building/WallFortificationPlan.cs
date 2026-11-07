public static class WallFortificationPlan
{
    public static bool CanReuseEndpoint(int index, int count, bool compatibleWall)
        => compatibleWall && count > 0 && index >= 0 && index < count && (index == 0 || index == count - 1);
    public static bool IsTower(int index, int count, int interval)
    {
        if (count <= 0 || index < 0 || index >= count) return false;
        var layout = new bool[count];
        MarkSpan(layout, 0, count - 1, interval);
        return layout[index];
    }
    /// <summary>Равномерно распределяет промежуточные башни, не ставя их вплотную к опорным.</summary>
    public static void MarkSpan(bool[] layout, int start, int end, int interval)
    {
        if (layout.Length == 0 || end < start) return;
        layout[start % layout.Length] = true;
        layout[end % layout.Length] = true;
        int distance = end - start;
        int desiredSpans = (distance + System.Math.Max(2, interval) - 1) / System.Math.Max(2, interval);
        int spans = System.Math.Min(System.Math.Max(1, desiredSpans), System.Math.Max(1, distance / 2));
        for (int i = 1; i < spans; i++)
        {
            int position = start + (int)(((long)distance * i + spans / 2) / spans);
            layout[position % layout.Length] = true;
        }
    }
}
