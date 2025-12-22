using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class ActionAssetHelper
{
    [OnOpenAsset(1)] // 优先级设为1，优先处理我们的ActionAsset
    public static bool OnOpenActionAsset(int instanceID, int line)
    {
        var actionAsset = EditorUtility.InstanceIDToObject(instanceID) as ActionAsset;
        if (actionAsset == null || actionAsset.TimelineAsset == null)
            return false; // 如果不是ActionAsset或者它没有Timeline，则不做任何事

        // 查找并设置 PlayableDirector
        var director = FindBestPlayableDirector();
        if (director != null)
        {
            // 【改进2】记录撤销操作，并设置Timeline
            Undo.RecordObject(director, "Set Timeline Asset");
            director.playableAsset = actionAsset.TimelineAsset;
            EditorUtility.SetDirty(director); // 标记为已修改

            // 选中并高亮显示目标对象
            Selection.activeGameObject = director.gameObject;
            EditorGUIUtility.PingObject(director.gameObject);
        }

        // 【改进3】使用更健壮的方式打开Timeline窗口
        var timelineWindow = EditorWindow.GetWindow<TimelineEditorWindow>();
        timelineWindow.SetTimeline(actionAsset.TimelineAsset);

        return true; // 返回true表示我们已经处理了这个打开事件
    }

    /// <summary>
    /// 【改进1】按优先级查找最合适的PlayableDirector
    /// </summary>
    private static PlayableDirector FindBestPlayableDirector()
    {
        // 优先级1: 检查当前选中的GameObject及其子对象
        if (Selection.activeGameObject != null)
        {
            var directorInSelection = Selection.activeGameObject.GetComponentInChildren<PlayableDirector>();
            if (directorInSelection != null)
            {
                Debug.Log($"[ActionAssetHelper] 找到了选中对象 '{Selection.activeGameObject.name}' 上的PlayableDirector。");
                return directorInSelection;
            }
        }

        // 优先级2: 在预制件编辑模式下查找
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null && prefabStage.scene.IsValid())
        {
            foreach (var rootGo in prefabStage.scene.GetRootGameObjects())
            {
                var directorInPrefab = rootGo.GetComponentInChildren<PlayableDirector>();
                if (directorInPrefab != null)
                {
                    Debug.Log($"[ActionAssetHelper] 在预制件舞台中找到了 '{directorInPrefab.name}'。");
                    return directorInPrefab;
                }
            }
        }

        // 优先级3: (备用方案) 全局查找任意一个
        var anyDirector = Object.FindObjectOfType<PlayableDirector>();

        return anyDirector;
    }
}