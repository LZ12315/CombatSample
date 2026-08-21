using UnityEngine;

public sealed class ActionSequenceContext
{
    public Actor Actor { get; set; }
    public ActionContext Context { get; set; }
    public int Frame { get; internal set; }
    public float PoseFrame { get; internal set; }
    public int FrameRate { get; internal set; } = CombatSimulationTiming.FrameRate;
    public float DeltaTime { get; internal set; }
    public float SpeedScale { get; internal set; } = 1f;
    public bool IsPoseBaseline { get; internal set; }
    public bool IsPoseRefresh { get; internal set; }
    public object UserData { get; set; }

    public GameObject Owner => Actor != null ? Actor.gameObject : null;
    internal ActorHitBoxRuntime HitBoxes { get; set; }
}
