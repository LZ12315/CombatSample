using UnityEngine;

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
        if (_runtime == null || !_runtime.IsPlaying)
            return;

        if (!CombatSimulationTiming.IsGameplayFixedDeltaTime(Time.fixedDeltaTime))
        {
            Debug.LogError(
                $"ActionSequenceRunner requires Time.fixedDeltaTime to be {CombatSimulationTiming.FixedDeltaTime:R} ({CombatSimulationTiming.FrameRate} Hz), but it was {Time.fixedDeltaTime:R}.",
                this);
            Cancel();
            return;
        }

        _context.Actor = ResolveActor();
        _runtime.Tick(_context, Time.fixedDeltaTime, speedScale);
    }

    public void Play(ActionSequenceAsset asset)
    {
        Actor resolvedActor = ResolveActor();
        Play(asset, ActionContext.ForSelf(resolvedActor));
    }

    public void Play(ActionSequenceAsset asset, ActionContext context)
    {
        sequence = asset;
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
}
