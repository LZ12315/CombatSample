using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

// 这里CustomTimelineEditor限定的目标为TrackAsset
[CustomTimelineEditor(typeof(ActionTransitionTrack))]
public class ActionTransitionTrackEditor : TrackEditor
{
    // 以下函数用于优化Clip编辑流程，防止每次创建的Clip过长 设置新建默认片段时长为时间线总长度
    public override void OnCreate(TrackAsset track, TrackAsset copiedFrom)
    {
        // 如果是复制操作则跳过（保留原始内容）
        if (copiedFrom) return;

        if(track is ActionTransitionTrack actionTransitionTrack)
        {
            //获取创建Clip之前，TimeLine的总持续时间
            double duration = track.timelineAsset.duration;
            var clip = actionTransitionTrack.CreateDefaultClip();
            clip.duration = duration;
        }
    }
}

// 这里CustomTimelineEditor限定的目标为PlayableAsset
[CustomTimelineEditor(typeof(ActionTransitionAsset))]
class ActionTransitionClipEditor : ClipEditor
{

    public override void OnClipChanged(TimelineClip clip)
    {
        var asset = clip.asset as ActionTransitionAsset;
        if (asset == null) return;

        var inputType = asset.inputType.ToString();
        var nextActionName = asset.next != null ? asset.next.name : "无";

        clip.displayName = $"When {inputType} -> {nextActionName}";

        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }
}
