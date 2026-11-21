using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Спавнит жителя, ведёт его к дому и только по прибытии
/// включает Unit, AttackController и ResourcesMove.
/// </summary>
public class UnitSpawningVillage : MonoBehaviour
{
    public GameObject SpawnPoint;
    public float TimeToSpawn = 3f;
    public GameObject UnitToSpawn;
    public bool TimeToSpawnUnitInVillage;
    [SerializeField] private Faction owner = Faction.Player;

    private const float ArriveDistance = 2f;
    private const float MinMoveTime = 0.25f;
    private const float TravelTimeout = 60f;
    private const float DefaultMaxHealth = 100f;

    private GameObject spawnedUnit;
    private NavMeshAgent spawnedAgent;
    private HousesScript targetHouse;
    private bool arrived;

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        do
        {
            yield return new WaitForSeconds(Mathf.Max(0f, TimeToSpawn));
            yield return SpawnAndWalk();
        }
        while (TimeToSpawnUnitInVillage);
    }

    private IEnumerator SpawnAndWalk()
    {
        arrived = false;
        spawnedUnit = null;
        spawnedAgent = null;
        targetHouse = null;

        if (UnitToSpawn == null)
        {
            Debug.LogWarning($"[Village] {name}: не назначен Unit To Spawn", this);
            yield break;
        }

        Transform point = SpawnPoint != null ? SpawnPoint.transform : transform;

        if (!TryGetNavMeshPosition(point.position, 10f, out Vector3 spawnPosition))
        {
            spawnPosition = point.position;
            Debug.LogWarning($"[Village] {name}: рядом с точкой спавна нет NavMesh", this);
        }

        if (!FactionRules.IsPlayerSide(owner)) yield break;
        targetHouse = HousesScript.FindHouseWithFreePlace(spawnPosition, owner);
        if (targetHouse == null) yield break;
        spawnedUnit = Instantiate(UnitToSpawn, spawnPosition, point.rotation);
        FactionMember.Set(spawnedUnit, owner);
        SaveGameManager.Stamp(spawnedUnit,UnitToSpawn);
        // Резервируем жильё до первого yield.
        if (!targetHouse.TrySettle(spawnedUnit))
        {
            Destroy(spawnedUnit);
            yield break;
        }
        DisableGameplayUntilArrival(spawnedUnit);

        if (FindAnimator(spawnedUnit) == null)
        {
            Debug.LogWarning($"[Village] {name}: у \"{UnitToSpawn.name}\" нет Animator — " +
                             "житель будет двигаться без анимации. Назначьте в Unit To Spawn " +
                             "модель с Animator (например, префаб рабочего).", this);
        }

        spawnedAgent = spawnedUnit.GetComponent<NavMeshAgent>();
        if (spawnedAgent == null)
            spawnedAgent = spawnedUnit.AddComponent<NavMeshAgent>();

        spawnedAgent.enabled = true;

        // Агенту нужен кадр, чтобы встать на NavMesh.
        yield return null;

        if (spawnedUnit == null || spawnedAgent == null) yield break;
        if (!spawnedAgent.isOnNavMesh)
        {
            spawnedAgent.enabled = false;
            spawnedUnit.transform.position = spawnPosition;
            spawnedAgent.enabled = true;
            yield return null;
        }

        if (spawnedUnit == null || spawnedAgent == null) yield break;
        if (!spawnedAgent.isOnNavMesh)
        {
            Debug.LogWarning($"[Village] {name}: юнит не встал на NavMesh, он останется на точке спавна", this);
            FinishArrival();
            yield break;
        }

        if (targetHouse == null)
        {
            Debug.LogWarning($"[Village] {name}: нет дома со свободным местом", this);
            Destroy(spawnedUnit);
            yield break;
        }

        if (!TryGetNavMeshPosition(targetHouse.transform.position, 10f, out Vector3 housePosition))
        {
            Debug.LogWarning($"[Village] {name}: дом {targetHouse.name} не на NavMesh", this);
            FinishArrival();
            yield break;
        }

        string houseName = targetHouse.name;

        targetHouse.TrySettle(spawnedUnit);

        NavMeshPath checkPath = new NavMeshPath();
        bool completePath = NavMesh.CalculatePath(spawnPosition, housePosition, NavMesh.AllAreas, checkPath)
                            && checkPath.status == NavMeshPathStatus.PathComplete;

        if (!completePath)
            Debug.LogWarning($"[Village] {name}: до дома {houseName} нет полного пути по NavMesh", this);

        spawnedAgent.isStopped = false;

        if (!spawnedAgent.SetDestination(housePosition))
        {
            Debug.LogWarning($"[Village] {name}: не удалось отправить юнита к дому {houseName}", this);
            FinishArrival();
            yield break;
        }

        // Геймплей до прихода в дом выключен, поэтому Unit.Update не крутит
        // анимацию. Включаем ходьбу вручную, иначе житель «скользит» к дому.
        SetWalkingAnimation(spawnedUnit, true);

        // Прибытие считаем по фактическому расстоянию до дома:
        // remainingDistance сразу после SetDestination бывает 0.
        float moveStart = Time.time;
        bool reachedHouse = false;

        while (true)
        {
            if (spawnedUnit == null || spawnedAgent == null) yield break;
            if (Time.time - moveStart >= MinMoveTime && DistanceTo(spawnedUnit.transform.position, housePosition) <= ArriveDistance)
            {
                reachedHouse = true;
                break;
            }

            if (Time.time - moveStart >= TravelTimeout)
                break;

            yield return null;
        }

        if (!reachedHouse)
            Debug.LogWarning($"[Village] {name}: {spawnedUnit.name} не дошёл до дома {houseName}", this);

        FinishArrival();
    }

    private static float DistanceTo(Vector3 from, Vector3 to)
    {
        Vector3 delta = from - to;
        delta.y = 0f;
        return delta.magnitude;
    }
    public void RestoreSavedSpawning()
    {StopAllCoroutines();StartCoroutine(ResumeSavedSpawning());}
    private IEnumerator ResumeSavedSpawning()
    {
        while(SaveGameManager.IsLoading)yield return null;
        if(!arrived&&spawnedUnit!=null)
        {
            spawnedAgent=spawnedUnit.GetComponent<NavMeshAgent>();
            if(spawnedAgent!=null&&spawnedAgent.enabled&&spawnedAgent.isOnNavMesh&&targetHouse!=null
                &&TryGetNavMeshPosition(targetHouse.transform.position,10,out var point))
            {
                spawnedAgent.isStopped=false;spawnedAgent.SetDestination(point);SetWalkingAnimation(spawnedUnit,true);
                float began=Time.time;
                while(spawnedUnit!=null&&DistanceTo(spawnedUnit.transform.position,point)>ArriveDistance&&Time.time-began<TravelTimeout)yield return null;
            }
            FinishArrival();
            if(TimeToSpawnUnitInVillage)yield return SpawnLoop();
        }
        else yield return SpawnLoop();
    }

    private static bool TryGetNavMeshPosition(Vector3 position, float radius, out Vector3 result)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        result = position;
        return false;
    }

    private void FinishArrival()
    {
        if (arrived || spawnedUnit == null)
            return;

        arrived = true;

        SetWalkingAnimation(spawnedUnit, false);

        if (spawnedAgent != null && spawnedAgent.isOnNavMesh)
        {
            spawnedAgent.isStopped = true;
            spawnedAgent.ResetPath();
        }

        GiveBasicComponents(spawnedUnit);
    }

    private static void DisableGameplayUntilArrival(GameObject unit)
    {
        if (unit == null)
            return;

        Unit unitComponent = unit.GetComponent<Unit>();
        if (unitComponent != null)
            unitComponent.enabled = false;

        AttackController attack = unit.GetComponent<AttackController>();
        if (attack != null)
            attack.enabled = false;

        ResourcesMove move = unit.GetComponent<ResourcesMove>();
        if (move != null)
        {
            move.role = ResourcesMove.UnitRole.Villager;
            move.enabled = false;
        }
    }

    private void GiveBasicComponents(GameObject unit)
    {
        if (unit == null)
            return;

        NavMeshAgent unitAgent = unit.GetComponent<NavMeshAgent>();
        if (unitAgent == null)
            unitAgent = unit.AddComponent<NavMeshAgent>();

        Unit unitComponent = unit.GetComponent<Unit>();
        if (unitComponent == null)
            unitComponent = unit.AddComponent<Unit>();

        if (unitComponent.unitMaxHealth <= 0f)
            unitComponent.unitMaxHealth = DefaultMaxHealth;

        unitComponent.navMeshAgent = unitAgent;

        if (unitComponent.animator == null)
            unitComponent.animator = unit.GetComponent<Animator>();
        if (unitComponent.animator == null)
            unitComponent.animator = unit.GetComponentInChildren<Animator>();

        AttackController attack = unit.GetComponent<AttackController>();
        if (attack == null)
            attack = unit.AddComponent<AttackController>();

        FactionMember.Set(unit, owner);
        attack.targetToAttack = null;

        ResourcesMove move = unit.GetComponent<ResourcesMove>();
        if (move == null)
            move = unit.AddComponent<ResourcesMove>();

        move.role = ResourcesMove.UnitRole.Villager;

        unitComponent.enabled = true;
        attack.enabled = true;
        move.enabled = true;

        Debug.Log($"[Village] {unit.name} дошёл до дома и получил компоненты", unit);
    }

    // ------------------------------------------------------------- анимация

    private static Animator FindAnimator(GameObject unit)
    {
        if (unit == null)
            return null;

        Animator animator = unit.GetComponent<Animator>();
        if (animator == null)
            animator = unit.GetComponentInChildren<Animator>();

        return animator;
    }

    /// <summary>
    /// Включает/выключает анимацию ходьбы, пока геймплей выключен
    /// (Unit.Update в это время не работает).
    /// </summary>
    private static void SetWalkingAnimation(GameObject unit, bool walking)
    {
        Animator animator = FindAnimator(unit);
        if (animator == null)
            return;

        if (!HasBoolParameter(animator, "IsFollowing"))
            return;

        animator.SetBool("IsFollowing", walking);
    }

    private static bool HasBoolParameter(Animator animator, string parameterName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.name == parameterName)
                return true;
        }

        return false;
    }
}
