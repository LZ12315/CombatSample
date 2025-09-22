using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Timeline;
using Animancer;
using UnityEngine.Timeline;

[CustomTimelineEditor(typeof(ActionTrackBase))]
public class ActionTrackEditorBase : TrackEditor
{
    public override void OnCreate(TrackAsset track, TrackAsset copiedFrom)
    {
        if (!(track is ActionTrackBase actionTrack)) return;

        // 如果是复制操作，不修改时长
        if (copiedFrom != null) return;

        // 获取时间线资源
        var timelineAsset = track.timelineAsset;
        if (timelineAsset == null)
            return;

        // 计算合适的时长
        double duration = timelineAsset.duration;

        // 设置最小时长（例如1秒）
        const double minDuration = 1.0;
        if (duration < minDuration)
        {
            duration = minDuration;
        }

        // 创建并配置clip
        var clip = actionTrack.CreateDefaultClip();
        if (clip != null)
            clip.duration = duration;
    }
}


[CustomTimelineEditor(typeof(ActionClipBase))]
public class ActionClipEditorBase : ClipEditor
{
    //防止Clip起始时间过于靠近0
    //否则会导致逻辑错误: OnBehavior等方法被错误触发
    //因此自动向右偏移0.04f 也就是2.4帧
    protected void AdjustClipStartTime(TimelineClip clip)
    {
        if (clip.start <= 0.05f)
            clip.start = 0.04f;
    }

}
