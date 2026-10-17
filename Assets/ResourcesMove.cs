using System.Collections;
using System.Collections.Generic;
using _Game.Scripts.UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ResourcesMove : MonoBehaviour
{
    public enum UnitRole { Lumberjack, Carrier, Fisher, StoneMason }

    [Header("Настройки роли юнита")]
    public UnitRole role; 
    public int MaxUnitResources = 10;

    [Header("Доставка ресурсов (контактом ТЕЛОМ юнита, не большим триггером)")]
    [SerializeField] private float deliveryDistance = 1.0f;         // транзакция только когда тело юнита у склада
    [SerializeField] private float stopNearBuildingDistance = 1.2f; // юнит останавливается у стены склада, не заходит в центр

    [Header("Ссылки")]
    [SerializeField] private AttackController attackController;
    private GettingResources _gettingResources;
    private NavMeshAgent _agent;
    private GOScript _goScript;
    private Animator _animator; 

    [SerializeField] private bool hasResourcesInPocket = false; 

    void Start()
    {
        if (attackController == null) attackController = GetComponent<AttackController>();
        _gettingResources = GetComponent<GettingResources>();
        _agent = GetComponent<NavMeshAgent>();
        _goScript = GetComponent<GOScript>();
        
        _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (_goScript != null && _goScript.isCommandToMove) return; 

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

    private void GoToSawmill()
    {
        GameObject closestSawmill = FindClosestWithTag("Sawmill");
        if (closestSawmill != null && attackController != null)
        {
            attackController.targetToAttack = closestSawmill.transform;
            if (_animator != null) _animator.SetBool("IsFollowing", true);
        }
    }

    private void GoToFishHouse()
    {
        GameObject closestFishHouse = FindClosestWithTag("FishHouse");
        if (closestFishHouse != null && attackController != null)
        {
            attackController.targetToAttack = closestFishHouse.transform;
            if (_animator != null) _animator.SetBool("IsFollowing", true);
        }
    }

    private void GoToStoneHouse()
    {
        // Каменщик несёт добытый камень на ВРЕМЕННЫЙ склад камня (StoneHouse):
        // подходит, останавливается у стены склада и сдаёт камень (не заходя в центр).
        GameObject closestStoneHouse = FindClosestWithTag("StoneHouse");

        if (closestStoneHouse != null && attackController != null)
        {
            attackController.targetToAttack = closestStoneHouse.transform;
            if (_animator != null) _animator.SetBool("IsFollowing", true);
        }
    }

    private void GoToMainWarehouse()
    {
        GameObject closestWarehouse = FindClosestWithTag("Warehouse");
        if (closestWarehouse != null && attackController != null)
        {
            attackController.targetToAttack = closestWarehouse.transform;
            if (_animator != null) _animator.SetBool("IsFollowing", true);
        }
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
        // Транзакция проходит только когда ТЕЛО юнита подошло к складу вплотную.
        float bodyDistance = Vector3.Distance(transform.position, other.ClosestPoint(transform.position));
        if (bodyDistance > deliveryDistance) return;

        ProcessBuilding(other.gameObject);
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

        // 2. РЫБАК пришел в РЫБОЛОВНЮ
        if (role == UnitRole.Fisher && hasResourcesInPocket && building.CompareTag("FishHouse"))
        {
            ClearPocket();
            Debug.Log("<color=cyan>Успех!</color> Рыбак сдал улов в Рыболовню."); // ИСПРАВЛЕНО: закрыт тег </color>
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

    // Подходим к складу и ОСТАНАВЛИВАЕМСЯ у его стены (в центр не заходим), затем проводим транзакцию.
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

        Collider targetCollider = targetGO.GetComponent<Collider>();
        if (targetCollider == null) return false;

        // Юнит уже у стены склада?
        float distance = Vector3.Distance(transform.position, targetCollider.ClosestPoint(transform.position));
        if (distance > stopNearBuildingDistance) return false;

        // ОСТАНОВКА у стены склада
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

