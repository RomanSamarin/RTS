using System.Collections;
using System.Collections.Generic;
using _Game.Scripts.UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ResourcesMove : MonoBehaviour
{
    // Builder добавлен в конец, чтобы не сдвинуть значения ролей,
    // уже сохранённые в префабах сцены.
    // Villager — безработный житель: ничего не добывает и может быть нанят в здание.
    public enum UnitRole { Lumberjack, Carrier, Fisher, StoneMason, Builder, Villager }

    [Header("Настройки роли юнита")]
    [Tooltip("Профессия рабочего. Builder — строитель: сам находит стройплощадку, " +
             "подходит и достраивает здание (компонент Builder добавится автоматически). " +
             "Villager — безработный житель: ничего не добывает, его можно нанять в здание")]
    public UnitRole role; 
    public int MaxUnitResources = 10;

    [Header("Доставка ресурсов (подход к двери здания)")]
    [SerializeField] private float deliveryDistance = 1.0f;         // сдача без двери: тело юнита у стены
    [SerializeField] private float stopNearBuildingDistance = 1.2f; // юнит останавливается у стены склада, не заходит в центр
    [SerializeField] private float doorReachDistance = 0.8f;        // юнит дошёл до двери (BuildingEntrance)

    [Header("Ссылки")]
    [SerializeField] private AttackController attackController;
    private GettingResources _gettingResources;
    private NavMeshAgent _agent;
    private GOScript _goScript;
    private Animator _animator; 
    private Builder _builder;

    // Здание, в которое нанят рабочий. Если задано — рабочий несёт
    // добытые ресурсы именно сюда, а не в ближайшее здание того же типа.
    [SerializeField] private GameObject assignedWorkplace;

    [SerializeField] private bool hasResourcesInPocket = false;

    // ------------------------------------------------------------ Найм

    /// <summary>Текущая профессия рабочего.</summary>
    public UnitRole Role => role;

    /// <summary>Безработный житель — его можно нанять.</summary>
    public bool IsUnemployed => role == UnitRole.Villager;

    /// <summary>Здание, в которое нанят рабочий (может быть null).</summary>
    public GameObject AssignedWorkplace => assignedWorkplace;

    /// <summary>
    /// Меняет профессию рабочего и (необязательно) привязывает его к зданию.
    /// Используется системой найма (WorkplaceHiring), но можно вызывать и вручную.
    /// </summary>
    public void SetRole(UnitRole newRole, GameObject workplace = null)
    {
        UnitRole previousRole = role;

        // Недонесённый груз сбрасываем: иначе рабочий новой профессии
        // застрянет с полным рюкзаком и не пойдёт работать.
        if (previousRole != newRole)
            ClearPocket();

        // Уволенный строитель не должен продолжать искать стройки.
        // Проверяем не только previousRole: компонент Builder мог остаться
        // с прошлых версий сцены.
        if (newRole != UnitRole.Builder)
            RemoveBuilder();

        role = newRole;
        assignedWorkplace = workplace;

        // Старые цели (дерево, склад, стройка) больше не актуальны.
        if (attackController != null)
            attackController.targetToAttack = null;

        if (_agent != null && _agent.isOnNavMesh)
            _agent.ResetPath();

        if (_animator != null)
        {
            _animator.SetBool("IsFollowing", false);
            _animator.SetBool("isAttacking", false);
        }

        if (newRole == UnitRole.Builder)
            EnsureBuilder();
    }

    /// <summary>Привязывает рабочего к зданию (куда он будет сдавать ресурсы).</summary>
    public void AssignWorkplace(GameObject workplace)
    {
        assignedWorkplace = workplace;
    }

    /// <summary>Снимает привязку к зданию.</summary>
    public void ClearWorkplace()
    {
        assignedWorkplace = null;
    }
 

    void Start()
    {
        // Если в инспекторе выбрана роль «Builder», сам добавляем компонент
        // строителя — достаточно поменять роль на префабе рабочего.
        if (role == UnitRole.Builder)
            EnsureBuilder();

        if (attackController == null) attackController = GetComponent<AttackController>();
        _gettingResources = GetComponent<GettingResources>();
        _agent = GetComponent<NavMeshAgent>();
        _goScript = GetComponent<GOScript>();
        
        _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // Строитель не занимается добычей: его поведение полностью
        // ведёт компонент Builder (поиск стройки, подход, прогресс).
        // EnsureBuilder нужен и здесь: роль могли переключить на Builder
        // прямо в Play Mode, когда Start уже отработал.
        if (role == UnitRole.Builder)
        {
            EnsureBuilder();
            return;
        }

        if (_goScript != null && _goScript.isCommandToMove) return; 

        // Безработный житель не ищет работу: он просто стоит и ждёт, пока его
        // наймут в здание. Приказы игрока (движение, атака) обрабатывают
        // GOScript/ManagerGO независимо от этого скрипта.
        if (role == UnitRole.Villager) return;

        // СДАЧА/ЗАБОР РЕСУРСОВ: если юнит уже у нужного склада — ОСТАНАВЛИВАЕМСЯ у стены
        // и проводим транзакцию прямо здесь (не через большой отслеживающий триггер).
        if (TryStopAndProcessNearbyBuilding()) return;

        if (hasResourcesInPocket) return; 

        if (_gettingResources != null)
        {
            if ((role == UnitRole.Lumberjack && _gettingResources.Wood >= MaxUnitResources) ||
                (role == UnitRole.Fisher && _gettingResources.Fish >= MaxUnitResources) || 
                (role == UnitRole.StoneMason && _gettingResources.Stone >= MaxUnitResources))
            {
                SetInventoryFull();
                return;
            }
        }

        if (role == UnitRole.Lumberjack)
        {
            if (attackController != null && attackController.targetToAttack == null) FindAndGoToClosestTree();
        }

        if (role == UnitRole.Carrier)
        {
            if (attackController != null && attackController.targetToAttack == null) FindAndGoToActiveResourceBuilding();
        }

        if (role == UnitRole.Fisher)
        {
            if (attackController != null && attackController.targetToAttack == null) FindAndGoToClosestFishingSpot();
        }

        if (role == UnitRole.StoneMason)
        {
            // Если цель — полностью выработанная жила (кучу уже забрал носильщик), отпускаем её
            if (attackController != null && attackController.targetToAttack != null)
            {
                StoneQuarry finishedQuarry = attackController.targetToAttack.GetComponent<StoneQuarry>();
                if (finishedQuarry != null && finishedQuarry.currentStoneLeft <= 0
                    && _gettingResources != null && _gettingResources.Stone <= 0)
                {
                    attackController.targetToAttack = null;
                }
            }

            if (attackController != null && attackController.targetToAttack == null) FindAndGoToClosestStoneQuarry();
        }
    }

    public void SetInventoryFull()
    {
        hasResourcesInPocket = true;
        if (role == UnitRole.Lumberjack) GoToSawmill();
        if (role == UnitRole.Fisher) GoToFishHouse();
        if (role == UnitRole.StoneMason) GoToStoneHouse();
    }

    /// <summary>
    /// Гарантирует наличие компонента Builder у рабочего с ролью Builder.
    /// Нужно, потому что роль можно поменять в Play Mode уже после Start().
    /// </summary>
    private void EnsureBuilder()
    {
        if (_builder == null)
            _builder = GetComponent<Builder>();

        if (_builder == null)
            _builder = gameObject.AddComponent<Builder>();
    }

    /// <summary>
    /// Убирает компонент строителя, когда рабочего переводят на другую
    /// профессию. Без этого уволенный строитель продолжал бы искать стройки.
    /// </summary>
    private void RemoveBuilder()
    {
        Builder builder = _builder != null ? _builder : GetComponent<Builder>();
        _builder = null;

        if (builder == null)
            return;

        if (Application.isPlaying)
            Destroy(builder);
        else
            DestroyImmediate(builder);
    }

    /// <summary>
    /// Здание, в которое нанят рабочий, если оно подходит и ещё существует.
    /// Возвращает null, если привязки нет или она больше не действует.
    /// </summary>
    private GameObject GetAssignedWorkplace(string requiredTag)
    {
        if (assignedWorkplace == null)
            return null;

        if (!assignedWorkplace.activeInHierarchy)
            return null;

        if (!string.IsNullOrEmpty(requiredTag) && !assignedWorkplace.CompareTag(requiredTag))
            return null;

        return assignedWorkplace;
    }

    /// <summary>
    /// Точка, к которой рабочий должен подойти к цели. Для зданий с
    /// BuildingEntrance — дверь, для остальных целей (дерево, враг, водоём) —
    /// сам объект. Используется UnitFollowState вместо позиции цели.
    /// </summary>
    public Vector3 GetApproachPoint(Transform target)
    {
        if (target == null)
            return transform.position;

        if (BuildingEntrance.TryGetWorldPoint(target.gameObject, out Vector3 doorPoint))
            return doorPoint;

        return target.position;
    }

    // Назначает цель и включает анимацию следования.
    private void SetTarget(Transform target)
    {
        if (target == null || attackController == null)
            return;

        attackController.targetToAttack = target;

        if (_animator != null)
            _animator.SetBool("IsFollowing", true);
    }

    private void GoToSawmill()
    {
        GameObject sawmill = GetAssignedWorkplace("Sawmill");
        if (sawmill == null)
            sawmill = FindClosestWithTag("Sawmill");

        SetTarget(sawmill != null ? sawmill.transform : null);
    }

    private void GoToFishHouse()
    {
        GameObject fishHouse = GetAssignedWorkplace("FishHouse");
        if (fishHouse == null)
            fishHouse = FindClosestWithTag("FishHouse");

        SetTarget(fishHouse != null ? fishHouse.transform : null);
    }

    private void GoToStoneHouse()
    {
        // Каменщик несёт добытый камень на ВРЕМЕННЫЙ склад камня (StoneHouse):
        // подходит, останавливается у стены склада и сдаёт камень (не заходя в центр).
        GameObject stoneHouse = GetAssignedWorkplace("StoneHouse");
        if (stoneHouse == null)
            stoneHouse = FindClosestWithTag("StoneHouse");

        SetTarget(stoneHouse != null ? stoneHouse.transform : null);
    }

    private void GoToMainWarehouse()
    {
        GameObject warehouse = GetAssignedWorkplace("Warehouse");
        if (warehouse == null)
            warehouse = FindClosestWithTag("Warehouse");

        SetTarget(warehouse != null ? warehouse.transform : null);
    }

    private void FindAndGoToClosestTree()
    {
        GameObject[] trees = GameObject.FindGameObjectsWithTag("Enemy");
        GameObject closestTree = null;
        float closestDistance = Mathf.Infinity;

        foreach (GameObject treeObj in trees)
        {
            Unit treeUnit = treeObj.GetComponent<Unit>();
            if (treeUnit != null && treeUnit.unitHealth > 0)
            {
                float distance = Vector3.Distance(transform.position, treeObj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTree = treeObj;
                }
            }
        }

        if (closestTree != null && attackController != null)
        {
            attackController.targetToAttack = closestTree.transform;
            if (_animator != null) _animator.SetBool("IsFollowing", true);
        }
    }

    private void FindAndGoToClosestStoneQuarry()
    {
        GameObject[] stonesQuarry = GameObject.FindGameObjectsWithTag("StoneQuarry");
        GameObject closestQuarry = null;
        float closestDistance = Mathf.Infinity;

        foreach (GameObject quarryObj in stonesQuarry)
        {
            StoneQuarry quarryScript = quarryObj.GetComponent<StoneQuarry>();
            if (quarryScript != null && quarryScript.stoneHealth > 0)
            {
                float distance = Vector3.Distance(transform.position, quarryObj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestQuarry = quarryObj;
                }
            }
        }

        if (closestQuarry != null && attackController != null)
        {
            attackController.targetToAttack = closestQuarry.transform;
            if (_animator != null) _animator.SetBool("IsFollowing", true);
        }
    }

    private void FindAndGoToClosestFishingSpot()
    {
        GameObject[] spots = GameObject.FindGameObjectsWithTag("FishingSpot");
        GameObject closestSpot = null;
        float closestDistance = Mathf.Infinity;

        foreach (GameObject spotObj in spots)
        {
            FishingSpot spotScript = spotObj.GetComponent<FishingSpot>();
            if (spotScript != null && spotScript.fishHealth > 0)
            {
                float distance = Vector3.Distance(transform.position, spotObj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestSpot = spotObj;
                }
            }
        }

        if (closestSpot != null && attackController != null)
        {
            attackController.targetToAttack = closestSpot.transform;
            if (_animator != null) _animator.SetBool("IsFollowing", true);
        }
    }

    private void FindAndGoToActiveResourceBuilding()
    {
        GameObject targetBuilding = null;
        float closestDistance = Mathf.Infinity;

        // 1. Проверяем лесопилки
        GameObject[] sawmills = GameObject.FindGameObjectsWithTag("Sawmill");
        foreach (GameObject sawmillObj in sawmills)
        {
            Sawmill sawmillScript = sawmillObj.GetComponent<Sawmill>();
            if (sawmillScript != null && sawmillScript.storedWood > 0)
            {
                float distance = Vector3.Distance(transform.position, sawmillObj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    targetBuilding = sawmillObj;
                }
            }
        }

        // 2. Проверяем хижины рыбаков (FishHouse)
        GameObject[] fishHouses = GameObject.FindGameObjectsWithTag("FishHouse");
        foreach (GameObject fishHouseObj in fishHouses)
        {
            Fishing fishingScript = fishHouseObj.GetComponent<Fishing>();
            if (fishingScript != null && fishingScript.storeFish > 0)
            {
                float distance = Vector3.Distance(transform.position, fishHouseObj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    targetBuilding = fishHouseObj;
                }
            }
        }

        // 3. Проверяем склады камня (StoneHouse)
        GameObject[] stoneHouses = GameObject.FindGameObjectsWithTag("StoneHouse");
        foreach (GameObject stoneHouseObj in stoneHouses)
        {
            StoneHouse stoneHouseScript = stoneHouseObj.GetComponent<StoneHouse>();
            if (stoneHouseScript != null && stoneHouseScript.storedStone > 0)
            {
                float distance = Vector3.Distance(transform.position, stoneHouseObj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    targetBuilding = stoneHouseObj;
                }
            }
        }

        if (targetBuilding != null && attackController != null)
        {
            attackController.targetToAttack = targetBuilding.transform;
            if (_animator != null) _animator.SetBool("IsFollowing", true);
        }
    }

    private GameObject FindClosestWithTag(string tag)
    {
        GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
        GameObject closest = null;
        float closestDistance = Mathf.Infinity;

        foreach (GameObject obj in objects)
        {    float distance = Vector3.Distance(transform.position, obj.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = obj;
            }
        }
        return closest;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Sawmill") && !other.CompareTag("Warehouse") && !other.CompareTag("FishHouse") && !other.CompareTag("StoneHouse")) return;

        // ВАЖНО: у юнита есть БОЛЬШОЙ триггер (он только для отслеживания) — им ресурсы не сдаются.
        // Транзакция проходит только в точке сдачи: у двери здания (BuildingEntrance),
        // если она задана, иначе когда ТЕЛО юнита подошло к стене вплотную.
        if (!IsAtDeliveryPoint(other.gameObject, deliveryDistance)) return;

        ProcessBuilding(other.gameObject);
    }

    /// <summary>
    /// Юнит уже в точке сдачи ресурсов: у двери (BuildingEntrance), если она
    /// задана, иначе — у стены здания. <paramref name="wallDistance"/> —
    /// допуск для зданий без двери.
    /// </summary>
    private bool IsAtDeliveryPoint(GameObject building, float wallDistance)
    {
        if (building == null)
            return false;

        if (BuildingEntrance.TryGetWorldPoint(building, out Vector3 doorPoint))
        {
            if (Vector3.Distance(transform.position, doorPoint) <= doorReachDistance)
                return true;

            // Дверь оказалась вне NavMesh — не даём рабочему зависнуть:
            // сдаём у стены, но только когда он уже остановился возле неё,
            // а не пробегая мимо по пути к двери.
            return AgentReachedDestination() && IsNearWall(building, wallDistance);
        }

        return IsNearWall(building, wallDistance);
    }

    private bool IsNearWall(GameObject building, float wallDistance)
    {
        Collider targetCollider = building.GetComponent<Collider>();
        if (targetCollider == null)
            return true;

        float bodyDistance = Vector3.Distance(transform.position, targetCollider.ClosestPoint(transform.position));
        return bodyDistance <= wallDistance;
    }

    /// <summary>Агент дошёл до конечной точки пути (или уже стоит без пути).</summary>
    private bool AgentReachedDestination()
    {
        if (_agent == null || !_agent.isOnNavMesh || _agent.pathPending)
            return false;

        if (!_agent.hasPath)
            return _agent.velocity.sqrMagnitude <= 0.01f;

        return _agent.remainingDistance <= _agent.stoppingDistance + 0.15f;
    }

    // Все транзакции со складами (сдача/забор ресурсов). Возвращает true, если транзакция прошла.
    private bool ProcessBuilding(GameObject building)
    {
        bool processed = false;

        // 1. ЛЕСОРУБ пришел на ЛЕСОПИЛКУ
        if (role == UnitRole.Lumberjack && hasResourcesInPocket && building.CompareTag("Sawmill"))
        {
            Sawmill sawmill = building.GetComponent<Sawmill>();
            if (sawmill != null && _gettingResources != null)
            {
                sawmill.AddWood(_gettingResources.Wood);
                ClearPocket();
                FindAndGoToClosestTree();
                processed = true;
            }
        }

        // 2. РЫБАК пришел в РЫБОЛОВНЮ (сдает улов в хранилище рыбы)
        if (role == UnitRole.Fisher && hasResourcesInPocket && building.CompareTag("FishHouse"))
        {
            Fishing fishing = building.GetComponent<Fishing>();
            if (fishing != null && _gettingResources != null)
            {
                fishing.AddFish(_gettingResources.Fish);
            }
            ClearPocket();
            Debug.Log("<color=cyan>Успех!</color> Рыбак сдал улов в Рыболовню.");
            FindAndGoToClosestFishingSpot();
            processed = true;
        }

        // 2.5. КАМЕНЩИК пришел на ВРЕМЕННЫЙ СКЛАД КАМНЯ (сдает добытый камень)
        if (role == UnitRole.StoneMason && hasResourcesInPocket && building.CompareTag("StoneHouse"))
        {
            StoneHouse stoneHouse = building.GetComponent<StoneHouse>();
            if (stoneHouse != null && _gettingResources != null)
            {
                stoneHouse.AddStone(_gettingResources.Stone);
            }
            ClearPocket();
            Debug.Log("<color=grey>Успех!</color> Каменщик сдал камень на склад камня.");
            FindAndGoToClosestStoneQuarry(); // идём копать снова
            processed = true;
        }

        // 3. НОСИЛЬЩИК пришел на ЛЕСОПИЛКУ (Забирает дерево)
        if (role == UnitRole.Carrier && !hasResourcesInPocket && building.CompareTag("Sawmill"))
        {
            Sawmill sawmill = building.GetComponent<Sawmill>();
            if (sawmill != null && sawmill.storedWood > 0 && _gettingResources != null)
            {
                _gettingResources.Wood = sawmill.ExtractWood(MaxUnitResources);
                hasResourcesInPocket = true;
                if (attackController != null) attackController.targetToAttack = null;
                GoToMainWarehouse();
                processed = true;
            }
        }

        // 4. НОСИЛЬЩИК пришел на ГЛАВНЫЙ СКЛАД (Сдает любые ресурсы)
        if (role == UnitRole.Carrier && hasResourcesInPocket && building.CompareTag("Warehouse"))
        {
            if (ResourcesManager.Instance != null && _gettingResources != null)
            {
                ResourcesManager.Instance.TakeResource(_gettingResources);
                ClearPocket();
                processed = true;
            }
        }

        // 5. НОСИЛЬЩИК пришел в РЫБОЛОВНЮ (Забирает рыбу)
        if (role == UnitRole.Carrier && !hasResourcesInPocket && building.CompareTag("FishHouse"))
        {
            Fishing fishing = building.GetComponent<Fishing>(); // ИСПРАВЛЕНО: добавлен <Fishing>
            if (fishing != null && fishing.storeFish > 0 && _gettingResources != null)
            {
                _gettingResources.Fish = fishing.ExtractFish(MaxUnitResources);
                hasResourcesInPocket = true;
                if (attackController != null) attackController.targetToAttack = null;
                GoToMainWarehouse();
                processed = true;
            }
        }

        // 6. НОСИЛЬЩИК пришел на СКЛАД КАМНЯ (Забирает камень, далее несет на главный склад)
        if (role == UnitRole.Carrier && !hasResourcesInPocket && building.CompareTag("StoneHouse"))
        {
            StoneHouse stoneHouse = building.GetComponent<StoneHouse>();
            if (stoneHouse != null && stoneHouse.storedStone > 0 && _gettingResources != null)
            {
                _gettingResources.Stone = stoneHouse.ExtractStone(MaxUnitResources);
                hasResourcesInPocket = true;
                if (attackController != null) attackController.targetToAttack = null;
                GoToMainWarehouse();
                processed = true;
            }
        }

        return processed;
    }

    // Подходим к складу: к двери (BuildingEntrance), а если двери нет — к стене
    // (в центр не заходим), затем проводим транзакцию.
    // Работает из Update — независимо от того, каким коллайдером юнита сработал триггер склада.
    private bool TryStopAndProcessNearbyBuilding()
    {
        if (attackController == null || attackController.targetToAttack == null) return false;

        GameObject targetGO = attackController.targetToAttack.gameObject;
        string targetTag = targetGO.tag;

        // Подходящее ли это здание для текущего состояния юнита?
        bool isDeliveryTarget =
            (role == UnitRole.Lumberjack && hasResourcesInPocket && targetTag == "Sawmill") ||
            (role == UnitRole.Fisher && hasResourcesInPocket && targetTag == "FishHouse") ||
            (role == UnitRole.StoneMason && hasResourcesInPocket && targetTag == "StoneHouse") ||
            (role == UnitRole.Carrier && hasResourcesInPocket && targetTag == "Warehouse") ||
            (role == UnitRole.Carrier && !hasResourcesInPocket &&
             (targetTag == "Sawmill" || targetTag == "FishHouse" || targetTag == "StoneHouse"));

        if (!isDeliveryTarget) return false;

        // Юнит уже у двери (или у стены, если двери нет)?
        if (!IsAtDeliveryPoint(targetGO, stopNearBuildingDistance)) return false;

        // ОСТАНОВКА в точке сдачи
        if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;

        bool processed = ProcessBuilding(targetGO);

        // Возобновляем движение (ClearPocket внутри мог уже назначить новую цель)
        if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = false;

        if (!processed)
        {
            // Склад пуст или транзакция невозможна — отпускаем цель, Update найдёт другую
            if (attackController != null) attackController.targetToAttack = null;
            return false;
        }
        return true;
    }

    private void ClearPocket()
    {
        if (_gettingResources != null)
        {
            _gettingResources.Wood = 0;
            _gettingResources.Stone = 0;
            _gettingResources.Wheat = 0;
            _gettingResources.Fish = 0;
        }
        hasResourcesInPocket = false;
        if (attackController != null) attackController.targetToAttack = null;
        if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = false; // снова можно идти
        if (_animator != null)
        {
            _animator.SetBool("IsFollowing", false);
            _animator.SetBool("isAttacking", false);
        }
    }
}

