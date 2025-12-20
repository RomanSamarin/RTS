using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    // Start is called before the first frame update
    public int health;
    internal void ReceiveDamage(int damageToInflict)
    {
        health -= damageToInflict;
    }
     void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
