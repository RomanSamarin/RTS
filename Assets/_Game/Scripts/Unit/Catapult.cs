using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Unit),typeof(NavMeshAgent),typeof(AttackController))]
public class Catapult : MonoBehaviour
{
    public float moveSpeed=1.8f,minimumRange=8,maximumRange=40,deploySeconds=2,reloadSeconds=8;
    public float projectileSpeed=12,arcHeight=10,splashRadius=3;
    public int buildingDamage=90,unitDamage=20;
    public Transform launchPoint;
    public Transform throwingArm;
    public SiegeProjectile projectilePrefab;
    [Header("Анимация")]
    public Animator animator;
    [Min(.05f)] public float attackAnimationSeconds = .6f;
    public bool autoAttackBuildings=true,debugSiege=true;
    public string Status { get; private set; }="Готова";
    private AttackController attack;private GOScript command;private NavMeshAgent agent;
    private float readyAt,nextShot,nextSearch,nextMove;
    private Transform deployedTarget;
    private Quaternion restingArm;
    private bool hasRestingArm;
    private float firedAt=-100;
    private bool attackMoving,issuingMove;
    private enum AnimationState { Unset, Hidden, Idle, Moving, Attacking }
    [System.NonSerialized] private AnimationState lastAnimationState;
    [System.NonSerialized] private Animator lastAnimator;
    [System.NonSerialized] private RuntimeAnimatorController lastAnimationController;
    private Unit unit;
    public bool HoldingPosition { get; private set; }
    private Vector3 attackMoveGoal;
    private void OnManualOrder(){if(!issuingMove){attackMoving=false;HoldingPosition=false;}}
    public void StopOrder()
    {attackMoving=false;command.StopMoving();attack.targetToAttack=null;command.NotifyMilitaryOrder();deployedTarget=null;Status="Остановлена";}
    public void HoldOrder(){StopOrder();HoldingPosition=true;}
    public void AttackMoveTo(Vector3 point)
    {StopOrder();attackMoveGoal=point;attackMoving=true;MoveToGoal();}
    private void MoveToGoal()
    {issuingMove=true;try{command.MoveTo(attackMoveGoal);}finally{issuingMove=false;}}
    private Transform FindBuilding()
    {
        float best=maximumRange;Transform result=null;
        foreach(var building in BuildingManager.AllBuildings)
        {
            if(building==null||!Alive(building.transform))continue;
            float distance=Vector3.Distance(transform.position,AttackController.CombatPoint(building.transform,transform.position));
            if(!SiegeRules.InRange(distance,minimumRange,best))continue;
            best=distance;result=building.transform;
        }
        return result;
    }
    private void LateUpdate()
    {
        if(SaveGameManager.IsLoading)return;
        UpdateAnimation();
        if(ShipPassenger.Controls(gameObject))return;
        if(throwingArm==null)return;
        if(!hasRestingArm){restingArm=throwingArm.localRotation;hasRestingArm=true;}
        float phase=(Time.time-firedAt)/.6f;
        throwingArm.localRotation=phase>=0&&phase<1?restingArm*Quaternion.AngleAxis(-60*Mathf.Sin(phase*Mathf.PI),Vector3.right):restingArm;
    }
    private void UpdateAnimation()
    {
        if (animator == null)
            animator = unit != null && unit.animator != null ? unit.animator : GetComponentInChildren<Animator>(true);
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
        {
            lastAnimationState = AnimationState.Unset;
            return;
        }

        var state = AnimationState.Hidden;
        if (unit != null && unit.unitHealth > 0f && !ShipPassenger.Aboard(gameObject))
        {
            bool moving = agent.enabled && agent.isOnNavMesh && !agent.isStopped && agent.velocity.sqrMagnitude > .0025f;
            float sinceShot = Time.time - firedAt;
            bool firing = sinceShot >= 0f && sinceShot < Mathf.Max(.05f, attackAnimationSeconds);
            state = moving ? AnimationState.Moving : firing ? AnimationState.Attacking : AnimationState.Idle;
        }
        if (state == lastAnimationState && animator == lastAnimator && animator.runtimeAnimatorController == lastAnimationController) return;
        lastAnimationState = state;
        lastAnimator = animator;
        lastAnimationController = animator.runtimeAnimatorController;
        WorkerAnimation.Bool(animator, "isMoving", state == AnimationState.Moving);
        WorkerAnimation.Bool(animator, "idle", state == AnimationState.Idle);
        WorkerAnimation.Bool(animator, "isAttacking", state == AnimationState.Attacking);
    }
    private void Awake()
    {
        attack=GetComponent<AttackController>();agent=GetComponent<NavMeshAgent>();command=GetComponent<GOScript>();
        unit=GetComponent<Unit>();
        if(command==null)command=gameObject.AddComponent<GOScript>();
        var worker=GetComponent<ResourcesMove>();if(worker==null)worker=gameObject.AddComponent<ResourcesMove>();
        worker.role=ResourcesMove.UnitRole.Transport;
        attack.autoAcquireTargets=false;attack.damageHandledExternally=true;attack.unitDamage=0;
        command.ManualOrderIssued+=OnManualOrder;
    }
    private void OnDestroy(){if(command!=null)command.ManualOrderIssued-=OnManualOrder;}
    private void Start(){command.SetBaseSpeed(moveSpeed);agent.areaMask&=~WallNavigation.AreaMask;}
    private bool Alive(Transform target)
    {
        if(target==null||!target.gameObject.activeInHierarchy||!FactionMember.AreEnemies(gameObject,target.gameObject))return false;
        var building=target.GetComponent<BuildingHealth>();var unit=target.GetComponent<Unit>();
        return building!=null?!building.IsDead:unit!=null&&unit.unitHealth>0;
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if(ShipPassenger.Controls(gameObject))return;
        if(GetComponent<Unit>().unitHealth<=0||!agent.enabled||!agent.isOnNavMesh)return;
        if(command.IsRetreating)
        {attack.targetToAttack=null;deployedTarget=null;readyAt=Time.time+deploySeconds;Status="Отступает";return;}
        if(attackMoving&&command.isCommandToMove&&Time.time>=nextSearch)
        {
            nextSearch=Time.time+.75f;var obstacle=FindBuilding();
            if(obstacle!=null){command.StopMoving();attack.targetToAttack=obstacle;}
        }
        if(command.isCommandToMove){deployedTarget=null;readyAt=Time.time+deploySeconds;Status="Перемещается";return;}
        var target=attack.targetToAttack;
        if(!Alive(target)){attack.targetToAttack=null;target=null;}
        bool manual=command.CurrentOrder==MovementOrderState.Order.Combat;
        if(target==null&&(autoAttackBuildings||attackMoving)&&Time.time>=nextSearch)
        {
            nextSearch=Time.time+.75f;target=FindBuilding();
            attack.targetToAttack=target;
        }
        if(target==null)
        {
            deployedTarget=null;
            if(attackMoving)
            {
                if(Vector3.Distance(transform.position,attackMoveGoal)<.7f){attackMoving=false;Status="Приказ выполнен";}
                else if(Time.time>=nextMove){nextMove=Time.time+1;MoveToGoal();}
            }
            else Status="Ожидает цель";
            return;
        }
        var point=AttackController.CombatPoint(target,transform.position);float range=Vector3.Distance(transform.position,point);
        if(range<minimumRange){if(agent.hasPath)agent.ResetPath();deployedTarget=null;Status="Цель слишком близко";if(!manual)attack.targetToAttack=null;return;}
        if(range>maximumRange)
        {
            deployedTarget=null;Status="Выходит на дистанцию";
            if(!manual){attack.targetToAttack=null;return;}
            if(Time.time<nextMove)return;nextMove=Time.time+1;
            var desired=point+(transform.position-point).normalized*(maximumRange*.8f);
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            var path=new NavMeshPath();
            if(NavMesh.SamplePosition(desired,out var hit,3,filter)&&agent.CalculatePath(hit.position,path)&&path.status==NavMeshPathStatus.PathComplete)
            {agent.isStopped=false;agent.SetPath(path);}else Status="Нет пути к позиции стрельбы";
            return;
        }
        if(agent.hasPath)agent.ResetPath();
        if(deployedTarget!=target){deployedTarget=target;readyAt=Time.time+Mathf.Max(0,deploySeconds);}
        var facing=target.position-transform.position;facing.y=0;
        if(facing.sqrMagnitude>.01f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(facing),90*Time.deltaTime);
        if(Time.time<readyAt){Status="Разворачивается";return;}
        if(facing.sqrMagnitude>.01f&&Vector3.Angle(transform.forward,facing)>10){Status="Наводится";return;}
        if(Time.time<nextShot){Status="Перезаряжается";return;}
        var start=launchPoint!=null?launchPoint.position:transform.position+Vector3.up*2+transform.forward;
        var body=target.GetComponent<BuildingHealth>();var collider=body!=null?BuildingRoadAccess.SolidBody(body.gameObject):null;
        var end=collider!=null?collider.bounds.center:target.position+Vector3.up*.7f;
        var projectile=projectilePrefab!=null?Instantiate(projectilePrefab,start,Quaternion.identity):SiegeProjectile.Create(start);
        projectile.Launch(start,end,Mathf.Max(.5f,Vector3.Distance(start,end)/Mathf.Max(1,projectileSpeed)),arcHeight,splashRadius,buildingDamage,unitDamage,gameObject);
        projectile.gameObject.SetActive(true);firedAt=Time.time;
        nextShot=Time.time+Mathf.Max(.5f,reloadSeconds);Status="Выстрел";
        if(debugSiege)Debug.Log($"[Катапульта] {name}: выстрел по {target.name}.",this);
    }
    public static GameObject CreatePlaceholder(Vector3 position,Quaternion rotation)
    {
        var root=new GameObject("Catapult");root.transform.SetPositionAndRotation(position,rotation);root.layer=7;
        var nav=root.AddComponent<NavMeshAgent>();nav.radius=1.3f;nav.height=2.5f;nav.speed=1.8f;
        var body=root.AddComponent<BoxCollider>();body.center=new Vector3(0,1,0);body.size=new Vector3(2.2f,1.5f,3);
        var unit=root.AddComponent<Unit>();unit.unitMaxHealth=250;unit.SetInitialHealthRatio(1);
        root.AddComponent<GOScript>();root.AddComponent<AttackController>();
        var worker=root.AddComponent<ResourcesMove>();worker.role=ResourcesMove.UnitRole.Transport;
        var catapult=root.AddComponent<Catapult>();
        Part(root,"Frame",new Vector3(0,.9f,0),new Vector3(2,.4f,3));
        catapult.throwingArm=Part(root,"ThrowingArm",new Vector3(0,1.8f,0),new Vector3(.3f,.3f,3.2f));
        foreach(float x in new[]{-1.1f,1.1f})foreach(float z in new[]{-1f,1f})
            Part(root,"Wheel",new Vector3(x,.5f,z),new Vector3(.25f,1,1));
        return root;
    }
    private static Transform Part(GameObject root,string name,Vector3 position,Vector3 scale)
    {
        var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.layer=root.layer;
        part.transform.SetParent(root.transform,false);part.transform.localPosition=position;part.transform.localScale=scale;
        var collider=part.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        return part.transform;
    }
}
