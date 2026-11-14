using UnityEngine;
using UnityEngine.AI;

/// <summary>Фиксированная парабола с упреждением при запуске, без самонаведения.</summary>
public class Arrow : MonoBehaviour
{
    [Header("Полёт")]
    [SerializeField] private float speed = 25f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float minFlightTime = 0.4f;
    [SerializeField] private float hitRadius = 0.35f;
    [SerializeField] private float hitHeight = 1f;
    [SerializeField] private bool rotateToDirection = true;
    public enum ModelAxis { Auto, PositiveX, NegativeX, PositiveY, NegativeY, PositiveZ, NegativeZ }
    [Header("Ориентация модели")]
    [Tooltip("Продольная ось модели. Auto определяет её по геометрии, а не по Rotation префаба.")]
    public ModelAxis modelForwardAxis = ModelAxis.Auto;
    [Tooltip("Включите, если наконечник смотрит назад по траектории.")]
    public bool reverseModelAxis;
    [Range(-180f, 180f), Tooltip("Поворот оперения вокруг древка; не меняет направление наконечника.")]
    public float modelRoll;
    [Min(0f)] public float arcHeightPerMeter = 0.15f;
    [Min(0f)] public float minimumArcHeight = 0.5f;
    [Min(0f)] public float maximumArcHeight = 6f;
    public bool leadMovingTarget = true;
    [Min(0f)] public float maxLeadSeconds = 1.5f;
    [Header("Урон")]
    [SerializeField] private int damage = 10;
    [Header("Эффекты")]
    [SerializeField] private GameObject hitEffect;
    [SerializeField] private bool debugLog;
    private static readonly RaycastHit[] hits = new RaycastHit[64];
    private Transform target;
    private GameObject owner;
    private Faction shooterFaction;
    private Vector3 start, end;
    private float duration, height, elapsed, lifeTimer;
    private int collisionMask;
    private bool launched;
    private Quaternion modelCorrection;
    public float CollisionRadius => Mathf.Max(0.02f, hitRadius);

    public void FlightPlan(Vector3 origin, Transform aim, float flightSpeed, float aimHeight,
        out Vector3 destination, out float seconds, out float arcHeight)
    {
        Vector3 aimPoint = aim != null ? aim.position + Vector3.up * aimHeight : origin + Vector3.forward * 10f;
        Vector3 velocity = Vector3.zero;
        if (leadMovingTarget && aim != null)
        {
            var moving = aim.GetComponentInParent<NavMeshAgent>();
            if (moving != null && moving.enabled && moving.isOnNavMesh) velocity = moving.velocity;
            else if (aim.GetComponentInParent<ShipNavigation>() is ShipNavigation ship) velocity = ship.TrafficVelocity;
        }
        destination = aimPoint;
        seconds = Mathf.Max(0.05f, minFlightTime);
        for (int i = 0; i < 2; i++)
        {
            seconds = Mathf.Max(Mathf.Max(0.05f, minFlightTime), Vector3.Distance(origin, destination) / Mathf.Max(0.1f, flightSpeed));
            destination = aimPoint + velocity * Mathf.Min(seconds, Mathf.Max(0f, maxLeadSeconds));
        }
        float distance = Vector3.Distance(origin, destination);
        arcHeight = Mathf.Clamp(distance * Mathf.Max(0f, arcHeightPerMeter), Mathf.Max(0f, minimumArcHeight), Mathf.Max(minimumArcHeight, maximumArcHeight));
    }
    public void Launch(Transform target, int damage, float speed, GameObject owner, float hitHeight = 1f, int blockingMask = -1)
    {
        this.target = target; this.damage = damage; this.speed = speed; this.owner = owner; this.hitHeight = hitHeight;
        shooterFaction=FactionMember.Of(owner);
        collisionMask = blockingMask; start = transform.position;
        FlightPlan(start, target, speed, hitHeight, out end, out duration, out height);
        elapsed = 0f; lifeTimer = Mathf.Max(lifetime, duration + 0.25f);
        Vector3 axis = ResolveModelAxis();
        if (reverseModelAxis) axis = -axis;
        modelCorrection = Quaternion.AngleAxis(modelRoll, Vector3.forward) * Quaternion.FromToRotation(axis, Vector3.forward);
        launched = true; Rotate(0f);
        if (debugLog) Debug.Log($"[Arrow] полёт {duration:F2} с, дуга {height:F2} м", this);
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (!launched || Time.deltaTime <= 0f) return;
        lifeTimer -= Time.deltaTime;
        float next = Mathf.Min(duration, elapsed + Time.deltaTime);
        int steps = Mathf.Clamp(Mathf.CeilToInt((next - elapsed) / 0.025f), 1, 128);
        float previous = elapsed;
        for (int i = 1; i <= steps; i++)
        {
            float current = Mathf.Lerp(elapsed, next, (float)i / steps);
            Vector3 from = ArrowTrajectory.Position(start, end, previous / duration, height);
            Vector3 to = ArrowTrajectory.Position(start, end, current / duration, height);
            if (CheckSegment(from, to)) return;
            transform.position = to; previous = current;
        }
        elapsed = next; Rotate(elapsed / duration);
        if (elapsed >= duration || lifeTimer <= 0f) Finish(null);
    }
    private bool CheckSegment(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.00001f) return false;
        int count = Physics.SphereCastNonAlloc(from, CollisionRadius, delta / distance, hits, distance, collisionMask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) { Finish(null); return true; }
        Transform collision = null;
        float nearest = distance + 1f;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.transform == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (owner != null && (hit.transform == owner.transform || hit.transform.IsChildOf(owner.transform))) continue;
            if (hit.distance >= nearest) continue;
            nearest = hit.distance; collision = hit.transform;
        }
        if (target != null && target.gameObject.activeInHierarchy &&
            ArrowTrajectory.HitSphere(from, to, target.position + Vector3.up * hitHeight, CollisionRadius, out float fraction)
            && fraction * distance < nearest)
        { collision = target; nearest = fraction * distance; }
        if (collision == null) return false;
        transform.position = from + delta.normalized * nearest;
        Finish(collision); return true;
    }
    private void Rotate(float t)
    {
        if (!rotateToDirection) return;
        Vector3 direction = ArrowTrajectory.Tangent(start, end, t, height);
        if (direction.sqrMagnitude > 0.00001f)
        {
            // LookRotation задаёт устойчивый верх и не переносит случайный крен префаба.
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
            transform.rotation = Quaternion.LookRotation(direction.normalized, up) * modelCorrection;
        }
    }
    private Vector3 ResolveModelAxis()
    {
        switch (modelForwardAxis)
        {
            case ModelAxis.PositiveX: return Vector3.right;
            case ModelAxis.NegativeX: return Vector3.left;
            case ModelAxis.PositiveY: return Vector3.up;
            case ModelAxis.NegativeY: return Vector3.down;
            case ModelAxis.PositiveZ: return Vector3.forward;
            case ModelAxis.NegativeZ: return Vector3.back;
        }
        Vector3 longest = Vector3.forward;
        float bestLength = 0f;
        // Один раз при запуске: учитываем поворот/масштаб вложенной FBX-модели.
        foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Vector3 size = filter.sharedMesh.bounds.size;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 local = axis == 0 ? Vector3.right * size.x : axis == 1 ? Vector3.up * size.y : Vector3.forward * size.z;
                Vector3 world = filter.transform.TransformVector(local);
                if (world.sqrMagnitude <= bestLength) continue;
                bestLength = world.sqrMagnitude;
                longest = transform.InverseTransformDirection(world).normalized;
            }
        }
        return longest;
    }
    private void Finish(Transform collision)
    {
        if (!launched) return;
        launched = false;
        if (collision != null)
        {
            var victim = collision.GetComponentInParent<Unit>();
            bool friendly = FactionRules.AreAllies(shooterFaction,FactionMember.Of(collision.gameObject));
            if (!friendly)
            {
                if (victim != null && victim.unitHealth > 0f) victim.TakeDamage(damage, owner, CombatHitKind.Ranged);
                else
                {
                    var building = collision.GetComponentInParent<BuildingHealth>();
                    if (building != null && !building.IsDead &&
                        (building.GetComponent<BanditCamp>() != null || (target != null &&
                         (target == building.transform || target.IsChildOf(building.transform))))) building.TakeDamage(damage,owner);
                    else
                    {
                        var enemy = collision.GetComponentInParent<Enemy>();
                        if (enemy != null && enemy.health > 0) enemy.ReceiveDamage(damage);
                    }
                }
            }
            if (hitEffect != null) Instantiate(hitEffect, transform.position, Quaternion.identity);
        }
        if (debugLog) Debug.Log($"[Arrow] {(collision != null ? "попадание: " + collision.name : "промах")}", this);
        Destroy(gameObject);
    }
    private void OnDrawGizmosSelected()
    { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, CollisionRadius); }
}
