using System.Collections;
using System.Collections.Generic;
using _Game.Scripts.UI;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ResourcesMove : MonoBehaviour
{
    public enum UnitRole { Lumberjack, Carrier, Fisher }

    [Header("Настройки роли юнита")]
    public UnitRole role; 
    public int MaxUnitResources = 10;

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
        if (hasResourcesInPocket) return; 

        if (_gettingResources != null)
        {
            if ((role == UnitRole.Lumberjack && _gettingResources.Wood >= MaxUnitResources) ||
                (role == UnitRole.Fisher && _gettingResources.Fish >= MaxUnitResources))
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
            if (attackController != null && attackController.targetToAttack == null) FindAndGoToActiveSawmill();
        }

        // Ищем по новому тегу FishingSpot
        if (role == UnitRole.Fisher)
        {
            if (attackController != null && attackController.targetToAttack == null) FindAndGoToClosestFishingSpot();
        }
    }

    public void SetInventoryFull()
    {
        hasResourcesInPocket = true;
        if (role == UnitRole.Lumberjack) GoToSawmill();
        if (role == UnitRole.Fisher) GoToFishHouse(); 
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

    private void FindAndGoToClosestFishingSpot()
    {
        // ИЩЕМ ПО ТЕГУ FishingSpot
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

    private void FindAndGoToActiveSawmill()
    {
        GameObject[] sawmills = GameObject.FindGameObjectsWithTag("Sawmill");
        GameObject targetSawmill = null;
        float closestDistance = Mathf.Infinity;

        foreach (GameObject sawmillObj in sawmills)
        {
            Sawmill sawmillScript = sawmillObj.GetComponent<Sawmill>();
            if (sawmillScript != null && sawmillScript.storedWood > 0)
            {
                float distance = Vector3.Distance(transform.position, sawmillObj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    targetSawmill = sawmillObj;
                }
            }
        }

        if (targetSawmill != null && attackController != null)
        {
            attackController.targetToAttack = targetSawmill.transform;
            if (_animator != null) _animator.SetBool("IsFollowing", true);
        }
    }

    private GameObject FindClosestWithTag(string tag)
    {
        GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
        GameObject closest = null;
        float closestDistance = Mathf.Infinity;

        foreach (GameObject obj in objects)
        {
            float distance = Vector3.Distance(transform.position, obj.transform.position);
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
        if (!other.CompareTag("Sawmill") && !other.CompareTag("Warehouse") && !other.CompareTag("FishHouse")) return;

        // 1. ЛЕСОРУБ пришел на ЛЕСОПИЛКУ
        if (role == UnitRole.Lumberjack && hasResourcesInPocket && other.CompareTag("Sawmill"))
        {
            Sawmill sawmill = other.GetComponent<Sawmill>();
            if (sawmill != null && _gettingResources != null)
            {
                sawmill.AddWood(_gettingResources.Wood); 
                ClearPocket();
                FindAndGoToClosestTree(); 
            }
        }

        // 2. РЫБАК пришел в РЫБОЛОВНЮ
        if (role == UnitRole.Fisher && hasResourcesInPocket && other.CompareTag("FishHouse"))
        {
            ClearPocket();
            Debug.Log("<color=cyan>Успех!</color> Рыбак сдал улов в Рыболовню.");
            FindAndGoToClosestFishingSpot();
        }

        // 3. НОСИЛЬЩИК пришел на ЛЕСОПИЛКУ
        if (role == UnitRole.Carrier && !hasResourcesInPocket && other.CompareTag("Sawmill"))
        {
            Sawmill sawmill = other.GetComponent<Sawmill>();
            if (sawmill != null && sawmill.storedWood > 0 && _gettingResources != null)
            {
                _gettingResources.Wood = sawmill.ExtractWood(MaxUnitResources);
                hasResourcesInPocket = true;
                if (attackController != null) attackController.targetToAttack = null; 
                GoToMainWarehouse(); 
            }
        }

        // 4. НОСИЛЬЩИК пришел на ГЛАВНЫЙ СКЛАД
        if (role == UnitRole.Carrier && hasResourcesInPocket && other.CompareTag("Warehouse"))
        {
            if (ResourcesManager.Instance != null && _gettingResources != null)
            {
                ResourcesManager.Instance.TakeResource(_gettingResources); 
                ClearPocket();
            }
        }
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

        if (_animator != null)
        {
            _animator.SetBool("IsFollowing", false);
            _animator.SetBool("isAttacking", false);
        }
    }
}
