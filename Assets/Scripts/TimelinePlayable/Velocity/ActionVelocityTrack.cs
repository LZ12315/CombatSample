using UnityEngine;
using UnityEngine.Timeline;

/// <summary>Timeline Track — 持有 ActionVelocityClip。选色偏绿以与 Impulse（偏红）区分。</summary>
[TrackColor(0.2f, 0.7f, 0.4f)]
[TrackClipType(typeof(ActionVelocityClip))]
public class ActionVelocityTrack : ActionTrackBase { }
