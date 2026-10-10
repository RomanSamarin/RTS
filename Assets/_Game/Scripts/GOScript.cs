using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GOScript : MonoBehaviour
{
    public Camera camera;
    public NavMeshAgent agent;
    public LayerMask Ground;
    public bool isCommandToMove;
    Animator animator;
    DirectionIndicator directionIndicator;

    private float baseSpeed; 
    private void Start()
    {
        camera = Camera.main;
        directionIndicator = GetComponent<DirectionIndicator>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        
        if (agent != null)
        {
            baseSpeed = agent.speed; 
        }
    }

    private void Update()
{
    // 1. Логика изменения скорости на дороге (оставляем без изменений)
    if (agent != null && agent.isOnNavMesh)
    {
        NavMeshHit navMeshHit; 
        if (agent.SamplePathPosition(NavMesh.AllAreas, 0.0f, out navMeshHit))
        {
            int roadAreaIndex = NavMesh.GetAreaFromName("Road");
            if (roadAreaIndex != -1)
            {
                bool isOnRoad = (navMeshHit.mask & (1 << roadAreaIndex)) != 0;
                if (isOnRoad)
                {
                    agent.speed = baseSpeed * 1.5f; 
                }
                else
                {
                    agent.speed = baseSpeed; 
                }
            }
        }
    }
    
    // 2. УПРАВЛЕНИЕ АНИМАЦИЕЙ И ФЛАГОМ ДВИЖЕНИЯ
    // Проверяем реальное физическое движение агента
    if (agent != null && agent.hasPath && agent.velocity.sqrMagnitude > 0.01f)
    {
        isCommandToMove = true;
        if (animator != null)
        {
            animator.SetBool("isMoving", true);
        }
    }
    // Если агент дошел до цели и остановился
    else if (agent != null && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
    {
        isCommandToMove = false;
        if (animator != null)
        {
            animator.SetBool("isMoving", false);
        }
    }
}


}
