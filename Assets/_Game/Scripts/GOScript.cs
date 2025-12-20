using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;   // ← правильный using для NavMeshAgent

public class GOScript : MonoBehaviour
{
    public Camera camera;
    public NavMeshAgent agent;
    public LayerMask Ground;
    public bool isCommandToMove;

    private void Start()
    {
        camera = Camera.main;
        agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            RaycastHit hit;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, Ground))
            {
                isCommandToMove = true;
                agent.SetDestination(hit.point);
            }
        }
        if (agent.hasPath == false || agent.remainingDistance <= agent.stoppingDistance)
        {
            isCommandToMove = false;
        }
    }
}