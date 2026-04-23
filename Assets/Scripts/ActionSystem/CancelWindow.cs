using System;
using UnityEngine;

/// <summary>
/// 帧锚点类型：决定帧数取值方式。
/// </summary>
public enum FrameAnchor
{
    /// <summary>手动指定帧数。</summary>
    Custom = 0,

    /// <summary>锚定到动作第 0 帧。</summary>
    Start = 1,

    /// <summary>锚定到动作最后一帧（由 totalFrames 运行时解析）。</summary>
    End = 2,
}

/// <summary>
/// 取消窗口（以帧数为唯一计量单位）。
/// 用于 <see cref="CancelRule"/> 指定"当前动作在第几帧到第几帧之间允许被取消到目标动作"。
///
/// <para>设计约定：</para>
/// <list type="bullet">
///   <item><b>帧数唯一</b>：不使用 NormalizedTime。帧号由 <see cref="ActionPlayer.CurrentFrame"/> 维护，
///   等于 <c>Floor(director.time * frameRate)</c>。HitStop 冻结 director.time → 帧号自然冻结，语义天然正确。</item>
///   <item><b>startAnchor / startFrame</b>：窗口开启帧（包含）。锚点为 <see cref="FrameAnchor.Start"/> 时等价于帧 0；
///   为 <see cref="FrameAnchor.Custom"/> 时使用 <c>startFrame</c> 的值。</item>
///   <item><b>endAnchor / endFrame</b>：窗口结束帧（包含）。锚点为 <see cref="FrameAnchor.End"/> 时等价于动作最后一帧；
///   为 <see cref="FrameAnchor.Custom"/> 时使用 <c>endFrame</c> 的值。</item>
///   <item><b>FullRange</b>：便捷静态实例 {Start, End}，表示整段动作都可取消。</item>
/// </list>
/// </summary>
[Serializable]
public struct CancelWindow
{
    [Tooltip("起始帧锚点。Start = 从动作开头；Custom = 手动填帧数。")]
    public FrameAnchor startAnchor;

    [Min(0), Tooltip("窗口起始帧（含）。仅 startAnchor = Custom 时有效。")]
    public int startFrame;

    [Tooltip("结束帧锚点。End = 到动作结尾；Custom = 手动填帧数。")]
    public FrameAnchor endAnchor;

    [Tooltip("窗口结束帧（含）。仅 endAnchor = Custom 时有效。")]
    public int endFrame;

    /// <summary>
    /// 根据锚点解析实际的起始帧号。
    /// </summary>
    public int ResolveStart(int totalFrames)
    {
        return startAnchor switch
        {
            FrameAnchor.Start => 0,
            FrameAnchor.End => totalFrames,
            _ => startFrame, // Custom
        };
    }

    /// <summary>
    /// 根据锚点解析实际的结束帧号。
    /// </summary>
    public int ResolveEnd(int totalFrames)
    {
        return endAnchor switch
        {
            FrameAnchor.Start => 0,
            FrameAnchor.End => totalFrames,
            _ => endFrame, // Custom
        };
    }

    /// <summary>
    /// 判定给定帧号是否落在窗口内。
    /// <paramref name="totalFrames"/> 为该动作的总帧数，用于解析 <see cref="FrameAnchor.End"/> 锚点。
    /// </summary>
    public bool ContainsFrame(int currentFrame, int totalFrames)
    {
        int start = ResolveStart(totalFrames);
        int end = ResolveEnd(totalFrames);

        if (currentFrame < start)
            return false;
        return currentFrame <= end;
    }

    /// <summary>整段动作都可取消的便捷值：起始帧 0（Custom），结束锚定 End。</summary>
    public static CancelWindow FullRange => new CancelWindow
    {
        startAnchor = FrameAnchor.Custom,
        startFrame = 0,
        endAnchor = FrameAnchor.End,
        endFrame = 0,
    };
}
