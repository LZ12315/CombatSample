using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine.Timeline;

public class ActionTimelineAssetEditor : Editor
{
    public static ActionTimelineAsset CreateActionTimelineAsset(string path)
    {
        var actionTimeline = CreateInstance<ActionTimelineAsset>();
        AssetDatabase.CreateAsset(actionTimeline, path);
        var timeline = CreateInstance<TimelineAsset>();
        timeline.editorSettings.frameRate = TimelineProjectSettings.instance.defaultFrameRate;
        timeline.name = actionTimeline.name;
        actionTimeline.SetTimelineAsset(timeline);
        AssetDatabase.AddObjectToAsset(timeline, actionTimeline);
        AssetDatabase.SaveAssets();
        return actionTimeline;
    }

    internal class DoCreateTimeline : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            var actionTimeline = CreateActionTimelineAsset(pathName);
            ProjectWindowUtil.ShowCreatedAsset(actionTimeline);
        }
    }

    [MenuItem("Assets/Create/CombatSystem/ActionTimeline", false)]
    public static void CreateNewTimeline()
    {
        var icon = EditorGUIUtility.IconContent("TimelineAsset Icon").image as Texture2D;
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateTimeline>(), "New Timeline.asset", icon, null);
    }
}
