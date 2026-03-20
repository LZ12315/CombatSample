using UnityEngine;
using UnityEngine.Timeline;
using CombatSample.TimelinePlayable.Magnetism;

namespace CombatSample.TimelinePlayable.Magnetism
{
    [TrackColor(0.2f, 0.8f, 0.2f)]
    [TrackClipType(typeof(ActionMagnetismV2Clip))]
    public class ActionMagnetismV2Track : ActionTrackBase { }
}

