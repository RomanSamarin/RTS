using System;
using System.Collections.Generic;
using UnityEngine;

public class RaidManager : MonoBehaviour
{
    public static RaidManager Instance { get; private set; }
    public GameObject PrefabEnemy;
    [Tooltip("Устаревшие точки. Новые отряды создаёт только BanditCamp; список сохранён для совместимости сцены.")]
    public List<GameObject> points = new List<GameObject>();

    [Header("Рейд")]
    public float raidIntervalMinutes = 4f;
    [Min(0)] public float initialDelayMinutes = 10f;
    [Min(0)] public float warningSeconds = 45f;
    public int minEnemies = 1;
    public int maxEnemies = 5;

    public string LastWarning { get; private set; } = "";
    public event Action<string> WarningRaised;
    public float StartedAt { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this; StartedAt = Time.time;
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        foreach (var camp in new List<BanditCamp>(BanditCamp.All))
            if (camp != null && camp.isActiveAndEnabled) camp.Tick(this);
    }
    public void Warn(string message)
    { LastWarning = message; Debug.Log(message, this); WarningRaised?.Invoke(message); }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
