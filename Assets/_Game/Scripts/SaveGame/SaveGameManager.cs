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
    private SaveWorld pending;
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
        if(catalog!=null)snapshot=new SaveWorldSnapshot(scene,catalog);
        nextAuto=Time.realtimeSinceStartup+Mathf.Max(30,autoSaveSeconds);
        StartCoroutine(MarkReady());
    }
    private IEnumerator MarkReady(){yield return null;sceneReady=true;}
    private bool Gameplay=>UnityEngine.Object.FindObjectOfType<UnitManager>()!=null||UnityEngine.Object.FindObjectOfType<ManagerGO>()!=null;
    private void Update()
    {
        if(IsLoading||saving||!sceneReady||!Gameplay)return;
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
    public void AutoSave()
    {
        nextAuto=Time.realtimeSinceStartup+Mathf.Max(30,autoSaveSeconds);
        string slot=Enumerable.Range(0,3).Select(i=>"autosave"+i).OrderBy(s=>File.Exists(PathFor(s))?File.GetLastWriteTimeUtc(PathFor(s)):DateTime.MinValue).First();
        WriteSlot(slot);
    }
    private void WriteSlot(string slot)
    {
        if(IsLoading||saving||!sceneReady||!Gameplay)return;
        saving=true;
        try
        {
            if(snapshot==null||catalog==null)throw new InvalidOperationException("Нет каталога ассетов. Выполните Tools / RTS / Rebuild Save Catalog в Unity.");
            var world=snapshot.Capture();snapshot.Validate(world);
            world.values=SaveStateGraph.Pack(AllFields(world));
            string payload=JsonUtility.ToJson(world);
            var envelope=new SaveEnvelope{payload=payload,checksum=SaveFileStore.Digest(payload)};
            SaveFileStore.Write(PathFor(slot),JsonUtility.ToJson(envelope));
            Report(slot.StartsWith("auto")?"Автосохранение завершено":"Игра сохранена");
        }
        catch(Exception ex){Report("Сохранение не выполнено: "+ex.Message,true);}
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
    public void LoadGame()=>RequestLoad("quicksave");
    private static System.Collections.Generic.IEnumerable<System.Collections.Generic.List<SaveField>> AllFields(SaveWorld world)
    {
        foreach(var entity in world.entities)foreach(var component in entity.components)yield return component.fields;
        foreach(var component in world.controllers)yield return component.fields;
    }
    public void LoadLatestAutoSave()
    {
        var slots=Enumerable.Range(0,3).Select(i=>"autosave"+i).Where(s=>File.Exists(PathFor(s))).OrderByDescending(s=>File.GetLastWriteTimeUtc(PathFor(s))).ToArray();
        foreach(string slot in slots)if(RequestLoad(slot))return;
        if(slots.Length==0)Report("Автосохранений пока нет",true);
    }
    private bool RequestLoad(string slot)
    {
        if(IsLoading||saving)return false;
        string path=PathFor(slot);
        try
        {
            if(!File.Exists(path))throw new FileNotFoundException("Сохранение не найдено");
            try{pending=ReadFile(path);}
            catch(Exception primary)
            {
                if(!File.Exists(path+".bak"))throw;
                pending=ReadFile(path+".bak");Debug.LogWarning("[Сохранения] Основной файл недоступен, используется резервная копия: "+primary.Message);
            }
            IsLoading=true;StartCoroutine(LoadRoutine());return true;
        }
        catch(Exception ex){Report("Загрузка не начата: "+ex.Message,true);return false;}
    }
    private IEnumerator LoadRoutine()
    {
        Time.timeScale=0;
        Report("Загрузка мира…");
        var world=pending;pending=null;
        AsyncOperation operation;
        try{operation=SceneManager.LoadSceneAsync(world.scene,LoadSceneMode.Single);}
        catch(Exception ex){IsLoading=false;Report("Не удалось открыть сцену: "+ex.Message,true);yield break;}
        while(!operation.isDone)yield return null;
        // Start сценических объектов должен закончиться до перезаписи ресурсов/здоровья.
        yield return null;
        Exception error=null;
        try{snapshot.CreateEntities(world);}catch(Exception ex){error=ex;}
        // Динамические префабы также получают Start до восстановления снимка.
        yield return null;
        if(error==null)try{snapshot.Restore(world);}catch(Exception ex){error=ex;}
        IsLoading=false;
        Time.timeScale=error==null?Mathf.Max(0,world.timeScale):0;
        nextAuto=Time.realtimeSinceStartup+Mathf.Max(30,autoSaveSeconds);
        if(error!=null)Report("Не удалось восстановить мир; игра оставлена на паузе: "+error.Message,true);
        else Report("Игра загружена");
    }
}
