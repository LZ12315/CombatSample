using UnityEngine;
using UnityEngine.Timeline;
using CombatSample.Consts;

[System.Serializable]
public class ActionAssetData
{
    public double nomalizedTime = 0;
    public Enums.ActionPhase phase = Enums.ActionPhase.Neutral;
    public Enums.ActionProgress progress = Enums.ActionProgress.Finish;
}

public class ActionAsset : ScriptableObject
{
    [SerializeField, HideInInspector] private TimelineAsset _timelineAsset;
    public TimelineAsset TimelineAsset => _timelineAsset;

    [SerializeField, HideInInspector] private ActionAssetData _actionAssetData = new ActionAssetData();
    public ActionAssetData ActionAssetData => _actionAssetData;

    public void SetTimelineAsset(TimelineAsset timelineAsset)
    {
        _timelineAsset = timelineAsset;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public void Init()
    {
        _actionAssetData.nomalizedTime = 0;
        _actionAssetData.progress = Enums.ActionProgress.Play;
    }

}

public partial class Enums
{
    public enum ActionProgress
    {
        Play,
        Finish
    }
}