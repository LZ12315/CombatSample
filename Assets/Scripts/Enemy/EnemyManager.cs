using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public struct EnemyInfo
{
    public string ID;
    public EnemyController controller;

    public EnemyInfo(string ID, EnemyController controller)
    {
        this.ID = ID;
        this.controller = controller;
    }
}

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance;

    [SerializeField] private List<EnemyInfo> enemiesInCombat = new List<EnemyInfo>();
    private Queue<EnemyInfo> enemiesToAttack = new Queue<EnemyInfo>();

    [SerializeField] private Vector2 attackIntervalRandom = new Vector2(3,5);
    private float attackIntervalCounter = 0;

    private void Awake()
    {
        Instance = this;
        attackIntervalCounter = UnityEngine.Random.Range(attackIntervalRandom.x, attackIntervalRandom.y);
    }

    public void AddEnemy(EnemyInfo enemy)
    {
        if(!enemiesInCombat.Contains(enemy))
            enemiesInCombat.Add(enemy);
    }

    public void RemoveEnemy(EnemyInfo enemy)
    {
        if (enemiesInCombat.Contains(enemy))
            enemiesInCombat.Remove(enemy);
    }

    public void EnemyTryAttack(EnemyInfo enemy)
    {
        if (enemiesInCombat.Contains(enemy) && !enemiesToAttack.Contains(enemy))
            enemiesToAttack.Enqueue(enemy);
    }

    private void Update()
    {
        if (enemiesToAttack.Count == 0) return;

        if(!enemiesInCombat.Any(enemy => enemy.controller.IsInState(Utils.Enums.EnemyStates.Attack)))
        {
            if (attackIntervalCounter > 0)
                attackIntervalCounter -= Time.deltaTime;
            else
            {
                string eventName = ReleaseAttack() + "Attack";
                EventCenter.Instance.EventTrigger(eventName);
                attackIntervalCounter = UnityEngine.Random.Range(attackIntervalRandom.x, attackIntervalRandom.y);
            }
        }
    }

    string ReleaseAttack()
    {
        if (enemiesToAttack.Count == 0) return null;

        return enemiesToAttack.Dequeue().ID;
    }

}
