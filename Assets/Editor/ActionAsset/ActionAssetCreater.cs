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
        // 【推荐】开始批量资产编辑，提高性能和操作的原子性
        AssetDatabase.StartAssetEditing();

        ActionAsset actionAsset = null;
        try
        {
            // 创建主资源
            actionAsset = ScriptableObject.CreateInstance<ActionAsset>();
            AssetDatabase.CreateAsset(actionAsset, path);

            // 创建并附加Timeline资源
            CreateAndAttachTimeline(actionAsset);
        }
        finally
        {
            // 【推荐】结束批量资产编辑，并让Unity一次性处理所有变更
            AssetDatabase.StopAssetEditing();

            // 确保即使在try块中发生错误，我们也能保存已经创建的部分
            if (actionAsset != null)
            {
                EditorUtility.SetDirty(actionAsset);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(); // 确保Project窗口状态最新
        }

        return actionAsset;
    }

    private static void CreateAndAttachTimeline(ActionAsset actionAsset)
    {
        // 创建Timeline资源
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = $"{actionAsset.name}_Timeline";
        timeline.editorSettings.frameRate = TimelineProjectSettings.instance.defaultFrameRate;
        timeline.hideFlags = HideFlags.HideInHierarchy;

        // 创建轨道
        var animancerTrack = timeline.CreateTrack<AnimancerTrack>(null, "Animancer");
        animancerTrack.hideFlags = HideFlags.HideInHierarchy;

        var transitionTrack = timeline.CreateTrack<ActionDataTrack>(null, "ActionTransition");
        transitionTrack.hideFlags = HideFlags.HideInHierarchy;

        // 关联资源
        actionAsset.SetTimelineAsset(timeline);

        // 持久化子资源
        AssetDatabase.AddObjectToAsset(timeline, actionAsset);
        AssetDatabase.AddObjectToAsset(animancerTrack, actionAsset);
        AssetDatabase.AddObjectToAsset(transitionTrack, actionAsset);

        // 在批量编辑模式下，SetDirty可以放在最后一起做，但放在这里也无妨
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
            if (asset != null)
            {
                ProjectWindowUtil.ShowCreatedAsset(asset);
            }
        }
    }
}