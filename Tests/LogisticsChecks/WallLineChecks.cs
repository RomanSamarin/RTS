using System;
using System.Collections.Generic;

public static class WallLineChecks
{
    public static void Run()
    {
        var single = WallLinePlan.Create(3, -4, 3, -4);
        Check(single.Count == 1 && single[0].x == 3 && single[0].z == -4, "single wall cell");
        var line = WallLinePlan.Create(4, 7, -2, 9);
        Check(line.Count == 7 && line[6].x == -2 && line[6].z == 7, "negative direction and dominant X");
        var vertical = WallLinePlan.Create(4, 7, 5, -2);
        Check(vertical.Count == 10 && vertical[9].x == 4 && vertical[9].z == -2, "negative dominant Z");
        var unique = new HashSet<string>();
        for (int i = 0; i < vertical.Count; i++)
        {
            var cell = vertical[i];
            Check(unique.Add(cell.x + ":" + cell.z), "no double cost for duplicate cell");
            if (i > 0) Check(Math.Abs(cell.x - vertical[i - 1].x) + Math.Abs(cell.z - vertical[i - 1].z) == 1, "no gaps");
        }
        Check(WallLinePlan.Create(0, 0, 99, 0).Count == 100, "inclusive segment limit");
        Check(WallLinePlan.Create(0, 0, 100, 0).Count == 0, "reject oversized line rather than silently truncate");
        Check(WallLinePlan.Create(int.MinValue, 0, int.MaxValue, 0).Count == 0, "coordinate overflow guarded");
        var forcedZ = WallLinePlan.Create(3, 4, 30, 7, alongX: false);
        Check(forcedZ.Count == 4 && forcedZ[3].x == 3 && forcedZ[3].z == 7, "90-degree turn locks Z instead of following dominant cursor X");
        var forcedX = WallLinePlan.Create(3, 4, 5, 30, alongX: true);
        Check(forcedX.Count == 3 && forcedX[2].x == 5 && forcedX[2].z == 4, "180-degree turn locks X instead of following dominant cursor Z");
        Check(WallLinePlan.NormalizeRotation(-1) == 3 && WallLinePlan.NormalizeRotation(4) == 0,
            "clockwise and counterclockwise rotation wrap correctly");
        var firstDraft = WallDraftPlan.Preview(new List<WallDraftPlan.Node>(), 0, 0, 3, 0, 2, out var draftError);
        Check(draftError == "" && firstDraft.Count == 4 && firstDraft[0].rotation == 2, "draft stores the first rotated segment");
        Check(WallDraftPlan.FixTower(firstDraft, 3, 0), "shift-click pins a tower at the endpoint");
        var bentDraft = WallDraftPlan.Preview(firstDraft, 3, 0, 3, 2, null, out draftError);
        Check(draftError == "" && bentDraft.Count == 6 && firstDraft.Count == 4, "preview continuation keeps fixed leg unchanged and charges common corner once");
        Check(bentDraft[0].rotation == 2 && bentDraft[4].rotation == 1, "turn resets for the new leg while fixed preview retains its rotation");
        Check(bentDraft[3].x == 3 && bentDraft[3].z == 0 && bentDraft[3].fixedTower
            && WallDraftPlan.NeedsTower(bentDraft, 3, 6) && !WallFortificationPlan.IsTower(3, bentDraft.Count, 6),
            "fixed corner remains a tower even after it stops being the last or periodic section");
        Check(WallDraftPlan.FixTower(bentDraft, 3, 2), "second corner tower can be pinned");
        var twiceBent = WallDraftPlan.Preview(bentDraft, 3, 2, 5, 2, null, out draftError);
        Check(draftError == "" && twiceBent.Count == 8 && twiceBent[3].fixedTower && twiceBent[5].fixedTower,
            "multiple pinned towers keep their cells while another wall is previewed");
        Check(twiceBent[3].rotation == 2 && twiceBent[5].rotation == 1 && twiceBent[6].rotation == 0,
            "rotating a new leg cannot rotate fixed towers and earlier walls");
        Check(WallDraftPlan.ActiveRotation(3, 0, 3, 0, null) == 0, "rotation resets at the new corner");
        var badDraft = WallDraftPlan.Preview(firstDraft, 3, 0, 1, 0, null, out draftError);
        Check(badDraft.Count == 0 && draftError != "" && firstDraft.Count == 4, "invalid crossing does not corrupt staged preview");
        var oversizedDraft = WallDraftPlan.Preview(firstDraft, 3, 0, 3, 99, null, out draftError);
        Check(oversizedDraft.Count == 0 && draftError != "", "combined draft limit applies across all legs");
        var towers = new List<int>();
        for (int i = 0; i < 14; i++) if (WallFortificationPlan.IsTower(i, 14, 6)) towers.Add(i);
        Check(towers.Count == 4 && towers[0] == 0 && towers[1] == 4 && towers[2] == 9 && towers[3] == 13,
            "intermediate towers spread evenly without a duplicate next to the end tower");
        var shortDraft = WallDraftPlan.Preview(new List<WallDraftPlan.Node>(), 0, 0, 5, 0, null, out draftError);
        WallDraftPlan.FixTower(shortDraft, 5, 0);
        var afterCorner = WallDraftPlan.Preview(shortDraft, 5, 0, 5, 4, null, out draftError);
        var shortLayout = WallDraftPlan.TowerLayout(afterCorner, 6);
        Check(shortLayout[5] && !shortLayout[6] && !shortLayout[4], "no extra automatic tower directly beside a pinned corner");
        var rectangle = WallDraftPlan.Preview(shortDraft, 5, 0, 5, 5, null, out draftError);
        WallDraftPlan.FixTower(rectangle, 5, 5);
        rectangle = WallDraftPlan.Preview(rectangle, 5, 5, 0, 5, null, out draftError);
        WallDraftPlan.FixTower(rectangle, 0, 5);
        rectangle = WallDraftPlan.Preview(rectangle, 0, 5, 0, 0, null, out draftError);
        var rectangleLayout = WallDraftPlan.TowerLayout(rectangle, 6);
        int rectangleTowerCount = 0;
        for (int i = 0; i < rectangleLayout.Length; i++) if (rectangleLayout[i]) rectangleTowerCount++;
        Check(draftError == "" && rectangle.Count == 20 && rectangleTowerCount == 4
            && rectangleLayout[0] && rectangleLayout[5] && rectangleLayout[10] && rectangleLayout[15] && !rectangleLayout[19],
            "rectangle has exactly four corner towers and no extra terminal tower next to the starting one");
        Check(WallFortificationPlan.IsTower(0, 1, 6), "single cell creates one usable entrance tower");
        Check(WallFortificationPlan.CanReuseEndpoint(0, 6, true) && WallFortificationPlan.CanReuseEndpoint(5, 6, true),
            "existing walls can anchor both ends of a perpendicular branch");
        Check(!WallFortificationPlan.CanReuseEndpoint(2, 6, true) && !WallFortificationPlan.CanReuseEndpoint(0, 6, false),
            "branch cannot silently cross interior walls or reuse an unrelated building");
        Check(!WallFortificationPlan.IsTower(-1, 14, 6) && !WallFortificationPlan.IsTower(14, 14, 6), "no tower outside plan");
        // A tower replaces a wall, so total material demand must not charge both.
        int stone = 0;
        for (int i = 0; i < 14; i++) stone += WallFortificationPlan.IsTower(i, 14, 6) ? 25 : 5;
        Check(stone == 150, "mixed line price is ten walls and four towers, not fourteen walls plus towers");
        var gate = new GateMotion(false);
        Check(!gate.Passable, "closed gate blocks movement");
        gate.Request(true); gate.Tick(0.5f, 1);
        Check(!gate.Passable && gate.Progress == 0.5f, "half-open gate cannot be crossed");
        gate.Tick(0.5f, 1); Check(gate.Passable, "fully open gate admits units");
        gate.Request(false); gate.Tick(0.2f, 1); Check(!gate.Passable, "closing gate blocks a new route");
        float interrupted = gate.Progress; gate.Request(true); gate.Tick(0.1f, 1);
        Check(gate.Progress > interrupted, "interrupted closing reverses without snapping");
        gate.Tick(100, 1); Check(gate.Progress == 1, "animation overshoot clamps open");
        gate.Request(false); gate.Tick(100, 1); Check(gate.Progress == 0 && !gate.Passable, "overshoot clamps closed");
        gate.Request(true); gate.Tick(0, 0); Check(gate.Passable, "zero-duration animation is safe");
        Console.WriteLine("PASS: wall direction, continuity, tower spacing, mixed cost and animated gate passability");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
