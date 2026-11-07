/// <summary>Объединяет изменения, сохраняя запросы, пришедшие во время пересчёта.</summary>
public sealed class RoadNavigationQueue
{
    public bool IsBuilding { get; private set; }
    public bool IsDirty { get; private set; }
    private float nextBuild;
    public void Changed(float now,float delay)
    { IsDirty=true; nextBuild=now+(delay>0f?delay:0f); }
    public bool TryBegin(float now)
    {
        if(IsBuilding||!IsDirty||now<nextBuild)return false;
        IsDirty=false;IsBuilding=true;return true;
    }
    public void Complete(){IsBuilding=false;}
    public void Reset(){IsDirty=false;IsBuilding=false;nextBuild=0f;}
}
