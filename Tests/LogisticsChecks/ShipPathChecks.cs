using System;
using System.Collections.Generic;
using UnityEngine;

static class ShipPathChecks
{
    static void Check(bool condition, string name) { if (!condition) throw new Exception("Ship path: " + name); }
    public static void Run()
    {
        var route = new List<Vector3>();
        Vector3 start = new Vector3(-8, 0, 0), goal = new Vector3(8.3f, 0, .3f);
        bool Water(Vector3 p) => Math.Abs(p.x) < 9.5f && Math.Abs(p.z) < 9.5f;
        Check(ShipPathfinder.Find(start, goal, 20, 20, 1, .2f, Water, route), "open water route");
        Check(route.Count == 1 && Vector3.Distance(route[0], goal) < .001f, "direct route ends at exact clicked point");
        bool Island(Vector3 p) => Water(p) && !(Math.Abs(p.x) < 2 && Math.Abs(p.z) < 4);
        Check(ShipPathfinder.Find(start, goal, 20, 20, 1, .2f, Island, route), "route around island");
        Check(route.Count > 1, "island cannot be crossed directly");
        Vector3 previous = start;
        foreach (var point in route)
        {
            Check(ShipPathfinder.Segment(previous, point, .1f, Island), "every smoothed segment stays in water");
            previous = point;
        }
        bool Barrier(Vector3 p) => Water(p) && Math.Abs(p.x) > 2;
        Check(!ShipPathfinder.Find(start, goal, 20, 20, 1, .2f, Barrier, route) && route.Count == 0,
            "disconnected basins reject order and clear old route");
        Check(!ShipPathfinder.Find(start, new Vector3(0, 0, 0), 20, 20, 1, .2f, Island, route), "blocked destination");
        Check(!ShipPathfinder.Segment(start, goal, .2f, Barrier), "dynamic obstruction stops swept step");
        Check(!ShipPathfinder.Find(new Vector3(0, 0, 0), goal, 20, 20, 1, .2f, Island, route), "blocked start");
        // Touching obstacles leave only a diagonal crack: a ship cannot cut through it.
        bool Corner(Vector3 p) => Water(p) && !(p.x >= -.15f && p.z <= .15f) && !(p.x <= .15f && p.z >= -.15f);
        Check(!ShipPathfinder.Find(new Vector3(-3, 0, -3), new Vector3(3, 0, 3), 20, 20, 1, .1f, Corner, route),
            "no diagonal corner cutting");
        Console.WriteLine("Ship water paths: PASS");
    }
}
