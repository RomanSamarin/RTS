/// <summary>Пока створки не полностью открыты, проход остаётся закрыт.</summary>
public class GateMotion
{
    public float Progress { get; private set; }
    public bool WantsOpen { get; private set; }
    public bool Passable => Progress >= 1f;
    public GateMotion(bool open) { WantsOpen = open; Progress = open ? 1f : 0f; }
    public void Request(bool open) { WantsOpen = open; }
    public void Tick(float delta, float duration)
    {
        float step = duration <= 0 ? 1f : System.Math.Max(0, delta) / duration;
        Progress = System.Math.Max(0, System.Math.Min(1, Progress + (WantsOpen ? step : -step)));
    }
}
