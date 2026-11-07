using System;
using System.Collections.Generic;

/// <summary>Плоский граф исключает обрезание вложенных словарей лимитом глубины JsonUtility.</summary>
public static class SaveStateGraph
{
    public static List<SaveValue> Pack(IEnumerable<List<SaveField>> groups)
    {
        var nodes=new List<SaveValue>();
        foreach(var fields in groups)foreach(var field in fields)Add(field.value,nodes);
        return nodes;
    }
    private static int Add(SaveValue node,List<SaveValue> nodes)
    {
        int id=nodes.Count;nodes.Add(node);
        if(node.items!=null)
        {node.childIds=new List<int>();foreach(var child in node.items)node.childIds.Add(Add(child,nodes));}
        if(node.fields!=null)
        {
            node.fieldNames=new List<string>();node.fieldIds=new List<int>();
            foreach(var field in node.fields){node.fieldNames.Add(field.name);node.fieldIds.Add(Add(field.value,nodes));}
        }
        return id;
    }
    public static void Unpack(IEnumerable<List<SaveField>> groups,List<SaveValue> nodes)
    {
        if(nodes==null)throw new InvalidOperationException("Нет таблицы данных сохранения");
        foreach(var fields in groups)foreach(var field in fields)Expand(field.value,nodes,new HashSet<int>(),0);
    }
    private static SaveValue Child(int id,List<SaveValue> nodes,HashSet<int> path,int depth)
    {
        if(id<0||id>=nodes.Count||!path.Add(id))throw new InvalidOperationException("Повреждён граф сохранения");
        var value=nodes[id];Expand(value,nodes,path,depth+1);path.Remove(id);return value;
    }
    private static void Expand(SaveValue node,List<SaveValue> nodes,HashSet<int> path,int depth)
    {
        if(node==null||depth>30)throw new InvalidOperationException("Некорректная глубина сохранения");
        if(node.childIds!=null)
        {node.items=new List<SaveValue>();foreach(int id in node.childIds)node.items.Add(Child(id,nodes,path,depth));}
        if(node.fieldIds!=null)
        {
            if(node.fieldNames==null||node.fieldNames.Count!=node.fieldIds.Count)throw new InvalidOperationException("Повреждены поля сохранения");
            node.fields=new List<SaveField>();
            for(int i=0;i<node.fieldIds.Count;i++)node.fields.Add(new SaveField{name=node.fieldNames[i],value=Child(node.fieldIds[i],nodes,path,depth)});
        }
    }
}
