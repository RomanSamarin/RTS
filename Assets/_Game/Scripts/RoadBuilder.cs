using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-80)]
public class RoadBuilder : MonoBehaviour
{
    public static RoadBuilder Instance { get; private set; }
    private static int endedFrame=-1;
    public static bool BlocksWorldInput => (Instance!=null&&Instance.placing)||endedFrame==Time.frameCount;
    public static int Revision { get; private set; }
    public LayerMask groundLayer=1<<6;
    [Min(.3f)] public float roadWidth=1.8f;
    [Min(.01f)] public float surfaceOffset=.04f;
    [Min(.2f)] public float sampleSpacing=.5f;
    [Min(.2f)] public float snapDistance=2f;
    [Min(5f)] public float maximumRoadLength=150f;
    [Range(1f,60f)] public float maximumSlope=35f;
    public Material roadMaterial;
    [Tooltip("Необязательный визуальный модуль дороги. Длина вдоль Z, ширина вдоль X. Без префаба строится обычный меш.")]
    public GameObject roadPrefab;
    public KeyCode buildKey=KeyCode.B;
    public string Status { get; private set; }="";
    // Совместимость со старыми кнопками и RoadTile. Новая прокладка не использует клетки.
    [HideInInspector] public float cellSize=2.5f;
    [HideInInspector] public GameObject straightPrefab,cornerPrefab,tJunctionPrefab,crossroadPrefab,endPrefab,isolatedPrefab;
    [HideInInspector] public RoadShape selectedShape;
    public Material RoadMaterial => roadMaterial!=null?roadMaterial:defaultMaterial;

    private readonly List<RoadStroke> roads=new List<RoadStroke>();
    private struct Segment { public RoadStroke road;public int index; }
    private const float BucketSize=8f;
    private readonly Dictionary<Vector2Int,List<Segment>> segments=new Dictionary<Vector2Int,List<Segment>>();
    private readonly List<Vector3> anchors=new List<Vector3>();
    private readonly List<Vector3> knots=new List<Vector3>();
    private readonly List<Vector3> draft=new List<Vector3>();
    private readonly List<Vector3> removalPreview=new List<Vector3>();
    private RoadStroke preview,hoverRoad;
    private GameObject pointer;
    private Material defaultMaterial,greenMaterial,redMaterial,snapMaterial;
    private bool placing,removing,valid,hasLastHover;
    private int startedFrame,lastRevision=-1;
    private float bend,nextPreviewTime,nextTerrainRefresh;
    private Vector3 lastHover,startDirection;
    private BuildingRoadAccess snapBuilding;
    private BuildingRoadAccess startBuilding;
    private RoadStroke snappedRoad;
    private bool startRoadJoin;
    private Terrain[] terrains;

    private void Awake()
    {
        if(Instance!=null&&Instance!=this){enabled=false;return;}
        Instance=this;
        var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color")??Shader.Find("Standard");
        defaultMaterial=new Material(shader){color=new Color(.43f,.32f,.20f)};
        greenMaterial=new Material(shader){color=new Color(.12f,.8f,.3f)};
        redMaterial=new Material(shader){color=new Color(.9f,.12f,.1f)};
        snapMaterial=new Material(shader){color=new Color(1f,.75f,.12f)};
        terrains=Terrain.activeTerrains;Revision++;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics(){Instance=null;Revision=0;endedFrame=-1;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfMissing()
    {if(Instance==null&&FindObjectOfType<RoadBuilder>()==null)new GameObject("RoadBuilder").AddComponent<RoadBuilder>();}

    public void StartNewRoad(){StartRoadPlacement();}
    public void StartRoadPlacement(){Begin(false);}
    public void StartAutomaticRoad(){Begin(false);}
    public void StartStraightRoad(){Begin(false);}
    public void StartCornerRoad(){Begin(false);bend=3f;}
    public void StartTJunctionRoad(){Begin(false);}
    public void StartCrossroadRoad(){Begin(false);}
    public void StartEndRoad(){Begin(false);}
    public void StartRemovingRoad(){Begin(true);}
    public void SelectShape(int shape){selectedShape=(RoadShape)Mathf.Clamp(shape,0,6);bend=shape==(int)RoadShape.Corner?3f:0f;InvalidatePreview();}
    private void Begin(bool erase)
    {
        if(!isActiveAndEnabled)return;
        if(SoldiersFormation.IsPlacing){Status="Сначала завершите расстановку войск";return;}
        PlacementSystem.Instance?.CancelPlacement();Barracks.CancelActiveRallySelection();
        BuildingSelectionManager.Instance?.DeselectCurrent();
        ClearDraft();placing=true;removing=erase;startedFrame=Time.frameCount;
        Status=erase?"Выберите дорогу для удаления":"ЛКМ — начало/конец. Shift+ЛКМ — промежуточная точка. R — изгиб";
        EnsurePreview();
    }
    public static void CancelActivePlacement(){if(Instance!=null)Instance.StopRoadPlacement();}
    public void StopRoadPlacement()
    {
        if(!placing)return;
        placing=false;endedFrame=Time.frameCount;ClearDraft();
        if(preview!=null)preview.gameObject.SetActive(false);if(pointer!=null)pointer.SetActive(false);
    }
    private void ClearDraft(){anchors.Clear();draft.Clear();startDirection=Vector3.zero;snapBuilding=null;startBuilding=null;snappedRoad=null;startRoadJoin=false;bend=0;InvalidatePreview();}
    private void InvalidatePreview(){hasLastHover=false;lastRevision=-1;nextPreviewTime=0;}
    private void EnsurePreview()
    {
        if(preview==null){var go=new GameObject("Road preview");go.layer=2;go.transform.SetParent(transform,false);preview=go.AddComponent<RoadStroke>();}
        if(pointer==null)
        {
            pointer=GameObject.CreatePrimitive(PrimitiveType.Sphere);pointer.name="Road snap point";pointer.layer=2;
            pointer.transform.SetParent(transform,false);pointer.transform.localScale=Vector3.one*.25f;
            pointer.GetComponent<Collider>().enabled=false;
        }
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if(buildKey!=KeyCode.None&&Input.GetKeyDown(buildKey))
        {
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            if(selected!=null&&(selected.GetComponentInParent<TMPro.TMP_InputField>()!=null||selected.GetComponentInParent<UnityEngine.UI.InputField>()!=null))return;
            if(placing)StopRoadPlacement();else Begin(false);
        }
    }
    private void LateUpdate()
    {
        if(SaveGameManager.IsLoading)return;
        if(!placing)return;
        if(SoldiersFormation.IsPlacing||Barracks.IsChoosingRallyPoint||(PlacementSystem.Instance!=null&&PlacementSystem.Instance.IsPlacing))
        {StopRoadPlacement();return;}
        if(Input.GetKeyDown(KeyCode.Escape)){StopRoadPlacement();return;}
        if(Input.GetMouseButtonDown(1))
        {if(anchors.Count>0){ClearDraft();preview.gameObject.SetActive(false);}else StopRoadPlacement();return;}
        if(Time.frameCount==startedFrame)return;
        if(Input.GetKeyDown(KeyCode.Delete)){removing=!removing;ClearDraft();}
        if(Input.GetKeyDown(KeyCode.Backspace)&&anchors.Count>0){anchors.RemoveAt(anchors.Count-1);InvalidatePreview();}
        if(Input.GetKeyDown(KeyCode.R)){bend+=Input.GetKey(KeyCode.LeftShift)?-2f:2f;if(bend>10f)bend=-10f;if(bend<-10f)bend=10f;InvalidatePreview();}
        Camera camera=Camera.main;
        if((EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())||camera==null||
            !Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition),out var hit,2000f,GroundMask,QueryTriggerInteraction.Ignore))
        {preview.gameObject.SetActive(false);pointer.SetActive(false);InvalidatePreview();return;}
        Vector3 point=hit.point;Vector3 endDirection;
        var previousRoad=snappedRoad;var previousBuilding=snapBuilding;
        bool snapped=Snap(ref point,out endDirection);
        if(previousRoad!=snappedRoad||previousBuilding!=snapBuilding)InvalidatePreview();
        pointer.SetActive(true);pointer.transform.position=point+Vector3.up*(surfaceOffset+.1f);
        pointer.GetComponent<MeshRenderer>().sharedMaterial=snapped?snapMaterial:greenMaterial;
        bool click=Input.GetMouseButtonDown(0)||Input.GetKeyDown(KeyCode.Return);
        if(removing){UpdateRemoval(point,click);return;}
        if(!hasLastHover||(point-lastHover).sqrMagnitude>.0025f||lastRevision!=Revision||click)
        {
            if(click||Time.unscaledTime>=nextPreviewTime)
            {
                RefreshDraft(point,endDirection);lastHover=point;hasLastHover=true;lastRevision=Revision;
                nextPreviewTime=Time.unscaledTime+1f/30f;
            }
        }
        if(!click)return;
        if(anchors.Count==0)
        {
            anchors.Add(point);startDirection=(snapBuilding!=null||snappedRoad!=null)?-endDirection:Vector3.zero;
            startRoadJoin=snappedRoad!=null;
            startBuilding=snapBuilding;
            snapBuilding=null;InvalidatePreview();return;
        }
        if(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))
        {
            if(anchors.Count>=32){Status="Не больше 32 контрольных точек";return;}
            if(Vector3.Distance(anchors[anchors.Count-1],point)<.5f)return;
            anchors.Add(point);bend=0;snapBuilding=null;InvalidatePreview();return;
        }
        // Повторная проверка прямо перед фиксацией: строится ровно показанный маршрут.
        if(!valid){Status="Маршрут пересекает здание, стройку или слишком крутой склон";return;}
        var roadObject=new GameObject("Road");roadObject.layer=2;roadObject.transform.SetParent(transform,false);
        var road=roadObject.AddComponent<RoadStroke>();road.Rebuild(this,draft,roadWidth,RoadMaterial,true,startRoadJoin,snappedRoad!=null);
        roads.Add(road);IndexRoad(road);Revision++;ClearDraft();preview.gameObject.SetActive(false);
        Status="Дорога построена бесплатно. Выберите начало следующей";
    }
    private void RefreshDraft(Vector3 end,Vector3 endDirection)
    {
        if(anchors.Count==0){preview.gameObject.SetActive(false);valid=false;return;}
        knots.Clear();knots.AddRange(anchors);
        Vector3 last=anchors[anchors.Count-1],delta=end-last;delta.y=0;
        if(Mathf.Abs(bend)>.01f&&delta.sqrMagnitude>1f)
            knots.Add((last+end)*.5f+Vector3.Cross(Vector3.up,delta.normalized)*Mathf.Clamp(bend,-delta.magnitude*.5f,delta.magnitude*.5f));
        if(Vector3.Distance(last,end)>.1f)knots.Add(end);
        float length=0;for(int i=1;i<knots.Count;i++)length+=Vector3.Distance(knots[i-1],knots[i]);
        if(length>Mathf.Max(5f,maximumRoadLength)){preview.gameObject.SetActive(false);valid=false;Status="Разбейте длинную дорогу на несколько участков";return;}
        RoadCurve.Sample(knots,sampleSpacing,draft,startDirection,endDirection);
        valid=ValidateDraft();preview.gameObject.SetActive(draft.Count>1);
        preview.Rebuild(this,draft,roadWidth,valid?greenMaterial:redMaterial,false,startRoadJoin,snappedRoad!=null);
    }
    private bool ValidateDraft()
    {
        if(draft.Count<2)return false;
        float totalLength=0;
        Bounds area=new Bounds(draft[0],Vector3.zero);
        for(int i=0;i<draft.Count;i++)
        {
            Vector3 p=draft[i];if(!SampleGround(p,out float y))return false;p.y=y;draft[i]=p;area.Encapsulate(p);
            Vector3 tangent=draft[Mathf.Min(draft.Count-1,i+1)]-draft[Mathf.Max(0,i-1)];tangent.y=0;
            if(tangent.sqrMagnitude>.0001f)
            {
                Vector3 side=Vector3.Cross(Vector3.up,tangent.normalized)*roadWidth*.5f;
                if(!SampleGround(p-side,out float left)||!SampleGround(p+side,out float right))return false;
                if(Mathf.Atan2(Mathf.Abs(right-left),Mathf.Max(.3f,roadWidth))*Mathf.Rad2Deg>maximumSlope)return false;
            }
            if(i>0)
            {
                Vector3 delta=p-draft[i-1];float horizontal=new Vector2(delta.x,delta.z).magnitude;
                totalLength+=delta.magnitude;
                if(horizontal>.001f&&Mathf.Atan2(Mathf.Abs(delta.y),horizontal)*Mathf.Rad2Deg>maximumSlope)return false;
            }
            foreach(var site in ConstructionSite.AllSites)
                if(site!=null&&site.IsAvailable&&site.OverlapsRoadCell(p,roadWidth*.5f))return false;
        }
        if(totalLength<.5f||totalLength>Mathf.Max(5f,maximumRoadLength))return false;
        area.Expand(roadWidth);
        foreach(var building in BuildingManager.AllBuildings)
        {
            if(building==null||!building.isActiveAndEnabled||building.GetComponent<WallGate>()!=null)continue;
            var body=BuildingRoadAccess.SolidBody(building.gameObject);if(body==null)continue;Bounds box=body.bounds;
            if(box.max.x<area.min.x||box.min.x>area.max.x||box.max.z<area.min.z||box.min.z>area.max.z)continue;
            float radius=roadWidth*.5f+.015f;
            if(startRoadJoin&&JunctionIntersects(draft[0],radius,box))return false;
            if(snappedRoad!=null&&JunctionIntersects(draft[draft.Count-1],radius,box))return false;
            // Разрешён только непрерывный короткий заход от торца к SnapPoint этого здания.
            int startLimit=0,endLimit=draft.Count;
            var access=building.GetComponent<BuildingRoadAccess>();
            if(access!=null&&access.SnapPoint!=null)
            {
                if(startBuilding==access)
                    while(startLimit+1<draft.Count&&RoadCurve.RibbonIntersectsBox(draft[startLimit],draft[startLimit+1],roadWidth*.5f,box))startLimit++;
                if(snapBuilding==access)
                    while(endLimit>1&&RoadCurve.RibbonIntersectsBox(draft[endLimit-2],draft[endLimit-1],roadWidth*.5f,box))endLimit--;
            }
            for(int i=1;i<draft.Count;i++)
                if(i>startLimit&&i<endLimit&&RoadCurve.RibbonIntersectsBox(draft[i-1],draft[i],roadWidth*.5f,box))return false;
        }
        return true;
    }
    private static bool JunctionIntersects(Vector3 point,float radius,Bounds box)
    {Vector3 nearest=box.ClosestPoint(point);nearest.y=point.y;return (nearest-point).sqrMagnitude<radius*radius-.001f;}
    private bool Snap(ref Vector3 point,out Vector3 endDirection)
    {
        endDirection=Vector3.zero;
        snappedRoad=null;
        if(Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt)){snapBuilding=null;return false;}
        float radius=Mathf.Max(.2f,snapDistance);
        if(snapBuilding!=null&&snapBuilding.isActiveAndEnabled&&snapBuilding.TryConnectionPoint(out var locked,out var outward)
            &&HorizontalDistance(point,locked)<radius*1.3f)
        {point=locked;endDirection=-outward;return true;}
        snapBuilding=null;BuildingRoadAccess nearest=null;Vector3 bestPoint=point,bestDirection=Vector3.zero;float best=radius;
        foreach(var building in BuildingManager.AllBuildings)
        {
            if(building==null||!building.isActiveAndEnabled)continue;
            var access=building.GetComponent<BuildingRoadAccess>();
            if(access==null||!access.requiresRoad||!access.TryConnectionPoint(out var door,out var direction))continue;
            float distance=HorizontalDistance(point,door);
            if(distance>=best)continue;best=distance;nearest=access;bestPoint=door;bestDirection=-direction;
        }
        if(nearest!=null){snapBuilding=nearest;point=bestPoint;endDirection=bestDirection;return true;}
        var road=NearestRoad(point,out var closest,out float roadDistance);
        if(road!=null&&roadDistance<Mathf.Min(radius,road.Width*.5f+.4f))
        {
            RoadCurve.SnapEndpoint(road.Points,ref closest,Mathf.Min(radius,road.Width*.6f),out endDirection);
            point=closest;snappedRoad=road;return true;
        }
        return false;
    }
    private static float HorizontalDistance(Vector3 a,Vector3 b){a.y=b.y;return Vector3.Distance(a,b);}
    private RoadStroke NearestRoad(Vector3 point,out Vector3 closest,out float distance)
    {
        RoadStroke best=null;closest=point;float squared=float.PositiveInfinity;
        if(!segments.TryGetValue(Bucket(point),out var nearby)){distance=float.PositiveInfinity;return null;}
        foreach(var segment in nearby)
        {
            var road=segment.road;
            if(road==null||!road.isActiveAndEnabled)continue;
            Vector3 p=RoadCurve.Closest(road.Points[segment.index-1],road.Points[segment.index],point,out _);
            Vector3 offset=p-point;offset.y=0;float d=offset.sqrMagnitude;
            if(d>=squared||Mathf.Abs(point.y-p.y)>2f)continue;best=road;closest=p;squared=d;
        }
        distance=Mathf.Sqrt(squared);return best;
    }
    private void UpdateRemoval(Vector3 point,bool click)
    {
        var road=NearestRoad(point,out _,out float distance);
        if(road!=null&&distance>road.Width*.5f+.5f)road=null;
        if(hoverRoad!=road||lastRevision!=Revision)
        {
            hoverRoad=road;lastRevision=Revision;preview.gameObject.SetActive(road!=null);
            if(road!=null){removalPreview.Clear();removalPreview.AddRange(road.Points);preview.Rebuild(this,removalPreview,road.Width,redMaterial,false,road.RoundStart,road.RoundEnd);}
        }
        if(click&&road!=null){roads.Remove(road);RemoveIndex(road);road.gameObject.SetActive(false);Destroy(road.gameObject);Revision++;hoverRoad=null;preview.gameObject.SetActive(false);}
    }
    private static Vector2Int Bucket(Vector3 point)=>new Vector2Int(Mathf.FloorToInt(point.x/BucketSize),Mathf.FloorToInt(point.z/BucketSize));
    private void IndexRoad(RoadStroke road)
    {
        float padding=road.Width*.5f+Mathf.Max(2f,snapDistance);
        for(int i=1;i<road.Points.Count;i++)
        {
            Vector3 a=road.Points[i-1],b=road.Points[i];
            var min=Bucket(new Vector3(Mathf.Min(a.x,b.x)-padding,0,Mathf.Min(a.z,b.z)-padding));
            var max=Bucket(new Vector3(Mathf.Max(a.x,b.x)+padding,0,Mathf.Max(a.z,b.z)+padding));
            for(int x=min.x;x<=max.x;x++)for(int z=min.y;z<=max.y;z++)
            {
                var key=new Vector2Int(x,z);
                if(!segments.TryGetValue(key,out var list)){list=new List<Segment>();segments.Add(key,list);}
                list.Add(new Segment{road=road,index=i});
            }
        }
    }
    private void RemoveIndex(RoadStroke road)
    {foreach(var list in segments.Values)list.RemoveAll(item=>item.road==road);}
    private int GroundMask=>groundLayer.value!=0?groundLayer.value:1<<6;
    public bool SampleGround(Vector3 point,out float height)
    {
        if(Time.unscaledTime>=nextTerrainRefresh){terrains=Terrain.activeTerrains;nextTerrainRefresh=Time.unscaledTime+2f;}
        if(terrains!=null)foreach(var terrain in terrains)
        {
            if(terrain==null||!terrain.isActiveAndEnabled||terrain.terrainData==null)continue;
            Vector3 origin=terrain.GetPosition(),size=terrain.terrainData.size;
            if(point.x<origin.x||point.z<origin.z||point.x>origin.x+size.x||point.z>origin.z+size.z)continue;
            height=terrain.SampleHeight(point)+origin.y;return true;
        }
        if(Physics.Raycast(point+Vector3.up*1000f,Vector3.down,out var hit,2000f,GroundMask,QueryTriggerInteraction.Ignore)){height=hit.point.y;return true;}
        height=0;return false;
    }
    public static bool IsOnRoad(Vector3 point)
    {
        if(Instance==null||!Instance.isActiveAndEnabled)return false;
        if(!Instance.segments.TryGetValue(Bucket(point),out var nearby))return false;
        foreach(var segment in nearby)
        {
            var road=segment.road;if(road==null||!road.isActiveAndEnabled)continue;
            Vector3 p=RoadCurve.Closest(road.Points[segment.index-1],road.Points[segment.index],point,out _);
            if(Mathf.Abs(p.y-point.y)<=2f&&HorizontalDistance(p,point)<=road.Width*.5f)return true;
        }
        return false;
    }
    public bool HasConnection(Vector3 anchor,float gap,float heightTolerance,Bounds? footprint=null)
    {
        if(!footprint.HasValue)
        {
            Vector2Int min=Bucket(anchor-new Vector3(gap,0,gap)),max=Bucket(anchor+new Vector3(gap,0,gap));
            for(int x=min.x;x<=max.x;x++)for(int z=min.y;z<=max.y;z++)
            {
                if(!segments.TryGetValue(new Vector2Int(x,z),out var list))continue;
                foreach(var segment in list)
                {
                    var road=segment.road;if(road==null||!road.isActiveAndEnabled)continue;
                    Vector3 p=RoadCurve.Closest(road.Points[segment.index-1],road.Points[segment.index],anchor,out _);
                    if(Mathf.Abs(p.y-anchor.y)<=heightTolerance&&HorizontalDistance(p,anchor)<=gap+road.Width*.5f)return true;
                }
            }
            return false;
        }
        foreach(var road in roads)
        {
            if(road==null||!road.isActiveAndEnabled)continue;
            for(int i=1;i<road.Points.Count;i++)
            {
                Vector3 point=footprint.Value.ClosestPoint(RoadCurve.Closest(road.Points[i-1],road.Points[i],anchor,out _));
                Vector3 p=RoadCurve.Closest(road.Points[i-1],road.Points[i],point,out _);
                if(Mathf.Abs(p.y-point.y)<=heightTolerance&&HorizontalDistance(p,point)<=gap+road.Width*.5f)return true;
            }
        }
        return false;
    }
    public bool OverlapsRoad(Bounds footprint)
    {
        foreach(var road in roads)
        {
            if(road==null||!road.isActiveAndEnabled)continue;
            for(int i=1;i<road.Points.Count;i++)if(RoadCurve.RibbonIntersectsBox(road.Points[i-1],road.Points[i],road.Width*.5f,footprint))return true;
        }
        return false;
    }
    public void InvalidateConnections(){Revision++;}
    public void NotifyRemoved(RoadStroke road){roads.Remove(road);RemoveIndex(road);Revision++;}
    public void RestoreSavedRoad(RoadStroke road,IReadOnlyList<Vector3> points,float width,bool roundStart,bool roundEnd)
    {
        road.Rebuild(this,points,width,RoadMaterial,true,roundStart,roundEnd);
        if(!roads.Contains(road))roads.Add(road);
        RebuildSavedRoadIndex();
    }
    private void RebuildSavedRoadIndex()
    {
        segments.Clear();
        foreach(var road in roads)if(road!=null)IndexRoad(road);
        InvalidateConnections();
    }
    public void NotifyRemoved(RoadTile tile){Revision++;}
    public GameObject VisualPrefab(RoadShape shape)=>null;
    public void ConformMesh(Mesh mesh,Vector3 origin,float extraOffset=0)
    {
        var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)if(SampleGround(origin+vertices[i],out float y))vertices[i].y=y+surfaceOffset+extraOffset-origin.y;
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
    }
    private void OnDisable(){StopRoadPlacement();Revision++;}
    private void OnDestroy()
    {
        if(Instance==this)Instance=null;
        if(defaultMaterial!=null)Destroy(defaultMaterial);if(greenMaterial!=null)Destroy(greenMaterial);
        if(redMaterial!=null)Destroy(redMaterial);if(snapMaterial!=null)Destroy(snapMaterial);
    }
}
