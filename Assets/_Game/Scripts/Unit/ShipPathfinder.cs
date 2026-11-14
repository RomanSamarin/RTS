using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Bounded water grid. The caller supplies hull-clearance and depth checks.</summary>
public static class ShipPathfinder
{
    public static bool Segment(Vector3 start, Vector3 end, float step, Func<Vector3, bool> free)
    {
        int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, end) / Mathf.Max(.1f, step)));
        for (int i = 0; i <= count; i++)
            if (!free(start + (end - start) * ((float)i / count))) return false;
        return true;
    }

    // Coordinates are local to a single water rectangle. No diagonal corner cutting.
    public static bool Find(Vector3 start, Vector3 end, float width, float length, float cell,
        float sampleStep, Func<Vector3, bool> free, List<Vector3> result)
    {
        result.Clear();
        if (!free(start) || !free(end)) return false;
        if (Segment(start, end, sampleStep, free)) { result.Add(end); return true; }
        cell = Mathf.Max(.5f, Mathf.Max(cell, Mathf.Max(width, length) / 100));
        int nx = Mathf.CeilToInt(width / cell) + 1, nz = Mathf.CeilToInt(length / cell) + 1;
        if (nx < 2 || nz < 2 || (long)nx * nz > 11000) return false;
        int Index(Vector3 p) => Mathf.Clamp((int)Math.Round((p.x + width * .5f) / cell), 0, nx - 1)
            + nx * Mathf.Clamp((int)Math.Round((p.z + length * .5f) / cell), 0, nz - 1);
        Vector3 Point(int i) => new Vector3(i % nx * cell - width * .5f, start.y, i / nx * cell - length * .5f);
        int source = Index(start), target = Index(end), size = nx * nz;
        var costs = new float[size]; var parents = new int[size]; var visited = new bool[size];
        var occupancy = new sbyte[size]; var open = new List<int>();
        for (int i = 0; i < size; i++) { costs[i] = float.PositiveInfinity; parents[i] = -1; }
        bool Free(int i)
        {
            if (occupancy[i] == 0) occupancy[i] = (sbyte)(free(Point(i)) ? 1 : -1);
            return occupancy[i] > 0;
        }
        if (!Free(source) || !Free(target) || !Segment(start, Point(source), sampleStep, free)
            || !Segment(Point(target), end, sampleStep, free)) return false;
        costs[source] = 0; open.Add(source);
        while (open.Count > 0)
        {
            int best = 0; float score = float.PositiveInfinity;
            for (int i = 0; i < open.Count; i++)
            {
                float f = costs[open[i]] + Vector3.Distance(Point(open[i]), end);
                if (f < score) { score = f; best = i; }
            }
            int current = open[best]; open.RemoveAt(best);
            if (current == target)
            {
                var raw = new List<Vector3> { end };
                for (int node = target; node != -1; node = parents[node]) raw.Add(Point(node));
                raw.Add(start); raw.Reverse();
                int anchor = 0;
                while (anchor < raw.Count - 1)
                {
                    int next = anchor + 1;
                    for (int j = raw.Count - 1; j > next; j--)
                        if (Segment(raw[anchor], raw[j], sampleStep, free)) { next = j; break; }
                    result.Add(raw[next]); anchor = next;
                }
                return true;
            }
            visited[current] = true;
            for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int x = current % nx + dx, z = current / nx + dz;
                if (x < 0 || x >= nx || z < 0 || z >= nz) continue;
                int n = x + z * nx;
                if (visited[n] || !Free(n)) continue;
                if (dx != 0 && dz != 0 && (!Free(current + dx) || !Free(current + dz * nx))) continue;
                float candidate = costs[current] + Vector3.Distance(Point(current), Point(n));
                if (candidate >= costs[n] || !Segment(Point(current), Point(n), sampleStep, free)) continue;
                costs[n] = candidate; parents[n] = current;
                if (!open.Contains(n)) open.Add(n);
            }
        }
        return false;
    }
}
