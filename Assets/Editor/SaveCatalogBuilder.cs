using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Каталог постоянных ассетов автоматически обновляется перед Play и сборкой.</summary>
[InitializeOnLoad]
public class SaveCatalogBuilder : IPreprocessBuildWithReport
{
    public int callbackOrder=>0;
    static SaveCatalogBuilder()
    {EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Rebuild();};}
    public void OnPreprocessBuild(BuildReport report) => Rebuild(includeOpenScene: false);

    [MenuItem("Tools/RTS/Rebuild Save Catalog")]
    public static void Rebuild() => Rebuild(includeOpenScene: true);

    private static void Rebuild(bool includeOpenScene)
    {
        const string directory="Assets/_Game/Resources";
        if(!AssetDatabase.IsValidFolder(directory))AssetDatabase.CreateFolder("Assets/_Game","Resources");
        const string path=directory+"/SaveAssetCatalog.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<SaveAssetCatalog>(path);
        if(catalog==null){catalog=ScriptableObject.CreateInstance<SaveAssetCatalog>();AssetDatabase.CreateAsset(catalog,path);}
        var roots=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p=>p!=null).Cast<Object>().ToList();
        var assets=new HashSet<Object>(EditorUtility.CollectDependencies(roots.ToArray()));
        foreach(var root in roots)assets.Add(root);
        var sceneDependencies = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .SelectMany(scene => AssetDatabase.GetDependencies(scene.path, true))
            .Distinct();
        foreach (string dependency in sceneDependencies)
        {
            // GetDependencies also returns the scene itself. LoadAllAssetsAtPath
            // cannot load objects serialized inside a scene during a player build.
            if (dependency.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                dependency == path || IsEditorOnlyPath(dependency))
                continue;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(dependency))
                if (asset != null) assets.Add(asset);
        }

        if (includeOpenScene)
        {
            // Поддержка Play Mode для открытой карты, даже если её ещё нет в сборке.
            var openScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var asset in EditorUtility.CollectDependencies(openScene.GetRootGameObjects()))
                if (asset != null) assets.Add(asset);
        }
        catalog.entries.Clear();var keys=new HashSet<string>();
        foreach(var asset in assets)
        {
            if (asset == null || asset == catalog || asset is MonoScript || asset is SceneAsset ||
                asset is DefaultAsset || !EditorUtility.IsPersistent(asset) ||
                (asset.hideFlags & HideFlags.DontSaveInBuild) != 0 ||
                IsEditorOnlyPath(AssetDatabase.GetAssetPath(asset)))
                continue;
            if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,out string guid,out long local))continue;
            string key=guid+":"+local;
            if(keys.Add(key))catalog.entries.Add(new SaveAssetCatalog.Entry{key=key,asset=asset});
        }
        catalog.entries.Sort((a,b)=>string.CompareOrdinal(a.key,b.key));EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        Debug.Log("[Сохранения] Каталог обновлён: "+catalog.entries.Count+" ассетов.");
    }

    private static bool IsEditorOnlyPath(string path)
    {
        // Component gizmo icons may arrive through prefab dependencies even
        // when their editor-only folder was skipped in the scene scan.
        return path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("/Editor Resources/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("/Editor Default Resources/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("/Gizmos/", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
