using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class ActionAssetHelper
{
    [OnOpenAsset(1)] 
    public static bool OnOpenActionAsset(int instanceID, int line)
    {
        var actionAsset = EditorUtility.InstanceIDToObject(instanceID) as ActionAsset;
        if (actionAsset == null) return false; 

        if (actionAsset.TimelineAsset == null)
        {
            Debug.LogWarning($"[ActionAssetHelper] {actionAsset.name} 没有关联的 TimelineAsset！");
            return false;
        }

        // 查找最佳的 PlayableDirector
        var director = FindBestPlayableDirector();
        
        if (director != null)
        {
            // 记录撤销操作，并替换 Director 正在播放的 Timeline
            Undo.RecordObject(director, "Set Timeline Asset");
            director.playableAsset = actionAsset.TimelineAsset;
            EditorUtility.SetDirty(director); 

            // ? 第一步：选中挂载了 Director 的游戏物体
            Selection.activeGameObject = director.gameObject;
            EditorGUIUtility.PingObject(director.gameObject);
        }
        else
        {
            // 兜底方案：如果没有 Director，就选中 Timeline 资产本身
            Selection.activeObject = actionAsset.TimelineAsset;
            Debug.LogWarning("[ActionAssetHelper] 场景或预制体中未找到 PlayableDirector，已进入纯资产预览模式。");
        }

        // ? 第二步：最暴力的跨版本绝招 —— 模拟点击菜单栏强制呼出 Timeline 窗口
        // 窗口一旦打开，就会自动读取上面的 Selection 焦点并完美加载数据！
        EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");

        return true; 
    }

    /// <summary>
    /// 按优先级查找最合适的PlayableDirector
    /// </summary>
    private static PlayableDirector FindBestPlayableDirector()
    {
        // 优先级1: 检查当前选中的GameObject及其子对象
        if (Selection.activeGameObject != null)
        {
            var directorInSelection = Selection.activeGameObject.GetComponentInChildren<PlayableDirector>();
            if (directorInSelection != null) return directorInSelection;
        }

        // 优先级2: 在预制件编辑模式下查找
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null && prefabStage.scene.IsValid())
        {
            foreach (var rootGo in prefabStage.scene.GetRootGameObjects())
            {
                var directorInPrefab = rootGo.GetComponentInChildren<PlayableDirector>();
                if (directorInPrefab != null) return directorInPrefab;
            }
        }

        // 优先级3: 全局查找任意一个
        return Object.FindObjectOfType<PlayableDirector>();
    }
}