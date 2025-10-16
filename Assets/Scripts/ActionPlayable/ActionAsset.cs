using UnityEngine;
using UnityEngine.Timeline;
using CombatSample.Consts;

[System.Serializable]
public class ActionAsset : ScriptableObject
{
    public bool isLoop;
    public Enums.ActorActionType actionType;
    public ActionAsset nextAction;

    [SerializeField, HideInInspector]
    private TimelineAsset _timelineAsset;
    public TimelineAsset TimelineAsset => _timelineAsset;

    public void SetTimelineAsset(TimelineAsset timelineAsset)
    { 
        _timelineAsset = timelineAsset; 
    }

}

public partial class Enums
{
    public enum ActorActionType
    {
        Normal, Combat
    }
}