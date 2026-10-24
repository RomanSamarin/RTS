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
    DirectionIndicator directionIndicator;

    private float baseSpeed; 
    private void Start()
    {
        camera = Camera.main;
        directionIndicator = GetComponent<DirectionIndicator>();
        agent = GetComponent<NavMeshAgent>();
        
        if (agent != null)
        {
            baseSpeed = agent.speed; 
        }
    }

    private void Update()
    {
        
        if (Input.GetMouseButtonDown(1))
        {
            RaycastHit mouseHit; // 
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out mouseHit, Mathf.Infinity, Ground))
            {
                isCommandToMove = true;
                agent.SetDestination(mouseHit.point);
                Debug.Log("GrounMarker avaible");
                directionIndicator.DrawLine(mouseHit);
            }
        } 

        if (agent != null && agent.isOnNavMesh)
        {
            NavMeshHit navMeshHit; 
            
            if (agent.SamplePathPosition(NavMesh.AllAreas, 0.0f, out navMeshHit))
            {
                int roadAreaIndex = NavMesh.GetAreaFromName("Road");
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
        
        // Замените ваш нижний блок if-else на этот
                // Проверяем: если путь еще считается, то ничего не делаем и ждем
                    // Ваша исходная логика проверки остановки
                    if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance)
                    {
                        isCommandToMove = false;
                
                    }

    }
}
