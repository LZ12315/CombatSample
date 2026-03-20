using System;
using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.9f, 0.3f, 0.9f)]
[TrackClipType(typeof(ActionMagnetismClip))]
[Obsolete("请改用 Action Magnetism V2 Track（根表面双向吸附）。")]
public class ActionMagnetismTrack : ActionTrackBase { }
