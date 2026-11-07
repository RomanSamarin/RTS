using System;

public static class RoadNavigationChecks
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Road navigation: "+message);}
    public static void Run()
    {
        var queue=new RoadNavigationQueue();
        Check(!queue.TryBegin(0),"no rebuild without road edits");
        queue.Changed(1,.35f);Check(!queue.TryBegin(1.2f),"wait for mesh edits/destruction to finish");
        queue.Changed(1.2f,.35f);Check(!queue.TryBegin(1.4f),"rapid edits combined into one pending update");
        Check(queue.TryBegin(1.6f)&&queue.IsBuilding&&!queue.IsDirty,"begin exactly one asynchronous batch");
        Check(!queue.TryBegin(2),"cannot launch overlapping asynchronous batch");
        queue.Changed(2,.35f);Check(queue.IsDirty&&!queue.TryBegin(3),"edit during build retained without concurrent build");
        queue.Complete();Check(queue.TryBegin(3),"late edit gets another rebuild after completion");
        queue.Complete();Check(!queue.TryBegin(5),"unchanged scene does not rebuild repeatedly");
        queue.Changed(5,.35f);queue.Reset();Check(!queue.IsDirty&&!queue.IsBuilding&&!queue.TryBegin(10),"disable cancels queued state");
        queue.Changed(10,-1);Check(queue.TryBegin(10),"negative debounce clamped safely");
        Console.WriteLine("PASS: road NavMesh rebuild debounce, single active batch, edits during rebuild and cleanup");
    }
}
