using System;
using UnityEngine;

[Obsolete("Gameplay ActionSequence playback is owned by ActionPlayer and CombatSimulationDriver. This compatibility component no longer advances runtime gameplay.")]
[DefaultExecutionOrder(-40)]
public sealed class ActionSequenceRunner : MonoBehaviour
{
    [SerializeField]
    private Actor actor;

    [SerializeField]
    private ActionSequenceAsset sequence;

    [SerializeField]
    private bool playOnEnable;

    [SerializeField, Min(0f)]
    private float speedScale = 1f;

    private ActionSequenceRuntime _runtime;
    private readonly ActionSequenceContext _context = new ActionSequenceContext();
    private bool _reportedRuntimeRejection;

    public ActionSequenceAsset Sequence => sequence;
    public ActionSequenceRuntime Runtime => _runtime;
    public bool IsPlaying => _runtime != null && _runtime.IsPlaying;

    private void Awake()
    {
        ResolveActor();
    }

    private void OnEnable()
    {
        if (playOnEnable && sequence != null)
            Play(sequence);
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void FixedUpdate()
    {
        if (_runtime == null)
            return;

        ReportRuntimeRejection();
        Cancel();
    }

    public void Play(ActionSequenceAsset asset)
    {
        Actor resolvedActor = ResolveActor();
        Play(asset, ActionContext.ForSelf(resolvedActor));
    }

    public void Play(ActionSequenceAsset asset, ActionContext context)
    {
        sequence = asset;
        if (Application.isPlaying)
        {
            _runtime = null;
            ReportRuntimeRejection();
            return;
        }

        if (sequence != null && !CombatSimulationTiming.IsGameplayFrameRate(sequence.FrameRate))
        {
            Debug.LogError(
                $"ActionSequenceRunner rejected '{sequence.name}' because Gameplay ActionSequence frame rate must be {CombatSimulationTiming.FrameRate} Hz, but data uses {sequence.FrameRate} Hz.",
                this);
            _runtime = null;
            return;
        }

        _runtime = sequence != null ? new ActionSequenceRuntime(sequence) : null;

        _context.Actor = ResolveActor();
        _context.Context = context;

        ActionSequenceRuntimeDiagnostics diagnostics = _runtime?.Diagnostics;
        if (diagnostics != null && diagnostics.HasIssues)
            Debug.LogWarning(diagnostics.ToSummary("ActionSequenceRunner runtime diagnostics"), this);
    }

    public void Replay()
    {
        Actor resolvedActor = ResolveActor();
        Replay(ActionContext.ForSelf(resolvedActor));
    }

    public void Replay(ActionContext context)
    {
        Play(sequence, context);
    }

    public void Cancel()
    {
        if (_runtime == null)
            return;

        _context.Actor = ResolveActor();
        _runtime.Cancel(_context);
        _runtime = null;
    }

    private Actor ResolveActor()
    {
        if (actor == null)
            actor = GetComponent<Actor>();
        if (actor == null)
            actor = GetComponentInParent<Actor>();

        return actor;
    }

    private void ReportRuntimeRejection()
    {
        if (_reportedRuntimeRejection)
            return;

        _reportedRuntimeRejection = true;
        Debug.LogWarning(
            "ActionSequenceRunner is deprecated and cannot advance Gameplay Sequence in Play Mode. " +
            "Use ActionPlayer through CombatSimulationDriver's Action Phase.",
            this);
    }
}
