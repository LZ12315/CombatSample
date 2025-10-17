using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

public class ActionAssetCreater
{
    /// <summary>
    /// 创建并持久化ActionAsset的核心方法
    /// </summary>
    public static ActionAsset CreateActionAsset(string path)
    {
        // 创建主资源
        var actionAsset = ScriptableObject.CreateInstance<ActionAsset>();
        AssetDatabase.CreateAsset(actionAsset, path);

        // 创建并附加Timeline资源
        CreateAndAttachTimeline(actionAsset);

        // 确保所有修改被记录
        EditorUtility.SetDirty(actionAsset);
        AssetDatabase.SaveAssets();

        return actionAsset;
    }

    private static void CreateAndAttachTimeline(ActionAsset actionAsset)
    {
        // 创建Timeline资源
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = $"{actionAsset.name}_Timeline";
        timeline.editorSettings.frameRate = TimelineProjectSettings.instance.defaultFrameRate;
        timeline.hideFlags = HideFlags.HideInHierarchy;

        // 创建轨道 - 使用正确的CreateTrack方法
        var animancerTrack = timeline.CreateTrack<AnimancerTrack>(null, "Animancer");
        animancerTrack.hideFlags = HideFlags.HideInHierarchy;

        // 修复：使用正确的CreateTrack方法而不是CreateInstance
        var transitionTrack = timeline.CreateTrack<ActionTransitionTrack>(null, "ActionTransition");
        transitionTrack.hideFlags = HideFlags.HideInHierarchy;

        // 关联资源
        actionAsset.SetTimelineAsset(timeline);

        // 持久化子资源
        AssetDatabase.AddObjectToAsset(timeline, actionAsset);
        AssetDatabase.AddObjectToAsset(animancerTrack, actionAsset);
        AssetDatabase.AddObjectToAsset(transitionTrack, actionAsset);

        // 标记子资源为Dirty
        EditorUtility.SetDirty(timeline);
        EditorUtility.SetDirty(animancerTrack);
        EditorUtility.SetDirty(transitionTrack);
    }

    [MenuItem("Assets/Create/ActionSystem/ActionAsset", priority = 0)]
    public static void CreateActionTimelineAsset()
    {
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
            0,
            ScriptableObject.CreateInstance<CreateActionAssetCallback>(),
            "New ActionTimeline.asset",
            EditorGUIUtility.IconContent("TimelineAsset Icon").image as Texture2D,
            null
        );
    }

    private class CreateActionAssetCallback : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public override void Action(int instanceId, string path, string resourceFile)
        {
            var asset = CreateActionAsset(path);
            ProjectWindowUtil.ShowCreatedAsset(asset);
            AssetDatabase.Refresh();
        }
    }
}