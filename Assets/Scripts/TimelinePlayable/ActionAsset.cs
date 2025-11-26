using UnityEngine;
using UnityEngine.Timeline;
using CombatSample.Consts;

public class ActionAssetData
{
    public double nomalizedTime = 0;
    public Enums.ActionPhase phase = Enums.ActionPhase.Neutral;
}

[System.Serializable]
public class ActionAssetAttribute
{
    public Enums.ActionType actionType;
}

public class ActionAsset : ScriptableObject
{
    [SerializeField, HideInInspector] private TimelineAsset _timelineAsset;
    public TimelineAsset TimelineAsset => _timelineAsset;

    [SerializeField, HideInInspector] private ActionAssetData _actionAssetData = new ActionAssetData();
    public ActionAssetData ActionAssetData => _actionAssetData;

    [SerializeField] private ActionAssetAttribute _actionAssetAttribute = new ActionAssetAttribute(); 
    public ActionAssetAttribute ActionAssetAttribute => _actionAssetAttribute;

    public void SetTimelineAsset(TimelineAsset timelineAsset)
    {
        _timelineAsset = timelineAsset;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public void DataReset()
    {
        _actionAssetData.nomalizedTime = 0;
        _actionAssetData.phase = Enums.ActionPhase.Neutral;
    }

}

public static partial class Enums
{
    [System.Flags]
    public enum ActionPhase
    {
        None = 0,
        Neutral = 2,
        Startup = 4,
        Charging = 8,
        FullPower = 16,
        OverCharge = 32,
        Effect = 64,
        Recovery = 128
    }

    public enum ActionType
    {
        None, Movement, Attack
    }

}