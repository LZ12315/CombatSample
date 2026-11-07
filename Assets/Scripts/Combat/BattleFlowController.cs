using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-local one-on-one battle results and restart. Keep this object outside
/// both combatants' hierarchies so either actor can be disabled on death.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Combat/Battle Flow Controller")]
public sealed class BattleFlowController : MonoBehaviour
{
    [SerializeField, Tooltip("Explicit player death source. Must belong to this battle scene.")]
    private ActorCombater playerCombater;

    [SerializeField, Tooltip("Defeating this enemy wins the battle. If empty, auto-binds only when this scene has exactly one other active ActorCombater.")]
    private ActorCombater enemyCombater;

    [SerializeField, Min(0f), Tooltip("Real seconds showing the victory or defeat countdown before the restart transition.")]
    private float restartDelaySeconds = 3f;

    [SerializeField, Min(0f), Tooltip("Real seconds showing the restart transition before scene loading actually begins.")]
    private float restartTransitionSeconds = 2f;

    private ActorCombater _boundPlayer;
    private ActorCombater _boundEnemy;
    private bool _started;
    private bool _resultPending;
    private bool _restartPending;
    private BattleResultView _resultView;

    private void Start()
    {
        // All scene Awake calls must finish before inspecting initial health.
        _started = true;
        BindCombatants();
    }

    private void OnEnable()
    {
        if (_started)
            BindCombatants();
    }

    private void OnDisable()
    {
        if (_boundPlayer != null)
            _boundPlayer.Died -= OnCombatantDied;
        if (_boundEnemy != null)
            _boundEnemy.Died -= OnCombatantDied;
        _boundPlayer = null;
        _boundEnemy = null;
        StopAllCoroutines();
        _resultView?.Hide();
        _resultPending = false;
        _restartPending = false;
    }

    private void BindCombatants()
    {
        if (playerCombater == null)
        {
            Debug.LogError("[Battle Flow] Assign the player's ActorCombater in the Inspector.", this);
            enabled = false;
            return;
        }

        if (!ValidateCombatant(playerCombater))
            return;

        if (enemyCombater == null)
            enemyCombater = FindSingleEnemy();
        if (enemyCombater == playerCombater)
        {
            Debug.LogError("[Battle Flow] Player Combater and Enemy Combater must be different actors.", this);
            enabled = false;
            return;
        }
        if (enemyCombater != null && !ValidateCombatant(enemyCombater))
            return;

        _boundPlayer = playerCombater;
        _boundEnemy = enemyCombater;
        _boundPlayer.Died += OnCombatantDied;
        if (_boundEnemy != null)
            _boundEnemy.Died += OnCombatantDied;

        // Also handles a controller enabled after a combatant has already died.
        _resultPending = _boundPlayer.IsDead || (_boundEnemy != null && _boundEnemy.IsDead);
    }

    private bool ValidateCombatant(ActorCombater combatant)
    {
        if (!transform.IsChildOf(combatant.transform) && combatant.gameObject.scene == gameObject.scene)
            return true;

        Debug.LogError("[Battle Flow] Place this controller outside both combatants' hierarchies, in the same battle scene.", this);
        enabled = false;
        return false;
    }

    private ActorCombater FindSingleEnemy()
    {
        ActorCombater candidate = null;
        foreach (ActorCombater combatant in FindObjectsOfType<ActorCombater>())
        {
            if (combatant == playerCombater || !combatant.isActiveAndEnabled || combatant.gameObject.scene != gameObject.scene)
                continue;
            if (candidate != null)
            {
                Debug.LogWarning("[Battle Flow] Multiple possible enemies. Assign Enemy Combater explicitly; automatic victory is disabled until configured.", this);
                return null;
            }
            candidate = combatant;
        }

        if (candidate == null)
            Debug.LogWarning("[Battle Flow] No active enemy found. Assign Enemy Combater to enable victory results. Player defeat still restarts the battle.", this);
        return candidate;
    }

    private void OnCombatantDied(ActorCombater combatant, AttackHitData hit)
    {
        if (!_restartPending && (combatant == _boundPlayer || combatant == _boundEnemy))
            _resultPending = true;
    }

    private void LateUpdate()
    {
        if (!_resultPending || _restartPending)
            return;

        // Settle after this frame's fixed hit batches, not inside a death callback.
        // If both actors die in that frame, defeat takes priority over victory.
        _resultPending = false;
        BattleOutcome outcome = BattleOutcomeRules.Resolve(
            _boundPlayer != null && _boundPlayer.IsDead,
            _boundEnemy != null && _boundEnemy.IsDead);
        if (outcome == BattleOutcome.InProgress)
            return;

        _restartPending = true;
        StartCoroutine(RestartBattle(outcome));
    }

    private IEnumerator RestartBattle(BattleOutcome outcome)
    {
        // A single real-time clock drives the countdown, progress line and fades.
        double countdownStartedAt = Time.realtimeSinceStartupAsDouble;
        float countdownDuration = Mathf.Max(0f, restartDelaySeconds);
        double countdownEndsAt = countdownStartedAt + countdownDuration;
        _resultView ??= BattleResultView.Create(transform);
        _resultView?.SetOutcome(outcome);
        while (Time.realtimeSinceStartupAsDouble < countdownEndsAt)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float remaining = Mathf.Max(0f, (float)(countdownEndsAt - now));
            _resultView?.ShowCountdown(
                Mathf.Max(1, Mathf.CeilToInt(remaining)),
                countdownDuration > 0f ? remaining / countdownDuration : 0f,
                (float)(now - countdownStartedAt));
            yield return null;
        }

        // This is a presentation hold, NOT scene loading. Start its full duration
        // after the countdown finishes, even when a slow frame crosses the boundary.
        double transitionStartedAt = Time.realtimeSinceStartupAsDouble;
        float transitionDuration = Mathf.Max(0f, restartTransitionSeconds);
        do
        {
            _resultView?.ShowRestarting((float)(Time.realtimeSinceStartupAsDouble - transitionStartedAt));
            yield return null;
        }
        while (Time.realtimeSinceStartupAsDouble - transitionStartedAt < transitionDuration);

        // Keep the fully dark transition visible during the actual async load.
        _resultView?.ShowRestarting(Mathf.Max(1f, transitionDuration));

        string scenePath = gameObject.scene.path;
        if (string.IsNullOrEmpty(scenePath) || !Application.CanStreamedLevelBeLoaded(scenePath))
        {
            _resultView?.ShowRestartFailed();
            Debug.LogError($"[Battle Flow] Cannot reload '{scenePath}'. Save the battle scene and add it to the enabled Build Settings scenes.", this);
            yield break;
        }

        // Scene loading does not reset global time scale.
        Time.timeScale = 1f;
        yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
    }
}
