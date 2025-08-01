using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

class TimeLineHelper
{
    //以下函数用于优化TimeLine编辑流程，双击PlayableAsset就可以自动装配到Director上

    [OnOpenAsset(0)]
    public static bool OnDoubleClick(int instanceID, int line)
    {
        var assetDoubleClicked = EditorUtility.InstanceIDToObject(instanceID) as TimelineAsset;
        if (assetDoubleClicked == null)
            return false;

        var director = FindPlayableDirector();
        if(director != null)
        {
            // 选中包含 PlayableDirector 的 GameObject
            Selection.activeObject = director.gameObject;
            // 在 Hierarchy 中高亮显示
            EditorGUIUtility.PingObject(director.gameObject);
            director.playableAsset = assetDoubleClicked;
        }

        return false;
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
            return Object.FindAnyObjectByType<PlayableDirector>();
        }

        return null;
    }

}
