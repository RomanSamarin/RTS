using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BuildingHealth), typeof(GettingResources))]
public class BanditCamp : MonoBehaviour
{
    private static readonly List<BanditCamp> camps = new List<BanditCamp>();
    public static IReadOnlyList<BanditCamp> All => camps;
    public string campName = "Лагерь разбойников";
    public GameObject enemyPrefab;
    public Transform spawnPoint;
    public LootCache lootPrefab;
    [Min(0)] public int guards = 2;
    [Min(1)] public int maxLivingBandits = 15;
    [Min(0)] public int startingGold = 20, startingSwords = 2, startingSpears = 3;
    [Min(1)] public float guardRadius = 20f;
    public bool IsDestroyed => destroyed || health == null || health.IsDead;
    public int LivingBandits
    { get { raiders.RemoveAll(r => r == null || !r.IsAlive); return raiders.Count; } }
    public float NextRaidAt => schedule != null && !schedule.Cancelled ? schedule.NextAt : float.PositiveInfinity;
    private readonly List<BanditRaider> raiders = new List<BanditRaider>();
    private BuildingHealth health;
    private GettingResources stash;
    private RaidSchedule schedule;
    private bool destroyed, initialized;
    private void Awake()
    {
        health = GetComponent<BuildingHealth>(); stash = GetComponent<GettingResources>();
        health.Died += OnDefeated;
    }
    private void Start()
    { stash.Gold += Mathf.Max(0, startingGold); stash.Sword += Mathf.Max(0, startingSwords); stash.Spear += Mathf.Max(0, startingSpears); }
    private void OnEnable() { if (!camps.Contains(this)) camps.Add(this); }
    private void OnDisable() { camps.Remove(this); }
    private void OnDestroy() { if (health != null) health.Died -= OnDefeated; schedule?.Cancel(); }
    public void Tick(RaidManager manager)
    {
        if (IsDestroyed) return;
        if (!initialized)
        {
            initialized = true;
            schedule = new RaidSchedule(Mathf.Max(Time.time + manager.warningSeconds,
                manager.StartedAt + Mathf.Max(0, manager.initialDelayMinutes) * 60f));
            for (int i = 0; i < guards; i++) Spawn(manager, BanditRaider.Mission.Guard, null, null);
        }
        bool room = LivingBandits < Mathf.Max(1, maxLivingBandits);
        var signal = schedule.Tick(Time.time, manager.warningSeconds, manager.raidIntervalMinutes * 60f, room && HasSettlement());
        if (signal == RaidSchedule.Signal.Warning)
            manager.Warn($"{campName}: готовится набег на поселение! До выхода около {Mathf.CeilToInt(schedule.NextAt - Time.time)} сек.");
        else if (signal == RaidSchedule.Signal.Launch)
        {
            int count = Random.Range(Mathf.Max(1, manager.minEnemies), Mathf.Max(manager.minEnemies, manager.maxEnemies) + 1);
            int spawned = 0;
            for (int i = 0; i < count; i++) if (Spawn(manager, BanditRaider.Mission.Raid, null, null) != null) spawned++;
            if (spawned > 0) manager.Warn($"{campName}: отряд разбойников вышел к поселению ({spawned}).");
        }
    }
    private static bool HasSettlement()
    {
        foreach (var b in BuildingManager.AllBuildings)
            if (b != null && b.isActiveAndEnabled && !b.IsDead && b.GetComponent<BanditCamp>() == null) return true;
        return false;
    }
    public BanditRaider Spawn(RaidManager manager, BanditRaider.Mission mission, Unit target, Transform ambushSpawn)
    {
        if (!isActiveAndEnabled || IsDestroyed || LivingBandits >= Mathf.Max(1, maxLivingBandits)) return null;
        var prefab = enemyPrefab != null ? enemyPrefab : manager.PrefabEnemy;
        if (prefab == null || prefab.GetComponent<NavMeshAgent>() == null) return null;
        var point = ambushSpawn != null ? ambushSpawn : spawnPoint != null ? spawnPoint : transform;
        var template = prefab.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter { agentTypeID = template.agentTypeID, areaMask = template.areaMask };
        if (!NavMesh.SamplePosition(point.position + Random.insideUnitSphere * 2f, out var hit, 5f, filter)) return null;
        var enemy = Instantiate(prefab, hit.position, prefab.transform.rotation);
        SaveGameManager.Stamp(enemy,prefab);
        enemy.tag = "Enemy";
        var ai = enemy.GetComponent<EnemyAi>(); if (ai == null) ai = enemy.AddComponent<EnemyAi>();
        ai.enabled = true;
        var attack = enemy.GetComponent<AttackController>();
        FactionMember.Set(enemy,FactionMember.Of(gameObject));
        var unit = enemy.GetComponent<Unit>(); if (unit == null) unit = enemy.AddComponent<Unit>();
        unit.SetInitialHealthRatio(1f);
        var population = enemy.GetComponent<UnitAdd>(); if (population != null) population.enabled = false;
        var worker = enemy.GetComponent<ResourcesMove>(); if (worker != null) worker.role = ResourcesMove.UnitRole.Soldier;
        var food = enemy.GetComponent<UnitEat>(); if (food != null) { food.CancelForCommand(false); food.enabled = false; }
        var cargo = enemy.GetComponent<GettingResources>(); if (cargo == null) cargo = enemy.AddComponent<GettingResources>();
        cargo.Clear(); cargo.Sword = 1; cargo.Gold = Random.Range(1, 4);
        var raider = enemy.GetComponent<BanditRaider>(); if (raider == null) raider = enemy.AddComponent<BanditRaider>();
        raider.Initialize(this, mission, target); raiders.Add(raider);
        return raider;
    }
    public void ReceiveLoot(GettingResources cargo)
    {
        if (IsDestroyed || cargo == null || cargo == stash) return;
        LootCache.Transfer(cargo, stash, int.MaxValue);
    }
    public Vector3 CombatPoint(Vector3 from)
    {
        var col = GetComponentInChildren<Collider>();
        if (col == null) return transform.position;
        Vector3 point = col.ClosestPoint(from);
        Vector3 away = from - point; away.y = 0;
        return away.sqrMagnitude > 0.001f ? point + away.normalized * 0.5f : point;
    }
    private void OnDefeated(BuildingHealth _)
    {
        if (destroyed) return;
        destroyed = true; schedule?.Cancel();
        LootCache.Drop(transform.position, stash, lootPrefab);
        RaidManager.Instance?.Warn($"{campName} уничтожен. Новых набегов и засад из него не будет. Трофеи ждут доставки на склад.");
    }
}
