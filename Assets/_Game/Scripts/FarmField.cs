using System.Collections.Generic;
using UnityEngine;

/// <summary>Возобновляемое поле пшеницы. Урожай получает работающий фермер.</summary>
[DisallowMultipleComponent]
public class FarmField : MonoBehaviour
{
    private static readonly List<FarmField> fields = new List<FarmField>();
    public static IReadOnlyList<FarmField> All => fields;
    [Min(1)] public int harvestAmount = 20;
    [Min(0.1f)] public float secondsPerWheat = 12f;
    [Min(0.1f)] public float growthSeconds = 60f;
    public bool startsRipe = true;
    public Transform workPoint;
    [SerializeField] private int remaining;
    [SerializeField] private float growthRemaining;
    private float harvestProgress;
    public int Remaining => remaining;
    public bool IsRipe => remaining > 0;
    public float Growth01 => IsRipe ? 1f : 1f - Mathf.Clamp01(growthRemaining / Mathf.Max(0.1f, growthSeconds));
    public Vector3 WorkPosition => workPoint != null ? workPoint.position : transform.position;

    private void Awake()
    {
        ResetHarvest();
    }

    internal void ConfigureHarvest(int wheat, float regrowth)
    {
        harvestAmount = wheat;
        growthSeconds = regrowth;
        ResetHarvest();
    }

    private void ResetHarvest()
    {
        remaining = startsRipe ? Mathf.Max(1, harvestAmount) : 0;
        growthRemaining = Mathf.Max(0.1f, growthSeconds);
        harvestProgress = 0;
    }
    private void OnEnable() { if (!fields.Contains(this)) fields.Add(this); }
    private void OnDisable() { fields.Remove(this); }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (IsRipe) return;
        growthRemaining -= Time.deltaTime;
        if (growthRemaining > 0f) return;
        remaining = Mathf.Max(1, harvestAmount);
        harvestProgress = 0f;
    }

    public int Harvest(float workSeconds, int freeCapacity)
    {
        if (!IsRipe || workSeconds <= 0f || freeCapacity <= 0) return 0;
        harvestProgress += workSeconds;
        float duration = Mathf.Max(0.1f, secondsPerWheat);
        int amount = Mathf.Min(freeCapacity, Mathf.Min(remaining, Mathf.FloorToInt(harvestProgress / duration)));
        remaining -= amount;
        harvestProgress -= amount * duration;
        if (!IsRipe) growthRemaining = Mathf.Max(0.1f, growthSeconds);
        return amount;
    }
}
