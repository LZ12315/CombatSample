using System.Collections.Generic;
using UnityEngine;

public interface IDamageable
{
    void TakeDamage(AttackHitData attackData);
}

public class ActorCombater : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private bool _debugLog = true;
    [SerializeField] private Actor _actor;
    [SerializeField] private ActionStateManager _asm;

    [Header("Targeting")]
    [SerializeField] private float autoAcquireRadius = 15f;
    [SerializeField] private float lockRetainRadius = 25f;
    [SerializeField] private Enums.LockMode lockMode = Enums.LockMode.None;

    [SerializeField] private GameObject combatTarget;
    public GameObject CombatTarget => combatTarget;
    public Enums.LockMode LockMode => lockMode;
    public bool IsLocked => lockMode != Enums.LockMode.None;

    private float _currentHealth;

    private static readonly List<ActorCombater> _activeCombaters = new List<ActorCombater>();

    private void Awake()
    {
        if (_actor == null)
            _actor = GetComponent<Actor>();
        if (_asm == null)
            _asm = GetComponent<ActionStateManager>();
        _currentHealth = _maxHealth;
    }

    private void OnEnable()
    {
        if (!_activeCombaters.Contains(this))
            _activeCombaters.Add(this);
    }

    private void OnDisable()
    {
        _activeCombaters.Remove(this);
    }

    private void OnDestroy()
    {
        _activeCombaters.Remove(this);
    }

    private void Update()
    {
        MaintainTarget();
    }

    #region === Targeting ===

    private void MaintainTarget()
    {
        if (lockMode == Enums.LockMode.None)
        {
            combatTarget = FindBestAutoTarget()?.gameObject;
        }
        else
        {
            if (IsValidTarget(combatTarget, lockRetainRadius))
            {
                // keep current lock target
            }
            else if (TryFindBestLockFallback(out var fallback))
            {
                combatTarget = fallback;
            }
            else
            {
                ClearLock();
            }
        }
    }

    private ActorCombater FindBestAutoTarget()
    {
        ActorCombater best = null;
        float bestScore = float.MinValue;
        ActorCombater currentTarget = combatTarget != null ? combatTarget.GetComponent<ActorCombater>() : null;

        for (int i = _activeCombaters.Count - 1; i >= 0; i--)
        {
            ActorCombater candidate = _activeCombaters[i];
            if (candidate == null)
            {
                _activeCombaters.RemoveAt(i);
                continue;
            }

            if (!IsValidTarget(candidate.gameObject, autoAcquireRadius)) continue;

            float score = ScoreTarget(candidate, currentTarget, autoAcquireRadius);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private bool TryFindBestLockFallback(out GameObject fallback)
    {
        ActorCombater best = null;
        float bestScore = float.MinValue;

        for (int i = _activeCombaters.Count - 1; i >= 0; i--)
        {
            ActorCombater candidate = _activeCombaters[i];
            if (candidate == null)
            {
                _activeCombaters.RemoveAt(i);
                continue;
            }

            if (!IsValidTarget(candidate.gameObject, lockRetainRadius)) continue;

            float score = ScoreTarget(candidate, null, lockRetainRadius);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        fallback = best != null ? best.gameObject : null;
        return fallback != null;
    }

    private bool IsValidTarget(GameObject target, float radius)
    {
        if (target == null) return false;
        if (!target.activeInHierarchy) return false;
        if (target == gameObject) return false;

        ActorCombater combater = target.GetComponent<ActorCombater>();
        if (combater == null) return false;
        if (!combater.enabled) return false;

        float dist = Vector3.Distance(transform.position, target.transform.position);
        return dist <= radius;
    }

    private float ScoreTarget(ActorCombater candidate, ActorCombater currentTarget, float scoreRadius)
    {
        Vector3 toTarget = candidate.transform.position - transform.position;
        float dist = toTarget.magnitude;

        // Distance score: closer is better (0 to 1 range)
        float distScore = 1f - Mathf.Clamp01(dist / Mathf.Max(0.001f, scoreRadius));

        // Forward facing bonus: prefer targets in front (0 to 0.5 range)
        Vector3 forward = transform.forward;
        Vector3 toTargetXZ = toTarget;
        forward.y = 0f;
        toTargetXZ.y = 0f;
        float angleScore = 0f;
        if (toTargetXZ.sqrMagnitude > 0.001f)
        {
            float dot = Vector3.Dot(forward.normalized, toTargetXZ.normalized);
            angleScore = (dot + 1f) * 0.25f;
        }

        // Current target retention bonus
        float retainBonus = (currentTarget != null && candidate == currentTarget) ? 0.3f : 0f;

        return distScore + angleScore + retainBonus;
    }

    #endregion

    #region === Lock API ===

    public bool TryEnterSoftLock()
    {
        if (lockMode != Enums.LockMode.None) return false;

        GameObject target = IsValidTarget(combatTarget, lockRetainRadius)
            ? combatTarget
            : FindBestAutoTarget()?.gameObject;
        if (target == null) return false;

        combatTarget = target;
        lockMode = Enums.LockMode.SoftLock;
        return true;
    }

    public bool TryEnterHardLock()
    {
        if (lockMode == Enums.LockMode.HardLock) return false;

        GameObject target = IsValidTarget(combatTarget, lockRetainRadius)
            ? combatTarget
            : FindBestAutoTarget()?.gameObject;
        if (target == null) return false;

        combatTarget = target;
        lockMode = Enums.LockMode.HardLock;
        return true;
    }

    public void ClearLock()
    {
        lockMode = Enums.LockMode.None;
        combatTarget = FindBestAutoTarget()?.gameObject;
    }

    public void ToggleSoftLock()
    {
        if (lockMode == Enums.LockMode.SoftLock)
            ClearLock();
        else
            TryEnterSoftLock();
    }

    #endregion

    #region === Damage ===

    public void TakeDamage(AttackHitData attackData)
    {
        _currentHealth -= attackData.Damage;

        ApplyHitEventTag(attackData);

        if (_debugLog)
        {
            string attackerName = attackData.Attacker != null ? attackData.Attacker.name : "Unknown";
            Debug.Log($"[受到伤害] 来源: {attackerName}, " +
                     $"伤害: {attackData.Damage}, " +
                     $"剩余生命: {_currentHealth}/{_maxHealth}");
        }

        if (_currentHealth <= 0)
        {
            Die(attackData);
        }
    }

    private void Die(AttackHitData attackData)
    {
        string attackerName = attackData.Attacker != null ? attackData.Attacker.name : "Unknown";
        if (_debugLog) Debug.Log($"[死亡] {name} 被 {attackerName} 击败");
        gameObject.SetActive(false);
    }

    private void ApplyHitEventTag(AttackHitData attackData)
    {
        if (attackData.HitEventTag == null)
            return;

        if (_asm != null)
        {
            var context = new ActionEventContext
            {
                Instigator = attackData.Attacker != null ? attackData.Attacker.gameObject : null,
                Target     = gameObject,
                HitPoint   = attackData.HitPoint,
                Direction  = attackData.Attacker != null
                    ? (transform.position - attackData.Attacker.transform.position).normalized
                    : Vector3.zero,
                Magnitude  = attackData.Damage
            };
            _asm.SendEvent(attackData.HitEventTag, context);
        }
    }

    #endregion
}

public partial class Enums
{
    public enum LockMode
    {
        None, SoftLock, HardLock
    }
}
