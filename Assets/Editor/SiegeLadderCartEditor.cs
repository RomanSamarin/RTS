using UnityEditor;
using UnityEngine;

public static class SiegeLadderCartEditor
{
    [MenuItem("Tools/RTS/Create Siege Ladder Cart Prefab")]
    private static void CreatePrefab()
    {
        const string folder = "Assets/_Game/Prefabs";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Game", "Prefabs");
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/SiegeLadderCart.prefab");
        var cart = SiegeLadderCartFactory.CreatePlaceholder(Vector3.zero, Quaternion.identity);
        try
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(cart, path);
            Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab);
            Debug.Log("[Осадная повозка] Префаб создан: " + path + ". Назначьте его в Workshop.Ladder Cart Prefab.");
        }
        finally { Object.DestroyImmediate(cart); }
    }
}
