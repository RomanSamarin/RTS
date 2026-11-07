using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Каталог постоянных ассетов автоматически обновляется перед Play и сборкой.</summary>
[InitializeOnLoad]
public class SaveCatalogBuilder : IPreprocessBuildWithReport
{
    public int callbackOrder=>0;
    static SaveCatalogBuilder()
    {EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Rebuild();};}
    public void OnPreprocessBuild(BuildReport report)=>Rebuild();
    [MenuItem("Tools/RTS/Rebuild Save Catalog")]
    public static void Rebuild()
    {
        const string directory="Assets/_Game/Resources";
        if(!AssetDatabase.IsValidFolder(directory))AssetDatabase.CreateFolder("Assets/_Game","Resources");
        const string path=directory+"/SaveAssetCatalog.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<SaveAssetCatalog>(path);
        if(catalog==null){catalog=ScriptableObject.CreateInstance<SaveAssetCatalog>();AssetDatabase.CreateAsset(catalog,path);}
        var roots=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p=>p!=null).Cast<Object>().ToList();
        var assets=new HashSet<Object>(EditorUtility.CollectDependencies(roots.ToArray()));
        foreach(var root in roots)assets.Add(root);
        foreach(var scene in EditorBuildSettings.scenes.Where(s=>s.enabled))
            foreach(string dependency in AssetDatabase.GetDependencies(scene.path,true))
                foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(dependency))if(asset!=null)assets.Add(asset);
        // Включаем и ассеты открытой сцены, ещё не включённой в Build Settings.
        foreach(var obj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach(var asset in EditorUtility.CollectDependencies(new Object[]{obj}))if(asset!=null)assets.Add(asset);
        catalog.entries.Clear();var keys=new HashSet<string>();
        foreach(var asset in assets)
        {
            if(asset==null||asset==catalog||asset is MonoScript||asset is SceneAsset||asset is DefaultAsset||!EditorUtility.IsPersistent(asset))continue;
            if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,out string guid,out long local))continue;
            string key=guid+":"+local;
            if(keys.Add(key))catalog.entries.Add(new SaveAssetCatalog.Entry{key=key,asset=asset});
        }
        catalog.entries.Sort((a,b)=>string.CompareOrdinal(a.key,b.key));EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        Debug.Log("[Сохранения] Каталог обновлён: "+catalog.entries.Count+" ассетов.");
    }
}
