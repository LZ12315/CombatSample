using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.8f, 0.2f, 0.2f)]
[TrackClipType(typeof(ActionTagCleanupClip))]
[TrackClipType(typeof(ActionInputBufferCleanupClip))]
public class ActionCleanupTrack : ActionTrackBase { }
