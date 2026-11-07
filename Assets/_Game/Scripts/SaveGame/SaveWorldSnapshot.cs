using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Мир сохраняется отдельно от Canvas, сцена обеспечивает базовые объекты и настройки.</summary>
public sealed class SaveWorldSnapshot
{
    private readonly SaveAssetCatalog catalog;
    private readonly Dictionary<GameObject,string> sceneKeys=new Dictionary<GameObject,string>();
    private readonly Dictionary<string,GameObject> sceneObjects=new Dictionary<string,GameObject>();
    private readonly Dictionary<string,GameObject> entities=new Dictionary<string,GameObject>();
    private readonly SaveStateCodec codec;
    private static readonly HashSet<string> anchors=new HashSet<string>{"Unit","BuildingHealth","OreDeposit","IronVein","GoldVein","StoneQuarry","BerryBush","FarmField","WildlifeAnimal","FishingSpot","ConstructionSite","RoadStroke","RoadTile","ArmyGroup","LootCache","GettingResources","Resource","ResourceHealth","WoodResource"};
    private static readonly HashSet<string> controllers=new HashSet<string>{"ObjectPlacer","PlacementSystem","UnitManager","ResourcesManager","RaidManager","ManagerGO","UnitSpawningVillage"};
    public SaveWorldSnapshot(Scene scene,SaveAssetCatalog assets)
    {
        catalog=assets;codec=new SaveStateCodec{Reference=Reference,Resolve=Resolve};
        foreach(var root in scene.GetRootGameObjects())foreach(var node in root.GetComponentsInChildren<Transform>(true))
        {string key=ScenePath(node);sceneKeys[node.gameObject]=key;sceneObjects[key]=node.gameObject;}
        MarkRoots();
    }
    private static string ScenePath(Transform transform)
    {return transform.parent==null?transform.GetSiblingIndex().ToString():ScenePath(transform.parent)+"/"+transform.GetSiblingIndex();}
    public static string NodePath(Transform node,Transform root)
    {return node==root?"":NodePath(node.parent,root)+"/"+node.GetSiblingIndex();}
    public static Transform Node(Transform root,string path)
    {
        var node=root;
        foreach(string part in path.Split('/'))if(part.Length>0)
        {int index=int.Parse(part);if(index>=node.childCount)return null;node=node.GetChild(index);}
        return node;
    }
    private bool Anchor(GameObject obj)
    {
        if(obj.GetComponentInParent<Canvas>()!=null)return false;
        var road=obj.GetComponent<RoadStroke>();if(road!=null)return road.IsBuilt;
        foreach(var script in obj.GetComponents<MonoBehaviour>())if(script!=null&&anchors.Contains(script.GetType().Name))return true;
        return obj.GetComponent<SaveEntity>()!=null;
    }
    public void MarkRoots()
    {
        var candidates=UnityEngine.Object.FindObjectsOfType<Transform>(true).Where(t=>t.gameObject.scene==SceneManager.GetActiveScene()&&Anchor(t.gameObject)).ToList();
        foreach(var candidate in candidates)
        {
            bool child=false;
            for(var p=candidate.parent;p!=null;p=p.parent)if(candidates.Contains(p)){child=true;break;}
            if(child)continue;
            var marker=candidate.GetComponent<SaveEntity>();if(marker==null)marker=candidate.gameObject.AddComponent<SaveEntity>();
            if(string.IsNullOrEmpty(marker.id))
            {
                if(sceneKeys.TryGetValue(candidate.gameObject,out string key)){marker.sceneKey=key;marker.id="scene:"+key;}
                else marker.id="spawn:"+Guid.NewGuid().ToString("N");
            }
            entities[marker.id]=candidate.gameObject;
        }
    }
    private string Reference(UnityEngine.Object value)
    {
        if(value==null)return null;
        string asset=catalog.Key(value);if(asset!=null)return "asset:"+asset;
        var obj=value as GameObject;var component=value as Component;if(component!=null)obj=component.gameObject;
        if(obj==null)return null;
        string owner;
        var marker=obj.GetComponentInParent<SaveEntity>(true);
        if(marker!=null)owner="entity:"+marker.id+"|"+NodePath(obj.transform,marker.transform);
        else if(sceneKeys.TryGetValue(obj,out var key))owner="scene:"+key;
        else return null;
        if(component!=null)
        {
            var siblings=obj.GetComponents(component.GetType());int index=Array.IndexOf(siblings,component);
            owner+="|component|"+component.GetType().AssemblyQualifiedName+"|"+index;
        }
        return owner;
    }
    private UnityEngine.Object Resolve(string key)
    {
        if(key==null)return null;
        if(key.StartsWith("asset:"))return catalog.Resolve(key.Substring(6));
        int c=key.IndexOf("|component|",StringComparison.Ordinal);string owner=c>=0?key.Substring(0,c):key;
        GameObject obj=null;
        if(owner.StartsWith("scene:"))sceneObjects.TryGetValue(owner.Substring(6),out obj);
        else if(owner.StartsWith("entity:"))
        {
            string[] pair=owner.Substring(7).Split('|');
            if(entities.TryGetValue(pair[0],out var root)&&root!=null)obj=Node(root.transform,pair[1])?.gameObject;
        }
        if(c<0||obj==null)return obj;
        string[] detail=key.Substring(c+11).Split('|');var type=Type.GetType(detail[0],true);
        var components=obj.GetComponents(type);int index=int.Parse(detail[1]);return index<components.Length?components[index]:null;
    }
    public SaveWorld Capture()
    {
        MarkRoots();var world=new SaveWorld{scene=SceneManager.GetActiveScene().path,utc=DateTime.UtcNow.ToString("O"),time=Time.time,timeScale=Time.timeScale};
        foreach(var pair in entities.ToArray())
        {
            var obj=pair.Value;if(obj==null)continue;var marker=obj.GetComponent<SaveEntity>();
            var data=new SaveEntityData{id=marker.id,sceneKey=marker.sceneKey,name=obj.name,position=obj.transform.position,rotation=obj.transform.rotation,scale=obj.transform.localScale,active=obj.activeSelf,parent=Reference(obj.transform.parent)};
            data.holding=FormationHold.TryGetPoint(obj,out data.holdPoint);
            if(data.holding){FormationHold.TryGetFacing(obj,out data.holdFacing);data.holdGroup=FormationHold.GetGroup(obj);}
            var road=obj.GetComponent<RoadStroke>();var site=obj.GetComponent<ConstructionSite>();
            if(road!=null)
            {data.kind="road";data.roadPoints=new List<Vector3>(road.Points);data.roadWidth=road.Width;data.roundStart=road.RoundStart;data.roundEnd=road.RoundEnd;world.entities.Add(data);continue;}
            if(site!=null)
            {
                data.kind="construction";
                data.constructionBuilding=Reference(Field<GameObject>(site,"building"));data.constructionVisual=Reference(Field<GameObject>(site,"visualPrefab"));data.constructionTime=Field<float>(site,"buildTime");data.constructionSize=Field<Vector2Int>(site,"size");
            }
            else if(string.IsNullOrEmpty(data.sceneKey))
            {
                var prefab=catalog.Prefab(obj);
                if(prefab!=null)data.prefabKey=catalog.Key(prefab);
                else if(obj.GetComponent<ArmyGroup>()!=null)data.kind="army";
                else if(obj.GetComponent<LootCache>()!=null)data.kind="loot";
                else throw new InvalidOperationException("Нет префаба для "+obj.name+". Назначьте SaveEntity.Prefab и обновите каталог сохранений.");
            }
            foreach(var node in obj.GetComponentsInChildren<Transform>(true))
            {
                if(node!=obj.transform&&node.GetComponentInParent<Canvas>()!=null)continue;
                var state=new SaveNodeData{path=NodePath(node,obj.transform),name=node.name,active=node.gameObject.activeSelf,position=node.localPosition,rotation=node.localRotation,scale=node.localScale};
                foreach(var item in node.GetComponents<Component>())
                {
                    if(item is Renderer renderer){state.enabled.Add(Reference(item));state.enabledValues.Add(renderer.enabled);}
                    else if(item is Collider collider){state.enabled.Add(Reference(item));state.enabledValues.Add(collider.enabled);}
                    else if(item is Animator animator){state.enabled.Add(Reference(item));state.enabledValues.Add(animator.enabled);}
                    else if(item is NavMeshObstacle obstacle){state.enabled.Add(Reference(item));state.enabledValues.Add(obstacle.enabled);}
                }
                var body=node.GetComponent<Rigidbody>();
                if(body!=null){state.hasBody=true;state.kinematic=body.isKinematic;state.gravity=body.useGravity;state.velocity=body.velocity;state.angularVelocity=body.angularVelocity;}
                var animation=node.GetComponent<Animator>();
                if(animation!=null&&animation.runtimeAnimatorController!=null)foreach(var parameter in animation.parameters)
                {
                    if(parameter.type==AnimatorControllerParameterType.Trigger)continue;
                    state.animatorNames.Add(parameter.name);state.animatorTypes.Add((int)parameter.type);
                    state.animatorValues.Add(parameter.type==AnimatorControllerParameterType.Bool?(animation.GetBool(parameter.name)?1:0):parameter.type==AnimatorControllerParameterType.Int?animation.GetInteger(parameter.name):animation.GetFloat(parameter.name));
                }
                var agent=node.GetComponent<NavMeshAgent>();
                if(agent!=null){state.hasAgent=true;state.agentEnabled=agent.enabled;state.speed=agent.speed;state.stoppingDistance=agent.stoppingDistance;state.areaMask=agent.areaMask;state.hasGoal=agent.enabled&&agent.isOnNavMesh&&agent.hasPath;if(state.hasGoal)state.goal=agent.destination;}
                data.nodes.Add(state);
                foreach(var script in node.GetComponents<MonoBehaviour>())if(Savable(script))data.components.Add(CaptureComponent(script,marker.id,NodePath(node,obj.transform)));
            }
            world.entities.Add(data);
        }
        foreach(var script in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            if(script!=null&&controllers.Contains(script.GetType().Name)&&script.gameObject.scene==SceneManager.GetActiveScene())world.controllers.Add(CaptureComponent(script,"",Reference(script.gameObject)));
        return world;
    }
    private static bool Savable(MonoBehaviour script)=>script!=null&&SaveStateCodec.GameType(script.GetType())&&!(script is SaveEntity)&&!(script is SaveGameManager)&&script.GetComponentInParent<Canvas>()==null;
    private SaveComponentData CaptureComponent(MonoBehaviour script,string owner,string path)
    {return new SaveComponentData{owner=owner,path=path,type=script.GetType().AssemblyQualifiedName,index=Array.IndexOf(script.GetComponents(script.GetType()),script),enabled=script.enabled,fields=codec.CaptureFields(script)};}
    public static T Field<T>(object obj,string name)
    {foreach(var field in SaveStateCodec.Fields(obj.GetType()))if(field.Name==name)return (T)field.GetValue(obj);throw new InvalidOperationException("Нет поля "+name);}
    public void Validate(SaveWorld world)
    {
        if(world==null||string.IsNullOrEmpty(world.scene)||world.entities==null)throw new InvalidOperationException("Пустое сохранение");
        if(!Application.CanStreamedLevelBeLoaded(world.scene))throw new InvalidOperationException("Сцена отсутствует в Build Settings: "+world.scene);
        var ids=new HashSet<string>();
        foreach(var entity in world.entities)
        {
            if(string.IsNullOrEmpty(entity.id)||!ids.Add(entity.id))throw new InvalidOperationException("Повтор идентификатора объекта");
            if(!string.IsNullOrEmpty(entity.prefabKey)&&!(catalog.Resolve(entity.prefabKey) is GameObject))throw new InvalidOperationException("Не найден префаб "+entity.name);
            foreach(var component in entity.components)ValidateComponent(component);
        }
        foreach(var component in world.controllers)ValidateComponent(component);
    }
    private static void ValidateComponent(SaveComponentData data)
    {var type=Type.GetType(data.type);if(type==null||!typeof(MonoBehaviour).IsAssignableFrom(type)||!SaveStateCodec.GameType(type))throw new InvalidOperationException("Неизвестный игровой компонент "+data.type);}
    // Первый этап вызывается после перезагрузки сцены, до восстановления связей.
    public void CreateEntities(SaveWorld world)
    {
        var savedIds=new HashSet<string>(world.entities.Select(e=>e.id));
        foreach(var pair in entities.ToArray())if(pair.Value!=null&&!savedIds.Contains(pair.Key)){pair.Value.SetActive(false);UnityEngine.Object.Destroy(pair.Value);entities.Remove(pair.Key);}
        foreach(var data in world.entities)
        {
            if(entities.TryGetValue(data.id,out var existing)&&existing!=null){EnsureComponents(existing,data);if(data.active)existing.SetActive(true);continue;}
            if(!string.IsNullOrEmpty(data.sceneKey)&&sceneObjects.TryGetValue(data.sceneKey,out existing)&&existing!=null){entities[data.id]=existing;EnsureComponents(existing,data);if(data.active)existing.SetActive(true);continue;}
            if(data.kind=="construction")continue;
            GameObject obj;
            if(!string.IsNullOrEmpty(data.prefabKey))obj=UnityEngine.Object.Instantiate((GameObject)catalog.Resolve(data.prefabKey),data.position,data.rotation);
            else obj=data.kind=="loot"?GameObject.CreatePrimitive(PrimitiveType.Cube):new GameObject(data.name);
            obj.name=data.name;
            var marker=obj.GetComponent<SaveEntity>()??obj.AddComponent<SaveEntity>();marker.id=data.id;marker.sceneKey=data.sceneKey;
            entities[data.id]=obj;
            if(data.active)obj.SetActive(true);
            if(data.kind=="road")
            {
                var builder=RoadBuilder.Instance;if(builder==null)throw new InvalidOperationException("Нет RoadBuilder для восстановления дорог");
                builder.RestoreSavedRoad(obj.AddComponent<RoadStroke>(),data.roadPoints,data.roadWidth,data.roundStart,data.roundEnd);
            }
            EnsureComponents(obj,data);
        }
        foreach(var data in world.entities.Where(e=>e.kind=="construction"))
        {
            if(entities.TryGetValue(data.id,out var existing)&&existing!=null)continue;
            var building=Resolve(data.constructionBuilding) as GameObject;
            if(building==null)throw new InvalidOperationException("Потеряно здание стройплощадки");
            var site=ConstructionSite.Begin(building,data.constructionTime,data.constructionSize,Resolve(data.constructionVisual) as GameObject);
            var marker=site.gameObject.AddComponent<SaveEntity>();marker.id=data.id;marker.sceneKey=data.sceneKey;entities[data.id]=site.gameObject;
            EnsureComponents(site.gameObject,data);
        }
    }
    private static void EnsureComponents(GameObject root,SaveEntityData data)
    {
        foreach(var state in data.nodes.OrderBy(n=>n.path.Count(c=>c=='/')))
        {
            if(Node(root.transform,state.path)!=null)continue;
            int split=state.path.LastIndexOf('/');var parent=Node(root.transform,state.path.Substring(0,split));
            if(parent==null)throw new InvalidOperationException("Не найден родитель узла "+state.path);
            int index=int.Parse(state.path.Substring(split+1));
            while(parent.childCount<=index){var child=new GameObject(state.name);child.transform.SetParent(parent,false);}
        }
        foreach(var state in data.nodes)
        {
            var node=Node(root.transform,state.path);
            if(node==null)continue;
            if(state.hasAgent&&node.GetComponent<NavMeshAgent>()==null)node.gameObject.AddComponent<NavMeshAgent>();
            if(state.hasBody&&node.GetComponent<Rigidbody>()==null)node.gameObject.AddComponent<Rigidbody>();
        }
        foreach(var component in data.components)
        {
            var node=Node(root.transform,component.path);if(node==null)continue;
            Type type=Type.GetType(component.type,true);
            while(node.GetComponents(type).Length<=component.index)node.gameObject.AddComponent(type);
        }
    }
    public void Restore(SaveWorld world)
    {
        codec.timeOffset=Time.time-world.time;
        var formationGroups=new Dictionary<int,int>();
        foreach(var data in world.entities)
        {
            var obj=entities[data.id];var parent=Resolve(data.parent) as Transform;
            obj.transform.SetParent(parent,true);obj.transform.SetPositionAndRotation(data.position,data.rotation);obj.transform.localScale=data.scale;
            if(data.kind=="road")continue;
            foreach(var state in data.nodes)
            {
                var node=Node(obj.transform,state.path);if(node==null)continue;
                var agent=node.GetComponent<NavMeshAgent>();if(agent!=null)agent.enabled=false;
                node.localPosition=state.position;node.localRotation=state.rotation;node.localScale=state.scale;
                node.gameObject.SetActive(state.active);
            }
        }
        foreach(var data in world.entities)foreach(var state in data.components)RestoreComponent(entities[data.id],state);
        foreach(var state in world.controllers)
        {var obj=Resolve(state.path) as GameObject;if(obj!=null)RestoreComponent(obj,state,true);}
        foreach(var data in world.entities)
        {
            var obj=entities[data.id];obj.SetActive(data.active);
            foreach(var state in data.nodes)
            {
                var node=Node(obj.transform,state.path);if(node==null)continue;
                for(int i=0;i<state.enabled.Count;i++)
                {var value=Resolve(state.enabled[i]);if(value is Renderer renderer)renderer.enabled=state.enabledValues[i];else if(value is Collider collider)collider.enabled=state.enabledValues[i];else if(value is Animator animator)animator.enabled=state.enabledValues[i];else if(value is NavMeshObstacle obstacle)obstacle.enabled=state.enabledValues[i];}
                var body=node.GetComponent<Rigidbody>();
                if(body!=null&&state.hasBody){body.isKinematic=state.kinematic;body.useGravity=state.gravity;if(!body.isKinematic){body.velocity=state.velocity;body.angularVelocity=state.angularVelocity;}}
                var animation=node.GetComponent<Animator>();
                if(animation!=null&&animation.runtimeAnimatorController!=null)for(int i=0;i<state.animatorNames.Count;i++)
                {
                    var type=(AnimatorControllerParameterType)state.animatorTypes[i];
                    if(type==AnimatorControllerParameterType.Bool)animation.SetBool(state.animatorNames[i],state.animatorValues[i]!=0);
                    else if(type==AnimatorControllerParameterType.Int)animation.SetInteger(state.animatorNames[i],(int)state.animatorValues[i]);
                    else animation.SetFloat(state.animatorNames[i],state.animatorValues[i]);
                }
                var agent=node.GetComponent<NavMeshAgent>();
                if(agent==null||!state.hasAgent)continue;
                agent.speed=state.speed;agent.stoppingDistance=state.stoppingDistance;agent.areaMask=state.areaMask;agent.enabled=state.agentEnabled;
                if(agent.enabled&&agent.isOnNavMesh){agent.Warp(node.position);if(state.hasGoal)agent.SetDestination(state.goal);}
            }
            var unit=obj.GetComponent<Unit>();if(unit!=null)unit.SetInitialHealthRatio(unit.unitMaxHealth>0?unit.unitHealth/unit.unitMaxHealth:1);
            obj.GetComponent<FactionMember>()?.RefreshControlRegistration();
            obj.GetComponent<HospitalPatient>()?.RestoreSavedVisibility();
            obj.GetComponent<WallStation>()?.RestoreSavedHooks();
            obj.GetComponent<WallGateUpgrade>()?.RestoreSavedHooks();
            if(data.holding)
            {
                if(!formationGroups.TryGetValue(data.holdGroup,out int group)){group=FormationHold.BeginGroup();formationGroups[data.holdGroup]=group;}
                FormationHold.SetPoint(obj,data.holdPoint,data.holdFacing,group);
            }
        }
        CarrierTask.RebuildSavedReservations();
        PlacementSystem.Instance?.RestoreSavedPlacementHooks();
        foreach(var village in UnityEngine.Object.FindObjectsOfType<UnitSpawningVillage>())village.RestoreSavedSpawning();
        Physics.SyncTransforms();RoadNavigation.Changed();
        _Game.Scripts.UI.ResourcesManager.Instance?.UpdateUI();
    }
    private void RestoreComponent(GameObject root,SaveComponentData data,bool controller=false)
    {
        var node=controller?root.transform:Node(root.transform,data.path);if(node==null)throw new InvalidOperationException("Изменилась структура префаба "+root.name+data.path);
        var list=node.GetComponents(Type.GetType(data.type,true));if(data.index>=list.Length)throw new InvalidOperationException("Потерян компонент "+data.type);
        var component=(MonoBehaviour)list[data.index];codec.RestoreFields(component,data.fields);component.enabled=data.enabled;
    }
}
