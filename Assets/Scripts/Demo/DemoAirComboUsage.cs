using UnityEngine;

/// <summary>Demo-only, per-Actor allowance: one air-combo opener per airtime.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Actor))]
[AddComponentMenu("CombatSample/Demo/Air Combo Usage")]
public sealed class DemoAirComboUsage : MonoBehaviour
{
    [SerializeField, Tooltip("Assign Action_Katana_AirCombo_1. Only successful starts of this asset consume the allowance.")]
    private ActionAsset _airComboStart;

    private Actor _actor;
    private ActionPlayer _player;
    private ActorMotor _motor;
    private readonly DemoAirComboUsageState _state = new();

    public int StartsThisAirtime => _state.StartsThisAirtime;
    public bool IsReady => isActiveAndEnabled && _actor != null && _actor.isActiveAndEnabled
        && _airComboStart != null && _player != null && _player.isActiveAndEnabled
        && _motor != null && _motor.isActiveAndEnabled;
    public bool CanStartAirCombo => IsReady && _state.CanStart(_motor.IsAirborne);

    private void OnEnable()
    {
        // Actor.Awake may not have populated its references yet. Also support an
        // ActionPlayer on a child, as used by the player prefab.
        _actor = GetComponent<Actor>();
        _player = _actor.actionPlayer != null
            ? _actor.actionPlayer : GetComponentInChildren<ActionPlayer>(true);
        _motor = _actor.actorMotor != null ? _actor.actorMotor : GetComponent<ActorMotor>();

        if (_player != null)
            _player.OnActionStarted += HandleActionStarted;
        if (_motor != null)
        {
            _motor.OnLanded += HandleLanded;
            if (_motor.IsGrounded)
                _state.Land();
        }
    }

    private void OnDisable()
    {
        if (_player != null)
            _player.OnActionStarted -= HandleActionStarted;
        if (_motor != null)
            _motor.OnLanded -= HandleLanded;
        // Disabling/re-enabling in midair is not a refund.
    }

    private void HandleActionStarted(ActionInstance action)
    {
        if (IsReady && action != null)
            _state.RecordStart(action.Config, _airComboStart, _motor.IsAirborne);
    }

    private void HandleLanded() => _state.Land();
}

/// <summary>Small demo rule kept independent of Unity lifecycle and repository assets.</summary>
internal sealed class DemoAirComboUsageState
{
    public int StartsThisAirtime { get; private set; }

    public bool CanStart(bool isAirborne) => isAirborne && StartsThisAirtime == 0;

    public void RecordStart(ActionAsset startedAction, ActionAsset airComboStart, bool isAirborne)
    {
        if (isAirborne && airComboStart != null && startedAction == airComboStart
            && StartsThisAirtime < int.MaxValue)
            StartsThisAirtime++;
    }

    public void Land() => StartsThisAirtime = 0;
}
