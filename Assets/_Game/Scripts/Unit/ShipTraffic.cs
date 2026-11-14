using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Temporary yielding never replaces the ship's destination or berth reservation.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(ShipNavigation))]
public class ShipTraffic : MonoBehaviour
{
    private static readonly List<ShipTraffic> fleet=new List<ShipTraffic>();
    [Min(.2f)] public float safetyGap=1;
    [Min(1)] public float lookAheadSeconds=6;
    [Min(1)] public float maxRetreatDistance=60;
    private class Maneuver
    {
        public ShipTraffic winner;
        public int winnerRevision;
        public Vector3 start, winnerHeading;
        public readonly List<Vector3> path=new List<Vector3>();
        public int index;
        public float nextPlanAt;
        public float blockedSince=-1;
    }
    [NonSerialized] private Maneuver maneuver=new Maneuver();
    public string Status { get; private set; }="";
    public float SpeedLimit { get; private set; }=float.PositiveInfinity;
    public bool IsYielding => maneuver.winner!=null;
    private ShipNavigation Navigation => GetComponent<ShipNavigation>();
    private ShipHull Hull => GetComponent<ShipHull>();
    private float Radius => Mathf.Sqrt(Hull.width*Hull.width+Hull.length*Hull.length)*.5f+.3f;
    private float Clearance(ShipTraffic other) => Radius+other.Radius+Mathf.Max(safetyGap,other.safetyGap);
    private bool SameWater(ShipTraffic other) => other!=null&&other!=this&&other.isActiveAndEnabled
        && other.GetComponent<Unit>() is Unit u&&(!u.HasStarted||u.unitHealth>0)
        && Mathf.Abs(other.transform.position.y-transform.position.y)<.5f;
    private void OnEnable(){if(!fleet.Contains(this))fleet.Add(this);}
    private void OnDisable(){fleet.Remove(this);ResetManeuver();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry(){fleet.Clear();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterScene()
    {fleet.Clear();foreach(var ship in UnityEngine.Object.FindObjectsOfType<ShipTraffic>())if(ship.isActiveAndEnabled)fleet.Add(ship);}
    public void ResetManeuver(){maneuver=new Maneuver();Status="";SpeedLimit=float.PositiveInfinity;}
    public void NotifyBlocked()
    {
        if(maneuver.winner==null)return;
        if(maneuver.blockedSince<0)maneuver.blockedSince=Time.time;
        else if(Time.time-maneuver.blockedSince>3)ResetManeuver();
    }
    public void NotifyProgress(){maneuver.blockedSince=-1;}
    private bool RefugeClear(Vector3 point,ShipTraffic winner)
    {
        foreach(var other in fleet)
        {
            if(!SameWater(other)||other==winner)continue;
            float clearance=Clearance(other);
            float initial=ShipTrafficRules.Flat(transform.position-other.transform.position).magnitude;
            if(ShipTrafficRules.Flat(point-other.transform.position).magnitude<Mathf.Min(clearance,initial)-.01f)return false;
            var otherPath=other.maneuver.path;
            if(other.IsYielding&&otherPath.Count>0
                &&ShipTrafficRules.Flat(point-otherPath[otherPath.Count-1]).magnitude<clearance)return false;
        }
        return true;
    }

    public bool FixedShipsClear(Vector3 point, Vector3 start)
    {
        foreach(var other in fleet)
        {
            if(!SameWater(other)||other.Navigation.TrafficMoving)continue;
            float radius=Clearance(other);
            // Allow an initially crowded ship to leave, without planning closer to the obstacle.
            radius=Mathf.Min(radius,ShipTrafficRules.Flat(start-other.transform.position).magnitude);
            if(ShipTrafficRules.Flat(point-other.transform.position).magnitude<radius-.01f)return false;
        }
        return true;
    }
    public bool CanStep(Vector3 start,Vector3 end,float seconds)
    {
        foreach(var other in fleet)
            if(SameWater(other)&&!ShipTrafficRules.SafeStep(start,end,other.transform.position,
                other.Navigation.TrafficVelocity,seconds,Clearance(other)))return false;
        return true;
    }
    public bool Prepare(Vector3 mainTarget,out Vector3 target,out bool temporary)
    {
        target=mainTarget;temporary=false;Status="";SpeedLimit=float.PositiveInfinity;
        var nav=Navigation;
        // The crewless launch exception permits only a short local mooring, never a long traffic detour.
        if(!nav.TrafficCanYield)return true;
        Vector3 heading=nav.TrafficHeading;
        if(maneuver.winner!=null)
        {
            var winner=maneuver.winner;
            bool changed=!SameWater(winner)||!winner.Navigation.TrafficMoving||winner.Navigation.CommandRevision!=maneuver.winnerRevision
                || Vector3.Dot(winner.Navigation.TrafficHeading,maneuver.winnerHeading)<.5f;
            var passingPoint=maneuver.path.Count>0?maneuver.path[maneuver.path.Count-1]:maneuver.start;
            bool passed=!changed&&ShipTrafficRules.HasPassed(winner.transform.position,passingPoint,maneuver.winnerHeading,Clearance(winner));
            if(changed||passed)
            {
                ResetManeuver();
                if(!nav.RebuildTrafficRoute()){Status="Ожидает путь после разъезда";return false;}
                target=nav.TrafficTarget;return true;
            }
            while(maneuver.index<maneuver.path.Count&&Vector3.Distance(transform.position,maneuver.path[maneuver.index])<.1f)maneuver.index++;
            temporary=true;
            if(maneuver.index>=maneuver.path.Count){Status="Пропускает корабль";return false;}
            target=maneuver.path[maneuver.index];Status="Уступает дорогу";return true;
        }
        ShipTraffic threat=null;float nearest=float.PositiveInfinity;
        foreach(var other in fleet)
        {
            if(!SameWater(other))continue;
            float clearance=Clearance(other);
            Vector3 offset=ShipTrafficRules.Flat(other.transform.position-transform.position);
            if(offset.magnitude>clearance+(nav.speed+other.Navigation.speed)*lookAheadSeconds)continue;
            var otherNav=other.Navigation;
            bool following=otherNav.TrafficMoving&&Vector3.Dot(heading,otherNav.TrafficHeading)>.7f;
            if(following)
            {
                float forward=Vector3.Dot(offset,heading);
                float lateral=(offset-heading*forward).magnitude;
                if(forward>0&&lateral<clearance)
                    SpeedLimit=Mathf.Min(SpeedLimit,ShipTrafficRules.FollowingSpeed(forward,clearance+.5f,
                        otherNav.TrafficVelocity.magnitude,2));
                continue;
            }
            if(!ShipTrafficRules.Conflict(transform.position,heading*nav.speed,other.transform.position,
                otherNav.TrafficHeading*otherNav.speed,clearance,lookAheadSeconds))continue;
            if(!ShipTrafficRules.GivesWay(GetInstanceID(),true,other.GetInstanceID(),otherNav.TrafficCanYield))continue;
            if(offset.sqrMagnitude<nearest){nearest=offset.sqrMagnitude;threat=other;}
        }
        if(threat==null){if(SpeedLimit<nav.speed)Status="Держит дистанцию";return true;}
        if(Time.time<maneuver.nextPlanAt){Status="Ожидает свободный проход";return false;}
        maneuver.nextPlanAt=Time.time+1.5f;
        if(!threat.Navigation.TrafficMoving)
        {
            if(nav.RebuildTrafficRoute()){target=nav.TrafficTarget;return true;}
            Status="Проход занят стоящим кораблём";return false;
        }
        float spacing=Clearance(threat)+2;
        bool Free(Vector3 p)=>nav.TrafficPointClear(p)&&FixedShipsClear(p,transform.position)&&RefugeClear(p,threat);
        bool Path(Vector3 from,Vector3 to,List<Vector3> path)=>nav.FindTrafficPath(from,to,path);
        if(!ShipTrafficRules.FindRefuge(transform.position,heading,spacing,maxRetreatDistance,Free,Path,maneuver.path))
        {Status="Нет места для разъезда — ожидает";return false;}
        maneuver.winner=threat;maneuver.winnerRevision=threat.Navigation.CommandRevision;
        maneuver.start=transform.position;maneuver.winnerHeading=threat.Navigation.TrafficHeading;maneuver.index=0;
        temporary=true;target=maneuver.path[0];Status="Уступает дорогу";return true;
    }
}
