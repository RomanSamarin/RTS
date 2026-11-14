using System.Collections.Generic;
using UnityEngine;

/// <summary>Water-only movement, independent of land NavMesh and military formations.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(ShipHull))]
public class ShipNavigation : MonoBehaviour
{
    public enum Motion { Idle, Sailing, Mooring, Moored, Waiting }
    [Min(.1f)] public float speed = 4, acceleration = 2, turnSpeed = 45;
    [Min(.5f)] public float gridCell = 3;
    [SerializeField] private Motion state;
    [SerializeField] private ShipLaunchArea water;
    [SerializeField] private ShipSupplyDock destinationDock;
    [SerializeField] private Vector3 destination;
    private readonly List<Vector3> route = new List<Vector3>();
    private int waypoint;
    private float currentSpeed, nextRepathAt, autoDockAfter;
    private bool harbourManeuver;
    private ShipHull escortTarget;
    private int escortSlot;
    private Vector3 escortPlannedPoint;
    private float nextEscortPlanAt;
    public bool IsRetreating { get; private set; }
    [System.NonSerialized] private Vector3 trafficVelocity;
    public int CommandRevision { get; private set; }
    public event System.Action ManualOrderIssued;
    public Vector3 TrafficVelocity => trafficVelocity;
    public Vector3 TrafficTarget => waypoint<route.Count?route[waypoint]:transform.position;
    public Vector3 TrafficHeading => TrafficMoving?ShipTrafficRules.Flat(TrafficTarget-transform.position).normalized:Vector3.zero;
    public bool TrafficMoving => Alive && state!=Motion.Idle && state!=Motion.Moored && waypoint<route.Count
        && (harbourManeuver||(GetComponent<ShipCrew>()?.Ready??false));
    public bool TrafficCanYield => TrafficMoving && !harbourManeuver;
    private ShipTraffic Traffic => GetComponent<ShipTraffic>();
    private void Awake(){if(GetComponent<ShipTraffic>()==null)gameObject.AddComponent<ShipTraffic>();}
    [SerializeField] private string message = "Ожидает приказ";
    public Motion State => state;
    public string Status => IsRetreating ? "Отступает · " + message
        : escortTarget != null ? $"Сопровождает {escortTarget.name} · {message}" : message;
    public ShipSupplyDock DestinationDock => destinationDock;
    public bool CanAutoDock => !HasNavalOrder && state == Motion.Idle && Time.time >= autoDockAfter && !(GetComponent<ShipTradeRoute>()?.IsRunning??false);
    private bool HasNavalOrder => IsRetreating || escortTarget != null;
    private ShipHull Hull => GetComponent<ShipHull>();
    private bool Alive => isActiveAndEnabled && GetComponent<Unit>() is Unit unit && (!unit.HasStarted || unit.unitHealth > 0);
    public bool IsMooredTo(ShipSupplyDock dock) => dock != null && Alive && state == Motion.Moored && destinationDock == dock
        && Vector3.Distance(transform.position, dock.BerthPoint) <= .25f;

    private bool ClearAt(Vector3 point) => ClearAt(water, point);
    private bool ClearAt(ShipLaunchArea area, Vector3 point)
    {
        // A conservative turning envelope keeps the entire hull clear at every heading.
        float diameter = Mathf.Sqrt(Hull.width * Hull.width + Hull.length * Hull.length);
        return area != null && area.CanOccupy(point, Quaternion.identity, diameter, diameter,
            Hull.minimumDepth, Hull.clearanceHeight, transform, out _, true);
    }
    public bool TrafficPointClear(Vector3 point)=>ClearAt(point);
    public bool FindTrafficPath(Vector3 start,Vector3 goal,List<Vector3> path)
        => water!=null&&FindWaterRoute(water,start,goal,path);
    private bool FindWaterRoute(ShipLaunchArea area,Vector3 start,Vector3 goal,List<Vector3> path)
    {
        start.y=goal.y=area.SurfaceHeight;
        var localPath=new List<Vector3>();
        bool Free(Vector3 local)
        {
            var p=area.transform.TransformPoint(local);
            return ClearAt(area,p)&&(Traffic==null||Traffic.FixedShipsClear(p,start));
        }
        if(!ShipPathfinder.Find(area.transform.InverseTransformPoint(start),area.transform.InverseTransformPoint(goal),
            area.width,area.length,gridCell,Mathf.Max(.25f,Hull.width*.25f),Free,localPath))return false;
        path.Clear();foreach(var local in localPath)path.Add(area.transform.TransformPoint(local));return true;
    }
    public bool RebuildTrafficRoute()
    {
        var path=new List<Vector3>();
        if(!BuildRoute(destination,path))return false;
        route.Clear();route.AddRange(path);waypoint=0;return true;
    }
    private bool BuildRoute(Vector3 goal, List<Vector3> path)
    {
        Physics.SyncTransforms();
        ShipLaunchArea selectedWater = null;
        foreach (var area in Object.FindObjectsOfType<ShipLaunchArea>())
            if (area.isActiveAndEnabled && Mathf.Abs(area.SurfaceHeight - transform.position.y) <= .25f
                && area.ContainsPoint(transform.position) && area.ContainsPoint(goal)
                && (selectedWater == null || area.width * area.length > selectedWater.width * selectedWater.length)) selectedWater = area;
        if (selectedWater == null) return false;
        if(!FindWaterRoute(selectedWater,transform.position,goal,path))return false;
        water = selectedWater;
        return true;
    }
    public bool SailTo(Vector3 point) => Begin(point, null, true);
    public bool SailForRoute(Vector3 point) => Begin(point,null,false);
    public bool RetreatTo(Vector3 point)
    {
        if (!Begin(point, null, true)) return false;
        IsRetreating = true;
        message = "К указанной точке";
        return true;
    }

    public bool Escort(ShipHull target)
    {
        if (!Alive || Hull.type != ShipType.Warship || !ValidEscortTarget(target)) return false;
        if (GetComponent<ShipTransport>()?.BlocksDeparture ?? false)
            return Reject("Завершите посадку или высадку перед сопровождением");
        if (!(GetComponent<ShipCrew>()?.Ready ?? false))
            return Reject("Для сопровождения нужен полный экипаж");
        if (escortTarget == target) return true;

        // Keep an existing berth until the convoy needs to move.
        if (state == Motion.Moored)
        {
            CommandRevision++;
            ManualOrderIssued?.Invoke();
        }
        else StopInternal(true);

        IsRetreating = false;
        escortTarget = target;
        escortSlot = FindEscortSlot(target);
        nextEscortPlanAt = 0;
        message = "Занимает позицию";
        return true;
    }

    private bool ValidEscortTarget(ShipHull target)
    {
        return target != null && target != Hull && target.isActiveAndEnabled && target.type != ShipType.Warship
            && target.GetComponent<Unit>() is Unit health && health.unitHealth > 0
            && FactionMember.AreAllies(gameObject, target.gameObject);
    }

    private int FindEscortSlot(ShipHull target)
    {
        var usedSlots = new HashSet<int>();
        foreach (var member in FactionMember.All)
        {
            if (member == null || !member.TryGetComponent<ShipNavigation>(out var other) || other == this) continue;
            if (other.Alive && other.escortTarget == target) usedSlots.Add(other.escortSlot);
        }
        int slot = 0;
        while (usedSlots.Contains(slot)) slot++;
        return slot;
    }

    private void UpdateEscort()
    {
        // A destroyed Unity object still has a reference until this order is cleared.
        if (ReferenceEquals(escortTarget, null)) return;
        if (!ValidEscortTarget(escortTarget) || !(GetComponent<ShipCrew>()?.Ready ?? false))
        {
            escortTarget = null;
            if (state != Motion.Moored) StopInternal(false);
            message = state == Motion.Moored ? "Пришвартован · Сопровождение завершено" : "Сопровождение завершено";
            return;
        }
        if (Time.time < nextEscortPlanAt || (Traffic?.IsYielding ?? false)) return;
        nextEscortPlanAt = Time.time + 1.5f;

        var leader = escortTarget;
        var leadNav = leader.GetComponent<ShipNavigation>();
        var heading = leadNav != null && leadNav.TrafficMoving ? leadNav.TrafficHeading : leader.transform.forward;
        float ownDiameter = Mathf.Sqrt(Hull.width * Hull.width + Hull.length * Hull.length);
        float leadDiameter = Mathf.Sqrt(leader.width * leader.width + leader.length * leader.length);
        float spacing = (ownDiameter + leadDiameter) * .5f + 3f
            + Mathf.Max(Traffic != null ? Traffic.safetyGap : 1f, leader.GetComponent<ShipTraffic>()?.safetyGap ?? 1f);
        var position = leader.transform.position;
        Vector3 follow = ShipTrafficRules.EscortPoint(position, heading, spacing, escortSlot);
        if ((follow - transform.position).sqrMagnitude < 4f)
        {
            if (state != Motion.Moored)
            {
                route.Clear();
                waypoint = 0;
                currentSpeed = 0;
                state = Motion.Idle;
            }
            message = "Держит позицию";
            return;
        }
        if (state == Motion.Sailing && waypoint < route.Count && (escortPlannedPoint - follow).sqrMagnitude < 9f) return;
        if (GetComponent<ShipTransport>()?.BlocksDeparture ?? false)
        {
            message = "Ожидает посадку или высадку";
            return;
        }

        var path = new List<Vector3>();
        if (!BuildRoute(follow, path)
            && !BuildRoute(ShipTrafficRules.EscortPoint(position, heading, spacing, escortSlot, true), path))
        {
            if (state == Motion.Idle || state == Motion.Moored) message = "Ожидает свободный подход";
            return;
        }

        if (destinationDock != null) LeaveDock();
        destinationDock = null;
        destination = path[path.Count - 1];
        escortPlannedPoint = follow;
        route.Clear();
        route.AddRange(path);
        waypoint = 0;
        harbourManeuver = false;
        state = Motion.Sailing;
    }
    public bool MoorAt(ShipSupplyDock dock) => TryMoor(dock,true);
    public bool MoorForRoute(ShipSupplyDock dock) => TryMoor(dock,false);
    public string LastOrderError { get; private set; }="";
    private bool TryMoor(ShipSupplyDock dock,bool manual)
    {
        if (dock == null || !dock.CanReserve(this)) return Reject("Причал занят или недоступен",manual);
        return Begin(dock.BerthPoint, dock,manual);
    }
    private bool Begin(Vector3 point, ShipSupplyDock dock,bool manual)
    {
        if (!Alive) return false;
        if(!manual&&HasNavalOrder)return Reject("Корабль выполняет морской приказ",false);
        if(!manual&&Hull.RepairPort!=null)return Reject("Корабль назначен на ремонт",false);
        if(GetComponent<ShipTransport>()?.BlocksDeparture??false)return Reject("Дождитесь посадки/высадки армии или отмените посадку",manual);
        bool crewReady = GetComponent<ShipCrew>()?.Ready ?? false;
        bool localMooring = dock != null && (state == Motion.Idle || state == Motion.Moored)
            && Vector3.Distance(transform.position, dock.BerthPoint) <= dock.mooringRadius
            && !(GetComponent<ShipCrew>()?.Disembarking ?? false);
        if (!crewReady && !localMooring) return Reject("Для выхода в рейс нужен полный экипаж",manual);
        var previousWater = water;
        var path = new List<Vector3>();
        if (!BuildRoute(point, path)) return Reject("Нет водного пути: проверьте глубину и свободное место для корпуса",manual);
        if (!crewReady)
        {
            float distance=0; Vector3 previous=transform.position;
            foreach(var step in path) { distance+=Vector3.Distance(previous,step); previous=step; }
            if(distance > dock.mooringRadius * 2) { water=previousWater; return Reject("Без экипажа разрешён только короткий подход к причалу у спуска",manual); }
        }
        if (dock != null && Mathf.Abs(point.y - water.SurfaceHeight) > .2f)
        { water = previousWater; return Reject("Berth Point должен находиться на высоте поверхности воды",manual); }
        if (dock != null && !dock.TryReserve(this)) { water = previousWater; return Reject("Причал занят",manual); }
        if (manual) ClearNavalOrder();
        LeaveDock(dock);
        CommandRevision++; Traffic?.ResetManeuver();
        harbourManeuver = !crewReady;
        destinationDock = dock; destination = path[path.Count - 1];
        route.Clear(); route.AddRange(path); waypoint = 0; currentSpeed = 0;
        state = Motion.Sailing; message = dock != null ? "Идёт к причалу" : "Плывёт";
        LastOrderError="";if(manual)ManualOrderIssued?.Invoke();
        return true;
    }
    private bool Reject(string reason,bool log=true) { LastOrderError=reason;if(log)Debug.LogWarning("[Корабль] " + reason, this); return false; }
    private void LeaveDock(ShipSupplyDock keep = null)
    {
        var provisions = GetComponent<ShipProvisions>();
        if (provisions != null && provisions.Dock != null) provisions.Dock.ReleaseShip(provisions);
        if (destinationDock != null && destinationDock != keep) destinationDock.CancelReservation(this);
        autoDockAfter = Time.time + 5;
    }
    public void Stop()=>StopInternal(true);
    public void StopForRoute()=>StopInternal(false);
    private void StopInternal(bool manual)
    {
        ClearNavalOrder();
        LeaveDock(); destinationDock = null; route.Clear(); currentSpeed = 0;
        CommandRevision++; trafficVelocity=Vector3.zero; Traffic?.ResetManeuver();
        state = Motion.Idle; message = "Остановлен";
        if(manual)ManualOrderIssued?.Invoke();
    }
    private void ClearNavalOrder()
    {
        escortTarget = null;
        IsRetreating = false;
    }
    private void Update()
    {
        if (SaveGameManager.IsLoading) return;
        trafficVelocity=Vector3.zero;
        if (!Alive) { if (state != Motion.Idle) StopInternal(false); return; }
        if (destinationDock != null && !destinationDock.CanReserve(this)) { StopInternal(false); message = "Причал недоступен"; return; }
        UpdateEscort();
        if (state == Motion.Moored)
        {
            if (!IsMooredTo(destinationDock)) StopInternal(false);
            return;
        }
        if (state == Motion.Idle) return;
        if (!harbourManeuver && !(GetComponent<ShipCrew>()?.Ready ?? false))
        { StopInternal(false); message = "Движение остановлено: недостаточно экипажа"; return; }
        if (water == null || !water.isActiveAndEnabled) { StopInternal(false); message = "Зона воды недоступна"; return; }
        if (state == Motion.Waiting)
        {
            if (Time.time < nextRepathAt) return;
            nextRepathAt = Time.time + 2;
            if (!BuildRoute(destination, route)) return;
            waypoint = 0; state = Motion.Sailing; message = "Плывёт";
        }
        Physics.SyncTransforms();
        if (!ClearAt(transform.position))
        { state = Motion.Waiting; currentSpeed = 0; nextRepathAt = Time.time + 2; message = "Недостаточно места для корпуса"; return; }
        if (waypoint >= route.Count)
        {
            currentSpeed = 0;
            if (destinationDock == null)
            {
                state = Motion.Idle;
                if (IsRetreating)
                {
                    IsRetreating = false;
                    autoDockAfter = Time.time + 5f;
                    message = "Отступление завершено";
                }
                else message = escortTarget != null ? "Держит позицию" : "Прибыл";
                return;
            }
            state = Motion.Mooring; message = "Швартуется";
            Quaternion facing = Quaternion.Euler(0, destinationDock.BerthRotation.eulerAngles.y, 0);
            if(!TurnTowards(facing)){message="Ожидает место для швартовки";return;}
            if (Quaternion.Angle(transform.rotation, facing) < .5f)
            {
                state = Motion.Moored;
                if (destinationDock.CompleteMooring(this)) message = "Пришвартован";
                else StopInternal(false);
            }
            return;
        }
        Vector3 target=route[waypoint];bool temporary=false;
        if(Traffic!=null&&!Traffic.Prepare(target,out target,out temporary))
        {currentSpeed=0;message=Traffic.Status;return;}
        Vector3 delta = target - transform.position; delta.y = 0;
        if (delta.magnitude < .05f) { if(!temporary)waypoint++; return; }
        bool reverse=temporary&&Vector3.Dot(delta.normalized,transform.forward)<-.5f;
        var rotation = Quaternion.LookRotation(reverse?-delta:delta);
        if(!TurnTowards(rotation)){currentSpeed=0;Traffic?.NotifyBlocked();message="Ожидает место для поворота";return;}
        if (Quaternion.Angle(transform.rotation, rotation) > 5) { currentSpeed = 0; message=temporary?"Поворачивает для разъезда":"Поворачивает";return; }
        float desiredSpeed = Mathf.Min(speed*(reverse?.6f:1), Mathf.Sqrt(2 * acceleration * delta.magnitude));
        if(Traffic!=null)desiredSpeed=Mathf.Min(desiredSpeed,Traffic.SpeedLimit);
        currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * Time.deltaTime);
        Vector3 next = Vector3.MoveTowards(transform.position, target, currentSpeed * Time.deltaTime);
        if(Traffic!=null&&!Traffic.CanStep(transform.position,next,Time.deltaTime))
        {currentSpeed=0;Traffic.NotifyBlocked();message="Пропускает корабль";return;}
        if (!ShipPathfinder.Segment(transform.position, next, Mathf.Max(.2f, Hull.width * .2f), ClearAt))
        { Traffic?.ResetManeuver(); state = Motion.Waiting; currentSpeed = 0; nextRepathAt = Time.time + 2; message = "Проход занят — ожидает путь"; return; }
        if(!water.CanOccupy(next,transform.rotation,Hull.width,Hull.length,Hull.minimumDepth,Hull.clearanceHeight,transform,out _))
        {currentSpeed=0;Traffic?.NotifyBlocked();message="Проход занят кораблём";return;}
        trafficVelocity=Time.deltaTime>0?(next-transform.position)/Time.deltaTime:Vector3.zero;
        transform.position = next;
        if(trafficVelocity.sqrMagnitude>.0001f)Traffic?.NotifyProgress();
        message=Traffic!=null&&Traffic.Status!=""?Traffic.Status:destinationDock!=null?"Идёт к причалу":"Плывёт";
    }
    private bool TurnTowards(Quaternion rotation)
    {
        var next=Quaternion.RotateTowards(transform.rotation,rotation,turnSpeed*Time.deltaTime);
        if(!water.CanOccupy(transform.position,next,Hull.width,Hull.length,Hull.minimumDepth,Hull.clearanceHeight,transform,out _))return false;
        transform.rotation=next;return true;
    }
    public void RestoreSavedHooks()
    {
        currentSpeed = 0;
        nextEscortPlanAt = 0;
        trafficVelocity=Vector3.zero;Traffic?.ResetManeuver();
        if (state == Motion.Sailing || state == Motion.Waiting || state == Motion.Mooring)
        { state = Motion.Waiting; nextRepathAt = 0; }
    }
    private void OnDisable() { if (!SaveGameManager.IsLoading) StopInternal(false); }

    public static bool HandleCommand(Ray ray, List<GameObject> selection)
    {
        var ships = SelectedShips(selection);
        if (ships.Count == 0) return false;
        bool onlyShips = true;
        foreach (var obj in selection) if (obj != null && obj.GetComponentInParent<ShipHull>() == null) onlyShips = false;
        if (Physics.Raycast(ray, out var hit, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore))
        {
            var enemyHull = hit.collider.GetComponentInParent<ShipHull>();
            if (enemyHull != null)
            {
                bool orderedAttack = false;
                bool orderedEscort = false;
                foreach (var nav in ships)
                {
                    if (nav.GetComponent<ShipCombat>() is ShipCombat combat && combat.Focus(enemyHull))
                        orderedAttack = true;
                    else if (nav.Hull.type == ShipType.Warship && nav.ValidEscortTarget(enemyHull))
                    {
                        nav.Escort(enemyHull);
                        orderedEscort = true;
                    }
                }
                if (orderedAttack || orderedEscort) return onlyShips;
            }
            var dock = hit.collider.GetComponentInParent<ShipSupplyDock>();
            if (dock != null)
            {
                // One berth accepts exactly one selected ship; others retain their orders.
                foreach (var nav in ships) if (nav.MoorAt(dock)) break;
                return onlyShips;
            }
        }
        if (TryGetWaterPoint(ray, out var point))
            for (int i = 0; i < ships.Count; i++)
            {
                float spacing = ships[i].Hull.length + ships[i].Hull.width + 1;
                ships[i].SailTo(point + new Vector3(i * spacing, 0, 0));
            }
        return onlyShips;
    }

    internal static List<ShipNavigation> SelectedShips(List<GameObject> selection)
    {
        var ships = new List<ShipNavigation>();
        foreach (var obj in selection)
        {
            var hull = obj != null ? obj.GetComponentInParent<ShipHull>() : null;
            if (hull == null || !FactionMember.IsPlayer(hull.gameObject)) continue;
            var nav = hull.GetComponent<ShipNavigation>();
            if (nav != null && nav.Alive && !ships.Contains(nav)) ships.Add(nav);
        }
        return ships;
    }

    internal static int RetreatGroup(List<ShipNavigation> ships, Vector3 point)
    {
        float spacing = 1f;
        foreach (var nav in ships)
            spacing = Mathf.Max(spacing, nav.Hull.length + nav.Hull.width + 1f);
        int accepted = 0;
        for (int i = 0; i < ships.Count; i++)
        {
            int offset = (i + 1) / 2 * (i % 2 == 0 ? -1 : 1);
            if (ships[i].RetreatTo(point + Vector3.right * (spacing * offset))) accepted++;
        }
        return accepted;
    }

    internal static bool TryGetWaterPoint(Ray ray, out Vector3 point)
    {
        point = default;
        float best = float.PositiveInfinity;
        foreach (var area in Object.FindObjectsOfType<ShipLaunchArea>())
        {
            if (!area.isActiveAndEnabled) continue;
            var plane = new Plane(Vector3.up, new Vector3(0, area.SurfaceHeight, 0));
            if (!plane.Raycast(ray, out float distance) || distance >= best) continue;
            var candidate = ray.GetPoint(distance);
            if (!area.ContainsPoint(candidate)) continue;
            point = candidate; best = distance;
        }
        return best < float.PositiveInfinity;
    }
}
