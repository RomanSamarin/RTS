using UnityEngine;

/// <summary>Срабатывает при появлении повозки у дороги, без предварительного предупреждения.</summary>
public class BanditAmbushPoint : MonoBehaviour
{
    public BanditCamp camp;
    public Transform concealedSpawn;
    [Min(1)] public float triggerRadius = 12f;
    [Min(1)] public float cooldownSeconds = 180f;
    [Min(1)] public int bandits = 3;
    private float nextScan, nextAmbush;
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (Time.time < nextScan || Time.time < nextAmbush) return;
        nextScan = Time.time + 1f;
        if (camp == null || !camp.isActiveAndEnabled || camp.IsDestroyed || RaidManager.Instance == null) return;
        foreach (var target in CaravanTarget.All)
        {
            if(target!=null&&!FactionMember.AreEnemies(camp.gameObject,target.gameObject))continue;
            if (target == null || !target.IsAvailable || (target.transform.position - transform.position).sqrMagnitude > triggerRadius * triggerRadius) continue;
            int spawned = 0;
            for (int i = 0; i < bandits; i++)
                if (camp.Spawn(RaidManager.Instance, BanditRaider.Mission.Ambush, target.GetComponent<Unit>(), concealedSpawn != null ? concealedSpawn : transform) != null) spawned++;
            nextAmbush = Time.time + (spawned > 0 ? cooldownSeconds : 5f);
            if (spawned > 0) RaidManager.Instance.Warn("Засада! Разбойники напали на повозку.");
            return;
        }
    }
}
