using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using System.IO;

public class ActionAssetCreater
{
    public static ActionAsset CreateActionAsset(string path)
    {
        ActionAsset actionAsset = ScriptableObject.CreateInstance<ActionAsset>();
        AssetDatabase.CreateAsset(actionAsset, path);

        CreateAndLinkTimeline(actionAsset, path);

        EditorUtility.SetDirty(actionAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return actionAsset;
    }

private static void CreateAndLinkTimeline(ActionAsset actionAsset, string actionPath)
    {
        string directory = Path.GetDirectoryName(actionPath);
        string actionName = Path.GetFileNameWithoutExtension(actionPath);
        
        string timelinePath = Path.Combine(directory, $"{actionName}_Timeline.playable").Replace("\\", "/");

        // 1. 创建纯净的 Timeline 资源
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.editorSettings.frameRate = TimelineProjectSettings.instance.defaultFrameRate;
        
        // ? 必须先让 Timeline 成为磁盘上的独立实体文件
        AssetDatabase.CreateAsset(timeline, timelinePath);

        // ==========================================
        // ? 2. 添加默认轨道
        // 因为 Timeline 已经是独立文件了，CreateTrack 会自动把轨道保存在 .playable 内部，绝对不会污染 ActionAsset
        // ==========================================
        
        // 添加 Animancer 动画轨道
        var animancerTrack = timeline.CreateTrack<AnimancerTrack>(null, "Animancer");
        
        // 添加 Tag/Data 轨道 (根据你之前的代码，我猜你的类名叫 ActionDataTrack)
        var tagTrack = timeline.CreateTrack<ActionTagTrack>(null, "ActionTag"); 

        // ==========================================

        // 3. 逻辑关联
        actionAsset.SetTimelineAsset(timeline);

        // 标记脏数据，等待保存
        EditorUtility.SetDirty(timeline);
    }

    [MenuItem("Assets/Create/ActionSystem/ActionAsset", priority = 0)]
    public static void CreateActionTimelineAsset()
    {
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
            0,
            ScriptableObject.CreateInstance<CreateActionAssetCallback>(),
            "New Action", // 默认名字
            EditorGUIUtility.IconContent("ScriptableObject Icon").image as Texture2D,
            null
        );
    }

    private class CreateActionAssetCallback : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public override void Action(int instanceId, string path, string resourceFile)
        {
            // ? 核心优化：拦截路径，自动创建专属文件夹
            string directory = Path.GetDirectoryName(path);
            string actionName = Path.GetFileNameWithoutExtension(path);

            // 算出专属文件夹的路径
            string folderPath = Path.Combine(directory, actionName).Replace("\\", "/");
            
            // 如果文件夹不存在，就创建一个
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                // CreateFolder 的参数是 (父目录, 新文件夹名)
                AssetDatabase.CreateFolder(directory, actionName);
            }

            // 更新最终资产要保存的路径（放到刚建好的文件夹里）
            string finalAssetPath = Path.Combine(folderPath, $"{actionName}.asset").Replace("\\", "/");

            var asset = CreateActionAsset(finalAssetPath);
            if (asset != null)
            {
                // 创建完毕后，高亮选中生成的 ActionAsset
                ProjectWindowUtil.ShowCreatedAsset(asset);
            }
        }
    }
}