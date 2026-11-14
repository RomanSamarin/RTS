using UnityEngine;
using UnityEngine.AI;

public static class ShipFactory
{
    public static string ConfigurationError(GameObject prefab, ShipType type, ShipLaunchArea water, Transform launch)
    {
        if (water == null) return "Назначьте Water Area верфи";
        if (launch == null) return "Назначьте Launch Point верфи";
        if (water.seabedLayers.value == 0) return "Назначьте Seabed Layers зоны спуска";
        if (prefab == null) return null;
        var hull = prefab.GetComponent<ShipHull>();
        var collider = prefab.GetComponent<Collider>();
        if (hull == null || hull.type != type || prefab.GetComponent<Unit>() == null
            || collider == null || !collider.enabled || collider.isTrigger || hull.width <= 0 || hull.length <= 0
            || hull.minimumDepth <= 0 || hull.clearanceHeight <= 0)
            return "Проверьте тип, размеры, Unit и Collider префаба корабля";
        if (prefab.GetComponentInChildren<NavMeshAgent>(true) != null || prefab.GetComponent<ResourcesMove>() != null
            || prefab.GetComponent<UnitAdd>() != null || prefab.GetComponent<UnitEat>() != null || prefab.GetComponent<GOScript>() != null)
            return "Префаб корабля не должен содержать наземную профессию, питание жителя или NavMeshAgent";
        return null;
    }
    public static bool TrySpawn(GameObject prefab, ShipType type, GameObject shipyard,
        ShipLaunchArea water, Transform launch, out string reason)
    {
        reason = ConfigurationError(prefab, type, water, launch);
        if (!string.IsNullOrEmpty(reason) || shipyard == null) return false;
        ShipHull.DefaultDimensions(type, out float width, out float length, out float depth);
        float height = 6;
        if (prefab != null)
        { var hull = prefab.GetComponent<ShipHull>(); width = hull.width; length = hull.length; depth = hull.minimumDepth; height = hull.clearanceHeight; }
        Vector3 point = launch.position; point.y = water.SurfaceHeight;
        Quaternion rotation = Quaternion.Euler(0, launch.eulerAngles.y, 0);
        if (!water.CanLaunch(point, rotation, width, length, depth, height, out reason)) return false;
        var ship = prefab != null ? Object.Instantiate(prefab, point, rotation) : CreatePlaceholder(type, point, rotation);
        FactionMember.Set(ship, FactionMember.Of(shipyard)); SaveGameManager.Stamp(ship, prefab);
        ship.SetActive(true); return true;
    }
    public static GameObject CreatePlaceholder(ShipType type, Vector3 position, Quaternion rotation)
    {
        var root = new GameObject(type == ShipType.Trade ? "Торговый корабль" : type == ShipType.Transport ? "Транспортный корабль" : "Боевой корабль");
        root.SetActive(false); root.layer = 7; root.transform.SetPositionAndRotation(position, rotation);
        root.AddComponent<GettingResources>();
        var unit = root.AddComponent<Unit>(); unit.unitMaxHealth = type == ShipType.Trade ? 300 : type == ShipType.Transport ? 350 : 500;
        unit.SetInitialHealthRatio(1);
        var ship = root.AddComponent<ShipHull>(); ship.type = type;
        if (root.GetComponent<ShipProvisions>() == null) root.AddComponent<ShipProvisions>();
        if (root.GetComponent<ShipCargo>() == null) root.AddComponent<ShipCargo>();
        if (root.GetComponent<ShipNavigation>() == null) root.AddComponent<ShipNavigation>();
        if (root.GetComponent<ShipCrew>() == null) root.AddComponent<ShipCrew>();
        if (root.GetComponent<ShipTraffic>() == null) root.AddComponent<ShipTraffic>();
        if (root.GetComponent<ShipTradeRoute>() == null) root.AddComponent<ShipTradeRoute>();
        if (root.GetComponent<ShipTransport>() == null) root.AddComponent<ShipTransport>();
        if (type == ShipType.Warship && root.GetComponent<ShipCombat>() == null) root.AddComponent<ShipCombat>();
        ShipHull.DefaultDimensions(type, out ship.width, out ship.length, out ship.minimumDepth);
        var collider = root.AddComponent<BoxCollider>(); collider.center = new Vector3(0, .5f, 0);
        collider.size = new Vector3(ship.width, 1.2f, ship.length);
        Part(root, "Hull", new Vector3(0, .25f, 0), new Vector3(ship.width, .8f, ship.length));
        foreach (float side in new[] { -1f, 1f })
            Part(root, "Rail", new Vector3(side * ship.width * .46f, .9f, 0), new Vector3(.15f, .6f, ship.length));
        Part(root, "Mast", new Vector3(0, 2.7f, 0), new Vector3(.15f, 4.8f, .15f));
        Part(root, "Sail", new Vector3(0, 3.5f, .1f), new Vector3(ship.width * .85f, 2.4f, .06f));
        if (type == ShipType.Trade) for (int i = -1; i <= 1; i++)
            Part(root, "CargoCrate", new Vector3(0, 1, i * 1.5f), new Vector3(1, .8f, 1));
        if (type == ShipType.Transport)
            for (int i = -2; i <= 2; i++) Part(root, "Bench", new Vector3(0, .85f, i * 1.2f), new Vector3(ship.width * .8f, .15f, .35f));
        if (type == ShipType.Warship) foreach (float side in new[] { -1f, 1f })
            for (int i = -1; i <= 1; i++) Part(root, "Shield", new Vector3(side * ship.width * .5f, 1.2f, i * 2), new Vector3(.1f, .7f, .6f));
        root.SetActive(true); return root;
    }
    private static void Part(GameObject root, string name, Vector3 position, Vector3 scale)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name; part.layer = root.layer;
        part.transform.SetParent(root.transform, false); part.transform.localPosition = position; part.transform.localScale = scale;
        var collider = part.GetComponent<Collider>(); collider.enabled = false;
        if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
    }
}
