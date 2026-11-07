using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

/// <summary>Снимок данных игрового кода. Unity-ссылки заменяются стабильными ключами.</summary>
public sealed class SaveStateCodec
{
    public Func<UnityEngine.Object,string> Reference;
    public Func<string,UnityEngine.Object> Resolve;
    public float timeOffset;
    private readonly HashSet<object> stack=new HashSet<object>(new ReferenceComparer());
    private class ReferenceComparer:IEqualityComparer<object>
    {public new bool Equals(object a,object b)=>ReferenceEquals(a,b);public int GetHashCode(object a)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a);}
    public static bool GameType(Type type)=>type.Assembly==typeof(Unit).Assembly;
    public static IEnumerable<FieldInfo> Fields(Type type)
    {
        for(var current=type;current!=null&&current!=typeof(MonoBehaviour)&&current!=typeof(Behaviour)&&current!=typeof(UnityEngine.Object);current=current.BaseType)
            foreach(var field in current.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                if(!field.IsStatic&&!field.IsDefined(typeof(NonSerializedAttribute),false)&&!typeof(Delegate).IsAssignableFrom(field.FieldType))yield return field;
    }
    public List<SaveField> CaptureFields(object instance)
    {
        var fields=new List<SaveField>();
        foreach(var field in Fields(instance.GetType()))
        {
            if(Skip(instance,field.Name))continue;
            var value=Capture(field.GetValue(instance),0);
            if(value.kind!="skip")fields.Add(new SaveField{name=field.DeclaringType.FullName+"/"+field.Name,value=value});
        }
        return fields;
    }
    private static bool Skip(object owner,string name)
    {
        // Эти объекты генерируются заново и не являются состоянием симуляции.
        if(owner is ConstructionSite&&(name=="barRoot"||name=="barFillImage"||name=="barText"||name=="disabledBehaviours"))return true;
        if(owner is GOScript&&(name=="lastCommandUpdateFrame"||name=="camera"))return true;
        if(owner is PlacementSystem&&(name=="buildingState"||name=="placementEndedFrame"))return true;
        if(owner is ManagerGO&&(name=="militaryInputEndedFrame"||name.StartsWith("IsChoosing")||name=="patrolPoints"||name=="patrolUnits"))return true;
        return false;
    }
    private SaveValue Capture(object value,int depth)
    {
        if(value==null)return new SaveValue{kind="null"};
        if(depth>30)throw new InvalidOperationException("Слишком глубокий граф состояния "+value.GetType().Name);
        Type type=value.GetType();string typeName=type.AssemblyQualifiedName;
        if(value is UnityEngine.Object unity)
        {
            if(unity==null)return new SaveValue{kind="null"};
            string key=Reference(unity);
            return new SaveValue{kind=key==null?"skip":"ref",text=key,type=typeName};
        }
        if(type.IsEnum||type.IsPrimitive||type==typeof(string)||type==typeof(decimal))
            return new SaveValue{kind="scalar",type=typeName,text=Convert.ToString(value,CultureInfo.InvariantCulture)};
        if(type.IsValueType&&type.Namespace!=null&&type.Namespace.StartsWith("UnityEngine"))
            return new SaveValue{kind="json",type=typeName,text=JsonUtility.ToJson(value)};
        bool sequence=value is IList||value is IDictionary;
        if(!sequence&&!GameType(type))return new SaveValue{kind="skip"};
        if(!type.IsValueType&&!stack.Add(value))throw new InvalidOperationException("Циклические данные "+type.Name);
        try
        {
            if(value is IDictionary dictionary)
            {
                var result=new SaveValue{kind="dictionary",type=typeName,items=new List<SaveValue>()};
                foreach(DictionaryEntry pair in dictionary)
                {
                    var key=Capture(pair.Key,depth+1);var item=Capture(pair.Value,depth+1);
                    if(key.kind=="skip"||item.kind=="skip")continue;
                    result.items.Add(key);result.items.Add(item);
                }
                return result;
            }
            if(value is IList list)
            {
                var result=new SaveValue{kind="list",type=typeName,items=new List<SaveValue>()};
                foreach(var item in list)result.items.Add(Capture(item,depth+1));return result;
            }
            var record=new SaveValue{kind="object",type=typeName,fields=new List<SaveField>()};
            foreach(var field in Fields(type))
            {
                var item=Capture(field.GetValue(value),depth+1);
                if(item.kind!="skip")record.fields.Add(new SaveField{name=field.DeclaringType.FullName+"/"+field.Name,value=item});
            }
            return record;
        }
        finally{if(!type.IsValueType)stack.Remove(value);}
    }
    public void RestoreFields(object target,List<SaveField> data)
    {
        if(data==null)return;
        foreach(var item in data)
        {
            FieldInfo match=null;
            foreach(var field in Fields(target.GetType()))if(field.DeclaringType.FullName+"/"+field.Name==item.name){match=field;break;}
            if(match==null)throw new InvalidOperationException("Поле изменилось: "+item.name);
            object value=Restore(item.value,match.FieldType,match.GetValue(target));
            if(value is float number&&IsTimestamp(match.Name)&&!float.IsInfinity(number)&&number>0)value=number+timeOffset;
            // readonly коллекции уже изменены на месте. Остальные поля восстанавливаются явно.
            if(!match.IsInitOnly||!ReferenceEquals(value,match.GetValue(target)))match.SetValue(target,value);
        }
    }
    private static bool IsTimestamp(string name)
    {
        string n=name;
        if(n.StartsWith("<")&&n.Contains(">"))n=n.Substring(1,n.IndexOf('>')-1);
        return n.StartsWith("next",StringComparison.OrdinalIgnoreCase)||n.EndsWith("At",StringComparison.OrdinalIgnoreCase)
            ||n.EndsWith("Until",StringComparison.OrdinalIgnoreCase)||n.EndsWith("Since",StringComparison.OrdinalIgnoreCase)
            ||n=="lastProgressTime"||n=="moveStartTime"||n=="lastHostileDamage"||n=="lastWarningTime";
    }
    private object Restore(SaveValue value,Type expected,object existing)
    {
        if(value==null||value.kind=="skip")return existing;
        if(value.kind=="null")return null;
        if(value.kind=="ref")return Resolve(value.text);
        Type type=Type.GetType(value.type,true);
        if(!expected.IsAssignableFrom(type))throw new InvalidOperationException("Несовместимый тип "+type.FullName);
        if(value.kind=="scalar")return type.IsEnum?Enum.Parse(type,value.text):Convert.ChangeType(value.text,type,CultureInfo.InvariantCulture);
        if(value.kind=="json")
        {
            if(!type.IsValueType||type.Namespace==null||!type.Namespace.StartsWith("UnityEngine"))throw new InvalidOperationException("Недопустимый тип JSON");
            return JsonUtility.FromJson(value.text,type);
        }
        if(value.kind=="list")
        {
            Type element=type.IsArray?type.GetElementType():type.GetGenericArguments()[0];
            IList list=type.IsArray?(IList)Array.CreateInstance(element,value.items.Count):(IList)(existing??Activator.CreateInstance(type));
            if(!type.IsArray)list.Clear();
            for(int i=0;i<value.items.Count;i++)
            {var item=Restore(value.items[i],element,null);if(type.IsArray)list[i]=item;else list.Add(item);}
            return list;
        }
        if(value.kind=="dictionary")
        {
            var dictionary=(IDictionary)(existing??Activator.CreateInstance(type));dictionary.Clear();var arguments=type.GetGenericArguments();
            for(int i=0;i<value.items.Count;i+=2)
            {var key=Restore(value.items[i],arguments[0],null);if(key!=null)dictionary[key]=Restore(value.items[i+1],arguments[1],null);}
            return dictionary;
        }
        if(value.kind!="object"||!GameType(type))throw new InvalidOperationException("Недопустимый тип данных сохранения");
        // Некоторые игровые классы требуют аргументы конструктора. Восстанавливаем их поля без запуска игрового кода.
#pragma warning disable SYSLIB0050
        object target=existing??System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
#pragma warning restore SYSLIB0050
        RestoreFields(target,value.fields);return target;
    }
}
