using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public class ManagerGO : MonoBehaviour
{
    public static ManagerGO Instance { get; set; }

    public List<GameObject> allUnitsList = new List<GameObject>();
    public List<GameObject> unitsSelected = new List<GameObject>();
    public LayerMask clickable;
    public LayerMask Ground;
    public GameObject groundMarker;
    public LayerMask attackable;
    public bool attackCursorVisible;
    public GOScript GOScript;

    [Header("Движение группы")]
    [Tooltip("Расстояние между точками, в которые встают юниты при движении группой")]
    public float groupSpacing = 1.7f;

    [Tooltip("Насколько далеко искать ближайшую точку NavMesh для позиции юнита")]
    public float navMeshSnapRadius = 2f;

    private Camera cam;
    [Header("Боевые приказы")]
    public KeyCode attackMoveKey=KeyCode.T;
    public KeyCode holdPositionKey=KeyCode.H;
    public KeyCode stopOrderKey=KeyCode.X;
    public KeyCode defenseFacingKey=KeyCode.Y;
    public KeyCode retreatKey=KeyCode.V;
    public KeyCode patrolKey=KeyCode.P;
    [Tooltip("Allow keyboard shortcuts in addition to the on-screen command buttons.")]
    public bool useMilitaryOrderHotkeys;
    public SelectedMilitaryCommandPanel militaryCommandPanel;
    public bool InstallLadderAt(WallSegment wall, Vector3 point)
    {
        foreach(var selected in unitsSelected)
        {
            var unit=ControllableRoot(selected);
            if(!SiegeLadderInstaller.CanInstall(unit))continue;
            var installer=unit.GetComponent<SiegeLadderInstaller>();
            if(installer!=null&&installer.IsBusy)continue;
            if(installer==null)installer=unit.AddComponent<SiegeLadderInstaller>();
            if(installer.TryAssign(wall,point))return true;
        }
        MilitaryDebug("Нельзя установить лестницу: проверьте расстояние, свободное место, путь и запас в повозке.");return false;
    }
    public bool IsChoosingPatrol { get; private set; }
    private readonly List<Vector3> patrolPoints=new List<Vector3>();
    private readonly List<GameObject> patrolUnits=new List<GameObject>();
    public bool IsChoosingRetreat { get; private set; }
    public bool IsChoosingDefenseFacing { get; private set; }
    public bool debugMilitaryOrders=true;
    private void MilitaryDebug(string message)
    {if(debugMilitaryOrders)Debug.Log("[Боевые приказы / ввод] "+message,this);}
    public bool IsChoosingAttackMove { get; private set; }
    private int militaryInputEndedFrame=-1;
    public bool BlocksMilitarySelection=>IsChoosingPatrol||IsChoosingRetreat||IsChoosingAttackMove||IsChoosingDefenseFacing||militaryInputEndedFrame==Time.frameCount;
    private void CancelPatrolSelection()
    {IsChoosingPatrol=false;patrolPoints.Clear();patrolUnits.Clear();}
    public void BeginPatrol()
    {
        bool networked = RtsNetworkSession.BlocksOfflineCommands;
        if (networked && !CanIssueNetworkGroundOrder()) return;
        CancelPatrolSelection();IsChoosingRetreat=false;IsChoosingAttackMove=false;IsChoosingDefenseFacing=false;
        foreach(var selected in unitsSelected)
        {
            var unit=ControllableRoot(selected);
            if (unit == null || patrolUnits.Contains(unit)) continue;
            if (networked ? unit.GetComponent<RtsNetworkUnit>()?.IsMeleeFighter == true : MilitaryOrders.Ensure(unit) != null)
                patrolUnits.Add(unit);
        }
        IsChoosingPatrol=patrolUnits.Count>0;
        MilitaryDebug(IsChoosingPatrol?"Патруль: задайте минимум две точки ЛКМ. Shift+ЛКМ добавляет промежуточную точку, обычный ЛКМ завершает маршрут.":"Выберите солдат для патруля.");
    }
    private void CompletePatrol()
    {
        if (RtsNetworkSession.BlocksOfflineCommands)
        {
            IssueNetworkPatrol(patrolPoints.ToArray());
            return;
        }
        int columns=Mathf.Max(1,Mathf.CeilToInt(Mathf.Sqrt(patrolUnits.Count)));
        int rows=Mathf.CeilToInt((float)patrolUnits.Count/columns);
        float spacing=groupSpacing;
        foreach(var unit in patrolUnits)
        {if(unit!=null&&unit.TryGetComponent<NavMeshAgent>(out var agent))spacing=Mathf.Max(spacing,agent.radius*2+.2f);}
        for(int i=0;i<patrolUnits.Count;i++)
        {
            var unit=patrolUnits[i];if(ControllableRoot(unit)==null)continue;
            var agent=unit.GetComponent<NavMeshAgent>();if(agent==null)continue;
            var route=new List<Vector3>();
            var offset=new Vector3(i%columns-(columns-1)*.5f,0,i/columns-(rows-1)*.5f)*spacing;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            foreach(var point in patrolPoints)
            {
                if(!NavMesh.SamplePosition(point+offset,out var hit,navMeshSnapRadius,filter))break;
                route.Add(hit.position);
            }
            if(route.Count!=patrolPoints.Count||!(MilitaryOrders.Ensure(unit)?.Patrol(route)??false))
                MilitaryDebug($"{unit.name}: маршрут недоступен, предыдущий приказ сохранён.");
        }
        CancelPatrolSelection();militaryInputEndedFrame=Time.frameCount;
    }
    public void BeginRetreat()
    {
        bool networked = RtsNetworkSession.BlocksOfflineCommands;
        if (networked && !CanIssueNetworkGroundOrder()) return;
        CancelPatrolSelection();
        IsChoosingAttackMove=false;IsChoosingDefenseFacing=false;
        IsChoosingRetreat=networked ? HasNetworkFightersSelected() : unitsSelected.Count>0;
        if (!IsChoosingRetreat) MilitaryDebug("Не выбраны войска или корабли для отступления.");
        else if (ShipNavigation.SelectedShips(unitsSelected).Count > 0)
            MilitaryDebug("Отступление флота: укажите точку ЛКМ по воде.");
        else MilitaryDebug("Отступление: укажите точку ЛКМ по земле.");
    }
    public void RetreatSelectedTo(Vector3 destination)
    {
        if (RtsNetworkSession.BlocksOfflineCommands)
        {
            IssueNetworkRetreat(destination);
            return;
        }
        var troops=new List<GameObject>();float spacing=groupSpacing;
        foreach(var selected in unitsSelected)
        {
            var unit=ControllableRoot(selected);if(unit==null||troops.Contains(unit))continue;
            var role=unit.GetComponent<ResourcesMove>();
            if(unit.GetComponent<Catapult>()==null&&(role==null||role.role!=ResourcesMove.UnitRole.Soldier))continue;
            var agent=unit.GetComponent<NavMeshAgent>();
            if(agent==null||unit.GetComponent<GOScript>()==null)continue;
            troops.Add(unit);spacing=Mathf.Max(spacing,agent.radius*2+.2f);
        }
        int columns=Mathf.Max(1,Mathf.CeilToInt(Mathf.Sqrt(troops.Count)));
        int rows=Mathf.CeilToInt((float)troops.Count/columns);
        for(int i=0;i<troops.Count;i++)
        {
            var unit=troops[i];var agent=unit.GetComponent<NavMeshAgent>();
            Vector3 point=destination+new Vector3(i%columns-(columns-1)*.5f,0,i/columns-(rows-1)*.5f)*spacing;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            if(NavMesh.SamplePosition(point,out var ground,navMeshSnapRadius,filter)&&unit.GetComponent<GOScript>().RetreatTo(ground.position))
                MilitaryDebug($"{unit.name}: отступает к {ground.position}.");
            else MilitaryDebug($"{unit.name}: нет полного пути отступления. Текущий приказ сохранён.");
        }
        IsChoosingRetreat=false;militaryInputEndedFrame=Time.frameCount;
    }
    public void BeginDefenseFacing()
    {
        bool networked = RtsNetworkSession.BlocksOfflineCommands;
        if (networked && !CanIssueNetworkGroundOrder()) return;
        CancelPatrolSelection();
        IsChoosingRetreat=false;
        IsChoosingAttackMove=false;
        IsChoosingDefenseFacing=networked ? HasNetworkFightersSelected() : unitsSelected.Count>0;
        MilitaryDebug(IsChoosingDefenseFacing?"Укажите ЛКМ направление обороны щитоносцев и копейщиков.":"Не выбраны бойцы для поворота обороны.");
    }
    public void FaceDefenseAt(Vector3 point)
    {
        if (RtsNetworkSession.BlocksOfflineCommands)
        {
            IssueNetworkDefenseFacing(point);
            return;
        }
        var defenders=new List<GameObject>();Vector3 center=Vector3.zero;
        foreach(var unit in unitsSelected)
        {
            if(ControllableRoot(unit)==null)continue;
            var kind=BattleCombatant.TypeOf(unit);
            if(kind!=TroopType.Swordsman&&kind!=TroopType.Spearman)continue;
            defenders.Add(unit);center+=unit.transform.position;
        }
        if(defenders.Count==0){MilitaryDebug("В выделении нет мечников или копейщиков.");return;}
        center/=defenders.Count;var forward=point-center;forward.y=0;
        if(forward.sqrMagnitude<.01f){MilitaryDebug("Точка направления слишком близка к центру отряда.");return;}
        int group=FormationHold.BeginGroup();
        foreach(var defender in defenders)MilitaryOrders.Ensure(defender)?.HoldFacing(forward.normalized,group);
        MilitaryDebug($"Направление обороны изменено для {defenders.Count} бойцов.");
    }
    public void BeginAttackMove()
    {
        if (RtsNetworkSession.BlocksOfflineCommands && !CanIssueNetworkGroundOrder()) return;
        CancelPatrolSelection();
        IsChoosingRetreat=false;
        IsChoosingDefenseFacing=false;
        IsChoosingAttackMove=RtsNetworkSession.BlocksOfflineCommands
            ? HasNetworkFightersSelected()
            : unitsSelected.Count>0;
        MilitaryDebug(IsChoosingAttackMove?$"Выбор точки атаки. Выделено: {unitsSelected.Count}. Нажмите ЛКМ по земле.":"Атака не начата: нет выделенных юнитов.");
    }
    public void HoldSelected()
    {
        if (TryHandleNetworkStance(true)) return;
        CancelPatrolSelection();
        IsChoosingRetreat=false;
        IsChoosingDefenseFacing=false;
        IsChoosingAttackMove=false;int group=FormationHold.BeginGroup();
        MilitaryDebug($"Удержание позиции. Выделено: {unitsSelected.Count}.");
        foreach(var unit in new List<GameObject>(unitsSelected))
        {
            if(ControllableRoot(unit)==null)continue;
            if(unit.GetComponent<ShipNavigation>() is ShipNavigation ship){ship.Stop();continue;}
            var orders=MilitaryOrders.Ensure(unit);
            if(orders==null){var machine=unit.GetComponent<Catapult>();if(machine!=null)machine.HoldOrder();else MilitaryDebug($"{unit.name}: пропущен — нужна роль Soldier и GOScript.");continue;}
            orders.Hold();
            if(orders.Current==MilitaryOrders.Order.Hold)FormationHold.SetPoint(unit,unit.transform.position,unit.transform.forward,group);
        }
    }
    public void StopSelected()
    {
        if (TryHandleNetworkStance(false)) return;
        CancelPatrolSelection();
        IsChoosingRetreat=false;
        IsChoosingDefenseFacing=false;
        IsChoosingAttackMove=false;
        MilitaryDebug($"Остановка. Выделено: {unitsSelected.Count}.");
        foreach(var unit in new List<GameObject>(unitsSelected))
            if(ControllableRoot(unit)!=null)
            {
                if(unit.GetComponent<ShipNavigation>() is ShipNavigation ship){ship.Stop();continue;}
                var cart=unit.GetComponent<SiegeLadderCart>();
                if(cart!=null){cart.CancelLoading();unit.GetComponent<GOScript>()?.StopMoving();continue;}
                var machine=unit.GetComponent<Catapult>();if(machine!=null)machine.StopOrder();
                else if (unit.TryGetComponent<BatteringRam>(out var ram)) ram.StopOrder();
                else if (unit.TryGetComponent<RamCrewMember>(out var crew) && crew.IsAssigned) crew.Release();
                else MilitaryOrders.Ensure(unit)?.Stop();
            }
    }
    private bool TryHandleNetworkStance(bool hold)
    {
        if (!RtsNetworkSession.BlocksOfflineCommands) return false;
        CancelPatrolSelection();
        IsChoosingAttackMove = IsChoosingRetreat = IsChoosingDefenseFacing = false;
        foreach (var selected in unitsSelected)
        {
            var root = ControllableRoot(selected);
            if (root == null || !root.TryGetComponent<RtsNetworkUnit>(out var unit)) continue;
            if (hold) unit.RequestHold();
            else unit.RequestStop();
        }
        return true;
    }

    private bool HandleMilitaryInput(Ray ray,bool pointerOverUI)
    {
        var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
        bool typing=selected!=null&&((selected.GetComponentInParent<TMPro.TMP_InputField>()?.isFocused??false)
            ||(selected.GetComponentInParent<UnityEngine.UI.InputField>()?.isFocused??false));
        if(typing)return IsChoosingPatrol||IsChoosingRetreat||IsChoosingAttackMove||IsChoosingDefenseFacing;
        var buttons = militaryCommandPanel != null && militaryCommandPanel.isActiveAndEnabled && !useMilitaryOrderHotkeys
            ? militaryCommandPanel : null;
        if ((buttons == null || buttons.patrolButton == null) && Input.GetKeyDown(patrolKey)) BeginPatrol();
        if ((buttons == null || buttons.retreatButton == null) && Input.GetKeyDown(retreatKey)) BeginRetreat();
        if ((buttons == null || buttons.holdButton == null) && Input.GetKeyDown(holdPositionKey)) { HoldSelected(); return true; }
        if ((buttons == null || buttons.stopButton == null) && Input.GetKeyDown(stopOrderKey)) { StopSelected(); return true; }
        if ((buttons == null || buttons.attackButton == null) && Input.GetKeyDown(attackMoveKey)) BeginAttackMove();
        if ((buttons == null || buttons.faceDefenseButton == null) && Input.GetKeyDown(defenseFacingKey)) BeginDefenseFacing();
        if (HandlePatrolSelection(ray, pointerOverUI)) return true;
        if(IsChoosingRetreat)
        {
            if(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1)||unitsSelected.Count==0)
            {IsChoosingRetreat=false;militaryInputEndedFrame=Time.frameCount;return true;}
            if (!pointerOverUI && Input.GetMouseButtonDown(0))
            {
                var ships = ShipNavigation.SelectedShips(unitsSelected);
                if (ships.Count > 0)
                {
                    if (!ShipNavigation.TryGetWaterPoint(ray, out var waterPoint))
                        MilitaryDebug("Укажите точку в зоне плавания.");
                    else
                    {
                        int accepted = ShipNavigation.RetreatGroup(ships, waterPoint);
                        MilitaryDebug($"Отступают корабли: {accepted}/{ships.Count}.");
                        if (accepted > 0)
                        {
                            IsChoosingRetreat = false;
                            militaryInputEndedFrame = Time.frameCount;
                        }
                    }
                }
                else if (Physics.Raycast(ray, out var retreatHit, Mathf.Infinity, Ground, QueryTriggerInteraction.Ignore))
                    RetreatSelectedTo(retreatHit.point);
            }
            return true;
        }
        if(IsChoosingDefenseFacing)
        {
            if(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1)||unitsSelected.Count==0)
            {IsChoosingDefenseFacing=false;militaryInputEndedFrame=Time.frameCount;return true;}
            if(!pointerOverUI&&Input.GetMouseButtonDown(0)&&Physics.Raycast(ray,out var facingHit,Mathf.Infinity,Ground,QueryTriggerInteraction.Ignore))
            {FaceDefenseAt(facingHit.point);IsChoosingDefenseFacing=false;militaryInputEndedFrame=Time.frameCount;}
            return true;
        }
        if(!IsChoosingAttackMove)return false;
        if(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1)||unitsSelected.Count==0)
        {MilitaryDebug("Выбор точки атаки отменён.");IsChoosingAttackMove=false;militaryInputEndedFrame=Time.frameCount;return true;}
        if(!pointerOverUI&&Input.GetMouseButtonDown(0)&&Physics.Raycast(ray,out var hit,Mathf.Infinity,Ground,QueryTriggerInteraction.Ignore))
        {
            MilitaryDebug($"Точка атаки: {hit.point}. Ground Mask: {Ground.value}.");
            int index=0;int columns=Mathf.CeilToInt(Mathf.Sqrt(unitsSelected.Count));
            foreach(var unit in new List<GameObject>(unitsSelected))
            {
                if(ControllableRoot(unit)==null)continue;
                var orders=MilitaryOrders.Ensure(unit);
                if(orders==null&&unit.GetComponent<Catapult>()==null){MilitaryDebug($"{unit.name}: пропущен — нужна роль Soldier и GOScript.");continue;}
                var agent=unit.GetComponent<NavMeshAgent>();if(agent==null)continue;
                Vector3 point=hit.point+new Vector3(index%columns-(columns-1)*.5f,0,index/columns)*groupSpacing;index++;
                var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
                if(NavMesh.SamplePosition(point,out var ground,navMeshSnapRadius,filter))
                {if(orders!=null)orders.AttackMove(ground.position);else unit.GetComponent<Catapult>().AttackMoveTo(ground.position);}
                else MilitaryDebug($"{unit.name}: нет NavMesh возле {point}; Agent Type={agent.agentTypeID}, Area Mask={agent.areaMask}.");
            }
            IsChoosingAttackMove=false;militaryInputEndedFrame=Time.frameCount;
        }
        else if(Input.GetMouseButtonDown(0))MilitaryDebug(pointerOverUI?"Клик по UI: точка атаки не выбрана.":$"Луч не попал в землю. Проверьте слой земли и Ground в ManagerGO (маска {Ground.value}).");
        return true;
    }
    private readonly List<GameObject>[] controlGroups = new List<GameObject>[9];

    private void HandleControlGroups()
    {
        var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (focused != null)
        {
            var tmpInput = focused.GetComponentInParent<TMPro.TMP_InputField>();
            var legacyInput = focused.GetComponentInParent<UnityEngine.UI.InputField>();
            if ((tmpInput != null && tmpInput.isFocused) || (legacyInput != null && legacyInput.isFocused)) return;
        }
        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) return;
        bool save = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool controlPressed = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl);
        for (int i = 0; i < controlGroups.Length; i++)
        {
            var alpha = (KeyCode)((int)KeyCode.Alpha1 + i);
            var keypad = (KeyCode)((int)KeyCode.Keypad1 + i);
            if (!Input.GetKeyDown(alpha) && !Input.GetKeyDown(keypad)
                && !(controlPressed && (Input.GetKey(alpha) || Input.GetKey(keypad)))) continue;
            if (save) SaveControlGroup(i + 1);
            else SelectControlGroup(i + 1);
            return;
        }
    }

    /// <summary>Заменяет состав группы 1–9 текущим выделением. Пустое выделение очищает группу.</summary>
    public void SaveControlGroup(int number)
    {
        if (number < 1 || number > controlGroups.Length) return;
        var group = controlGroups[number - 1] ?? (controlGroups[number - 1] = new List<GameObject>());
        group.Clear();
        foreach (var selected in unitsSelected)
        {
            var unit = ControllableRoot(selected);
            if (unit != null && unit.activeInHierarchy && !group.Contains(unit)) group.Add(unit);
        }
        Debug.Log($"[Группы войск] Группа {number}: сохранено юнитов — {group.Count}.", this);
    }

    public void SelectControlGroup(int number)
    {
        if (number < 1 || number > controlGroups.Length) return;
        var group = controlGroups[number - 1];
        if (group == null) { Debug.Log($"[Группы войск] Группа {number} не назначена. Выделите юнитов и нажмите Ctrl + {number}.", this); return; }
        group.RemoveAll(unit => unit == null || !unit.activeInHierarchy || (ControllableRoot(unit) != unit&&!ShipPassenger.Aboard(unit)&&!HospitalPatient.Inside(unit)));
        if (group.Count == 0) { Debug.Log($"[Группы войск] Группа {number} пуста.", this); return; }
        DeselectAll();
        foreach (var unit in group)
        {
            if(ControllableRoot(unit)!=unit)continue;
            unitsSelected.Add(unit);
            TriggerSelectionIndicator(unit, true);
            EnableUnitMovement(unit, true);
        }
    }

    /// <summary>Смена модели при обучении/увольнении сохраняет членство человека в группах.</summary>
    public void ReplaceControlGroupMember(GameObject previous, GameObject replacement)
    {
        foreach (var group in controlGroups)
        {
            if (group == null) continue;
            int index = group.IndexOf(previous);
            if (index < 0) continue;
            group.RemoveAt(index);
            if (replacement != null && !group.Contains(replacement)) group.Insert(index, replacement);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    void Start()
    {
        cam = Camera.main;
    }

    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (RtsNetworkSession.BlocksOfflineCommands) { UpdateNetworkInput(); return; }
        // Клавиатурное выделение не зависит от камеры, лучей и режима размещения.
        if (!SettingsMenu.BlocksWorldInput) HandleControlGroups();
        // Завершаем приказы на перемещение у всех юнитов (и выделенных, и нет),
        // чтобы по прибытии сбрасывался путь и маркер цели исчезал.
        UpdateMoveCommands();
        if (SettingsMenu.BlocksWorldInput) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        RaycastHit hit;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (groundMarker != null && groundMarker.activeSelf)
        {
            bool anyUnitStillMoving = false;
            foreach (GameObject unit in unitsSelected)
            {
                if (unit == null) continue;
                GOScript move = unit.GetComponent<GOScript>();
                if (move != null && move.isCommandToMove)
                {
                    anyUnitStillMoving = true;
                    break;
                }
            }
            // Если абсолютно все выделенные юниты остановились — выключаем маркер
            if (!anyUnitStillMoving)
            {
                groundMarker.SetActive(false);
            }
        }


        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (Barracks.BlocksWorldInput || RoadBuilder.BlocksWorldInput) {CancelPatrolSelection();IsChoosingRetreat=false;IsChoosingAttackMove=false;IsChoosingDefenseFacing=false;return;}
        if (PlacementSystem.Instance != null && PlacementSystem.Instance.BlocksUnitInput) {CancelPatrolSelection();IsChoosingRetreat=false;IsChoosingAttackMove=false;IsChoosingDefenseFacing=false;return;}

        // Пока включён режим построения строя и есть выделенные юниты,
        // ЛКМ/ПКМ обрабатывает SoldiersFormation. Если выделения нет — даём
        // сначала выбрать юнитов, иначе из режима было бы не выйти.
        if (SoldiersFormation.IsPlacing && unitsSelected.Count > 0)
        {
            CancelPatrolSelection();
            IsChoosingRetreat=false;
            IsChoosingAttackMove=false;
            IsChoosingDefenseFacing=false;
            CursorSelector();
            return;
        }

        if(HandleMilitaryInput(ray,pointerOverUI))return;

        // ЛКМ — выбор юнитов
        if (Input.GetMouseButtonDown(0) && !pointerOverUI)
        {
            if (unitsSelected.Exists(unit=>SiegeLadderInstaller.CanInstall(ControllableRoot(unit)))
                && Physics.Raycast(ray,out var ladderHit,Mathf.Infinity,~0,QueryTriggerInteraction.Ignore)
                && ladderHit.collider.GetComponentInParent<WallSegment>() is WallSegment wall
                && !(wall is WallTower) && wall.GetComponent<WallGate>()==null)
            {
                InstallLadderAt(wall,ladderHit.point);
                militaryInputEndedFrame=Time.frameCount;
                return;
            }
            if (WallGate.TryToggleAt(ray)) return;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickable))
            {
                if (Input.GetKey(KeyCode.LeftShift))
                    MultiSelect(hit.collider.gameObject);
                else
                    SelectByClicking(hit.collider.gameObject);
            }
            else
            {
                if (!Input.GetKey(KeyCode.LeftShift))
                    DeselectAll();
            }
        }

        // ПКМ — атака или движение
        if (Input.GetMouseButtonDown(1) && unitsSelected.Count > 0)
        {
            if (pointerOverUI) return;
            if (TryRamCrewOrder(ray)) return;
            if(ShipTransport.HandleBoardingClick(ray,unitsSelected))return;
            if(ShipNavigation.HandleCommand(ray,unitsSelected))return;
            if(SiegeLadderSite.HandleClimbClick(ray,unitsSelected))return;
            if(TryConstructionOrder(ray))return;
            // Сначала проверяем врагов (но НЕ свои склады и ресурсные объекты — приказ по ним = обычное движение)
            if (TryEnemyUnderCursor(ray,out hit))
            {
                attackCursorVisible = true;

                Transform target = hit.collider.transform;
                foreach (GameObject unit in unitsSelected)
                {
                    // ИСПРАВЛЕНИЕ: Проверяем, не уничтожен ли сам выделенный юнит
                    if (unit == null) continue;

                    GOScript move = unit.GetComponent<GOScript>();
                    if (move != null)
                        move.AttackTarget(target);
                    else
                    {
                        unit.GetComponent<UnitEat>()?.CancelForCommand(false);
                        var ac = unit.GetComponent<AttackController>();
                        if (ac != null) ac.targetToAttack = target;
                    }
                }
            }
            else if (WallWalkway.HandleClick(ray, unitsSelected)) return;
            // Потом проверяем землю
            else if (Physics.Raycast(ray, out hit, Mathf.Infinity, Ground))
            {
                attackCursorVisible = false;

                if (groundMarker != null)
                {
                    groundMarker.transform.position = hit.point;
                    groundMarker.SetActive(false);
                    groundMarker.SetActive(true);
                }

                MoveSelectedUnitsTo(hit.point, hit);
            }
            else
            {
                attackCursorVisible = false;
            }
        }
            CursorSelector();
    }

    private void UpdateNetworkInput()
    {
        var session = RtsNetworkSession.Instance;
        if (!session.Ready || SettingsMenu.BlocksWorldInput || RtsNetworkConstruction.BlocksWorldInput || RoadBuilder.BlocksWorldInput
            || SoldiersFormation.IsPlacing) return;
        HandleControlGroups();
        var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (focused != null && ((focused.GetComponentInParent<TMPro.TMP_InputField>()?.isFocused ?? false)
            || (focused.GetComponentInParent<UnityEngine.UI.InputField>()?.isFocused ?? false))) return;
        var buttons = militaryCommandPanel != null && militaryCommandPanel.isActiveAndEnabled && !useMilitaryOrderHotkeys
            ? militaryCommandPanel : null;
        if ((buttons == null || buttons.holdButton == null) && Input.GetKeyDown(holdPositionKey)) { HoldSelected(); return; }
        if ((buttons == null || buttons.stopButton == null) && Input.GetKeyDown(stopOrderKey)) { StopSelected(); return; }
        if ((buttons == null || buttons.attackButton == null) && Input.GetKeyDown(attackMoveKey)) { BeginAttackMove(); return; }
        if ((buttons == null || buttons.patrolButton == null) && Input.GetKeyDown(patrolKey)) { BeginPatrol(); return; }
        if ((buttons == null || buttons.retreatButton == null) && Input.GetKeyDown(retreatKey)) { BeginRetreat(); return; }
        if ((buttons == null || buttons.faceDefenseButton == null) && Input.GetKeyDown(defenseFacingKey)) { BeginDefenseFacing(); return; }
        if (cam == null) cam = Camera.main;
        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        Ray? worldRay = cam != null ? cam.ScreenPointToRay(Input.mousePosition) : (Ray?)null;
        if (HandlePatrolSelection(worldRay, pointerOverUI)) return;
        if (IsChoosingAttackMove || IsChoosingRetreat || IsChoosingDefenseFacing)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1) || unitsSelected.Count == 0)
            {
                IsChoosingAttackMove = IsChoosingRetreat = IsChoosingDefenseFacing = false;
                militaryInputEndedFrame = Time.frameCount;
            }
            else if (worldRay.HasValue && !pointerOverUI && Input.GetMouseButtonDown(0)
                && Physics.Raycast(worldRay.Value, out var orderPoint, Mathf.Infinity, Ground, QueryTriggerInteraction.Ignore))
            {
                if (IsChoosingRetreat) IssueNetworkRetreat(orderPoint.point);
                else if (IsChoosingDefenseFacing) FaceDefenseAt(orderPoint.point);
                else IssueNetworkAttackMove(orderPoint.point);
            }
            return;
        }
        if (!worldRay.HasValue || pointerOverUI) return;
        var ray = worldRay.Value;
        if (Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(ray, out var selected, Mathf.Infinity, clickable))
            {
                if (Input.GetKey(KeyCode.LeftShift)) MultiSelect(selected.collider.gameObject);
                else SelectByClicking(selected.collider.gameObject);
            }
            else if (!Input.GetKey(KeyCode.LeftShift)) DeselectAll();
        }
        if (!Input.GetMouseButtonDown(1)) return;
        if (Physics.Raycast(ray, out var hit, Mathf.Infinity, clickable | Ground, QueryTriggerInteraction.Ignore))
        {
            var target = hit.collider.GetComponentInParent<RtsNetworkUnit>();
            if (target != null && target.IsSpawned && FactionMember.HasPlayerOwner(target.gameObject)
                && target.PlayerId != session.LocalPlayerId
                && unitsSelected.Exists(unit => unit != null && unit.GetComponentInParent<RtsNetworkUnit>()?.IsCombatUnit == true))
            {
                IssueNetworkAttack(target);
                return;
            }
            var building = hit.collider.GetComponentInParent<RtsNetworkBuildSite>();
            if (building != null && building.IsSpawned && building.IsComplete && building.PlayerId != session.LocalPlayerId
                && unitsSelected.Exists(unit => unit != null && unit.GetComponentInParent<RtsNetworkUnit>()?.IsMeleeFighter == true))
            {
                IssueNetworkBuildingAttack(building);
                return;
            }
        }
        if (Physics.Raycast(ray, out var ground, Mathf.Infinity, Ground)) IssueNetworkMove(ground.point);
    }

    public void IssueNetworkAttack(RtsNetworkUnit target)
    {
        if (RtsNetworkSession.Instance == null || !RtsNetworkSession.Instance.Ready) return;
        foreach (var selected in unitsSelected)
        {
            var root = ControllableRoot(selected);
            if (root != null && root.TryGetComponent<RtsNetworkUnit>(out var unit)) unit.RequestAttack(target);
        }
    }

    public void IssueNetworkBuildingAttack(RtsNetworkBuildSite target)
    {
        if (RtsNetworkSession.Instance?.Ready != true) return;
        foreach (var selected in unitsSelected)
        {
            var root = ControllableRoot(selected);
            if (root != null && root.TryGetComponent<RtsNetworkUnit>(out var unit)) unit.RequestBuildingAttack(target);
        }
    }

    // Shared by ordinary RMB input and the runtime integration check.
    public void IssueNetworkMove(Vector3 point) => IssueNetworkTravel(point, RtsNetworkUnit.TravelOrder.Move);

    public void IssueNetworkAttackMove(Vector3 point)
    {
        if (!IsChoosingAttackMove || !CanIssueNetworkGroundOrder()) return;
        IsChoosingAttackMove = false;
        militaryInputEndedFrame = Time.frameCount;
        IssueNetworkTravel(point, RtsNetworkUnit.TravelOrder.AttackMove);
    }

    public void IssueNetworkRetreat(Vector3 point)
    {
        if (!IsChoosingRetreat || !CanIssueNetworkGroundOrder()) return;
        IsChoosingRetreat = false;
        militaryInputEndedFrame = Time.frameCount;
        IssueNetworkTravel(point, RtsNetworkUnit.TravelOrder.Retreat);
    }

    private bool HasNetworkFightersSelected()
        => unitsSelected.Exists(selected => ControllableRoot(selected)?.GetComponent<RtsNetworkUnit>()?.IsMeleeFighter == true);

    private void IssueNetworkDefenseFacing(Vector3 point)
    {
        if (!IsChoosingDefenseFacing || !CanIssueNetworkGroundOrder()) return;
        IsChoosingDefenseFacing = false;
        militaryInputEndedFrame = Time.frameCount;
        var ids = new List<ulong>();
        foreach (var selected in unitsSelected)
        {
            var root = ControllableRoot(selected);
            if (root != null && root.TryGetComponent<RtsNetworkUnit>(out var unit) && unit.IsMeleeFighter
                && !ids.Contains(unit.EntityId)) ids.Add(unit.EntityId);
        }
        RtsNetworkSession.Instance.RequestDefenseFacing(ids.ToArray(), point);
    }

    private static bool CanIssueNetworkGroundOrder() => RtsNetworkSession.Instance?.Ready == true
        && !SettingsMenu.BlocksWorldInput && !RtsNetworkConstruction.BlocksWorldInput && !RoadBuilder.BlocksWorldInput
        && !SoldiersFormation.IsPlacing;

    internal void CancelMilitaryTargetSelection()
    {
        CancelPatrolSelection();
        IsChoosingAttackMove = IsChoosingRetreat = IsChoosingDefenseFacing = false;
    }

    private bool HandlePatrolSelection(Ray? ray, bool pointerOverUI)
    {
        if (!IsChoosingPatrol) return false;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1) || unitsSelected.Count == 0)
        {
            CancelPatrolSelection();
            militaryInputEndedFrame = Time.frameCount;
            return true;
        }
        if (!ray.HasValue || pointerOverUI || !Input.GetMouseButtonDown(0)
            || !Physics.Raycast(ray.Value, out var hit, Mathf.Infinity, Ground, QueryTriggerInteraction.Ignore)) return true;
        if (patrolPoints.Count > 0 && Vector3.Distance(patrolPoints[patrolPoints.Count - 1], hit.point) < 1f)
        {
            MilitaryDebug("Точки патруля слишком близки, выберите другое место.");
            return true;
        }
        patrolPoints.Add(hit.point);
        MilitaryDebug($"Точка патруля {patrolPoints.Count}: {hit.point}.");
        bool addMore = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (patrolPoints.Count >= 2 && (!addMore
            || (RtsNetworkSession.BlocksOfflineCommands && patrolPoints.Count == RtsNetworkUnit.MaxPatrolPoints))) CompletePatrol();
        return true;
    }

    public void IssueNetworkPatrol(Vector3[] points)
    {
        if (!IsChoosingPatrol || !CanIssueNetworkGroundOrder() || points == null
            || points.Length < 2 || points.Length > RtsNetworkUnit.MaxPatrolPoints) return;
        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(patrolUnits.Count)));
        int rows = Mathf.CeilToInt((float)patrolUnits.Count / columns);
        float spacing = Mathf.Max(1.5f, groupSpacing);
        for (int i = 0; i < patrolUnits.Count; i++)
        {
            var root = ControllableRoot(patrolUnits[i]);
            if (root == null || !root.TryGetComponent<RtsNetworkUnit>(out var unit)) continue;
            var offset = new Vector3(i % columns - (columns - 1) * .5f, 0, i / columns - (rows - 1) * .5f) * spacing;
            var route = new Vector3[points.Length];
            for (int point = 0; point < points.Length; point++) route[point] = points[point] + offset;
            unit.RequestPatrol(route);
        }
        CancelPatrolSelection();
        militaryInputEndedFrame = Time.frameCount;
    }

    private void IssueNetworkTravel(Vector3 point, RtsNetworkUnit.TravelOrder order)
    {
        var session = RtsNetworkSession.Instance;
        if (session?.Ready != true) return;
        var units = new List<RtsNetworkUnit>();
        foreach (var selected in unitsSelected)
        {
            var root = ControllableRoot(selected);
            if (root != null && root.TryGetComponent<RtsNetworkUnit>(out var unit)
                && (order == RtsNetworkUnit.TravelOrder.Move || unit.IsMeleeFighter)
                && !units.Contains(unit)) units.Add(unit);
        }
        if (order == RtsNetworkUnit.TravelOrder.Move && units.Count >= 2)
        {
            int group = units[0].HeldFormationGroup;
            if (group != 0 && units.TrueForAll(unit => unit.HeldFormationGroup == group))
            {
                session.RequestFormationMarch(units.ConvertAll(unit => unit.EntityId).ToArray(), point);
                return;
            }
        }
        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(units.Count)));
        int rows = Mathf.CeilToInt((float)units.Count / columns);
        float rowCenter = order == RtsNetworkUnit.TravelOrder.Retreat ? (rows - 1) * .5f : 0f;
        for (int i = 0; i < units.Count; i++)
        {
            var offset = new Vector3(i % columns - (columns - 1) * .5f, 0, i / columns - rowCenter) * Mathf.Max(1.5f, groupSpacing);
            units[i].RequestTravel(point + offset, order);
        }
    }

    private bool TryRamCrewOrder(Ray ray)
    {
        if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, clickable, QueryTriggerInteraction.Ignore)) return false;
        var ram = hit.collider.GetComponentInParent<BatteringRam>();
        if (ram == null || !FactionMember.IsPlayer(ram.gameObject)) return false;

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            ram.ReleaseCrew();
            return true;
        }
        foreach (var selected in unitsSelected)
            ram.TryAssignCrew(ControllableRoot(selected));
        return true;
    }

    private bool TryConstructionOrder(Ray ray)
    {
        var builders=new List<Builder>();
        foreach(var selected in unitsSelected)
        {
            var unit=ControllableRoot(selected);if(unit==null)continue;
            var worker=unit.GetComponent<ResourcesMove>();
            if(worker==null||worker.role!=ResourcesMove.UnitRole.Builder)continue;
            var builder=unit.GetComponent<Builder>()??unit.AddComponent<Builder>();
            if(builder.isActiveAndEnabled&&!builders.Contains(builder))builders.Add(builder);
        }
        if(builders.Count==0)return false;
        var hits=Physics.RaycastAll(ray,Mathf.Infinity,~0,QueryTriggerInteraction.Collide);
        Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var hit in hits)
        {
            var site=hit.collider.GetComponentInParent<ConstructionSite>();
            if(site==null)
            {
                var building=hit.collider.GetComponentInParent<BuildingHealth>();
                if(building!=null)foreach(var candidate in ConstructionSite.AllSites)
                    if(candidate!=null&&candidate.Building==building.gameObject){site=candidate;break;}
            }
            if(site!=null&&site.IsAvailable)
            {
                if(!FactionMember.AreAllies(builders[0].gameObject,site.gameObject))return false;
                int assigned=0;
                foreach(var builder in builders)if(builder.AssignConstruction(site))assigned++;
                MilitaryDebug($"Стройка {site.name}: приказ принят строителями {assigned}/{builders.Count}.");
                attackCursorVisible=false;
                return true;
            }
            // Вспомогательные триггеры не перекрывают выбор, физическая преграда — перекрывает.
            if(!hit.collider.isTrigger)return false;
        }
        return false;
    }

    /// <summary>
    /// Проверяет у всех юнитов, дошёл ли каждый до своей точки приказа.
    /// Нужно, чтобы по прибытии сбрасывался NavMesh-путь и исчезал маркер,
    /// даже если юнит уже снят с выделения (его GOScript выключен).
    /// </summary>
    private void UpdateMoveCommands()
    {
        foreach (GameObject unit in allUnitsList)
        {
            if (unit == null) continue;

            GOScript move = unit.GetComponent<GOScript>();
            if (move != null)
                move.UpdateMoveCommand();
        }
    }

    /// <summary>
    /// Двигает выделенных юнитов к клику, но каждому выдаёт отдельную точку
    /// в сетке вокруг маркера. Если вести всех в одну точку, NavMeshAgent'ы
    /// начинают толкаться и не могут дойти.
    /// </summary>
    private void MoveSelectedUnitsTo(Vector3 point, RaycastHit groundHit)
    {
        List<GameObject> units = new List<GameObject>();

        foreach (GameObject unit in unitsSelected)
        {
            if (unit != null && unit.GetComponent<ShipHull>() == null && !RamCrewMember.Assigned(unit))
                units.Add(unit);
        }

        if (units.Count == 0)
            return;

        if(SquadMovement.MoveSelected(units,point,groupSpacing))return;

        Vector3 groupCenter = Vector3.zero;
        foreach (GameObject unit in units)
            groupCenter += unit.transform.position;
        groupCenter /= units.Count;

        List<Vector3> destinations = BuildGroupDestinations(point, units.Count, groupCenter);

        if (destinations.Count == 0)
            return;

        AssignNearestDestinations(units, destinations, point, groundHit);
    }

    private struct UnitDestinationPair
    {
        public int UnitIndex;
        public int DestinationIndex;
        public float SqrDistance;
    }

    /// <summary>
    /// Каждому юниту — ближайшая точка назначения. Пары «юнит-точка»
    /// сортируются по дистанции, поэтому порядок выделения не влияет:
    /// никто не побежит через всю группу.
    /// </summary>
    private void AssignNearestDestinations(
        List<GameObject> units,
        List<Vector3> destinations,
        Vector3 fallback,
        RaycastHit groundHit)
    {
        List<UnitDestinationPair> pairs = new List<UnitDestinationPair>();

        for (int u = 0; u < units.Count; u++)
        {
            Vector3 unitPosition = units[u].transform.position;

            for (int d = 0; d < destinations.Count; d++)
            {
                pairs.Add(new UnitDestinationPair
                {
                    UnitIndex = u,
                    DestinationIndex = d,
                    SqrDistance = (destinations[d] - unitPosition).sqrMagnitude
                });
            }
        }

        if (pairs.Count == 0)
            return;

        pairs.Sort((a, b) => a.SqrDistance.CompareTo(b.SqrDistance));

        bool[] unitAssigned = new bool[units.Count];
        bool[] destinationAssigned = new bool[destinations.Count];

        foreach (UnitDestinationPair pair in pairs)
        {
            if (unitAssigned[pair.UnitIndex] || destinationAssigned[pair.DestinationIndex])
                continue;

            unitAssigned[pair.UnitIndex] = true;
            destinationAssigned[pair.DestinationIndex] = true;

            ApplyGroupMove(units[pair.UnitIndex], destinations[pair.DestinationIndex], groundHit);
        }

        // Юнитов больше, чем точек (например, из-за притягивания к NavMesh)
        for (int u = 0; u < units.Count; u++)
        {
            if (unitAssigned[u])
                continue;

            ApplyGroupMove(units[u], fallback, groundHit);
        }
    }

    private static void ApplyGroupMove(GameObject unit, Vector3 destination, RaycastHit groundHit)
    {
        GOScript move = unit.GetComponent<GOScript>();
        if (move != null)
            move.MoveTo(destination);

        AttackController ac = unit.GetComponent<AttackController>();
        if (ac != null)
            ac.targetToAttack = null;

        // Линия-указатель от каждого юнита к точке приказа (как было раньше)
        DirectionIndicator direction = unit.GetComponent<DirectionIndicator>();
        if (direction != null)
            direction.DrawLine(groundHit);
    }

    /// <summary>
    /// Строит сетку точек вокруг клика: count позиций с шагом groupSpacing.
    /// Точки притягиваются к NavMesh, чтобы юниты не пытались идти в стену.
    /// </summary>
    private List<Vector3> BuildGroupDestinations(Vector3 center, int count, Vector3 groupCenter)
    {
        float spacing = Mathf.Max(0.6f, groupSpacing);

        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt((float)count / columns);

        // Ряды сетки ставим перпендикулярно направлению подхода группы:
        // тогда передний ряд достаётся ближайшим юнитам и никому не нужно
        // продираться сквозь уже вставших товарищей.
        Vector3 approach = groupCenter - center;
        approach.y = 0f;

        if (approach.sqrMagnitude < 0.0001f)
            approach = Vector3.forward;
        else
            approach.Normalize();

        Vector3 side = new Vector3(approach.z, 0f, -approach.x);

        List<Vector3> result = new List<Vector3>(count);
        int index = 0;

        for (int row = 0; row < rows; row++)
        {
            int inRow = Mathf.Min(columns, count - index);
            float rowOffset = (inRow - 1) * 0.5f;
            float z = (row - (rows - 1) * 0.5f) * spacing;

            for (int col = 0; col < inRow; col++)
            {
                float x = (col - rowOffset) * spacing;
                Vector3 candidate = center + side * x + approach * z;

                NavMeshHit navHit;
                if (NavMesh.SamplePosition(candidate, out navHit, navMeshSnapRadius, NavMesh.AllAreas))
                    result.Add(navHit.position);
                else if (NavMesh.SamplePosition(center, out navHit, navMeshSnapRadius, NavMesh.AllAreas))
                    result.Add(navHit.position);
                else
                    result.Add(candidate);

                index++;
            }
        }

        return result;
    }

    private void CursorSelector()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickable)&&ControllableRoot(hit.collider.gameObject)!=null)
        {
            CursorManager.Instance.SetMarkerType(CursorManager.CursorType.Selectable);
        }
        else if (TryEnemyUnderCursor(ray,out hit) && unitsSelected.Count > 0 && AtleastOneOffensiveUnit(unitsSelected))
        {
            CursorManager.Instance.SetMarkerType(CursorManager.CursorType.Attackable);
        }
        else if (Physics.Raycast(ray, out hit, Mathf.Infinity, Ground) && unitsSelected.Count > 0 )
        {
            CursorManager.Instance.SetMarkerType(CursorManager.CursorType.Walkable);
        }
        else
        {
            CursorManager.Instance.SetMarkerType(CursorManager.CursorType.None);
        }
        
    }

    private bool AtleastOneOffensiveUnit(List<GameObject> unitSelected)
    {
        foreach (GameObject unit in unitSelected)
        {
            // ИСПРАВЛЕНИЕ: Безопасная проверка
            if (unit != null && FactionMember.IsPlayer(unit) && unit.GetComponent<AttackController>() != null)
            {
                return true;
            }
        }
        return false;
    }

    // Наши ресурсные объекты (жила, склады) — не боевые цели: ПКМ по ним = команда движения
    private bool IsResourceTarget(GameObject obj)
    {
        if (obj == null) return false;
        if (obj.GetComponentInParent<OreDeposit>() != null) return true;
        if (obj.GetComponentInParent<FarmField>() != null || obj.GetComponentInParent<FarmHouse>() != null) return true;
        return obj.CompareTag("Sawmill") || obj.CompareTag("Warehouse")
               || obj.CompareTag("FishHouse") || obj.CompareTag("StoneHouse")
               || obj.CompareTag("StoneQuarry");
    }

    private bool IsBuilding(GameObject obj)
    {
        return obj != null &&
               (obj.GetComponentInParent<UiSelectBuilding>() != null ||
                obj.GetComponentInChildren<UiSelectBuilding>() != null);
    }
    private bool TryEnemyUnderCursor(Ray ray,out RaycastHit hit)
    {
        int mask=attackable.value|clickable.value|LayerMask.GetMask("Building","Kazarma");
        return Physics.Raycast(ray,out hit,Mathf.Infinity,mask,QueryTriggerInteraction.Ignore)
            && FactionRules.AreEnemies(FactionMember.LocalPlayer, FactionMember.Of(hit.collider.gameObject));
    }
    private static GameObject ControllableRoot(GameObject obj)
    {
        var unit=obj!=null?obj.GetComponentInParent<Unit>():null;
        return unit!=null&&unit.enabled&&unit.unitHealth>0&&!HospitalPatient.Inside(unit.gameObject)&&!ShipCrewMember.Aboard(unit.gameObject)&&!ShipPassenger.Aboard(unit.gameObject)&&FactionMember.IsPlayer(unit.gameObject)?unit.gameObject:null;
    }

    private void SelectByClicking(GameObject unit)
    {
        DeselectAll();
        unit=ControllableRoot(unit);
        if (unit == null || IsBuilding(unit)) return; // Защита
        unitsSelected.Add(unit);
        TriggerSelectionIndicator(unit, true);
        EnableUnitMovement(unit, true);
    }

    public void SelectFromCard(GameObject unit, bool removeFromSelection)
    {
        if (BlocksMilitarySelection || SoldiersFormation.IsPlacing || Barracks.BlocksWorldInput
            || RoadBuilder.BlocksWorldInput || (PlacementSystem.Instance?.BlocksUnitInput ?? false)) return;
        unit = ControllableRoot(unit);
        if (unit == null || !unitsSelected.Contains(unit)) return;
        if (removeFromSelection) MultiSelect(unit);
        else if (unitsSelected.Count > 1) SelectByClicking(unit);
    }

    private void MultiSelect(GameObject unit)
    {
        unit=ControllableRoot(unit);
        if (unit == null || IsBuilding(unit)) return; // Защита
        if (!unitsSelected.Contains(unit))
        {
            unitsSelected.Add(unit);
            TriggerSelectionIndicator(unit, true);
            EnableUnitMovement(unit, true);
        }
        else
        {
            EnableUnitMovement(unit, false);
            TriggerSelectionIndicator(unit, false);
            unitsSelected.Remove(unit);
        }
    }

    public void DeselectAll()
    {
        foreach (var unit in unitsSelected)
        {
            // ИСПРАВЛЕНИЕ: Не пытаемся отключить скрипты у уничтоженных объектов
            if (unit == null) continue;

            EnableUnitMovement(unit, false);
            TriggerSelectionIndicator(unit, false);
        }

        if (groundMarker != null)
            groundMarker.SetActive(false);
        unitsSelected.Clear();
    }

    private void EnableUnitMovement(GameObject unit, bool shouldMove)
    {
        if (unit == null) return; // Защита
        var script = unit.GetComponent<GOScript>();
        if (script != null)
            script.enabled = shouldMove;
    }

    private void TriggerSelectionIndicator(GameObject unit, bool IsVisible)
    {
        if (unit == null) return; // Защита

        // Не у всех юнитов есть дочерний объект Indicator — без проверки
        // на null выделение падало с NullReferenceException.
        Transform indicator = unit.transform.Find("Indicator");
        if (indicator != null)
            indicator.gameObject.SetActive(IsVisible);
    }

    public void DragSelect(GameObject unit)
    {
        unit=ControllableRoot(unit);
        if (unit == null || IsBuilding(unit)) return; // Защита
        if (!unitsSelected.Contains(unit))
        {
            unitsSelected.Add(unit);
            TriggerSelectionIndicator(unit, true);
            EnableUnitMovement(unit, true);
        }
    }
}
