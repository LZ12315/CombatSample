using UnityEngine;
using UnityEngine.Timeline;
using CombatSample.Consts;

[System.Serializable]
public class ActionAssetData
{
    public double nomalizedTime = 0;
    public Enums.ActionPhase phase = Enums.ActionPhase.Neutral;
}

public class ActionAsset : ScriptableObject
{
    [SerializeField, HideInInspector] private TimelineAsset _timelineAsset;
    public TimelineAsset TimelineAsset => _timelineAsset;

    [SerializeField] public ActionAssetData actionAssetData = new ActionAssetData();

    public void SetTimelineAsset(TimelineAsset timelineAsset)
    {
        _timelineAsset = timelineAsset;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public void DataReset()
    {
        actionAssetData.nomalizedTime = 0;
        actionAssetData.phase = Enums.ActionPhase.Neutral;
    }

}

public static partial class Enums
{
    [System.Flags]
    public enum ActionPhase
    {
        Neutral = 0,
        Startup = 2,
        Charging = 4,
        FullPower = 8,
        OverCharge = 16,
        Effect = 32,
        Recovery = 64
    }
}