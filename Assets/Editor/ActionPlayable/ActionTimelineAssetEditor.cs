using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine.Timeline;

public class ActionTimelineAssetEditor : Editor
{
    /// <summary>
    /// 创建ActionTimelineAsset的方法
    /// 需要特别注意的是：在创建轨道时要设置好hideFlags，否则会导致Timeline在播放时损坏
    /// </summary>
    public static ActionAsset CreateActionAsset(string path)
    {
        // 创建ActionAsset作为载体
        var actionAsset = CreateInstance<ActionAsset>();
        AssetDatabase.CreateAsset(actionAsset, path);

        // 创建Timeline
        var timeline = CreateInstance<TimelineAsset>();
        timeline.editorSettings.frameRate = TimelineProjectSettings.instance.defaultFrameRate;
        timeline.name = actionAsset.name;

        // 设置持久化标志
        timeline.hideFlags = HideFlags.HideInHierarchy;

        // 为Timeline创建轨道
        var animancerTrack = timeline.CreateTrack<AnimancerTrack>(null, "Animancer Track");
        animancerTrack.hideFlags = HideFlags.HideInHierarchy; // 关键修复

        var transitionTrack = timeline.CreateTrack<ActionTransitionTrack>(null, "ActionTransition Track");
        transitionTrack.hideFlags = HideFlags.HideInHierarchy; // 关键修复

        actionAsset.SetTimelineAsset(timeline);

        // 将子资源添加到主资源
        AssetDatabase.AddObjectToAsset(timeline, actionAsset);
        AssetDatabase.AddObjectToAsset(animancerTrack, actionAsset);
        AssetDatabase.AddObjectToAsset(transitionTrack, actionAsset);

        // 确保资源标记为已保存
        EditorUtility.SetDirty(actionAsset);
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
