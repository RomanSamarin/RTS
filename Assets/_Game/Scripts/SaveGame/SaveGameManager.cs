using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>F5 — быстрое сохранение, F9 — загрузка. Три автосохранения каждые пять минут.</summary>
public class SaveGameManager : MonoBehaviour
{
    public static SaveGameManager Instance {get;private set;}
    public static bool IsLoading {get;private set;}
    public bool autoSaveEnabled=true;
    [Min(30)] public float autoSaveSeconds=300;
    public KeyCode saveKey=KeyCode.F5,loadKey=KeyCode.F9;
    public string Status {get;private set;}="";
    public event Action<string> StatusChanged;
    public string SaveDirectory=>Path.Combine(Application.persistentDataPath,"Saves");
    public static void Stamp(GameObject instance,GameObject prefab)
    {
        if(instance==null||prefab==null)return;
        var entity=instance.GetComponent<SaveEntity>()??instance.AddComponent<SaveEntity>();entity.prefab=prefab;
    }
    private SaveAssetCatalog catalog;
    private SaveWorldSnapshot snapshot;
    private float nextAuto;
    private bool saving;
    private bool sceneReady;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics(){Instance=null;IsLoading=false;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if(Instance!=null)return;
        var obj=new GameObject("SaveGameManager");obj.AddComponent<SaveGameManager>();
    }
    private void Awake()
    {
        if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
        Instance=this;DontDestroyOnLoad(gameObject);
        catalog=Resources.Load<SaveAssetCatalog>("SaveAssetCatalog");
        SceneManager.sceneLoaded+=OnSceneLoaded;
        nextAuto=Time.realtimeSinceStartup+autoSaveSeconds;
    }
    private void OnDestroy(){SceneManager.sceneLoaded-=OnSceneLoaded;if(Instance==this)Instance=null;}
    private void OnSceneLoaded(Scene scene,LoadSceneMode mode)
    {
        if(mode!=LoadSceneMode.Single)return;
        sceneReady=false;
        if(catalog==null)catalog=Resources.Load<SaveAssetCatalog>("SaveAssetCatalog");
        snapshot=catalog!=null?new SaveWorldSnapshot(scene,catalog):null;
        nextAuto=Time.realtimeSinceStartup+Mathf.Max(30,autoSaveSeconds);
        StartCoroutine(MarkReady());
    }
    private IEnumerator MarkReady(){yield return null;sceneReady=true;}
    private bool Gameplay=>UnityEngine.Object.FindObjectOfType<UnitManager>()!=null||UnityEngine.Object.FindObjectOfType<ManagerGO>()!=null;
    private void Update()
    {
        if(IsLoading||saving||!sceneReady||PauseMenu.BlocksWorldInput||!Gameplay)return;
        var focused=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        bool typing=focused!=null&&((focused.GetComponentInParent<TMPro.TMP_InputField>()?.isFocused??false)||(focused.GetComponentInParent<UnityEngine.UI.InputField>()?.isFocused??false));
        if(!typing&&Input.GetKeyDown(saveKey))SaveGame();
        if(!typing&&Input.GetKeyDown(loadKey))LoadGame();
        if(autoSaveEnabled&&Time.timeScale>0&&Time.realtimeSinceStartup>=nextAuto)AutoSave();
    }
    private void Report(string message,bool error=false)
    {Status=message;StatusChanged?.Invoke(message);if(error)Debug.LogWarning("[Сохранения] "+message);else Debug.Log("[Сохранения] "+message);}
    private string PathFor(string slot)=>Path.Combine(SaveDirectory,slot+".json");
    public void SaveGame()=>WriteSlot("quicksave");
    internal bool TrySaveBeforeExit(float resumeTimeScale) => WriteSlot("quicksave", resumeTimeScale);
    public void AutoSave()
    {
        nextAuto=Time.realtimeSinceStartup+Mathf.Max(30,autoSaveSeconds);
        string slot=Enumerable.Range(0,3).Select(i=>"autosave"+i).OrderBy(s=>File.Exists(PathFor(s))?File.GetLastWriteTimeUtc(PathFor(s)):DateTime.MinValue).First();
        WriteSlot(slot);
    }
    private bool WriteSlot(string slot, float? resumeTimeScale = null)
    {
        if(IsLoading||saving||!sceneReady||!Gameplay)return false;
        saving=true;
        try
        {
            if(snapshot==null||catalog==null)throw new InvalidOperationException("Нет каталога ассетов. Выполните Tools / RTS / Rebuild Save Catalog в Unity.");
            var world=snapshot.Capture();snapshot.Validate(world);
            world.timeScale = PlaySpeed(resumeTimeScale ?? world.timeScale);
            world.values=SaveStateGraph.Pack(AllFields(world));
            string payload=JsonUtility.ToJson(world);
            var envelope=new SaveEnvelope{payload=payload,checksum=SaveFileStore.Digest(payload)};
            SaveFileStore.Write(PathFor(slot),JsonUtility.ToJson(envelope));
            Report(slot.StartsWith("auto")?"Автосохранение завершено":"Игра сохранена");
            return true;
        }
        catch(Exception ex){Report("Сохранение не выполнено: "+ex.Message,true);return false;}
        finally{saving=false;}
    }
    private SaveWorld ReadFile(string path)
    {
        if(new FileInfo(path).Length>128*1024*1024)throw new InvalidOperationException("Слишком большой файл сохранения");
        var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path));
        if(envelope==null||envelope.version!=1||string.IsNullOrEmpty(envelope.payload)||SaveFileStore.Digest(envelope.payload)!=envelope.checksum)
            throw new InvalidOperationException("Повреждённый файл или неподдерживаемая версия сохранения");
        var world=JsonUtility.FromJson<SaveWorld>(envelope.payload);
        SaveStateGraph.Unpack(AllFields(world),world.values);
        if(catalog==null)throw new InvalidOperationException("Нет каталога ассетов");
        (snapshot??new SaveWorldSnapshot(SceneManager.GetActiveScene(),catalog)).Validate(world);return world;
    }
    public void LoadGame()
    {
        string path=PathFor("quicksave");
        LoadFirstAvailable(new[]{path,path+".bak"},"Быстрое сохранение ещё не создано");
    }

    public bool HasSave(bool includeQuickSave) =>
        SaveFileStore.FindLoadCandidates(SaveDirectory, includeQuickSave).Length > 0;

    public void ContinueLatest() => LoadFirstAvailable(
        SaveFileStore.FindLoadCandidates(SaveDirectory, true), "Сохранений пока нет");

    public void LoadFromMenu()
    {
        string quick = PathFor("quicksave");
        LoadFirstAvailable(new[] { quick, quick + ".bak" }
            .Concat(SaveFileStore.FindLoadCandidates(SaveDirectory, false)).ToArray(), "Сохранений пока нет");
    }

    public void LoadLatestAutoSave() => LoadFirstAvailable(
        SaveFileStore.FindLoadCandidates(SaveDirectory, false), "Автосохранений пока нет");

    private static System.Collections.Generic.IEnumerable<System.Collections.Generic.List<SaveField>> AllFields(SaveWorld world)
    {
        foreach(var entity in world.entities)foreach(var component in entity.components)yield return component.fields;
        foreach(var component in world.controllers)yield return component.fields;
    }
    private void LoadFirstAvailable(string[] paths, string emptyMessage)
    {
        if (IsLoading || saving) return;
        IsLoading = true;
        StartCoroutine(LoadRoutine(paths, emptyMessage));
    }

    private static float PlaySpeed(float savedSpeed)
        => savedSpeed > 0 && !float.IsInfinity(savedSpeed) ? savedSpeed : 1f;

    private IEnumerator LoadRoutine(string[] paths, string emptyMessage)
    {
        float previousTimeScale = Time.timeScale;
        bool sceneOpened = false;
        bool found = false;
        Report("Загрузка мира…");
        foreach (string path in paths)
        {
            if (!File.Exists(path)) continue;
            found = true;
            SaveWorld world;
            try
            {
                world = ReadFile(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Сохранения] " + Path.GetFileName(path) + " — " + ex);
                continue;
            }

            Time.timeScale = 0;
            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(world.scene, LoadSceneMode.Single);
                if (operation == null) throw new InvalidOperationException("Сцена недоступна");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Сохранения] " + Path.GetFileName(path) + " — " + ex);
                continue;
            }
            while (!operation.isDone) yield return null;
            sceneOpened = true;

            // Объекты сцены и созданные префабы получают Start перед восстановлением связей.
            yield return null;
            Exception error = null;
            try { snapshot.CreateEntities(world); }
            catch (Exception ex) { error = ex; }
            yield return null;
            if (error == null)
            {
                try { snapshot.Restore(world); }
                catch (Exception ex) { error = ex; }
            }
            if (error != null)
            {
                Debug.LogWarning("[Сохранения] " + Path.GetFileName(path) + " — " + error);
                continue;
            }

            IsLoading = false;
            Time.timeScale = PlaySpeed(world.timeScale);
            nextAuto = Time.realtimeSinceStartup + Mathf.Max(30, autoSaveSeconds);
            Report("Игра загружена");
            yield break;
        }

        if (sceneOpened)
        {
            const string menu = "Assets/Scenes/LordsMenu.unity";
            if (Application.CanStreamedLevelBeLoaded(menu))
            {
                var recovery = SceneManager.LoadSceneAsync(menu, LoadSceneMode.Single);
                while (recovery != null && !recovery.isDone) yield return null;
            }
        }
        IsLoading = false;
        Time.timeScale = sceneOpened ? 1f : previousTimeScale;
        Report(found ? "Не удалось восстановить сохранение" : emptyMessage, found);
    }
}
