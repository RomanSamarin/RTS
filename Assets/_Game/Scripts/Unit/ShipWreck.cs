using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(GettingResources))]
public class ShipWreck : MonoBehaviour
{
    private static readonly List<ShipWreck> wrecks = new List<ShipWreck>();
    internal static bool HasWrecks => wrecks.Count > 0;

    internal static ShipWreck CreateAt(Vector3 position, Quaternion rotation)
    {
        var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "Плавающие обломки";
        crate.transform.SetPositionAndRotation(position + Vector3.up * .2f,
            Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
        crate.transform.localScale = new Vector3(1.4f, .55f, .9f);
        return crate.AddComponent<ShipWreck>();
    }

    private void Awake()
    {
        if (TryGetComponent<Collider>(out var collider)) collider.enabled = false;
        if (!TryGetComponent<Renderer>(out var renderer)) return;
        var properties = new MaterialPropertyBlock();
        var wood = new Color(.47f, .29f, .13f);
        properties.SetColor("_Color", wood);
        properties.SetColor("_BaseColor", wood);
        renderer.SetPropertyBlock(properties);
    }

    internal void KeepHalf()
    {
        var stock = GetComponent<GettingResources>();
        LootTransfer.KeepHalfOfEachResource(stock);
        if (LootTransfer.Count(stock) == 0) Destroy(gameObject);
    }

    internal static void CollectNearby(ShipHull ship)
    {
        var cargo = ship.Cargo;
        if (cargo == null || !cargo.isActiveAndEnabled || cargo.FreeSpace <= 0) return;

        float reach = ship.length * .5f + 2f;
        var target = ship.GetComponent<GettingResources>();
        foreach (var wreck in wrecks)
        {
            Vector3 offset = wreck.transform.position - ship.transform.position;
            if (Mathf.Abs(offset.y) > .8f || offset.x * offset.x + offset.z * offset.z > reach * reach) continue;

            var stock = wreck.GetComponent<GettingResources>();
            int collected = LootTransfer.Move(stock, target, cargo.FreeSpace);
            if (LootTransfer.Count(stock) == 0) Destroy(wreck.gameObject);
            if (collected > 0) return;
        }
    }

    private void OnEnable() => wrecks.Add(this);
    private void OnDisable() => wrecks.Remove(this);
}
