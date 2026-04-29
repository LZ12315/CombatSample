/// <summary>
/// 轻量句柄：标识一次 Clip 级持续运动控制的归属，防止旧 Clip Stop 误清新 Clip 写入的状态。
/// </summary>
public readonly struct MotionControlOwner
{
    public readonly int Id;

    public MotionControlOwner(int id)
    {
        Id = id;
    }

    public bool IsValid => Id != 0;
}
