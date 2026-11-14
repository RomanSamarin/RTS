using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public class SaveEnvelope
{public int version=1;public string checksum,payload;}
[Serializable] public class SaveWorld
{
    public string scene,utc;public float time,timeScale;
    public List<SaveEntityData> entities=new List<SaveEntityData>();
    public List<SaveComponentData> controllers=new List<SaveComponentData>();
    public List<SaveValue> values=new List<SaveValue>();
}
[Serializable] public class SaveEntityData
{
    public string id,sceneKey,sceneIdentity,prefabKey,kind,name,parent;
    public Vector3 position,scale;public Quaternion rotation;public bool active;
    public List<SaveNodeData> nodes=new List<SaveNodeData>();
    public List<SaveComponentData> components=new List<SaveComponentData>();
    public List<Vector3> roadPoints;public float roadWidth;public bool roundStart,roundEnd;
    public string constructionBuilding,constructionVisual;public float constructionTime;public Vector2Int constructionSize;
    public bool holding;public Vector3 holdPoint,holdFacing;public int holdGroup;
}
[Serializable] public class SaveNodeData
{
    public string path,name;public bool active;public Vector3 position,scale;public Quaternion rotation;
    public List<string> enabled=new List<string>();public List<bool> enabledValues=new List<bool>();
    public bool agentEnabled,hasAgent,hasGoal;public Vector3 goal;public float speed,stoppingDistance;public int areaMask;
    public bool hasBody,kinematic,gravity;public Vector3 velocity,angularVelocity;
    public List<string> animatorNames=new List<string>();public List<int> animatorTypes=new List<int>();public List<float> animatorValues=new List<float>();
}
[Serializable] public class SaveComponentData
{public string owner,path,type;public int index;public bool enabled;public List<SaveField> fields=new List<SaveField>();}
[Serializable] public class SaveField {public string name;public SaveValue value;}
[Serializable] public class SaveValue
{
    public string kind,type,text;
    [NonSerialized] public List<SaveValue> items;
    [NonSerialized] public List<SaveField> fields;
    public List<int> childIds;
    public List<string> fieldNames;
    public List<int> fieldIds;
}

