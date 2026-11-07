using System.Collections.Generic;
using UnityEngine;

/// <summary>Камень летит к зафиксированной точке и взрывается только один раз, без наведения в полёте.</summary>
public class SiegeProjectile : MonoBehaviour
{
    [Min(.05f)] public float collisionRadius=.25f;
    private Vector3 start,end;private float duration,height,elapsed,radius;
    private int buildingDamage,unitDamage;private GameObject owner;private Faction faction;
    private bool launched,spent;
    private static readonly RaycastHit[] hits=new RaycastHit[64];
    public static SiegeProjectile Create(Vector3 position)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);obj.name="Catapult stone";obj.transform.position=position;obj.transform.localScale=Vector3.one*.5f;
        var collider=obj.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        return obj.AddComponent<SiegeProjectile>();
    }
    public void Launch(Vector3 origin,Vector3 aim,float seconds,float arc,float splash,int buildings,int units,GameObject shooter)
    {
        start=origin;end=aim;duration=Mathf.Max(.1f,seconds);height=Mathf.Max(0,arc);radius=Mathf.Max(.1f,splash);
        buildingDamage=buildings;unitDamage=units;owner=shooter;faction=FactionMember.Of(shooter);
        elapsed=0;spent=false;launched=true;
        foreach(var body in GetComponentsInChildren<Rigidbody>())body.isKinematic=true;
        foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if(!launched||spent||Time.deltaTime<=0)return;
        float next=Mathf.Min(duration,elapsed+Time.deltaTime);
        int steps=Mathf.Clamp(Mathf.CeilToInt((next-elapsed)/.025f),1,128);
        float previous=elapsed;
        for(int i=1;i<=steps;i++)
        {
            float time=Mathf.Lerp(elapsed,next,(float)i/steps);
            var from=ArrowTrajectory.Position(start,end,previous/duration,height);
            var to=ArrowTrajectory.Position(start,end,time/duration,height);var delta=to-from;
            float length=delta.magnitude;
            if(length>.0001f)
            {
                int count=Physics.SphereCastNonAlloc(from,collisionRadius,delta/length,hits,length,~0,QueryTriggerInteraction.Ignore);
                if(count==hits.Length){spent=true;Destroy(gameObject);return;}
                float closest=float.PositiveInfinity;int index=-1;
                for(int n=0;n<count;n++)
                {
                    var hit=hits[n];if(hit.collider==null||hit.collider.transform.IsChildOf(transform))continue;
                    if(owner!=null&&hit.collider.transform.IsChildOf(owner.transform))continue;
                    if(hit.distance>=closest)continue;closest=hit.distance;index=n;
                }
                if(index>=0){Impact(hits[index].point);return;}
            }
            transform.position=to;previous=time;
        }
        elapsed=next;
        if(elapsed>=duration)Impact(end);
    }
    private void Impact(Vector3 point)
    {
        if(spent)return;spent=true;
        var buildings=new Dictionary<BuildingHealth,float>();var units=new Dictionary<Unit,float>();
        foreach(var collider in Physics.OverlapSphere(point,radius,~0,QueryTriggerInteraction.Ignore))
        {
            if(!FactionRules.AreEnemies(faction,FactionMember.Of(collider.gameObject)))continue;
            float distance=Vector3.Distance(point,collider.ClosestPoint(point));
            float power=Mathf.Max(.25f,SiegeRules.Splash(distance,radius));
            var building=collider.GetComponentInParent<BuildingHealth>();
            if(building!=null&&!building.IsDead)
            {
                float fortification=building.GetComponent<WallSegment>()!=null?1.5f:1;
                float damage=Mathf.Max(1,buildingDamage*power*fortification);
                buildings.TryGetValue(building,out float previous);buildings[building]=Mathf.Max(previous,damage);continue;
            }
            var unit=collider.GetComponentInParent<Unit>();
            if(unit!=null&&unit.unitHealth>0&&!HospitalPatient.Inside(unit.gameObject))
            {units.TryGetValue(unit,out float previous);units[unit]=Mathf.Max(previous,unitDamage*power);}
        }
        foreach(var hit in buildings)if(hit.Key!=null&&!hit.Key.IsDead)hit.Key.TakeDamage(hit.Value,owner);
        foreach(var hit in units)if(hit.Key!=null&&hit.Key.unitHealth>0&&hit.Value>0)
            hit.Key.TakeDamage(Mathf.Max(1,Mathf.RoundToInt(hit.Value)),owner,CombatHitKind.Ranged);
        Destroy(gameObject);
    }
}
