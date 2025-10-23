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
        None = 0,
        Neutral = 2,
        Startup = 4,
        Charging = 8,
        FullPower = 16,
        OverCharge = 32,
        Effect = 64,
        Recovery = 128
    }
}