using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveGameChecks
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private sealed class State
    {
        public readonly Dictionary<ResourceTypeStub,int> stock=new Dictionary<ResourceTypeStub,int>();
        public readonly List<Nested> queue=new List<Nested>();
        public UnityEngine.Object workplace;
        public float nextMeal;
        public float progress;
        public Vector3 position;
        public Action callback;
        public State self;
    }
    private enum ResourceTypeStub { Fish,PreparedFood }
    private sealed class Nested { public int count;public UnityEngine.Object target; }
    public static void Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),"RTS_Save_Check_"+Guid.NewGuid().ToString("N"));
        string path=Path.Combine(folder,"test.json");
        try
        {
            SaveFileStore.Write(path,"first");SaveFileStore.Write(path,"second");
            Check(File.ReadAllText(path)=="second"&&File.ReadAllText(path+".bak")=="first","atomic replacement keeps previous valid save");
            Check(!File.Exists(path+".tmp"),"temporary file not left after success");
            Check(SaveFileStore.Digest("same")==SaveFileStore.Digest("same")&&SaveFileStore.Digest("same")!=SaveFileStore.Digest("tampered"),"checksum detects payload changes");
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
        var owner=new UnityEngine.Object();var replacement=new UnityEngine.Object();
        var codec=new SaveStateCodec{Reference=value=>ReferenceEquals(value,owner)?"workplace":null,Resolve=key=>key=="workplace"?replacement:null,timeOffset=100};
        var original=new State{workplace=owner,nextMeal=20,progress=3,position=new Vector3(1,2,3),callback=()=>{}};
        original.stock[ResourceTypeStub.Fish]=8;original.queue.Add(new Nested{count=2,target=owner});
        var fields=codec.CaptureFields(original);
        // Реальный файловый формат переводит граф полей через JSON.
        var disk=JsonUtility.FromJson<SaveFieldList>(JsonUtility.ToJson(new SaveFieldList{fields=fields,values=SaveStateGraph.Pack(new[]{fields})}));
        SaveStateGraph.Unpack(new[]{disk.fields},disk.values);
        var restored=new State();restored.stock[ResourceTypeStub.PreparedFood]=99;
        codec.RestoreFields(restored,disk.fields);
        Check(restored.stock.Count==1&&restored.stock[ResourceTypeStub.Fish]==8,"stock replaced rather than added to initial supplies");
        Check(ReferenceEquals(restored.workplace,replacement)&&ReferenceEquals(restored.queue[0].target,replacement),"nested Unity references resolve to recreated objects");
        Check(restored.nextMeal==120&&restored.progress==3,"deadlines rebased; elapsed progress unchanged");
        Check(restored.position.x==1&&restored.position.z==3&&restored.queue[0].count==2,"vectors and production queue round trip");
        Check(restored.callback==null,"event callbacks excluded");
        original.self=original;bool rejected=false;try{codec.CaptureFields(original);}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"unsupported cycles rejected before file replacement");
        Console.WriteLine("PASS: atomic save/backup, checksums, JSON state graph, inventories, queue, stable references and timer rebasing");
    }
    [Serializable] public class SaveFieldList {public List<SaveField> fields;public List<SaveValue> values;}
}

// Минимальные контракты Unity и игровых типов только для проверки кодека вне Unity.
public class GOScript { }
public class ManagerGO { }
public class ConstructionSite { }
public class PlacementSystem { }
public class SaveGameManager { public static bool IsLoading=>false; public static void Stamp(GameObject obj,GameObject prefab){} }
[Serializable] public class SaveField {public string name;public SaveValue value;}
[Serializable] public class SaveValue
{public string kind,type,text;[NonSerialized] public List<SaveValue> items;[NonSerialized] public List<SaveField> fields;public List<int> childIds;public List<string> fieldNames;public List<int> fieldIds;}
namespace UnityEngine
{
    public class Behaviour:Object { }
    public static class JsonUtility
    {
        private static readonly System.Text.Json.JsonSerializerOptions options=Options();
        private static System.Text.Json.JsonSerializerOptions Options()
        {
            var resolver=new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();
            resolver.Modifiers.Add(info=>
            {
                for(int i=info.Properties.Count-1;i>=0;i--)
                    if(info.Properties[i].AttributeProvider?.IsDefined(typeof(NonSerializedAttribute),false)??false)info.Properties.RemoveAt(i);
            });
            return new System.Text.Json.JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true,TypeInfoResolver=resolver};
        }
        public static string ToJson(object value)=>System.Text.Json.JsonSerializer.Serialize(value,value.GetType(),options);
        public static object FromJson(string json,Type type)=>System.Text.Json.JsonSerializer.Deserialize(json,type,options);
        public static T FromJson<T>(string json)=>System.Text.Json.JsonSerializer.Deserialize<T>(json,options);
    }
}
