using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class SquadMember : MonoBehaviour
{
    public SquadMovement Squad { get; private set; }
    private GOScript command;
    private void Awake(){command=GetComponent<GOScript>();if(command!=null)command.ManualOrderIssued+=Leave;}
    public void Join(SquadMovement group){Leave();Squad=group;}
    public void Leave()
    {
        var previous=Squad;Squad=null;if(previous!=null)previous.Remove(this);
        var agent=GetComponent<NavMeshAgent>();
        if(command!=null&&agent!=null)agent.speed=command.NaturalSpeed;
    }
    private void OnDisable(){Leave();}
    private void OnDestroy(){if(command!=null)command.ManualOrderIssued-=Leave;}
    public static bool Travelling(GameObject obj)=>obj.GetComponent<SquadMember>()?.Squad!=null;
    public static float Speed(GameObject obj,float own)
    {
        var squad=obj.GetComponent<SquadMember>()?.Squad;
        return squad!=null?SquadMovementRules.LimitSpeed(own,squad.Speed):own;
    }
}
