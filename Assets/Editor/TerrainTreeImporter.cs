using UnityEditor;
using UnityEngine;
using System.IO;

// Applies only to the generated nature kit.
public class TerrainTreeImporter : AssetPostprocessor
{
    const string Root = "Assets/_Game/Environment/Trees/";
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Root)) return;
        var importer = (ModelImporter)assetImporter;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }
    Material OnAssignMaterialModel(Material source, Renderer renderer)
    {
        if (!assetPath.StartsWith(Root)) return null;
        var folder = Root + "Materials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root.TrimEnd('/'), "Materials");
        var path = folder + "/" + source.name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader) { name = source.name };
        mat.color = source.color;
        mat.SetFloat("_Metallic", 0);
        mat.SetFloat("_Smoothness", 0);
        mat.SetFloat("_Glossiness", 0);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
    [MenuItem("Tools/Terrain Trees/Create Prefabs")]
    static void CreatePrefabs()
    {
        var folder = Root + "Prefabs";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root.TrimEnd('/'), "Prefabs");
        foreach (var file in Directory.GetFiles(Root, "*.fbx"))
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
            if (model == null) continue;
            var path = folder + "/" + model.name + ".prefab";
            if (File.Exists(path)) continue;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var collider = instance.AddComponent<CapsuleCollider>();
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            collider.height = bounds.size.y;
            collider.radius = Mathf.Max(.1f, bounds.size.y * .055f);
            collider.center = new Vector3(0, bounds.size.y * .5f, 0);
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }
        AssetDatabase.SaveAssets();
    }
}
