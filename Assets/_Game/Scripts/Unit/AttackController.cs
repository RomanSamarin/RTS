using UnityEngine;

public class AttackController : MonoBehaviour
{
    public Transform targetToAttack;
    public int unitDamage;
    [Tooltip("Общая дистанция подхода, анимации и удара ближнего боя.")]
    [Min(.2f)] public float meleeRange=2f;
    [Tooltip("Время от начала замаха до урона; подберите под свой клип атаки.")]
    [Min(0)] public float strikeWindup=.3f;
    [Tooltip("Максимум противников ближнего боя, одновременно атакующих ЭТОГО юнита.")]
    [Min(1)] public int maxMeleeAttackers=6;
    private float nextTargetCheck;
    [Tooltip("Автоматически выбирать врага при входе в радиус. Не определяет принадлежность.")]
    public bool autoAcquireTargets=true;
    [Tooltip("Урон наносит внешний скрипт EnemyAi.")]
    public bool damageHandledExternally;
    [SerializeField,HideInInspector,UnityEngine.Serialization.FormerlySerializedAs("IsPlayer")]
    private bool legacyPlayerFlag=true;
    internal bool LegacyPlayerFlag=>legacyPlayerFlag;
    private void Awake(){FactionMember.Ensure(gameObject);}
    private void TryAcquire(Collider other)
    {
        if(!autoAcquireTargets||targetToAttack!=null||GetComponent<Archer>()!=null||GetComponent<EnemyAi>()!=null||
            GetComponent<FoodCart>()!=null||GetComponent<CartHorse>()!=null||UnitEat.ControlsMovement(this))return;
        var command=GetComponent<GOScript>();if(command!=null&&(command.isCommandToMove||command.IsRetreating))return;
        var candidate=FactionMember.Find(other.gameObject);
        if(candidate==null||!FactionMember.AreEnemies(gameObject,candidate.gameObject))return;
        if(!MilitaryOrders.AllowsTarget(gameObject,candidate.transform))return;
        if(BattleCombatant.Defends(this)&&GetComponent<Archer>()==null
            &&Vector3.Distance(transform.position,CombatPoint(candidate.transform,transform.position))>meleeRange)return;
        var unit=candidate.GetComponent<Unit>();var building=candidate.GetComponent<BuildingHealth>();
        if(unit!=null&&unit.unitHealth>0&&MeleeApproach.CanTarget(gameObject,unit.transform,true))targetToAttack=unit.transform;
        else if(building!=null&&!building.IsDead)targetToAttack=building.transform;
    }
    private void OnTriggerEnter(Collider other){TryAcquire(other);}
    private void OnTriggerStay(Collider other){TryAcquire(other);}
    public static Vector3 CombatPoint(Transform target,Vector3 from)
    {
        var camp=target.GetComponentInParent<BanditCamp>();if(camp!=null)return camp.CombatPoint(from);
        var building=target.GetComponentInParent<BuildingHealth>();
        if(building==null)return target.position;
        var body=BuildingRoadAccess.SolidBody(building.gameObject);
        return body!=null?body.bounds.ClosestPoint(from):building.transform.position;
    }
    // Приказ атаки не отменяется из-за выхода одного из коллайдеров цели из триггера.
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        var command=GetComponent<GOScript>();var role=GetComponent<ResourcesMove>();
        if(command!=null&&command.IsRetreating){targetToAttack=null;return;}
        if(Time.time>=nextTargetCheck&&autoAcquireTargets&&role!=null&&role.role==ResourcesMove.UnitRole.Soldier
            &&GetComponent<Archer>()==null&&GetComponent<EnemyAi>()==null&&!UnitEat.ControlsMovement(this)
            &&(command==null||(!command.isCommandToMove&&command.CurrentOrder!=MovementOrderState.Order.Combat)))
        {
            nextTargetCheck=Time.time+.5f;
            if(targetToAttack!=null&&!MeleeApproach.CanTarget(gameObject,targetToAttack))targetToAttack=null;
            if(targetToAttack==null)
            {
                float best=10f*10f;Transform nearest=null;
                foreach(var member in FactionMember.All)
                {
                    if(member==null||!FactionMember.AreEnemies(gameObject,member.gameObject)||!MilitaryOrders.AllowsTarget(gameObject,member.transform))continue;
                    var unit=member.GetComponent<Unit>();if(unit==null||unit.unitHealth<=0)continue;
                    float distance=(transform.position-member.transform.position).sqrMagnitude;if(distance>=best)continue;
                    if(!MeleeApproach.CanTarget(gameObject,member.transform))continue;
                    best=distance;nearest=member.transform;
                }
                if(nearest!=null&&MeleeApproach.CanTarget(gameObject,nearest,true))targetToAttack=nearest;
            }
        }
        if(targetToAttack==null)return;
        // Старые рабочие используют эту же ссылку для возвращения к зданию.
        var worker=GetComponent<ResourcesMove>();
        bool workDestination=worker!=null&&worker.role!=ResourcesMove.UnitRole.Soldier
            &&targetToAttack.GetComponentInParent<Unit>()==null&&GetComponent<EnemyAi>()==null;
        if(!targetToAttack.gameObject.activeInHierarchy||(!workDestination&&FactionMember.AreAllies(gameObject,targetToAttack.gameObject)))targetToAttack=null;
    }
    private void OnDrawGizmosSelected()
    {Gizmos.color=Color.yellow;Gizmos.DrawWireSphere(transform.position,10f);}
}
