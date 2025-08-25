using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine.Timeline;
using Animancer;

public class ActionTimelineAssetEditor : Editor
{
    public static ActionTimelineAsset CreateActionAsset(string path)
    {
        //创建ActionAsset作为载体
        var actionAsset = CreateInstance<ActionTimelineAsset>();
        AssetDatabase.CreateAsset(actionAsset, path);

        //创建Timeline
        var timeline = CreateInstance<TimelineAsset>();
        timeline.editorSettings.frameRate = TimelineProjectSettings.instance.defaultFrameRate;
        timeline.name = actionAsset.name;

        //为Timeline创建轨道
        timeline.CreateTrack<AnimancerTrack>(null, "Animancer Track");
        timeline.CreateTrack<ActionTransitionTrack>(null, "ActionTransition  Track");

        actionAsset.SetTimelineAsset(timeline);
        AssetDatabase.AddObjectToAsset(timeline, actionAsset);

        AssetDatabase.SaveAssets();
        return actionAsset;
    }

    internal class DoCreateActionTimelineAsset : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            var actionTimeline = CreateActionAsset(pathName);
            ProjectWindowUtil.ShowCreatedAsset(actionTimeline);
        }
    }

    [MenuItem("Assets/Create/CombatSystem/ActionTimelineAsset", false)]
    public static void CreateActionTimelineAsset()
    {
        var icon = EditorGUIUtility.IconContent("TimelineAsset Icon").image as Texture2D;
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateActionTimelineAsset>(), "New ActionTimelineAsset.asset", icon, null);
    }

}
