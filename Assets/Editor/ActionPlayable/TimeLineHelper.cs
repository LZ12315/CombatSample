using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

class TimeLineHelper
{
    //以下函数用于优化TimeLine编辑流程，双击Timeline就可以自动装配到Director上

    [OnOpenAsset(0)]
    public static bool OnOpenTimeline(int instanceID, int line)
    {
        var timelineOpened = EditorUtility.InstanceIDToObject(instanceID) as TimelineAsset;
        if (timelineOpened == null)
            return false;

        SelectDirector(timelineOpened);
        return false;
    }

    [OnOpenAsset(1)]
    public static bool OnOpenCustomTimeline(int instanceID, int line)
    {
        var actionTimelineOpened = EditorUtility.InstanceIDToObject(instanceID) as ActionAsset;
        if (actionTimelineOpened == null)
            return false;

        SelectDirector(actionTimelineOpened.TimelineAsset);
        OpenTimelineWindow(actionTimelineOpened.TimelineAsset);

        return true;
    }

    //手动打开Timeline窗口 因为没有能打开自定义资源的函数可响应
    //或许还可以用反射来获取TimelineWIndow类型来做更多事 敬请研究
    private static void OpenTimelineWindow(TimelineAsset timelineAsset)
    {
        EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
        TimelineEditor.GetOrCreateWindow().SetTimeline(timelineAsset);
    }

    private static void SelectDirector(TimelineAsset timelineAsset)
    {
        var director = FindPlayableDirector();
        if (director != null)
        {
            // 选中包含 PlayableDirector 的 GameObject
            Selection.activeObject = director.gameObject;
            // 在 Hierarchy 中高亮显示
            EditorGUIUtility.PingObject(director.gameObject);
            director.playableAsset = timelineAsset;
        }
    }

    private static PlayableDirector FindPlayableDirector()
    {
        //在编辑预制体时，优先使用当前预制体内的director
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null) 
        {
            foreach (var go in prefabStage.scene.GetRootGameObjects())
            {
                var director = go.GetComponentInChildren<PlayableDirector>();
                if(director != null) return director;
            }
        }
        //在普通场景下，使用场景内物体的director
        else
        {
            return UnityEngine.Object.FindAnyObjectByType<PlayableDirector>();
        }

        return null;
    }

}
