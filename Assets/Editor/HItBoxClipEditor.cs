#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using UnityEditor.Timeline;
using UnityEditor.Playables;

[CustomEditor(typeof(PlayableAsset), true)]
public class HitBoxClipEditor : Editor
{
    private ActionHitBoxAsset _hitBoxAsset;
    private TimelineClip _timelineClip;
    
    private void OnEnable()
    {
        _hitBoxAsset = target as ActionHitBoxAsset;
        if (_hitBoxAsset != null)
        {
            _timelineClip = GetTimelineClipForAsset(_hitBoxAsset);
        }
    }
    
    private void OnSceneGUI()
    {
        if (_hitBoxAsset == null) return;
        
        PlayableDirector director = TimelineEditor.inspectedDirector;
        if (director == null) return;
        
        Transform bone = _hitBoxAsset.boneTransform.Resolve(director);
        if (bone == null)
        {
            bone = _hitBoxAsset.boneTransform.defaultValue as Transform;
            if (bone == null) return;
        }
        
        Vector3 worldCenter = bone.TransformPoint(_hitBoxAsset.config.center);
        Quaternion worldRotation = bone.rotation * _hitBoxAsset.config.rotation;
        float handleSize = HandleUtility.GetHandleSize(worldCenter) * 0.2f;
        
        EditorGUI.BeginChangeCheck();
        
        // 位置手柄
        Handles.color = Color.white;
        Vector3 newCenter = Handles.PositionHandle(worldCenter, worldRotation);
        
        // 旋转手柄
        Handles.color = Color.yellow;
        Quaternion newRotation = Handles.RotationHandle(worldRotation, worldCenter);
        
        // 高度手柄
        Vector3 heightHandlePos = worldCenter + worldRotation * Vector3.up * (_hitBoxAsset.config.height / 2);
        Handles.color = Color.green;
        float newHeight = Handles.ScaleSlider(
            _hitBoxAsset.config.height,
            heightHandlePos,
            worldRotation * Vector3.up,
            worldRotation,
            handleSize * 2f,
            0.1f
        );
        
        // 半径手柄
        Vector3 radiusHandlePos = worldCenter + worldRotation * Vector3.right * _hitBoxAsset.config.radius;
        Handles.color = Color.red;
        float newRadius = Handles.ScaleSlider(
            _hitBoxAsset.config.radius,
            radiusHandlePos,
            worldRotation * Vector3.right,
            worldRotation,
            handleSize,
            0.1f
        );
        
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_hitBoxAsset, "修改HitBox参数");
            _hitBoxAsset.config.center = bone.InverseTransformPoint(newCenter);
            _hitBoxAsset.config.rotation = Quaternion.Inverse(bone.rotation) * newRotation;
            _hitBoxAsset.config.height = Mathf.Max(0.01f, newHeight);
            _hitBoxAsset.config.radius = Mathf.Max(0.01f, newRadius);
            EditorUtility.SetDirty(_hitBoxAsset);
            
            // 标记相关资源为脏（兼容所有Unity版本）
            MarkTimelineDirty();
        }
        
        DrawVisualization(worldCenter, worldRotation, _hitBoxAsset.config, handleSize);
    }
    
    private TimelineClip GetTimelineClipForAsset(PlayableAsset asset)
    {
        if (TimelineEditor.inspectedDirector == null) return null;
        
        TimelineAsset timeline = TimelineEditor.inspectedDirector.playableAsset as TimelineAsset;
        if (timeline == null) return null;
        
        foreach (var track in timeline.GetOutputTracks())
        {
            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip.asset == asset)
                {
                    return clip;
                }
            }
        }
        return null;
    }
    
    private void MarkTimelineDirty()
    {
        if (_timelineClip == null) return;
        
        // 标记轨道为脏
        if (_timelineClip.parentTrack != null)
        {
            EditorUtility.SetDirty(_timelineClip.parentTrack);
        }
        
        // 获取并标记Timeline资源为脏
        PlayableDirector director = TimelineEditor.inspectedDirector;
        if (director != null && director.playableAsset != null)
        {
            EditorUtility.SetDirty(director.playableAsset);
        }
        
        // 标记场景为脏（确保保存）
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }
    
    private void DrawVisualization(Vector3 center, Quaternion rotation, ActionHitBoxConfig config, float handleSize)
    {
        // 绘制坐标轴
        Handles.color = Color.red;
        Handles.DrawLine(center, center + rotation * Vector3.right * handleSize * 5f);
        Handles.color = Color.green;
        Handles.DrawLine(center, center + rotation * Vector3.up * handleSize * 5f);
        Handles.color = Color.blue;
        Handles.DrawLine(center, center + rotation * Vector3.forward * handleSize * 5f);
        
        // 绘制胶囊体
        Handles.color = new Color(1f, 0.5f, 0f, 0.3f);
        float cylinderHeight = config.height - config.radius * 2;
        Vector3 topCenter = center + rotation * Vector3.up * cylinderHeight / 2;
        Vector3 bottomCenter = center - rotation * Vector3.up * cylinderHeight / 2;
        
        // 绘制圆柱部分
        Handles.DrawWireDisc(topCenter, rotation * Vector3.up, config.radius);
        Handles.DrawWireDisc(bottomCenter, rotation * Vector3.up, config.radius);
        
        // 绘制连接线
        Vector3[] points = {
            rotation * Vector3.forward * config.radius,
            rotation * Vector3.right * config.radius,
            rotation * Vector3.back * config.radius,
            rotation * Vector3.left * config.radius
        };
        
        foreach (var point in points)
        {
            Handles.DrawLine(topCenter + point, bottomCenter + point);
        }
    }
}
#endif
