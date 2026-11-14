using System.Collections.Generic;
using UnityEngine;

/// <summary>Reserved and boarded passengers share the same finite seat budget.</summary>
public class ShipPassengerManifest
{
    private class Entry{public Object owner;public int cost;public bool aboard;}
    private readonly List<Entry> entries=new List<Entry>();
    private void Prune(){entries.RemoveAll(e=>e.owner==null);}
    public int Used{get{Prune();int count=0;foreach(var e in entries)count+=e.cost;return count;}}
    public int BoardedSeats{get{Prune();int count=0;foreach(var e in entries)if(e.aboard)count+=e.cost;return count;}}
    public bool Reserve(Object owner,int cost,int capacity)
    {Prune();if(owner==null||cost<=0||cost>capacity-Used||entries.Exists(e=>e.owner==owner))return false;entries.Add(new Entry{owner=owner,cost=cost});return true;}
    public bool Board(Object owner){Prune();if(owner==null||Next(false)!=owner)return false;var e=entries.Find(x=>x.owner==owner);if(e==null||e.aboard)return false;e.aboard=true;return true;}
    public void Release(Object owner){entries.RemoveAll(e=>e.owner==owner||e.owner==null);}
    public Object Next(bool aboard){Prune();var e=entries.Find(x=>x.aboard==aboard);return e!=null?e.owner:null;}
    public List<Object> Owners(){Prune();var result=new List<Object>();foreach(var e in entries)result.Add(e.owner);return result;}
    public int BoardedIndex(Object owner)
    {
        Prune();
        int index = 0;
        foreach (var entry in entries)
        {
            if (!entry.aboard) continue;
            if (entry.owner == owner) return index;
            index++;
        }
        return -1;
    }
    public static int Cost(bool cavalry,bool cart,bool catapult)=>catapult?6:cart?4:cavalry?2:1;
}
