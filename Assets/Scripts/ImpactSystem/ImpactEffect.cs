using UnityEngine;

/// <summary>
/// 打击效果基类
/// 所有具体打击效果（时间停顿、屏幕震动等）的抽象基类
/// </summary>
public abstract class ImpactEffect
{
    /// <summary>
    /// 执行打击效果
    /// </summary>
    /// <param name="impactData">打击数据</param>
    public abstract void Execute(ImpactData impactData);
    
    /// <summary>
    /// 每帧更新效果
    /// </summary>
    /// <returns>返回false表示效果结束，可以回收</returns>
    public abstract bool Update();
    
    /// <summary>
    /// 重置效果状态，用于对象池回收
    /// </summary>
    public abstract void Reset();
    
    /// <summary>
    /// 效果是否正在运行
    /// </summary>
    public virtual bool IsActive { get; protected set; }
}