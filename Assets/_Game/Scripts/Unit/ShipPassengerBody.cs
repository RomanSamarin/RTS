using UnityEngine;
using UnityEngine.AI;

/// <summary>Hides an existing unit without disabling population, army membership or inventory.</summary>
public class ShipPassengerBody
{
    private GameObject owner;
    private Renderer[] renderers;private bool[] rendererStates;
    private Collider[] colliders;private bool[] colliderStates;
    private Canvas[] canvases;private bool[] canvasStates;
    private Animator[] animators;private bool[] animatorStates;
    private Rigidbody[] bodies;private bool[] bodyStates;
    private NavMeshObstacle[] obstacles;private bool[] obstacleStates;
    private bool savedAgent;
    private Vector3 half=new Vector3(.4f,.9f,.4f);
    public void Capture(GameObject target, bool showDeckModel = false)
    {
        owner=target;var agent=owner.GetComponent<NavMeshAgent>();
        if(agent!=null){half=new Vector3(Mathf.Max(.3f,agent.radius),Mathf.Max(.5f,agent.height*.5f),Mathf.Max(.3f,agent.radius));savedAgent=agent.enabled;}
        renderers=owner.GetComponentsInChildren<Renderer>(true);rendererStates=new bool[renderers.Length];for(int i=0;i<renderers.Length;i++)rendererStates[i]=renderers[i].enabled;
        colliders=owner.GetComponentsInChildren<Collider>(true);colliderStates=new bool[colliders.Length];
        for(int i=0;i<colliders.Length;i++)
        {
            colliderStates[i]=colliders[i].enabled;
            if(!colliders[i].enabled||colliders[i].isTrigger)continue;
            var bounds=colliders[i].bounds;
            half.x=Mathf.Max(half.x,Mathf.Abs(bounds.center.x-target.transform.position.x)+bounds.extents.x);
            half.z=Mathf.Max(half.z,Mathf.Abs(bounds.center.z-target.transform.position.z)+bounds.extents.z);
            half.y=Mathf.Max(half.y,bounds.extents.y);
        }
        canvases=owner.GetComponentsInChildren<Canvas>(true);canvasStates=new bool[canvases.Length];for(int i=0;i<canvases.Length;i++)canvasStates[i]=canvases[i].enabled;
        animators=owner.GetComponentsInChildren<Animator>(true);animatorStates=new bool[animators.Length];for(int i=0;i<animators.Length;i++)animatorStates[i]=animators[i].enabled;
        bodies=owner.GetComponentsInChildren<Rigidbody>(true);bodyStates=new bool[bodies.Length];for(int i=0;i<bodies.Length;i++)bodyStates[i]=bodies[i].isKinematic;
        obstacles=owner.GetComponentsInChildren<NavMeshObstacle>(true);obstacleStates=new bool[obstacles.Length];for(int i=0;i<obstacles.Length;i++)obstacleStates[i]=obstacles[i].enabled;
        if(agent!=null&&agent.enabled&&agent.isOnNavMesh)agent.ResetPath();Hide(showDeckModel);
    }
    public void Hide(bool showDeckModel = false)
    {
        if(owner==null)return;
        if(renderers!=null)for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].enabled=showDeckModel&&rendererStates[i];
        if(colliders!=null)foreach(var c in colliders)if(c!=null)c.enabled=false;
        if(canvases!=null)foreach(var c in canvases)if(c!=null)c.enabled=false;
        if(animators!=null)foreach(var a in animators)if(a!=null)a.enabled=false;
        if(bodies!=null)foreach(var b in bodies)if(b!=null)b.isKinematic=true;
        if(obstacles!=null)foreach(var o in obstacles)if(o!=null)o.enabled=false;
        var agent=owner.GetComponent<NavMeshAgent>();if(agent!=null)agent.enabled=false;
    }
    public bool ExitClear(Vector3 point)
    {
        var extents=new Vector3(half.x+.05f,Mathf.Max(.2f,half.y-.1f),half.z+.05f);
        return !Physics.CheckBox(point+Vector3.up*(half.y+.1f),extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
    }
    public void Restore(Vector3 point)
    {
        if(owner==null)return;owner.transform.position=point;
        if(renderers!=null)for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].enabled=rendererStates[i];
        if(colliders!=null)for(int i=0;i<colliders.Length;i++)if(colliders[i]!=null)colliders[i].enabled=colliderStates[i];
        if(canvases!=null)for(int i=0;i<canvases.Length;i++)if(canvases[i]!=null)canvases[i].enabled=canvasStates[i];
        if(animators!=null)for(int i=0;i<animators.Length;i++)if(animators[i]!=null)animators[i].enabled=animatorStates[i];
        if(bodies!=null)for(int i=0;i<bodies.Length;i++)if(bodies[i]!=null)bodies[i].isKinematic=bodyStates[i];
        if(obstacles!=null)for(int i=0;i<obstacles.Length;i++)if(obstacles[i]!=null)obstacles[i].enabled=obstacleStates[i];
        var agent=owner.GetComponent<NavMeshAgent>();if(agent!=null){agent.enabled=savedAgent;if(agent.enabled)agent.Warp(point);}
    }
}
