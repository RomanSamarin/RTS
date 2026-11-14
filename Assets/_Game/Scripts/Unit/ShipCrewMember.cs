using UnityEngine;
using UnityEngine.AI;

/// <summary>One reserved resident travels to shore, boards and can disembark on land.</summary>
[DisallowMultipleComponent]
public class ShipCrewMember : MonoBehaviour
{
    public ShipCrew Crew { get; private set; }
    public bool IsAboard { get; private set; }
    public bool IsAssigned => Crew != null || IsAboard;
    public bool IsAlive => isActiveAndEnabled && GetComponent<Unit>() is Unit unit && (!unit.HasStarted || unit.unitHealth > 0);
    public string Status => IsAboard ? "На борту" : IsAssigned ? "Идёт на посадку" : "Свободен";
    private ShipSupplyDock boardingDock;
    private GOScript command;
    private NavMeshAgent agent;
    private int routeToken;
    private float startedAt, nextRepathAt, availableAt;
    private bool savedAgent;
    private Renderer[] renderers; private bool[] rendererStates;
    private Collider[] colliders; private bool[] colliderStates;
    private Canvas[] canvases; private bool[] canvasStates;
    private Animator[] animators; private bool[] animatorStates;
    private Rigidbody[] bodies; private bool[] bodyStates;
    private NavMeshObstacle[] obstacles; private bool[] obstacleStates;
    public static bool Aboard(GameObject obj) => obj != null && (obj.GetComponentInParent<ShipCrewMember>()?.IsAboard ?? false);
    private void Awake()
    {
        command = GetComponent<GOScript>(); agent = GetComponent<NavMeshAgent>();
        if (command != null) command.ManualOrderIssued += CancelBoarding;
    }
    public bool TryBoard(ShipCrew crew, ShipSupplyDock dock)
    {
        var worker = GetComponent<ResourcesMove>(); var eating = GetComponent<UnitEat>();
        if (IsAssigned || !IsAlive || Time.time < availableAt || crew == null || dock == null || crew.Dock != dock
            || worker == null || !worker.IsUnemployed || !worker.CanChangeRole || command == null || command.HasPlayerOrder
            || (eating != null && (eating.IsBusy || eating.WantEat || eating.InCombat()))
            || GetComponent<Unit>().RecentlyAttacked || agent == null || !agent.enabled || !agent.isOnNavMesh
            || !FactionMember.AreAllies(gameObject, crew.gameObject) || !Approach(dock, out var point)) return false;
        if (!command.TryBeginSupplyMove(out routeToken)) return false;
        if (!command.MoveForSupply(routeToken, point, .1f)) { command.EndSupplyMove(routeToken); routeToken = 0; return false; }
        if (!crew.Register(this)) { command.EndSupplyMove(routeToken); routeToken = 0; return false; }
        Crew = crew; boardingDock = dock; startedAt = Time.time; nextRepathAt = Time.time + .75f;
        if (eating == null) gameObject.AddComponent<UnitEat>();
        return true;
    }
    private bool Approach(ShipSupplyDock dock, out Vector3 point)
    {
        point = default;
        if (dock == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(dock.DeliveryPoint, out var hit, 1.5f, filter)) return false;
        var path = new NavMeshPath();
        if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        point = hit.position; return true;
    }
    private void Update()
    {
        if (SaveGameManager.IsLoading || !IsAssigned) return;
        if (!IsAlive) { if (!IsAboard) CancelBoarding(); return; }
        if (IsAboard)
        {
            if (Crew == null) { ShipLost(); return; }
            if (Crew.Disembarking) TryDisembark();
            return;
        }
        if (Crew == null || !Crew.isActiveAndEnabled || Crew.Dock != boardingDock || !BuildingRoadAccess.Allows(boardingDock)
            || !FactionMember.AreAllies(gameObject, Crew.gameObject) || !command.OwnsSupplyOrder(routeToken)
            || GetComponent<Unit>().RecentlyAttacked || (GetComponent<UnitEat>()?.InCombat() ?? false)
            || Time.time - startedAt > 90 || !Approach(boardingDock, out var point))
        { CancelBoarding(); return; }
        if (Vector3.Distance(transform.position, point) <= 1.2f) { Board(); return; }
        if (Time.time >= nextRepathAt)
        { nextRepathAt = Time.time + .75f; if (!command.MoveForSupply(routeToken, point, .1f)) CancelBoarding(); }
    }
    private void Board()
    {
        command.EndSupplyMove(routeToken); routeToken = 0;
        GetComponent<UnitEat>()?.CancelForCommand(false);
        renderers = GetComponentsInChildren<Renderer>(true); rendererStates = new bool[renderers.Length];
        for (int i=0;i<renderers.Length;i++) { rendererStates[i]=renderers[i].enabled; renderers[i].enabled=false; }
        colliders = GetComponentsInChildren<Collider>(true); colliderStates = new bool[colliders.Length];
        for (int i=0;i<colliders.Length;i++) { colliderStates[i]=colliders[i].enabled; colliders[i].enabled=false; }
        canvases = GetComponentsInChildren<Canvas>(true); canvasStates = new bool[canvases.Length];
        for (int i=0;i<canvases.Length;i++) { canvasStates[i]=canvases[i].enabled; canvases[i].enabled=false; }
        animators = GetComponentsInChildren<Animator>(true); animatorStates = new bool[animators.Length];
        for (int i=0;i<animators.Length;i++) { animatorStates[i]=animators[i].enabled; animators[i].enabled=false; }
        bodies = GetComponentsInChildren<Rigidbody>(true); bodyStates = new bool[bodies.Length];
        for (int i=0;i<bodies.Length;i++) { bodyStates[i]=bodies[i].isKinematic; bodies[i].isKinematic=true; }
        obstacles = GetComponentsInChildren<NavMeshObstacle>(true); obstacleStates = new bool[obstacles.Length];
        for (int i=0;i<obstacles.Length;i++) { obstacleStates[i]=obstacles[i].enabled; obstacles[i].enabled=false; }
        savedAgent = agent.enabled; agent.ResetPath(); agent.enabled=false;
        IsAboard=true; FactionMember.Ensure(gameObject).RefreshControlRegistration();
    }
    public void CancelBoarding()
    {
        if (IsAboard) return;
        var crew=Crew; Crew=null; boardingDock=null;
        if (command != null) command.EndSupplyMove(routeToken);
        routeToken=0; availableAt=Time.time+10;
        if (crew != null) crew.Remove(this);
    }
    private void LateUpdate()
    {
        if (!SaveGameManager.IsLoading && IsAboard && Crew != null) transform.position=Crew.transform.position;
    }
    private void TryDisembark()
    {
        var dock=Crew.Dock;
        if (dock==null || agent==null || Time.time<nextRepathAt) return;
        nextRepathAt=Time.time+.5f;
        var filter=new NavMeshQueryFilter { agentTypeID=agent.agentTypeID, areaMask=agent.areaMask };
        for (int i=0;i<25;i++)
        {
            float angle=i*Mathf.PI*.25f, radius=i==0?0:1+(i-1)/8;
            Vector3 target=dock.DeliveryPoint+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
            if (!NavMesh.SamplePosition(target,out var hit,.75f,filter)) continue;
            if (Physics.CheckCapsule(hit.position+Vector3.up*(agent.radius+.05f),
                hit.position+Vector3.up*Mathf.Max(agent.radius+.05f,agent.height-agent.radius),agent.radius,~0,QueryTriggerInteraction.Ignore)) continue;
            var crew=Crew; Crew=null; IsAboard=false; boardingDock=null; transform.position=hit.position;
            RestoreVisibility();
            agent.enabled=savedAgent; if (agent.enabled) agent.Warp(hit.position);
            availableAt=Time.time+10; crew.Remove(this);
            FactionMember.Ensure(gameObject).RefreshControlRegistration(); Physics.SyncTransforms(); return;
        }
    }
    private void RestoreVisibility()
    {
        if (renderers!=null) for(int i=0;i<renderers.Length;i++) if(renderers[i]!=null) renderers[i].enabled=rendererStates[i];
        if (colliders!=null) for(int i=0;i<colliders.Length;i++) if(colliders[i]!=null) colliders[i].enabled=colliderStates[i];
        if (canvases!=null) for(int i=0;i<canvases.Length;i++) if(canvases[i]!=null) canvases[i].enabled=canvasStates[i];
        if (animators!=null) for(int i=0;i<animators.Length;i++) if(animators[i]!=null) animators[i].enabled=animatorStates[i];
        if (bodies!=null) for(int i=0;i<bodies.Length;i++) if(bodies[i]!=null) bodies[i].isKinematic=bodyStates[i];
        if (obstacles!=null) for(int i=0;i<obstacles.Length;i++) if(obstacles[i]!=null) obstacles[i].enabled=obstacleStates[i];
    }
    public void RestoreSavedHooks()
    {
        if (!IsAboard) return;
        foreach(var r in GetComponentsInChildren<Renderer>(true)) r.enabled=false;
        foreach(var c in GetComponentsInChildren<Collider>(true)) c.enabled=false;
        foreach(var c in GetComponentsInChildren<Canvas>(true)) c.enabled=false;
        foreach(var a in GetComponentsInChildren<Animator>(true)) a.enabled=false;
        foreach(var b in GetComponentsInChildren<Rigidbody>(true)) b.isKinematic=true;
        foreach(var o in GetComponentsInChildren<NavMeshObstacle>(true)) o.enabled=false;
        if(agent!=null) agent.enabled=false;
        FactionMember.Ensure(gameObject).RefreshControlRegistration();
    }
    public void ShipLost()
    {
        if (!IsAboard) { CancelBoarding(); return; }
        var crew=Crew; Crew=null; if(crew!=null) crew.Remove(this);
        GetComponent<Unit>()?.Destroy((GameObject)null);
    }
    private void OnDisable()
    {
        if (SaveGameManager.IsLoading) return;
        if (!IsAboard) CancelBoarding(); else { var crew=Crew; Crew=null; if(crew!=null) crew.Remove(this); }
    }
    private void OnDestroy() { if(command!=null) command.ManualOrderIssued-=CancelBoarding; }
}
