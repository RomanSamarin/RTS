using UnityEngine;
using UnityEngine.AI;
using _Game.Scripts.UI;

/// <summary>Мирное животное блуждает около начальной позиции. После охоты остаётся туша с мясом.</summary>
[RequireComponent(typeof(NavMeshAgent))]
public class WildlifeAnimal : FoodResourceSource
{
    public override ResourceType Resource => ResourceType.Meat;
    [Min(1f)] public float health = 30f;
    [Min(0.1f)] public float huntDamagePerSecond = 10f;
    [Min(1f)] public float wanderRadius = 12f;
    [Min(0.1f)] public float pauseSeconds = 3f;
    public Animator animator;
    public bool IsDead => health <= 0;
    private NavMeshAgent agent;
    private Vector3 origin;
    private float nextWander;
    // Животное движется: отдельно стоящий WorkPoint не является его позицией.
    public override Vector3 WorkPosition
    {
        get
        {
            if (workPoint != null && workPoint.IsChildOf(transform)) return workPoint.position;
            return agent != null && agent.enabled && agent.isOnNavMesh ? agent.nextPosition : transform.position;
        }
    }
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>(); origin = transform.position;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (workPoint != null && !workPoint.IsChildOf(transform))
            Debug.LogWarning("[WildlifeAnimal] Work Point вне животного игнорируется. Используется позиция самого животного.", this);
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        bool onMesh = agent.enabled && agent.isOnNavMesh;
        SetBool("isMoving", onMesh && !IsDead && agent.velocity.sqrMagnitude > 0.01f);
        SetBool("isDead", IsDead);
        if (!onMesh || IsDead || Remaining <= 0) return;
        if (agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.2f)) return;
        if (Time.time < nextWander) return;
        nextWander = Time.time + Mathf.Max(0.1f, pauseSeconds);
        for (int i = 0; i < 6; i++)
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            Vector3 point = origin + new Vector3(offset.x, 0, offset.y);
            if (!NavMesh.SamplePosition(point, out var hit, 3f, agent.areaMask)) continue;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            agent.isStopped = false; agent.SetPath(path); break;
        }
    }
    public override int Work(float seconds, int capacity)
    {
        if (!isActiveAndEnabled || seconds <= 0 || capacity <= 0 || Remaining <= 0) return 0;
        if (!IsDead)
        {
            health = Mathf.Max(0, health - Mathf.Max(0.1f, huntDamagePerSecond) * seconds);
            if (IsDead)
            {
                if (agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
                SetBool("isMoving", false); SetBool("isDead", true);
            }
            return 0;
        }
        return base.Work(seconds, capacity);
    }
    protected override void OnDepleted()
    {
        if (IsDead && Remaining <= 0) Destroy(gameObject);
    }
    private void SetBool(string key, bool value)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var parameter in animator.parameters)
            if (parameter.name == key && parameter.type == AnimatorControllerParameterType.Bool) animator.SetBool(key, value);
    }
}
