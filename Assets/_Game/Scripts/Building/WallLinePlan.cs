using System.Collections.Generic;

/// <summary>Прямой участок по преобладающей оси X/Z, включая обе крайние клетки.</summary>
public static class WallLinePlan
{
    public struct Cell { public int x, z; public Cell(int x, int z) { this.x = x; this.z = z; } }
    public static int NormalizeRotation(int quarterTurns) => ((quarterTurns % 4) + 4) % 4;
    public static List<Cell> Create(int startX, int startZ, int endX, int endZ, int limit = 100, bool? alongX = null)
    {
        long dx = (long)endX - startX, dz = (long)endZ - startZ;
        bool horizontal = alongX ?? (System.Math.Abs(dx) >= System.Math.Abs(dz));
        long distance = System.Math.Abs(horizontal ? dx : dz);
        var cells = new List<Cell>();
        if (limit <= 0 || distance >= limit) return cells;
        int step = System.Math.Sign(horizontal ? dx : dz);
        for (int i = 0; i <= distance; i++)
            cells.Add(new Cell(startX + (horizontal ? i * step : 0), startZ + (horizontal ? 0 : i * step)));
        return cells;
    }
}
