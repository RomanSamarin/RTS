using UnityEditor;
using UnityEngine;

public static class ShipyardEditor
{
    private const string Folder = "Assets/_Game/Prefabs";
    [MenuItem("Tools/RTS/Create Shipyard Prefab")]
    private static void CreateShipyard()
    {
        var root = new GameObject("Судостроительная верфь");
        int layer = LayerMask.NameToLayer("Building"); if (layer >= 0) root.layer = layer;
        try
        {
            var body = root.AddComponent<BoxCollider>(); body.center = new Vector3(0, 1, 0); body.size = new Vector3(5, 2, 4);
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube); model.name = "PlaceholderBuilding";
            model.transform.SetParent(root.transform, false); model.transform.localPosition = new Vector3(0, 1, 0);
            model.transform.localScale = new Vector3(5, 2, 4); Object.DestroyImmediate(model.GetComponent<Collider>());
            root.AddComponent<BuildingHealth>();
            root.AddComponent<UiSelectBuilding>().panelName = "ShipyardPanel";
            var obstacle = root.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box; obstacle.center = body.center; obstacle.size = body.size; obstacle.carving = true;
            var door = new GameObject("SnapPoint").transform; door.SetParent(root.transform, false); door.localPosition = new Vector3(0, 0, -2.7f);
            root.AddComponent<BuildingEntrance>().doorPoint = door;
            var launch = new GameObject("LaunchPoint").transform; launch.SetParent(root.transform, false); launch.localPosition = new Vector3(0, 0, 10);
            var yard = root.AddComponent<Shipyard>(); yard.launchPoint = launch; yard.inputCapacity = 100; yard.SetRepeat(false);
            var supplies = root.AddComponent<ShipSupplyDock>(); supplies.berthPoint = launch; supplies.landDeliveryPoint = door;
            var hiring = root.GetComponent<WorkplaceHiring>(); hiring.profession = ResourcesMove.UnitRole.Shipwright; hiring.maxWorkers = 4;
            Save(root, "Shipyard");
        }
        finally { Object.DestroyImmediate(root); }
    }
    [MenuItem("Tools/RTS/Create Ship Prefabs")]
    private static void CreateShips()
    {
        foreach (ShipType type in System.Enum.GetValues(typeof(ShipType)))
        {
            var ship = ShipFactory.CreatePlaceholder(type, Vector3.zero, Quaternion.identity);
            try { Save(ship, type + "Ship"); }
            finally { Object.DestroyImmediate(ship); }
        }
    }
    [MenuItem("Tools/RTS/Create Ship Launch Area")]
    private static void CreateWater()
    {
        var obj = new GameObject("ShipLaunchArea"); Undo.RegisterCreatedObjectUndo(obj, "Create ship launch area");
        obj.AddComponent<ShipLaunchArea>(); Selection.activeGameObject = obj;
        Debug.Log("[Верфь] Поставьте зону на воду, задайте размеры и Seabed Layers с коллайдерами берега/дна.", obj);
    }
    [MenuItem("Tools/RTS/Create Ship Supply Dock")]
    private static void CreateSupplyDock()
    {
        var root = new GameObject("Точка снабжения корабля"); Undo.RegisterCreatedObjectUndo(root, "Create ship supply dock");
        int layer = LayerMask.NameToLayer("Building"); if (layer >= 0) root.layer = layer;
        var body = root.AddComponent<BoxCollider>(); body.center = new Vector3(0, .3f, 0); body.size = new Vector3(2, .6f, 2);
        root.AddComponent<BuildingHealth>(); root.AddComponent<UiSelectBuilding>().panelName = "ShipSuppliesPanel";
        var entrance = new GameObject("SnapPoint").transform; entrance.SetParent(root.transform, false); entrance.localPosition = new Vector3(0, 0, -1.8f);
        root.AddComponent<BuildingEntrance>().doorPoint = entrance;
        var berth = new GameObject("BerthPoint").transform; berth.SetParent(root.transform, false); berth.localPosition = new Vector3(0, 0, 8);
        var dock = root.AddComponent<ShipSupplyDock>(); dock.landDeliveryPoint = entrance; dock.berthPoint = berth;
        Selection.activeGameObject = root;
        Debug.Log("[Припасы] Подведите дорогу к SnapPoint, а BerthPoint расположите у центра стоящего на воде корабля.", root);
    }
    private static void Save(GameObject obj, string name)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game", "Prefabs");
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + name + ".prefab");
        var prefab = PrefabUtility.SaveAsPrefabAsset(obj, path); Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab);
        Debug.Log("[Верфь] Создан префаб: " + path);
    }
}
