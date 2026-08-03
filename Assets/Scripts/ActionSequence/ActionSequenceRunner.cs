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

        _context.Actor = ResolveActor();
        _runtime.Tick(_context, Time.fixedDeltaTime, speedScale);
    }

    public void Play(ActionSequenceAsset asset, ActionEventContext eventContext = default)
    {
        sequence = asset;
        _runtime = sequence != null ? new ActionSequenceRuntime(sequence) : null;

        _context.Actor = ResolveActor();
        _context.EventContext = eventContext;
    }

    public void Replay(ActionEventContext eventContext = default)
    {
        Play(sequence, eventContext);
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
