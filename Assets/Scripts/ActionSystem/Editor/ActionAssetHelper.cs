using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

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

        if (TryGetDirectorFromSelection(out var selectedDirector))
        {
            ApplyActionToDirectorAndOpenTimeline(actionAsset, selectedDirector);
            return true;
        }

        var directors = CollectDirectorsInEditScope();
        if (directors.Count == 1)
        {
            ApplyActionToDirectorAndOpenTimeline(actionAsset, directors[0]);
            return true;
        }

        if (directors.Count > 1)
        {
            ActionTimelineTargetPickerWindow.Show(actionAsset, directors);
            return true;
        }

        Selection.activeObject = actionAsset.TimelineAsset;
        Debug.LogWarning("[ActionAssetHelper] 场景或预制体中未找到 PlayableDirector，已进入纯资产预览模式。");
        EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
        return true; 
    }

    public static void ApplyActionToDirectorAndOpenTimeline(ActionAsset actionAsset, PlayableDirector director)
    {
        if (actionAsset == null || actionAsset.TimelineAsset == null || director == null)
            return;

        Undo.RecordObject(director, "Set Timeline Asset");
        director.playableAsset = actionAsset.TimelineAsset;
        EditorUtility.SetDirty(director);

        Selection.activeGameObject = director.gameObject;
        EditorGUIUtility.PingObject(director.gameObject);
        EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
    }

    private static bool TryGetDirectorFromSelection(out PlayableDirector director)
    {
        director = null;
        if (Selection.activeGameObject == null)
            return false;

        director = Selection.activeGameObject.GetComponentInChildren<PlayableDirector>(true);
        return director != null;
    }

    private static System.Collections.Generic.List<PlayableDirector> CollectDirectorsInEditScope()
    {
        var result = new System.Collections.Generic.List<PlayableDirector>();

        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null && prefabStage.scene.IsValid())
        {
            foreach (var rootGo in prefabStage.scene.GetRootGameObjects())
                result.AddRange(rootGo.GetComponentsInChildren<PlayableDirector>(true));
            return result;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            foreach (var rootGo in scene.GetRootGameObjects())
                result.AddRange(rootGo.GetComponentsInChildren<PlayableDirector>(true));
        }

        return result;
    }
}
