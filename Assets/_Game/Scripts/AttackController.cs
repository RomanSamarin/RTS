using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackController : MonoBehaviour
{
    // Start is called before the first frame update
    public Transform targetToAttack;
    public int unitDamage;
    public bool IsPlayer;

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer && other.CompareTag("Enemy") && targetToAttack == null )
        {
            // Проверка: если дерево уже уничтожается, игнорируем его
            Unit targetUnit = other.GetComponent<Unit>();
            if (targetUnit != null && targetUnit.unitHealth <= 0) return;

            targetToAttack = other.transform;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsPlayer && other.CompareTag("Enemy") && targetToAttack == null)
        {
            // Проверка: если дерево уже уничтожается, игнорируем его
            Unit targetUnit = other.GetComponent<Unit>();
            if (targetUnit != null && targetUnit.unitHealth <= 0) return;

            targetToAttack = other.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer && other.CompareTag("Enemy") && targetToAttack != null)
        {
            targetToAttack = null;
        }
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Сюда вы можете вставить вашу логику нанесения урона.
        // Чтобы ресурсы передавались, урон нужно наносить вот так:
        // if (targetToAttack != null) 
        // {
        //     Unit enemy = targetToAttack.GetComponent<Unit>();
        //     if (enemy != null) enemy.TakeDamage(unitDamage, gameObject);
        // }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 10f);

        Gizmos.color = Color.red ;
        Gizmos.DrawWireSphere(transform.position, 3f);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, 3.6f);
    }
}
