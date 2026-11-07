using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Ссылки на ассеты в сохранении имеют постоянные ключи, а не InstanceID.</summary>
public class SaveAssetCatalog : ScriptableObject
{
    [Serializable] public class Entry {public string key;public UnityEngine.Object asset;}
    public List<Entry> entries=new List<Entry>();
    public string Key(UnityEngine.Object asset)
    {foreach(var entry in entries)if(entry.asset==asset)return entry.key;return null;}
    public UnityEngine.Object Resolve(string key)
    {foreach(var entry in entries)if(entry.key==key)return entry.asset;return null;}
    public GameObject Prefab(GameObject instance)
    {
        var marker=instance.GetComponent<SaveEntity>();if(marker!=null&&marker.prefab!=null)return marker.prefab;
        string name=instance.name.Replace("(Clone)","").Trim();GameObject result=null;
        foreach(var entry in entries)
        {
            var candidate=entry.asset as GameObject;
            if(candidate==null||candidate.transform.parent!=null||candidate.name!=name)continue;
            // Одинаковые имена с разным назначением не должны подменять друг друга.
            if((instance.GetComponent<Unit>()!=null)!=(candidate.GetComponent<Unit>()!=null))continue;
            if(result!=null&&result!=candidate)throw new InvalidOperationException("Неоднозначный префаб "+name+". Назначьте SaveEntity.Prefab.");
            result=candidate;
        }
        return result;
    }
}
