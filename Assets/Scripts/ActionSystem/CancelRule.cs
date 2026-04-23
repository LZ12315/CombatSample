using System;
using DeiveEx.TagTree;
using UnityEngine;

/// <summary>
/// 取消规则的目标类型。
/// </summary>
public enum CancelTargetKind
{
    /// <summary>指向某个具体的 ActionAsset。</summary>
    SpecificAction = 0,

    /// <summary>匹配所有 SelfTags 中包含指定 Tag 的 ActionAsset（通过反向索引 O(1) 查表）。</summary>
    AnyWithTag = 1,

    /// <summary>不做任何目标过滤，把 ActionList 全表（跳过 Event 模式）送入候选。</summary>
    Any = 2,
}

/// <summary>
/// 取消规则：当某 Action 播放到 <see cref="window"/> 覆盖的帧范围时，允许取消到
/// <see cref="specificTarget"/>（SpecificAction）、所有 SelfTags 含 <see cref="targetTag"/> 的 Action（AnyWithTag）、
/// 或全表所有 Action（Any）。
///
/// <para><b>职责边界</b>：CancelRule 只负责"把候选加入仲裁列表"。目标 Action 是否真能进入仍由其自身的
/// <c>CheckEntry</c>（EntryConditions）决定。两层解耦，互不越权。</para>
///
/// <para><b>与 SelfTags 的配合（AnyWithTag）</b>：目标 Tag 通过 <see cref="ActionStateManager"/> 在
/// <c>Awake</c> 构建的 <c>_selfTagIndex</c>（Dictionary&lt;Tag.Id, List&lt;ActionAsset&gt;&gt;）查表，
/// 避免每次取消时扫全表。</para>
/// </summary>
[Serializable]
public class CancelRule
{
    [Tooltip("目标选择方式：SpecificAction 直接指向一个 Action；AnyWithTag 匹配所有 SelfTags 含指定 Tag 的 Action；Any 全表开放。")]
    public CancelTargetKind targetKind = CancelTargetKind.SpecificAction;

    [Tooltip("仅 targetKind = SpecificAction 时使用。")]
    public ActionAsset specificTarget;

    [Tooltip("仅 targetKind = AnyWithTag 时使用。匹配目标 Action 的 SelfTags 中是否包含此 Tag。")]
    public TagReference targetTag;

    [Tooltip("允许取消的帧窗口。可通过 FrameAnchor 锚点选择从头开始/到结尾，无需手动查帧数。")]
    public CancelWindow window = CancelWindow.FullRange;
}
