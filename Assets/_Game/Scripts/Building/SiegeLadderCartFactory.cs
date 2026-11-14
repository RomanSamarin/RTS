using UnityEngine;
using UnityEngine.AI;

public static class SiegeLadderCartFactory
{
    public static bool Valid(GameObject prefab) => prefab == null || (prefab.GetComponent<SiegeLadderCart>() != null
        && prefab.GetComponent<Unit>() != null && prefab.GetComponent<NavMeshAgent>() != null
        && prefab.GetComponent<FoodCart>() == null && prefab.GetComponent<CartHorse>() == null && prefab.GetComponent<Catapult>() == null);
    public static bool TrySpawn(GameObject prefab, GameObject workshop, Transform spawn)
    {
        if (!Valid(prefab) || workshop == null) return false;
        var template = prefab != null ? prefab.GetComponent<NavMeshAgent>() : null;
        float radius = template != null ? template.radius : 1.1f;
        float height = template != null ? template.height : 2f;
        var filter = new NavMeshQueryFilter { agentTypeID = template != null ? template.agentTypeID : 0,
            areaMask = (template != null ? template.areaMask : NavMesh.AllAreas) & ~WallNavigation.AreaMask };
        Vector3 exit = spawn != null ? spawn.position : workshop.transform.position + workshop.transform.forward * 6;
        if (spawn == null && BuildingEntrance.TryGetWorldPoint(workshop, out var door))
        {
            var body = BuildingRoadAccess.SolidBody(workshop);
            Vector3 outward = door - (body != null ? body.bounds.center : workshop.transform.position); outward.y = 0;
            if (outward.sqrMagnitude < .01f) outward = workshop.transform.forward;
            exit = door + outward.normalized * (radius + 1);
        }
        if (!NavMesh.SamplePosition(exit, out var hit, 2, filter)
            || Physics.CheckCapsule(hit.position + Vector3.up * (radius + .1f),
                hit.position + Vector3.up * Mathf.Max(radius + .1f, height - radius), radius, ~0, QueryTriggerInteraction.Ignore)) return false;
        var cart = prefab != null ? Object.Instantiate(prefab, hit.position, workshop.transform.rotation)
            : CreatePlaceholder(hit.position, workshop.transform.rotation);
        SaveGameManager.Stamp(cart, prefab);
        FactionMember.Set(cart, FactionMember.Of(workshop)); cart.SetActive(true);
        return true;
    }
    public static GameObject CreatePlaceholder(Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("Осадная повозка"); root.SetActive(false);
        root.transform.SetPositionAndRotation(position, rotation); root.layer = 7;
        var nav = root.AddComponent<NavMeshAgent>(); nav.radius = 1.1f; nav.height = 2; nav.speed = 2;
        nav.areaMask &= ~WallNavigation.AreaMask;
        var body = root.AddComponent<BoxCollider>(); body.center = new Vector3(0, .8f, 0); body.size = new Vector3(1.7f, 1.3f, 2.8f);
        var unit = root.AddComponent<Unit>(); unit.unitMaxHealth = 180; unit.SetInitialHealthRatio(1);
        root.AddComponent<GettingResources>(); root.AddComponent<GOScript>(); root.AddComponent<AttackController>();
        var worker = root.AddComponent<ResourcesMove>(); worker.role = ResourcesMove.UnitRole.Transport;
        var cart = root.AddComponent<SiegeLadderCart>();
        Part(root.transform, "Bed", new Vector3(0, .8f, 0), new Vector3(1.5f, .25f, 2.6f));
        foreach (float x in new[] { -.85f, .85f }) foreach (float z in new[] { -.85f, .85f })
            Part(root.transform, "Wheel", new Vector3(x, .45f, z), new Vector3(.2f, .85f, .85f));
        foreach (float x in new[] { -.6f, .6f })
            Part(root.transform, "Shaft", new Vector3(x, .7f, 1.8f), new Vector3(.1f, .1f, 2.6f));
        var hitch = new GameObject("HorseHitchPoint").transform; hitch.SetParent(root.transform, false);
        hitch.localPosition = new Vector3(0, 0, 2.6f); cart.horseHitchPoint = hitch;
        for (int i = 0; i < 2; i++)
        {
            var ladder = new GameObject("Ladder" + i); ladder.transform.SetParent(root.transform, false);
            ladder.transform.localPosition = new Vector3(0, 1.05f + i * .18f, 0);
            foreach (float x in new[] { -.4f, .4f }) Part(ladder.transform, "Rail", new Vector3(x, 0, 0), new Vector3(.1f, .1f, 3.5f));
            for (int rung = 0; rung < 7; rung++) Part(ladder.transform, "Rung", new Vector3(0, 0, -1.4f + rung * .45f), new Vector3(.8f, .08f, .08f));
            cart.ladderVisuals[i] = ladder; ladder.SetActive(false);
        }
        root.SetActive(true); return root;
    }
    private static void Part(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name; part.layer = parent.gameObject.layer;
        part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
        var collider = part.GetComponent<Collider>(); collider.enabled = false;
        if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
    }
}
