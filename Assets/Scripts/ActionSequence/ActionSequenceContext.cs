using UnityEngine;

public sealed class ActionSequenceContext
{
    public Actor Actor { get; set; }
    public ActionEventContext EventContext { get; set; }
    public int Frame { get; internal set; }
    public int FrameRate { get; internal set; } = 60;
    public float DeltaTime { get; internal set; }
    public float SpeedScale { get; internal set; } = 1f;
    public object UserData { get; set; }

    public GameObject Owner => Actor != null ? Actor.gameObject : null;
}
