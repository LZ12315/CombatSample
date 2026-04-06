using UnityEngine;

public interface IDamageable
{
    void TakeDamage(AttackHitData attackData);
}

public class ActorCombater : MonoBehaviour, IDamageable
{
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private bool _debugLog = true;
    [SerializeField] private Actor _actor;

    [SerializeField] private GameObject combatTarget;
    public GameObject CombatTarget => combatTarget;
    private float _currentHealth;

    private void Awake()
    {
        if (_actor == null)
            _actor = GetComponent<Actor>();
        _currentHealth = _maxHealth;
    }

    public void TakeDamage(AttackHitData attackData)
    {
        // 基础伤害应用
        _currentHealth -= attackData.Damage;

        ApplyHitEventTag(attackData);

        if (_debugLog)
        {
            Debug.Log($"[受到伤害] 来源: {attackData.Attacker.name}, " +
                     $"伤害: {attackData.Damage}, " +
                     $"剩余生命: {_currentHealth}/{_maxHealth}");
        }

        // 死亡检测
        if (_currentHealth <= 0)
        {
            Die(attackData);
        }
    }

    private void Die(AttackHitData attackData)
    {
        if (_debugLog) Debug.Log($"[死亡] {name} 被 {attackData.Attacker.name} 击败");
        // 死亡处理（动画、特效、掉落等）
        gameObject.SetActive(false);
    }

    private void ApplyHitEventTag(AttackHitData attackData)
    {
        if (_actor == null || attackData.HitEventTag == null)
            return;

        _actor.AddTag(attackData.HitEventTag, ActorTagContainerType.Transient);
    }
}
