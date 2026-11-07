using System.Collections.Generic;

/// <summary>Чертёж из нескольких отрезков. Здесь нет построек и резервов ресурсов.</summary>
public static class WallDraftPlan
{
    public struct Node
    {
        public int x, z, rotation;
        public bool fixedTower;
        public Node(int x, int z, int rotation) { this.x = x; this.z = z; this.rotation = rotation; fixedTower = false; }
    }
    public static bool FixTower(List<Node> plan, int x, int z)
    {
        for (int i = 0; i < plan.Count; i++)
        {
            var node = plan[i];
            if (node.x != x || node.z != z) continue;
            node.fixedTower = true; plan[i] = node; return true;
        }
        return false;
    }
    public static bool NeedsTower(IReadOnlyList<Node> plan, int index, int interval)
        => index >= 0 && index < plan.Count && TowerLayout(plan, interval)[index];
    public static bool[] TowerLayout(IReadOnlyList<Node> plan, int interval)
    {
        var layout = new bool[plan.Count];
        if (plan.Count == 0) return layout;
        layout[0] = true;
        int previous = 0;
        for (int i = 1; i < plan.Count; i++)
        {
            if (!plan[i].fixedTower) continue;
            WallFortificationPlan.MarkSpan(layout, previous, i, interval);
            previous = i;
        }
        // В замкнутом периметре конечная опора — первая башня, а не клетка рядом с ней.
        var first = plan[0]; var last = plan[plan.Count - 1];
        bool closed = plan.Count >= 4 && System.Math.Abs((long)last.x - first.x) + System.Math.Abs((long)last.z - first.z) == 1;
        WallFortificationPlan.MarkSpan(layout, previous, closed ? plan.Count : plan.Count - 1, interval);
        return layout;
    }
    public static int ActiveRotation(int startX, int startZ, int endX, int endZ, int? manualRotation)
    {
        if (manualRotation.HasValue) return WallLinePlan.NormalizeRotation(manualRotation.Value);
        return System.Math.Abs((long)endX - startX) >= System.Math.Abs((long)endZ - startZ) ? 0 : 1;
    }
    public static List<Node> Preview(IReadOnlyList<Node> fixedPart, int startX, int startZ, int endX, int endZ,
        int? manualRotation, out string error, int limit = 100)
    {
        error = "";
        int angle = ActiveRotation(startX, startZ, endX, endZ, manualRotation);
        var leg = WallLinePlan.Create(startX, startZ, endX, endZ, limit, angle % 2 == 0);
        var result = new List<Node>();
        if (leg.Count == 0) { error = "Максимум " + limit + " секций во всём чертеже."; return result; }
        var used = new HashSet<long>();
        foreach (var node in fixedPart) { result.Add(node); used.Add(Key(node.x, node.z)); }
        for (int i = 0; i < leg.Count; i++)
        {
            var cell = leg[i];
            if (i == 0 && result.Count > 0 && result[result.Count - 1].x == cell.x && result[result.Count - 1].z == cell.z) continue;
            if (!used.Add(Key(cell.x, cell.z)))
            {
                // Замыкание периметра не создаёт вторую секцию в начальной клетке.
                if (i == leg.Count - 1 && result.Count > 2 && result[0].x == cell.x && result[0].z == cell.z) break;
                error = "Чертёж пересекает уже намеченный участок."; result.Clear(); return result;
            }
            if (result.Count >= limit) { error = "Максимум " + limit + " секций во всём чертеже."; result.Clear(); return result; }
            result.Add(new Node(cell.x, cell.z, angle));
        }
        return result;
    }
    private static long Key(int x, int z) => ((long)x << 32) | (uint)z;
}
