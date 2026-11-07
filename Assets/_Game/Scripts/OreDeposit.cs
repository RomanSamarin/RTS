using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

/// <summary>Оставшийся запас породы. Добытая руда передаётся в рюкзак шахтёра.</summary>
public abstract class OreDeposit : MonoBehaviour
{
    private static readonly List<OreDeposit> deposits = new List<OreDeposit>();
    public static IReadOnlyList<OreDeposit> All => deposits;
    public abstract ResourceType Resource { get; }
    protected abstract int DefaultAmount { get; }
    [SerializeField, Min(0)] private int totalAmount;
    [Min(0.1f)] public float secondsPerUnit = 3f;
    public Transform workPoint;
    public int Remaining { get; private set; }
    private float progress;

    protected virtual void Awake() { Remaining = totalAmount > 0 ? totalAmount : DefaultAmount; }
    protected virtual void OnEnable() { if (!deposits.Contains(this)) deposits.Add(this); }
    protected virtual void OnDisable() { deposits.Remove(this); }
    public Vector3 WorkPosition => workPoint != null ? workPoint.position : transform.position;

    public int Work(float seconds, int capacity)
    {
        if (Remaining <= 0 || seconds <= 0 || capacity <= 0) return 0;
        progress += seconds;
        float duration = Mathf.Max(0.1f, secondsPerUnit);
        int mined = Mathf.Min(capacity, Mathf.Min(Remaining, Mathf.FloorToInt(progress / duration)));
        Remaining -= mined;
        progress -= mined * duration;
        return mined;
    }
}
