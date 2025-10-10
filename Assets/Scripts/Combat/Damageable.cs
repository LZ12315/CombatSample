using UnityEngine;

public interface IDamageable
{
    void TakeDamage(AttackHitData attackData);
}

public class Damageable : MonoBehaviour, IDamageable
{
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private bool _debugLog = true;

    private float _currentHealth;

    private void Awake()
    {
        _currentHealth = _maxHealth;
    }

    public void TakeDamage(AttackHitData attackData)
    {
        // 基础伤害应用
        _currentHealth -= attackData.Damage;

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
}
