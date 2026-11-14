using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class PortEditor
{
    [MenuItem("Tools/RTS/Create Port Prefab")]
    private static void CreatePort()
    {
        var root = new GameObject("Порт");
        try
        {
            int layer = LayerMask.NameToLayer("Building"); if (layer >= 0) root.layer = layer;
            var body = root.AddComponent<BoxCollider>(); body.center = new Vector3(0, 1, 0); body.size = new Vector3(6, 2, 4);
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube); model.transform.SetParent(root.transform, false);
            model.transform.localPosition = body.center; model.transform.localScale = body.size; Object.DestroyImmediate(model.GetComponent<Collider>());
            root.AddComponent<BuildingHealth>(); root.AddComponent<UiSelectBuilding>().panelName = "PortPanel";
            var obstacle = root.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = body.center; obstacle.size = body.size; obstacle.carving = true;
            var entry = new GameObject("SnapPoint").transform; entry.SetParent(root.transform, false); entry.localPosition = new Vector3(0, 0, -2.7f);
            root.AddComponent<BuildingEntrance>().doorPoint = entry;
            var berth = new GameObject("BerthPoint").transform; berth.SetParent(root.transform, false); berth.localPosition = new Vector3(0, 0, 10);
            var port = root.AddComponent<Port>(); port.Dock.landDeliveryPoint = entry; port.Dock.berthPoint = berth;
            var hiring = root.GetComponent<WorkplaceHiring>(); hiring.profession = ResourcesMove.UnitRole.PortWorker; hiring.maxWorkers = 3;
            const string folder = "Assets/_Game/Prefabs";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Game", "Prefabs");
            var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/Port.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path); Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab);
            Debug.Log("[Порт] Создан " + path + ". Настройте берег, дорогу и свою панель PortPanel.");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
