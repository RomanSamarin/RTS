using UnityEngine;
using UnityEngine.AI;

/// <summary>Зарезервированный житель остаётся на карте и сам приходит к казарме.</summary>
[DisallowMultipleComponent]
public class TrainingRecruit : MonoBehaviour
{
    public Barracks Owner { get; private set; }
    public bool IsReserved => Owner != null;
    public string CurrentTask { get; private set; } = "Идёт на обучение";
    private NavMeshAgent agent;
    private GOScript command;
    private ResourcesMove worker;
    private float nextRepath;
    private float savedStoppingDistance;
    public bool Arrived
    {
        get
        {
            if (Owner == null || worker == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
            if (UnitEat.ControlsMovement(this) || (command != null && (command.isCommandToMove || command.CurrentOrder == MovementOrderState.Order.Combat))) return false;
            if (!Owner.Approach(agent, out var point)) return false;
            return Vector3.Distance(transform.position, point) <= 1.2f;
        }
    }
    public bool Reserve(Barracks owner)
    {
        if (Owner != null || owner == null||!FactionMember.CanUse(gameObject,owner.gameObject)) return false;
        Owner = owner; agent = GetComponent<NavMeshAgent>(); worker = GetComponent<ResourcesMove>(); command = GetComponent<GOScript>();
        if (agent != null) savedStoppingDistance = agent.stoppingDistance;
        return true;
    }
    public void Release()
    {
        Owner = null; CurrentTask = "Свободен";
        bool ownsMovement = !UnitEat.ControlsMovement(this)
            && (command == null || (!command.isCommandToMove && command.CurrentOrder != MovementOrderState.Order.Combat));
        if (ownsMovement && agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            if (Mathf.Approximately(agent.stoppingDistance, Mathf.Min(savedStoppingDistance, 0.25f))) agent.stoppingDistance = savedStoppingDistance;
        }
    }
    public void CancelTraining() { if (Owner != null) Owner.CancelRecruit(this); }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (Owner == null) return;
        if(!FactionMember.CanUse(gameObject,Owner.gameObject)){CancelTraining();return;}
        if (!Owner.isActiveAndEnabled) { CurrentTask = "Обучение приостановлено"; return; }
        if (UnitEat.ControlsMovement(this)) { CurrentTask = "Перерыв на еду"; return; }
        if (command != null && (command.isCommandToMove || command.CurrentOrder == MovementOrderState.Order.Combat))
        { CurrentTask = "Выполняет приказ"; return; }
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) { CurrentTask = "Нет доступного пути"; return; }
        var attack = GetComponent<AttackController>(); if (attack != null) attack.targetToAttack = null;
        if (Arrived)
        { agent.ResetPath(); CurrentTask = Owner.IsCurrentRecruit(this) ? Owner.Status : "Ждёт обучения"; return; }
        CurrentTask = "Идёт на обучение";
        if (Time.time < nextRepath || agent.pathPending) return;
        nextRepath = Time.time + 0.75f;
        if (!Owner.Approach(agent, out var point)) { CurrentTask = "Нет доступного пути"; return; }
        // Подходим к двери ближе, чем солдатский префаб обычно подходит к врагу.
        var path = new NavMeshPath();
        if (!agent.CalculatePath(point, path) || path.status != NavMeshPathStatus.PathComplete)
        { CurrentTask = "Нет доступного пути"; return; }
        agent.isStopped = false; agent.stoppingDistance = Mathf.Min(agent.stoppingDistance, 0.25f); agent.SetPath(path);
    }
}
