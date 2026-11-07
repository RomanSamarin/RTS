using UnityEngine;

[RequireComponent(typeof(EnemyAi), typeof(GettingResources))]
public class BanditRaider : MonoBehaviour
{
    public enum Mission { Guard, Raid, Ambush }
    public BanditCamp Home { get; private set; }
    public Mission Job { get; private set; }
    public Unit Caravan { get; private set; }
    public Vector3 Origin { get; private set; }
    public float ExpiresAt { get; private set; }
    public bool IsAlive => isActiveAndEnabled && GetComponent<Unit>() != null && GetComponent<Unit>().unitHealth > 0;
    public void Initialize(BanditCamp camp, Mission job, Unit caravan)
    {
        Home = camp; Job = job; Caravan = caravan; Origin = transform.position;
        ExpiresAt = Time.time + 90f;
        var ai = GetComponent<EnemyAi>(); ai.IsRaid = job == Mission.Raid;
        ai.randomBuilding = false;
    }
    public void DropLoot()
    { LootCache.Drop(transform.position, GetComponent<GettingResources>(), Home != null ? Home.lootPrefab : null); }
}
