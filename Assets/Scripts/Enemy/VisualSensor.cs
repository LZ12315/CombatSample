using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisualSensor : MonoBehaviour
{
    [SerializeField] EnemyController enemyController;

    private void Start()
    {
        if(enemyController == null)
            enemyController = GetComponentInParent<EnemyController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        var target = other.GetComponent<MeleeAttacker>();
        if (target == null) return;
        enemyController.detectTarget.Add(target);
    }


    private void OnTriggerExit(Collider other)
    {
        var target = other.GetComponent<MeleeAttacker>();
        if (target == null) return;

        enemyController.detectTarget.Remove(target);
    }

}
