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
    private readonly Dictionary<GameObject,string> sceneIdentities=new Dictionary<GameObject,string>();
    private readonly Dictionary<string,GameObject> namedSceneObjects=new Dictionary<string,GameObject>();
    private readonly Dictionary<string,GameObject> entities=new Dictionary<string,GameObject>();
    private bool sceneLayoutChanged;
    private readonly SaveStateCodec codec;
    private static readonly HashSet<string> anchors=new HashSet<string>{"Unit","BuildingHealth","OreDeposit","IronVein","GoldVein","StoneQuarry","BerryBush","FarmField","WildlifeAnimal","FishingSpot","ConstructionSite","RoadStroke","RoadTile","ArmyGroup","LootCache","ShipWreck","GettingResources","Resource","ResourceHealth","WoodResource"};
    private static readonly HashSet<string> controllers=new HashSet<string>{"ObjectPlacer","PlacementSystem","UnitManager","ResourcesManager","RaidManager","ManagerGO","UnitSpawningVillage"};
    public SaveWorldSnapshot(Scene scene,SaveAssetCatalog assets)
    {
        catalog=assets;codec=new SaveStateCodec{Reference=Reference,Resolve=Resolve};
        IndexScene(scene.GetRootGameObjects().Select(root => root.transform), "");
        MarkRoots();
    }
    private void IndexScene(IEnumerable<Transform> siblings, string parent)
    {
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in siblings)
        {
            occurrences.TryGetValue(node.name, out int occurrence);
            occurrences[node.name] = occurrence + 1;
            string identity = parent + "/" + Uri.EscapeDataString(node.name) + ":" + occurrence;
            string key = ScenePath(node);
            sceneKeys[node.gameObject] = key;
            sceneObjects[key] = node.gameObject;
            sceneIdentities[node.gameObject] = identity;
            namedSceneObjects[identity] = node.gameObject;
            IndexScene(node.Cast<Transform>(), identity);
        }
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
        if(obj.GetComponentInParent<Canvas>(true)!=null)return false;
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
        else if(sceneIdentities.TryGetValue(obj,out var identity))owner="sceneName:"+identity;
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
        if(owner.StartsWith("sceneName:",StringComparison.Ordinal))namedSceneObjects.TryGetValue(owner.Substring(10),out obj);
        else if(owner.StartsWith("scene:",StringComparison.Ordinal)&&!sceneLayoutChanged)sceneObjects.TryGetValue(owner.Substring(6),out obj);
        else if(owner.StartsWith("entity:"))
        {
            string[] pair=owner.Substring(7).Split('|');
            if(entities.TryGetValue(pair[0],out var root)&&root!=null)obj=Node(root.transform,pair[1])?.gameObject;
        }
        if(c<0||obj==null)return obj;
        string[] detail=key.Substring(c+11).Split('|');var type=Type.GetType(detail[0],true);
        var components=obj.GetComponents(type);int index=int.Parse(detail[1]);return index>=0&&index<components.Length?components[index]:null;
    }
    public SaveWorld Capture()
    {
        entities.Clear();MarkRoots();var world=new SaveWorld{scene=SceneManager.GetActiveScene().path,utc=DateTime.UtcNow.ToString("O"),time=Time.time,timeScale=Time.timeScale};
        foreach(var pair in entities.ToArray())
        {
            var obj=pair.Value;if(obj==null)continue;var marker=obj.GetComponent<SaveEntity>();
            var data=new SaveEntityData{id=marker.id,sceneKey=marker.sceneKey,name=obj.name,position=obj.transform.position,rotation=obj.transform.rotation,scale=obj.transform.localScale,active=obj.activeSelf,parent=Reference(obj.transform.parent)};
            if (sceneIdentities.TryGetValue(obj, out var identity)) data.sceneIdentity = identity;
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
                else if(obj.GetComponent<ShipWreck>()!=null)data.kind="shipWreck";
                else if(obj.GetComponent<SiegeLadderCart>()!=null)data.kind="siegeLadderCart";
                else if(obj.GetComponent<BatteringRam>()!=null)data.kind="batteringRam";
                else if(obj.GetComponent<ShipHull>() is ShipHull hull)data.kind="ship:"+((int)hull.type);
                else throw new InvalidOperationException("Нет префаба для "+obj.name+". Назначьте SaveEntity.Prefab и обновите каталог сохранений.");
            }
            foreach(var node in obj.GetComponentsInChildren<Transform>(true))
            {
                if(node!=obj.transform&&node.GetComponentInParent<Canvas>(true)!=null)continue;
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
    private static bool Savable(MonoBehaviour script)=>script!=null&&SaveStateCodec.GameType(script.GetType())&&!(script is SaveEntity)&&!(script is SaveGameManager)&&!(script is HealthTracker)&&script.GetComponentInParent<Canvas>(true)==null;
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
        var original = entities.Values.Where(obj => obj != null).ToArray();
        var authored = new HashSet<GameObject>(original.Where(sceneKeys.ContainsKey));
        var savedSceneEntities = world.entities.Where(data => !string.IsNullOrEmpty(data.sceneKey)).ToArray();
        sceneLayoutChanged = savedSceneEntities.Any(data =>
        {
            var candidate = ExactSceneMatch(data, authored);
            return candidate == null || sceneKeys[candidate] != data.sceneKey;
        });
        codec.PreserveLegacySceneReferences = sceneLayoutChanged;

        var matched = new Dictionary<string, GameObject>();
        var used = new HashSet<GameObject>();
        var byName = authored.ToLookup(obj => obj.name);
        foreach (var data in savedSceneEntities)
        {
            var candidate = ExactSceneMatch(data, authored);
            // Legacy sibling indices can point at a different copy of a repeated object.
            if (candidate == null || (sceneLayoutChanged && string.IsNullOrEmpty(data.sceneIdentity) &&
                                      byName[data.name].Count() > 1))
            {
                var sameName = byName[data.name];
                candidate = sameName.Where(obj => !used.Contains(obj))
                    .OrderBy(obj => (obj.transform.position - data.position).sqrMagnitude).FirstOrDefault();
                if (candidate != null && sameName.Count() > 1 &&
                    (candidate.transform.position - data.position).sqrMagnitude > 4f)
                    candidate = null;
            }
            if (candidate != null && used.Add(candidate)) matched.Add(data.id, candidate);
        }

        foreach (var obj in original)
        {
            if (used.Contains(obj) || (authored.Contains(obj) && (sceneLayoutChanged || obj.GetComponent<TownHall>() != null))) continue;
            obj.SetActive(false);
            UnityEngine.Object.Destroy(obj);
        }
        if (matched.Count < savedSceneEntities.Length)
            Debug.LogWarning($"[Сохранения] Сцена изменилась. Для {savedSceneEntities.Length - matched.Count} объектов использовано текущее состояние карты.");
        entities.Clear();

        foreach(var data in world.entities)
        {
            if (!string.IsNullOrEmpty(data.sceneKey))
            {
                if (matched.TryGetValue(data.id, out var existing))
                {
                    entities[data.id] = existing;
                    if (!sceneLayoutChanged) EnsureComponents(existing, data);
                    if (data.active) existing.SetActive(true);
                }
                continue;
            }
            if(data.kind=="construction")continue;
            GameObject obj;
            if(!string.IsNullOrEmpty(data.prefabKey))obj=UnityEngine.Object.Instantiate((GameObject)catalog.Resolve(data.prefabKey),data.position,data.rotation);
            else if(data.kind=="siegeLadderCart")obj=SiegeLadderCartFactory.CreatePlaceholder(data.position,data.rotation);
            else if(data.kind=="batteringRam")obj=BatteringRam.CreatePlaceholder(data.position,data.rotation);
            else if(data.kind!=null&&data.kind.StartsWith("ship:"))obj=ShipFactory.CreatePlaceholder((ShipType)int.Parse(data.kind.Substring(5)),data.position,data.rotation);
            else obj=data.kind=="loot"||data.kind=="shipWreck"?GameObject.CreatePrimitive(PrimitiveType.Cube):new GameObject(data.name);
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
        foreach(var data in world.entities.Where(e=>e.kind=="construction"&&string.IsNullOrEmpty(e.sceneKey)))
        {
            if(entities.TryGetValue(data.id,out var existing)&&existing!=null)continue;
            var building=Resolve(data.constructionBuilding) as GameObject;
            if(building==null)throw new InvalidOperationException("Потеряно здание стройплощадки");
            var site=ConstructionSite.Begin(building,data.constructionTime,data.constructionSize,Resolve(data.constructionVisual) as GameObject);
            var marker=site.gameObject.AddComponent<SaveEntity>();marker.id=data.id;marker.sceneKey=data.sceneKey;entities[data.id]=site.gameObject;
            EnsureComponents(site.gameObject,data);
        }
    }

    private GameObject ExactSceneMatch(SaveEntityData data, HashSet<GameObject> authored)
    {
        GameObject candidate;
        if (!string.IsNullOrEmpty(data.sceneIdentity))
            namedSceneObjects.TryGetValue(data.sceneIdentity, out candidate);
        else
            sceneObjects.TryGetValue(data.sceneKey, out candidate);
        return candidate != null && candidate.name == data.name && authored.Contains(candidate) ? candidate : null;
    }

    private static void EnsureComponents(GameObject root,SaveEntityData data)
    {
        foreach(var state in data.nodes.OrderBy(n=>n.path.Count(c=>c=='/')))
        {
            if(Node(root.transform,state.path)!=null)continue;
            int split=state.path.LastIndexOf('/');var parent=split<0?root.transform:Node(root.transform,state.path.Substring(0,split));
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
            Type type=Type.GetType(component.type,true);
            if (type == typeof(HealthTracker)) continue;
            var node=Node(root.transform,component.path);if(node==null)continue;
            while (node.GetComponents(type).Length <= component.index)
                if (node.gameObject.AddComponent(type) == null)
                    throw new InvalidOperationException("Не удалось восстановить компонент " + type.Name);
        }
    }
    public void Restore(SaveWorld world)
    {
        codec.timeOffset=Time.time-world.time;
        var records = world.entities.Where(data => entities.ContainsKey(data.id)).ToArray();
        var formationGroups=new Dictionary<int,int>();
        foreach(var data in records)
        {
            var obj=entities[data.id];
            bool movedInScene = sceneLayoutChanged && !string.IsNullOrEmpty(data.sceneKey);
            if (!movedInScene) obj.transform.SetParent(Resolve(data.parent) as Transform,true);
            obj.transform.SetPositionAndRotation(data.position,data.rotation);
            obj.transform.localScale=data.scale;
            if(data.kind=="road")continue;
            foreach(var state in data.nodes)
            {
                var node=RestoreNode(obj,state,movedInScene);if(node==null)continue;
                var agent=node.GetComponent<NavMeshAgent>();if(agent!=null)agent.enabled=false;
                if (!movedInScene || node != obj.transform)
                {node.localPosition=state.position;node.localRotation=state.rotation;node.localScale=state.scale;}
                node.gameObject.SetActive(state.active);
            }
        }
        foreach (var data in records)
        {
            var obj = entities[data.id];
            bool movedInScene = sceneLayoutChanged && !string.IsNullOrEmpty(data.sceneKey);
            foreach (var state in data.components)
            {
                var type = Type.GetType(state.type, true);
                if (type == typeof(HealthTracker)) continue;
                var node = Node(obj.transform, state.path);
                if (movedInScene && (node == null || !data.nodes.Any(saved => saved.path == state.path && saved.name == node.name))) continue;
                RestoreComponent(node, state, type, movedInScene);
            }
        }
        foreach (var state in world.controllers) RestoreController(state);
        foreach(var data in records)
        {
            var obj=entities[data.id];obj.SetActive(data.active);
            foreach (var ladder in obj.GetComponentsInChildren<SiegeLadderSite>(true))
                ladder.RestoreSavedVisuals();
            bool movedInScene = sceneLayoutChanged && !string.IsNullOrEmpty(data.sceneKey);
            foreach(var state in data.nodes)
            {
                var node=RestoreNode(obj,state,movedInScene);if(node==null)continue;
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
            obj.GetComponent<TroopCombat>()?.RefreshSpearShield();
            obj.GetComponent<TownHall>()?.RestoreAppearance();
            obj.GetComponent<CarrierTask>()?.RestoreHandlingAnimation();
            obj.GetComponent<HospitalPatient>()?.RestoreSavedVisibility();
            obj.GetComponent<WallStation>()?.RestoreSavedHooks();
            obj.GetComponent<SiegeLadderInstaller>()?.RestoreSavedHooks();
            obj.GetComponent<SiegeLadderClimber>()?.RestoreSavedHooks();
            obj.GetComponent<RamCrewMember>()?.RestoreSavedHooks();
            obj.GetComponent<WallGateUpgrade>()?.RestoreSavedHooks();
            obj.GetComponent<ShipNavigation>()?.RestoreSavedHooks();
            obj.GetComponent<ShipCrewMember>()?.RestoreSavedHooks();
            obj.GetComponent<ShipTradeRoute>()?.RestoreSavedHooks();
            obj.GetComponent<ShipPassenger>()?.RestoreSavedHooks();
            obj.GetComponent<Port>()?.RestoreSavedRepairHooks();
            if(data.holding)
            {
                if(!formationGroups.TryGetValue(data.holdGroup,out int group)){group=FormationHold.BeginGroup();formationGroups[data.holdGroup]=group;}
                FormationHold.SetPoint(obj,data.holdPoint,data.holdFacing,group);
            }
        }
        RebuildUnitRegistries();
        HousesScript.RefreshLocalHousing();
        CarrierTask.RebuildSavedReservations();
        PlacementSystem.Instance?.RestoreSavedPlacementHooks();
        foreach(var village in UnityEngine.Object.FindObjectsOfType<UnitSpawningVillage>())village.RestoreSavedSpawning();
        Physics.SyncTransforms();RoadNavigation.Changed();
        _Game.Scripts.UI.ResourcesManager.Instance?.UpdateUI();
    }

    private static void RebuildUnitRegistries()
    {
        var orders = ManagerGO.Instance;
        var population = UnitManager.Instance;
        if (orders != null) orders.allUnitsList.Clear();
        if (population != null)
        {
            population.allUnits.Clear();
            population.UnitAmount = 0;
            population.FoodUnitEat = 0;
        }
        foreach (var member in FactionMember.All.ToArray())
            if (member != null) member.RefreshControlRegistration();
        if (orders != null)
            orders.unitsSelected.RemoveAll(unit => unit == null || !orders.allUnitsList.Contains(unit));
    }
    private static Transform RestoreNode(GameObject root, SaveNodeData data, bool verifyName)
    {
        var node = Node(root.transform, data.path);
        return node != null && (!verifyName || node.name == data.name) ? node : null;
    }

    private void RestoreController(SaveComponentData data)
    {
        var type = Type.GetType(data.type, true);
        var owner = Resolve(data.path) as GameObject;
        if (owner != null && data.index >= 0 && data.index < owner.GetComponents(type).Length)
        {
            RestoreComponent(owner.transform, data, type);
            return;
        }

        var matches = sceneKeys.Keys.Where(obj => obj != null)
            .SelectMany(obj => obj.GetComponents(type)).Take(2).ToArray();
        if (matches.Length == 1)
        {
            var controller = (MonoBehaviour)matches[0];
            codec.RestoreFields(controller, data.fields);
        }
        else
            Debug.LogWarning("[Сохранения] Контроллер из старого сохранения не найден однозначно: " + type.Name);
    }

    private void RestoreComponent(Transform node, SaveComponentData data, Type type, bool optional = false)
    {
        var list = node != null ? node.GetComponents(type) : Array.Empty<Component>();
        if (data.index < 0 || data.index >= list.Length)
        {
            if (optional) return;
            throw new InvalidOperationException("Потерян компонент " + data.type);
        }
        var component = (MonoBehaviour)list[data.index];
        codec.RestoreFields(component, data.fields);
        component.enabled = data.enabled;
    }
}
