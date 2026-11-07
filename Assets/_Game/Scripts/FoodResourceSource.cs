using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

/// <summary>Конечный запас: выключение и включение объекта не восстанавливает ресурс.</summary>
public abstract class FoodResourceSource : MonoBehaviour
{
    private static readonly List<FoodResourceSource> sources = new List<FoodResourceSource>();
    public static IReadOnlyList<FoodResourceSource> All => sources;
    public abstract ResourceType Resource { get; }
    [SerializeField, Min(0)] private int remaining = 20;
    [Min(0.1f)] public float secondsPerUnit = 2f;
    public Transform workPoint;
    public int Remaining => remaining;
    public virtual Vector3 WorkPosition => workPoint != null ? workPoint.position : transform.position;
    private float progress;
    protected virtual void OnEnable() { if (!sources.Contains(this)) sources.Add(this); }
    protected virtual void OnDisable() { sources.Remove(this); }
    public virtual int Work(float seconds, int capacity)
    {
        if (!isActiveAndEnabled || remaining <= 0 || seconds <= 0 || capacity <= 0) return 0;
        progress += seconds;
        float duration = Mathf.Max(0.1f, secondsPerUnit);
        int amount = Mathf.Min(capacity, Mathf.Min(remaining, Mathf.FloorToInt(progress / duration)));
        remaining -= amount;
        // Нельзя накопить время добычи для будущего рюкзака.
        progress = Mathf.Min(progress - amount * duration, duration);
        if (remaining == 0) { progress = 0; OnDepleted(); }
        return amount;
    }
    protected virtual void OnDepleted() { }
}
