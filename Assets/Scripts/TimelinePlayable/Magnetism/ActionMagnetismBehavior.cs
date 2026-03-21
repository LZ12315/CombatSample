using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Timeline：解析目标 → <see cref="ActionMagnetismSession"/>（片段内朝向敌人 + 距离门控）。
/// </summary>
public class ActionMagnetismBehavior : ActionBehaviourBase
{
    public bool useCombatTarget;
    public Transform customTarget;
    public MagnetismConfig config;

    private Transform _targetTransform;
    private ActionMagnetismSession _session;

    protected override void OnClipStart(Playable playable)
    {
        if (actor == null || config == null) return;

        _targetTransform = useCombatTarget ? actor.combater?.CombatTarget?.transform : customTarget;
        if (_targetTransform == null) return;

        _session = new ActionMagnetismSession(actor, _targetTransform, config);
        _session.Begin();
    }

    protected override void OnClipUpdate(Playable playable, FrameData info)
    {
        _session?.Tick();
    }

    protected override void OnClipStop(bool isNormal)
    {
        _session?.End();
        _session = null;
        _targetTransform = null;
    }
}
