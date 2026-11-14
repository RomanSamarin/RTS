using System.Collections.Generic;
using System;
using _Game.Scripts.UI;
using UnityEngine;

[RequireComponent(typeof(GettingResources))]
public class LootCache : MonoBehaviour
{
    private static readonly List<LootCache> caches = new List<LootCache>();
    public static IReadOnlyList<LootCache> All => caches;
    [Min(0)] public float dangerRadius = 15f;
    private GettingResources cargo;
    public int Amount => Count(Stock);
    public int AmountFor(Predicate<ResourceType> accepts) => LootTransfer.Count(Stock, accepts);
    private GettingResources Stock => cargo != null ? cargo : (cargo = GetComponent<GettingResources>());
    public bool SafeToCollect=>IsSafeFor(Faction.Player);
    public bool IsSafeFor(GameObject collector)=>collector!=null&&IsSafeFor(FactionMember.Of(collector));
    private bool IsSafeFor(Faction side)
    {
            foreach (var enemy in FactionMember.All)
                if (enemy != null && FactionRules.AreEnemies(side,enemy.Side)&&enemy.isActiveAndEnabled && enemy.GetComponent<Unit>() != null
                    && enemy.GetComponent<Unit>().unitHealth > 0
                    && (enemy.transform.position - transform.position).sqrMagnitude < dangerRadius * dangerRadius) return false;
            return true;
    }
    private void OnEnable() { if (!caches.Contains(this)) caches.Add(this); }
    private void OnDisable() { caches.Remove(this); }
    public int Collect(GettingResources target, int limit, Predicate<ResourceType> accepts = null)
    {
        if (target==null||!IsSafeFor(target.gameObject)) return 0;
        int taken = LootTransfer.Move(Stock, target, limit, accepts);
        if (Amount == 0) Destroy(gameObject);
        return taken;
    }
    public static int Count(GettingResources stock)
        => LootTransfer.Count(stock);
    public static int Transfer(GettingResources from, GettingResources to, int limit)
    {
        return LootTransfer.Move(from, to, limit);
    }
    public static LootCache Drop(Vector3 position, GettingResources stock, LootCache prefab = null)
    {
        if (Count(stock) == 0) return null;
        LootCache result;
        if (prefab != null) result = Instantiate(prefab, position, Quaternion.identity);
        else
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Трофеи — доставить на склад"; obj.transform.position = position + Vector3.up * 0.25f;
            obj.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
            result = obj.AddComponent<LootCache>();
        }
        result.Stock.Clear(); Transfer(stock, result.Stock, int.MaxValue); return result;
    }
}
