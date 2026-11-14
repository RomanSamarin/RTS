using System.Collections.Generic;
using UnityEngine;

/// <summary>Состав армии, одна снабженческая повозка и её охрана. Не подменяет боевые приказы.</summary>
public class ArmyGroup : MonoBehaviour
{
    public string armyName = "Армия";
    private readonly List<ArmyMember> members = new List<ArmyMember>();
    [SerializeField] private FoodCart supplyCart;
    public FoodCart SupplyCart => supplyCart;
    public IReadOnlyList<ArmyMember> Members { get { Cleanup(); return members; } }
    public int MemberCount { get { Cleanup(); return members.Count; } }
    public int HungryCount
    {
        get { Cleanup(); int count = 0; foreach (var member in members) { var food = member.GetComponent<UnitEat>(); if (food != null && food.WantEat) count++; } return count; }
    }
    public float SecondsToStarvation
    {
        get
        {
            float remaining = float.PositiveInfinity;
            foreach (var member in Members)
            {
                var food = member.GetComponent<UnitEat>();
                if (food != null && food.WantEat && !food.IsEating && food.mode == UnitEat.FeedingMode.Expedition)
                    remaining = Mathf.Min(remaining, food.SecondsUntilStarvation);
            }
            return remaining;
        }
    }
    public string Warning { get; private set; } = "";
    public event System.Action<string> WarningChanged;
    private float nextWarning;
    private Vector3 lastAnchor;
    private void Awake() { if(FactionMember.Find(gameObject)==null)FactionMember.Set(gameObject,Faction.Player);lastAnchor = transform.position; }
    private void OnEnable() { if (supplyCart != null) AssignCart(supplyCart); }
    public bool AddSoldier(GameObject soldier)
    {
        if (!isActiveAndEnabled || soldier == null || !soldier.activeInHierarchy||!FactionMember.AreAllies(gameObject,soldier)) return false;
        var worker = soldier.GetComponent<ResourcesMove>(); var unit = soldier.GetComponent<Unit>();
        if (worker == null || (worker.role != ResourcesMove.UnitRole.Soldier&&soldier.GetComponent<Catapult>()==null) || unit == null || unit.unitHealth <= 0) return false;
        var member = soldier.GetComponent<ArmyMember>(); if (member == null) member = soldier.AddComponent<ArmyMember>();
        if (member.Army != null && member.Army != this) member.LeaveArmy();
        member.SetArmy(this); if (!members.Contains(member)) members.Add(member);
        if (soldier.GetComponent<Catapult>()==null&&soldier.GetComponent<UnitEat>() == null) soldier.AddComponent<UnitEat>();
        if (members.Count == 1) lastAnchor = soldier.transform.position;
        return true;
    }
    public void RemoveMember(ArmyMember member)
    {
        members.Remove(member);
        if (member != null && member.Army == this)
        {
            member.SetArmy(null);
            var escort = member.GetComponent<ArmyEscort>(); if (escort != null) escort.enabled = false;
        }
    }
    public void AssignSelectedSoldiers()
    {
        if (ManagerGO.Instance == null) return;
        foreach (var unit in new List<GameObject>(ManagerGO.Instance.unitsSelected))
        {
            if (!AddSoldier(unit)) continue;
            var escort = unit.GetComponent<ArmyEscort>(); if (escort != null) escort.enabled = false;
        }
    }
    public void RemoveSelectedSoldiers()
    {
        if (ManagerGO.Instance == null) return;
        foreach (var unit in ManagerGO.Instance.unitsSelected)
        { var member = unit != null ? unit.GetComponent<ArmyMember>() : null; if (member != null && member.Army == this) RemoveMember(member); }
    }
    public void AssignSelectedEscorts()
    {
        if (ManagerGO.Instance == null) return;
        foreach (var unit in new List<GameObject>(ManagerGO.Instance.unitsSelected))
        {
            if (!AddSoldier(unit)) continue;
            var escort = unit.GetComponent<ArmyEscort>(); if (escort == null) escort = unit.AddComponent<ArmyEscort>();
            escort.enabled = true;
        }
    }
    public void AssignCart(FoodCart cart)
    {
        if(cart!=null&&!FactionMember.AreAllies(gameObject,cart.gameObject))return;
        if (supplyCart != null && supplyCart != cart) supplyCart.SetArmy(null);
        supplyCart = cart;
        if (cart == null) return;
        if (cart.Army != null && cart.Army != this) cart.Army.RemoveCart(cart);
        cart.SetArmy(this);
    }
    public void RemoveCart(FoodCart cart)
    { if (supplyCart == cart) supplyCart = null; if (cart != null && cart.Army == this) cart.SetArmy(null); }
    private void Cleanup()
    {
        if(supplyCart!=null&&!FactionMember.AreAllies(gameObject,supplyCart.gameObject))RemoveCart(supplyCart);
        for (int i = members.Count - 1; i >= 0; i--)
        {
            var member = members[i]; var unit = member != null ? member.GetComponent<Unit>() : null;
            var worker = member != null ? member.GetComponent<ResourcesMove>() : null;
            if (member == null || !member.isActiveAndEnabled || member.Army != this || unit == null || unit.unitHealth <= 0
                || worker == null || (worker.role != ResourcesMove.UnitRole.Soldier&&member.GetComponent<Catapult>()==null)||!FactionMember.AreAllies(gameObject,member.gameObject)) RemoveMember(member);
        }
    }
    public bool TryGetAnchor(out Vector3 point, out Vector3 forward)
    {
        Cleanup(); point = lastAnchor; forward = transform.forward;
        foreach (var member in members)
        {
            var escort = member.GetComponent<ArmyEscort>();
            if(HospitalPatient.Controls(member)||ArmyRejoin.Returning(member.gameObject)||ShipPassenger.Controls(member.gameObject))continue;
            var food = member.GetComponent<UnitEat>();
            if ((escort != null && escort.enabled) || (food != null && food.IsBusy)) continue;
            point = lastAnchor = member.transform.position; forward = member.transform.forward; return true;
        }
        // Не тянем повозку вслед за солдатами, которые сами идут к ней за едой.
        return false;
    }
    public bool TryGetRejoinAnchor(GameObject returningSoldier,out Vector3 point,out Vector3 forward,out bool moving)
    {
        Cleanup();point=lastAnchor;forward=transform.forward;moving=false;
        foreach(var member in members)
        {
            if(member.gameObject==returningSoldier||HospitalPatient.Controls(member)||ArmyRejoin.Returning(member.gameObject)||ShipPassenger.Controls(member.gameObject))continue;
            var food=member.GetComponent<UnitEat>();if(food!=null&&food.IsBusy)continue;
            var nav=member.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if(nav==null||!nav.enabled||!nav.isOnNavMesh)continue;
            point=lastAnchor=member.transform.position;forward=member.transform.forward;
            moving=!nav.isStopped&&(nav.pathPending||(nav.hasPath&&nav.remainingDistance>nav.stoppingDistance+.25f));
            return true;
        }
        // Если все лечатся, сохраняем последнее место армии, а не положение больницы.
        return members.Count>0;
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (Time.time < nextWarning) return; nextWarning = Time.time + 1f; Cleanup();
        TryGetAnchor(out _,out _);
        int fieldHungry = 0, fieldMembers = 0; bool outOfRange = false;
        foreach (var member in members)
        {
            var food = member.GetComponent<UnitEat>();
            if(HospitalPatient.Inside(member.gameObject)||ShipPassenger.Aboard(member.gameObject))continue;
            if (food == null || food.mode != UnitEat.FeedingMode.Expedition) continue;
            fieldMembers++;
            if (!food.WantEat) continue;
            fieldHungry++;
            if (supplyCart != null && !supplyCart.CanSupply(food)) outOfRange = true;
        }
        string warning = members.Count == 0 || fieldMembers == 0 ? "" : supplyCart == null ? "В походе нужна снабженческая повозка"
            : !supplyCart.isActiveAndEnabled ? "Снабженческая повозка недоступна"
            : fieldHungry > 0 && supplyCart.AvailableMeals == 0 ? $"Армия голодает: {fieldHungry}. В повозке нет еды!"
            : fieldHungry > 0 && outOfRange ? $"Голодных бойцов: {fieldHungry}. Повозка вне радиуса снабжения!"
            : supplyCart.AvailableMeals == 0 ? "В снабженческой повозке закончилась еда"
            : !supplyCart.CanMove ? "Повозка не может двигаться: нужна живая привязанная лошадь"
            : fieldHungry > 0 ? $"Голодных бойцов: {fieldHungry}. Еда доступна у повозки" : "";
        if (warning == Warning) return;
        Warning = warning; WarningChanged?.Invoke(warning);
        if (!string.IsNullOrEmpty(warning)) Debug.LogWarning("[" + armyName + "] " + warning, this);
    }
    private void OnDisable()
    {
        foreach (var member in new List<ArmyMember>(members)) RemoveMember(member);
        if (supplyCart != null) RemoveCart(supplyCart);
    }
}
