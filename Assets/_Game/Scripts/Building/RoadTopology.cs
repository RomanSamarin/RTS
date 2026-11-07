using System;
using System.Collections.Generic;

public enum RoadShape { Automatic, End, Straight, Corner, TJunction, Crossroad, Isolated }

/// <summary>Порты: север +Z=1, восток +X=2, юг -Z=4, запад -X=8.</summary>
public static class RoadTopology
{
    private static readonly RoadShape[] shapes={RoadShape.Isolated,RoadShape.End,RoadShape.Straight,RoadShape.Corner,RoadShape.TJunction,RoadShape.Crossroad};
    public static int Rotate(int mask, int quarterTurns)
    {
        int steps = ((quarterTurns % 4) + 4) % 4;
        mask &= 15;
        return ((mask << steps) | (mask >> (4 - steps))) & 15;
    }
    public static int Ports(RoadShape shape, int quarterTurns)
    {
        int mask = shape == RoadShape.End ? 1 : shape == RoadShape.Straight ? 5 :
            shape == RoadShape.Corner ? 3 : shape == RoadShape.TJunction ? 11 :
            shape == RoadShape.Crossroad ? 15 : 0;
        return Rotate(mask, quarterTurns);
    }
    public static void Resolve(int mask, out RoadShape shape, out int quarterTurns)
    {
        mask &= 15;
        foreach (var candidate in shapes)
            for (int rotation = 0; rotation < 4; rotation++)
                if (Ports(candidate, rotation) == mask) { shape = candidate; quarterTurns = rotation; return; }
        shape = RoadShape.Isolated; quarterTurns = 0;
    }
    public static List<(int x,int z)> Line(int startX,int startZ,int endX,int endZ,bool zFirst,int limit,out bool complete)
    {
        var cells=new List<(int x,int z)>();int x=startX,z=startZ;
        if(limit<=0){complete=false;return cells;}
        cells.Add((x,z));
        while((x!=endX||z!=endZ)&&cells.Count<limit)
        {
            if((zFirst&&z!=endZ)||x==endX)z+=z<endZ?1:-1;
            else x+=x<endX?1:-1;
            cells.Add((x,z));
        }
        complete=x==endX&&z==endZ;return cells;
    }
    public static float DistanceSquared(int mask, float x, float z, float cellSize, float roadWidth)
    {
        float w = Math.Max(0.05f, Math.Min(roadWidth, cellSize)) * 0.5f;
        float h = Math.Max(w, cellSize * 0.5f);
        float best = RectDistance(x,z,-w,w,-w,w);
        if ((mask & 1) != 0) best = Math.Min(best, RectDistance(x,z,-w,w,0,h));
        if ((mask & 2) != 0) best = Math.Min(best, RectDistance(x,z,0,h,-w,w));
        if ((mask & 4) != 0) best = Math.Min(best, RectDistance(x,z,-w,w,-h,0));
        if ((mask & 8) != 0) best = Math.Min(best, RectDistance(x,z,-h,0,-w,w));
        return best;
    }
    private static float RectDistance(float x,float z,float minX,float maxX,float minZ,float maxZ)
    {
        float dx = Math.Max(minX,Math.Min(maxX,x))-x;
        float dz = Math.Max(minZ,Math.Min(maxZ,z))-z;
        return dx*dx+dz*dz;
    }
}
