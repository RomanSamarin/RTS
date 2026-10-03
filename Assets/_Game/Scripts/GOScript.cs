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

    private float baseSpeed; // Создали переменную для хранения стартовой скорости

    private void Start()
    {
        camera = Camera.main;
        agent = GetComponent<NavMeshAgent>();
        
        if (agent != null)
        {
            baseSpeed = agent.speed; // Запоминаем скорость, настроенную в инспекторе юнита
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
            }
        } 

        if (agent != null && agent.isOnNavMesh)
        {
            NavMeshHit navMeshHit; // ИСПРАВЛЕНО: Переименовали в navMeshHit
            
            // Считываем информацию о текущем полигоне под ногами юнита
            if (agent.SamplePathPosition(NavMesh.AllAreas, 0.0f, out navMeshHit))
            {
                int roadAreaIndex = NavMesh.GetAreaFromName("Road");
                bool isOnRoad = (navMeshHit.mask & (1 << roadAreaIndex)) != 0;

                if (isOnRoad)
                {
                    agent.speed = baseSpeed * 1.5f; // Увеличиваем скорость на 50%
                }
                else
                {
                    agent.speed = baseSpeed; // Возвращаем базовую скорость
                }
            }
        }
        
        if (agent != null && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance))
        {
            isCommandToMove = false;
        }
    }
}
