using System.Collections.Generic;
using UnityEngine;

/// <summary>Принадлежность объекта, независимо от профессии, тегов и автоматической атаки.</summary>
[DisallowMultipleComponent]
public class FactionMember : MonoBehaviour
{
    public static readonly List<FactionMember> All=new List<FactionMember>();
    [SerializeField] private Faction faction=Faction.Player;
    public Faction Side=>faction;
    private Faction registeredFaction;
    private bool automaticallyAssigned;
    public bool IsAlly(FactionMember other)=>other!=null&&FactionRules.AreAllies(faction,other.faction);
    public bool IsEnemy(FactionMember other)=>other!=null&&FactionRules.AreEnemies(faction,other.faction);
    public void SetFaction(Faction value){automaticallyAssigned=false;faction=value;registeredFaction=value;RefreshControlRegistration();}
    public static FactionMember Ensure(GameObject obj)
    {
        if(obj==null)return null;
        var member=obj.GetComponent<FactionMember>();
        if(member!=null)
        {
            if(member.automaticallyAssigned){member.faction=Infer(obj);member.RefreshControlRegistration();}
            return member;
        }
        Faction inferred=Infer(obj);
        member=obj.AddComponent<FactionMember>();member.SetFaction(inferred);member.automaticallyAssigned=true;return member;
    }
    private static Faction Infer(GameObject obj)
    {
        var worker=obj.GetComponent<ResourcesMove>();
        var attack=obj.GetComponent<AttackController>();
        bool ownedWorker=(worker!=null&&worker.role!=ResourcesMove.UnitRole.Soldier)||obj.GetComponent<CartHorse>()!=null||obj.GetComponent<FoodCart>()!=null;
        bool hostile=obj.GetComponent<EnemyAi>()!=null||obj.GetComponent<BanditCamp>()!=null||obj.GetComponent<BanditRaider>()!=null;
        bool legacyEnemy=(obj.CompareTag("Enemy")||(attack!=null&&!attack.LegacyPlayerFlag))&&obj.GetComponent<UnityEngine.AI.NavMeshAgent>()!=null;
        bool entity=worker!=null||obj.GetComponent<Unit>()!=null||IsBuilding(obj);
        return FactionRules.InferLegacy(IsResource(obj),hostile,ownedWorker,legacyEnemy,entity);
    }
    private static bool IsBuilding(GameObject obj)
        =>obj.GetComponent<BuildingHealth>()!=null||obj.GetComponent<_Game.Scripts.UI.WarehouseStorage>()!=null
        ||obj.GetComponent<RecipeBuilding>()!=null||obj.GetComponent<WorkplaceHiring>()!=null||obj.GetComponent<Tavern>()!=null
        ||obj.GetComponent<Sawmill>()!=null||obj.GetComponent<StoneHouse>()!=null||obj.GetComponent<Fishing>()!=null
        ||obj.GetComponent<MiningHouse>()!=null||obj.GetComponent<FarmHouse>()!=null||obj.GetComponent<GatheringHouse>()!=null||obj.GetComponent<HousesScript>()!=null;
    public static bool IsResource(GameObject obj)
    {
        if(obj==null)return false;
        if(obj.GetComponent<WildlifeAnimal>()!=null||obj.GetComponent<FoodResourceSource>()!=null||obj.GetComponent<OreDeposit>()!=null||
            obj.GetComponent<FishingSpot>()!=null||obj.GetComponent<StoneQuarry>()!=null||obj.GetComponent<FarmField>()!=null||obj.GetComponent<LootCache>()!=null)return true;
        var unit=obj.GetComponent<Unit>();
        return unit!=null&&(unit.woodReward>0||unit.stoneReward>0||unit.wheatReward>0||unit.fishReward>0);
    }
    public static FactionMember Find(GameObject obj)
    {
        if(obj==null)return null;
        var member=obj.GetComponentInParent<FactionMember>();if(member!=null)return member;
        var unit=obj.GetComponentInParent<Unit>();if(unit!=null)return Ensure(unit.gameObject);
        var building=obj.GetComponentInParent<BuildingHealth>();if(building!=null)return Ensure(building.gameObject);
        if(IsBuilding(obj))return Ensure(obj);
        return null;
    }
    public static Faction Of(GameObject obj){var member=Find(obj);return member!=null?member.Side:Faction.Neutral;}
    public static void Set(GameObject obj,Faction side){var member=Ensure(obj);if(member!=null)member.SetFaction(side);}
    public static bool IsPlayer(GameObject obj)=>FactionRules.IsControllable(Of(obj));
    public static bool AreEnemies(GameObject a,GameObject b)=>a!=null&&b!=null&&!HospitalPatient.Inside(a)&&!HospitalPatient.Inside(b)&&FactionRules.AreEnemies(Of(a),Of(b));
    public static bool AreAllies(GameObject a,GameObject b)=>a!=null&&b!=null&&FactionRules.AreAllies(Of(a),Of(b));
    public static bool CanUse(GameObject actor,GameObject building)=>AreAllies(actor,building);
    public void RefreshControlRegistration()
    {
        var manager=ManagerGO.Instance;var unit=GetComponent<Unit>();
        if(unit==null||!unit.HasStarted)return;
        bool controllable=isActiveAndEnabled&&unit.enabled&&unit.unitHealth>0&&FactionRules.IsControllable(faction);
        var population=GetComponent<UnitAdd>();
        if(population!=null&&UnitManager.Instance!=null)
        {
            if(controllable&&population.isActiveAndEnabled)UnitManager.Instance.RegisterUnit(population);
            else UnitManager.Instance.UnregisterUnit(population);
        }
        if(manager==null)return;
        controllable=controllable&&!HospitalPatient.Inside(gameObject);
        if(controllable){if(!manager.allUnitsList.Contains(gameObject))manager.allUnitsList.Add(gameObject);}
        else
        {
            manager.allUnitsList.Remove(gameObject);manager.unitsSelected.Remove(gameObject);
            var indicator=transform.Find("Indicator");if(indicator!=null)indicator.gameObject.SetActive(false);
        }
    }
    private void OnEnable(){if(!All.Contains(this))All.Add(this);registeredFaction=faction;RefreshControlRegistration();}
    private void Start(){if(automaticallyAssigned){faction=Infer(gameObject);registeredFaction=faction;RefreshControlRegistration();}}
    private void OnDisable(){All.Remove(this);RefreshControlRegistration();}
    private void Update(){
        if(SaveGameManager.IsLoading)return;if(registeredFaction!=faction){registeredFaction=faction;RefreshControlRegistration();}}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry(){All.Clear();}
}
