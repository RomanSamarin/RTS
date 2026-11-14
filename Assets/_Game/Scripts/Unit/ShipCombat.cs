using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(ShipHull))]
public class ShipCombat : MonoBehaviour
{
    [Min(1f)] public float range = 24f;
    [Min(.2f)] public float volleyInterval = 2.5f;
    [Range(.05f, 1f)] public float hullDamageMultiplier = .25f;
    [Min(0f)] public float deckHeight = .7f;
    [Tooltip("Optional positions for the six archers, in boarding order.")]
    public Transform[] deckPositions;

    private ShipHull focus;
    private float volleyCooldown;
    public string Status { get; private set; } = "Нет лучников на палубе";
    private ShipHull Hull => GetComponent<ShipHull>();
    private ShipNavigation Navigation => GetComponent<ShipNavigation>();

    internal Vector3 DeckPosition(int index)
    {
        if (deckPositions != null && index < deckPositions.Length && deckPositions[index] != null)
            return deckPositions[index].position;

        var hull = Hull;
        float side = index % 2 == 0 ? -1f : 1f;
        return transform.TransformPoint(side * hull.width * .29f, deckHeight, (index / 2 - 1) * hull.length * .28f);
    }

    internal bool Focus(ShipHull target)
    {
        if (!isActiveAndEnabled || Hull.type != ShipType.Warship || !IsEnemy(target)) return false;
        if (Navigation != null && Navigation.IsRetreating) Navigation.Stop();
        focus = target;
        return true;
    }

    private bool IsEnemy(ShipHull target)
    {
        return target != null && target.isActiveAndEnabled
            && target.GetComponent<Unit>() is Unit health && health.unitHealth > 0
            && FactionMember.AreEnemies(gameObject, target.gameObject);
    }

    private ShipHull FindEnemyInRange()
    {
        float bestDistance = range * range;
        ShipHull nearest = null;
        foreach (var member in FactionMember.All)
        {
            if (member == null || !member.TryGetComponent<ShipHull>(out var candidate) || !IsEnemy(candidate))
                continue;
            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distance >= bestDistance) continue;
            nearest = candidate;
            bestDistance = distance;
        }
        return nearest;
    }

    private void Update()
    {
        if (SaveGameManager.IsLoading || Hull.type != ShipType.Warship || GetComponent<Unit>().unitHealth <= 0) return;
        if (Navigation != null && Navigation.IsRetreating)
        {
            Status = "Прекратил огонь";
            return;
        }

        var transport = GetComponent<ShipTransport>();
        if (transport == null || transport.AboardSeats == 0)
        {
            Status = "Нет лучников на палубе";
            return;
        }

        volleyCooldown -= Time.deltaTime;
        if (volleyCooldown > 0f || Time.deltaTime <= 0f) return;
        volleyCooldown = .35f;

        if (!IsEnemy(focus)) focus = null;
        var target = focus != null ? focus : FindEnemyInRange();
        if (target == null)
        {
            Status = "Нет врагов в пределах стрельбы";
            return;
        }
        if ((target.transform.position - transform.position).sqrMagnitude > range * range)
        {
            Status = "Цель вне дальности";
            return;
        }

        int shots = 0;
        foreach (var passenger in transport.Passengers)
        {
            if (!passenger.IsAboard || passenger.Ship != transport || passenger.GetComponent<Unit>().unitHealth <= 0
                || !passenger.TryGetComponent<Archer>(out var archer)) continue;
            passenger.PlaceOnShip();
            if (archer.TryShootFromDeck(Hull, target, hullDamageMultiplier)) shots++;
        }

        Status = shots > 0 ? "Обстреливает " + target.name : "Нет чистой линии выстрела или стрел";
        if (shots > 0) volleyCooldown = Mathf.Max(.2f, volleyInterval);
    }

    private void OnEnable()
    {
        if (Navigation != null) Navigation.ManualOrderIssued += ClearFocus;
    }
    private void OnDisable()
    {
        if (Navigation != null) Navigation.ManualOrderIssued -= ClearFocus;
    }
    private void ClearFocus() => focus = null;
}
