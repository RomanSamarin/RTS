using System;
using UnityEngine;

public static class LadderClimbChecks
{
    private static void Check(bool value, string reason) { if (!value) throw new Exception("Ladder climbing: " + reason); }
    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;
    public static void Run()
    {
        var queue = new LadderClimbQueue<object>();
        var first = new object(); var second = new object(); var third = new object();
        Check(queue.Join(first) == 0 && queue.Join(second) == 1 && queue.Join(first) == 0
            && queue.Join(third) == 2, "repeat orders do not duplicate queue entries");
        Check(!queue.TryBegin(second) && queue.TryBegin(first) && queue.TryBegin(first)
            && !queue.TryBegin(third), "FIFO allows only the first soldier on ladder");
        queue.Release(second);
        Check(queue.IndexOf(third) == 1 && !queue.TryBegin(third), "cancel waiting soldier preserves active climber");
        queue.Release(first); queue.Release(first);
        Check(queue.IndexOf(third) == 0 && queue.TryBegin(third), "finishing or aborting releases next climber exactly once");
        Check(queue.Join(null) == -1 && !queue.TryBegin(null), "null cannot occupy ladder");
        queue.Release(third); Check(queue.Snapshot().Count == 0, "queue releases every reservation");
        var oldA = new UnityEngine.Object(); var oldB = new UnityEngine.Object();
        var newA = new UnityEngine.Object(); var newB = new UnityEngine.Object();
        var saved = new LadderClimbQueue<UnityEngine.Object>(); saved.Join(oldA); saved.Join(oldB); saved.TryBegin(oldA);
        var codec = new SaveStateCodec
        {
            Reference = obj => ReferenceEquals(obj, oldA) ? "A" : ReferenceEquals(obj, oldB) ? "B" : null,
            Resolve = key => key == "A" ? newA : key == "B" ? newB : null
        };
        var restored = new LadderClimbQueue<UnityEngine.Object>(); codec.RestoreFields(restored, codec.CaptureFields(saved));
        Check(restored.IndexOf(newA) == 0 && restored.IndexOf(newB) == 1 && !restored.TryBegin(newB),
            "saved queue and active climber resolve to new scene objects");
        restored.Release(newA); Check(restored.TryBegin(newB), "restored active reservation releases normally");

        var start = new Vector3(0, 0, -2); var crest = new Vector3(0, 4, 0); var landing = new Vector3(0, 4, 1);
        Check(Near(LadderClimbMotion.Position(start, crest, landing, -.1f), start)
            && Near(LadderClimbMotion.Position(start, crest, landing, 0), start), "climb starts at actual foot position");
        Check(Near(LadderClimbMotion.Position(start, crest, landing, .85f), crest)
            && Near(LadderClimbMotion.Position(start, crest, landing, 2), landing), "climber crosses wall edge before stepping onto deck");
        var before = LadderClimbMotion.Position(start, crest, landing, .8499f);
        var after = LadderClimbMotion.Position(start, crest, landing, .8501f);
        Check(Vector3.Distance(before, after) < .01f, "wall-edge transition has no teleport");
        float previousY = -1;
        for (int step = 0; step <= 100; step++)
        {
            var point = LadderClimbMotion.Position(start, crest, landing, step / 100f);
            Check(point.y >= previousY && point.y <= 4.001f, "climber never falls into wall while ascending"); previousY = point.y;
        }
        Check(LadderClimbMotion.Duration(5, 1) == 5 && LadderClimbMotion.Duration(5, 2) == 2.5f
            && LadderClimbMotion.Duration(0, 0) > 0, "distance/speed define duration without zero division");
        Console.WriteLine("PASS: ladder FIFO/cancellation, single climber, continuous wall-edge traversal and duration");
    }
}
