using UnityEngine;
using UnityEngine.AI;

/// <summary>Резерв позиции, удержание высоты и освобождение при новом приказе/питании.</summary>
public class WallStation : MonoBehaviour
{
    public WallWalkway Platform { get; private set; }
    private GOScript command;
    private Vector3 point;
    private bool returningAfterMeal;
    public bool Holds => Platform != null && command != null && !command.isCommandToMove
        && !UnitEat.ControlsMovement(this) && Vector3.Distance(transform.position, point) < 1f;
    public static bool AllowsWalkway(GameObject unit)
    {
        var combat = unit.GetComponent<TroopCombat>();
        return unit.GetComponent<FoodCart>() == null && unit.GetComponent<SiegeLadderCart>() == null && unit.GetComponent<CartHorse>() == null&&unit.GetComponent<Catapult>()==null
            && unit.GetComponent<BatteringRam>() == null && (combat == null || combat.troopType != TroopType.Cavalry);
    }
    public static bool CanUse(GameObject unit) => AllowsWalkway(unit)
        && !RamCrewMember.Assigned(unit) && unit.GetComponent<Unit>() != null && unit.GetComponent<Unit>().unitHealth > 0;
    public bool Assign(WallWalkway target, Vector3 clicked)
    {
        if(target==null||!FactionMember.CanUse(gameObject,target.gameObject))return false;
        command = GetComponent<GOScript>(); var agent = GetComponent<NavMeshAgent>();
        if (command == null || agent == null || !agent.enabled || !agent.isOnNavMesh || !CanUse(gameObject)) return false;
        if (!target.TryPosition(this, agent, clicked, out var destination)) return false;
        // Сначала проверен полный путь; после MoveTo подписываем новую позицию.
        var previous = Platform;
        command.ManualOrderIssued -= Release;
        if (previous != null && previous != target) previous.Release(this);
        command.MoveTo(destination);
        Platform = target; point = destination;
        command.ManualOrderIssued += Release;
        return true;
    }
    public void Release()
    {
        if (command != null) command.ManualOrderIssued -= Release;
        if (Platform != null) Platform.Release(this);
        Platform = null;
        returningAfterMeal = false;
    }
    public void RestoreSavedHooks()
    {
        command=GetComponent<GOScript>();
        if(command!=null){command.ManualOrderIssued-=Release;if(Platform!=null)command.ManualOrderIssued+=Release;}
    }
    public void PlatformLost(WallWalkway platform)
    {
        if (Platform != platform) return;
        command?.StopMoving();
        Release();
    }
    public static void FallFrom(WallWalkway platform, Unit unit)
    {
        if (unit == null || unit.unitHealth <= 0) return;
        unit.GetComponent<WallStation>()?.Release();
        // Разрушенная опора: юнит падает на доступную землю с уроном, либо погибает.
        var agent = unit.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled) return;
        unit.GetComponent<GOScript>()?.StopMoving();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = ~WallNavigation.AreaMask };
        if (NavMesh.SamplePosition(unit.transform.position - Vector3.up * platform.Segment.height, out var hit,
            platform.Segment.length + 2f, filter) && agent.Warp(hit.position))
            unit.TakeDamage(Mathf.CeilToInt(unit.unitMaxHealth * 0.5f), null);
        else unit.TakeDamage(Mathf.CeilToInt(unit.unitHealth), null);
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (Platform == null) return;
        if(!FactionMember.CanUse(gameObject,Platform.gameObject)){Release();return;}
        var food = GetComponent<UnitEat>();
        if (food != null && food.IsBusy) { returningAfterMeal = true; return; }
        if (returningAfterMeal)
        {
            returningAfterMeal = false;
            if (!Assign(Platform, point)) Release();
        }
    }
    private void OnDisable() { Release(); }
    public static bool Holding(Component owner)
    { var station = owner.GetComponentInParent<WallStation>(); return station != null && station.Holds; }
}
