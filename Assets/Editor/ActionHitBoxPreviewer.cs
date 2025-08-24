#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;

[InitializeOnLoad]
public static class HitBoxPreviewer
{
    private static bool isInitialized = false;
    private static bool hasWarned = false; // 全局警告状态
    
    static HitBoxPreviewer()
    {
        if (!isInitialized)
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            isInitialized = true;
            hasWarned = false; // 重置警告状态
        }
    }
    
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            hasWarned = false; // 退出播放模式时重置警告
        }
    }
    
    private static void OnSceneGUI(SceneView view)
    {
        // 确保Timeline导演有效
        if (TimelineEditor.inspectedDirector == null) 
        {
            // 重置警告状态
            hasWarned = false;
            return;
        }
        
        // 获取当前Timeline资源
        TimelineAsset timeline = TimelineEditor.inspectedDirector.playableAsset as TimelineAsset;
        if (timeline == null) 
        {
            hasWarned = false;
            return;
        }
        
        // 获取当前PlayableDirector
        PlayableDirector director = TimelineEditor.inspectedDirector;
        
        // 遍历所有轨道
        bool anyClipActive = false;
        foreach (var track in timeline.GetOutputTracks())
        {
            // 只处理HitBox轨道
            if (track is ActionHitBoxTrack)
            {
                // 遍历轨道上的所有Clip
                foreach (var clip in track.GetClips())
                {
                    if (clip.asset is ActionHitBoxAsset hitboxAsset)
                    {
                        // 计算当前时间是否在Clip范围内
                        double currentTime = director.time;
                        bool isClipActive = currentTime >= clip.start && currentTime <= clip.end;
                        
                        // 如果Clip处于激活状态
                        if (isClipActive)
                        {
                            anyClipActive = true;
                            
                            // 解析骨骼引用
                            Transform bone = hitboxAsset.boneTransform.Resolve(director);
                            
                            // 如果解析失败，尝试使用默认值
                            if (bone == null)
                            {
                                bone = hitboxAsset.boneTransform.defaultValue as Transform;
                            }
                            
                            // 如果仍然没有骨骼
                            if (bone == null)
                            {
                                // 只显示一次警告
                                if (!hasWarned)
                                {
                                    Debug.LogWarning($"Bone Transform not assigned for HitBox asset in clip: {clip.displayName}\n" +
                                                     "Please bind the bone reference in the Timeline.");
                                    hasWarned = true;
                                }
                                continue;
                            }
                            
                            // 计算世界空间中的位置和旋转
                            Vector3 worldCenter = bone.TransformPoint(hitboxAsset.config.center);
                            Quaternion worldRotation = bone.rotation * hitboxAsset.config.rotation;
                            
                            // 设置Handles颜色
                            Handles.color = new Color(1f, 0.2f, 0.1f, 0.6f);
                            
                            // 绘制胶囊体
                            DrawCapsuleHandle(worldCenter, worldRotation, hitboxAsset.config.height, hitboxAsset.config.radius);
                            
                            // 绘制坐标轴
                            DrawAxis(worldCenter, worldRotation, 0.5f);
                            
                            // 绘制骨骼名称
                            DrawBoneLabel(worldCenter, bone.name);
                        }
                    }
                }
            }
        }
        
        // 如果没有激活的Clip，重置警告状态
        if (!anyClipActive)
        {
            hasWarned = false;
        }
    }
    
    // 绘制坐标轴
    private static void DrawAxis(Vector3 position, Quaternion rotation, float size)
    {
        Handles.color = Color.red;
        Handles.DrawLine(position, position + rotation * Vector3.right * size);
        
        Handles.color = Color.green;
        Handles.DrawLine(position, position + rotation * Vector3.up * size);
        
        Handles.color = Color.blue;
        Handles.DrawLine(position, position + rotation * Vector3.forward * size);
    }
    
    // 绘制骨骼名称标签
    private static void DrawBoneLabel(Vector3 position, string boneName)
    {
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.yellow;
        style.fontSize = 12;
        style.alignment = TextAnchor.MiddleCenter;
        style.contentOffset = new Vector2(0, 20);
        
        Handles.Label(position, boneName, style);
    }
    
    // 绘制胶囊体
    private static void DrawCapsuleHandle(Vector3 center, Quaternion rotation, float height, float radius)
    {
        // 计算圆柱部分高度
        float cylinderHeight = height - radius * 2;
        Vector3 topCenter = center + rotation * Vector3.up * cylinderHeight / 2;
        Vector3 bottomCenter = center - rotation * Vector3.up * cylinderHeight / 2;
        
        // 绘制顶部和底部的圆盘
        Handles.DrawWireDisc(topCenter, rotation * Vector3.up, radius);
        Handles.DrawWireDisc(bottomCenter, rotation * Vector3.up, radius);
        
        // 计算四个方向的向量
        Vector3 forward = rotation * Vector3.forward * radius;
        Vector3 right = rotation * Vector3.right * radius;
        Vector3 back = rotation * Vector3.back * radius;
        Vector3 left = rotation * Vector3.left * radius;
        
        // 绘制四条侧边
        Handles.DrawLine(topCenter + forward, bottomCenter + forward);
        Handles.DrawLine(topCenter + right, bottomCenter + right);
        Handles.DrawLine(topCenter + back, bottomCenter + back);
        Handles.DrawLine(topCenter + left, bottomCenter + left);
        
        // 绘制顶部和底部的半球
        DrawHemisphereHandle(topCenter, rotation, radius, true);
        DrawHemisphereHandle(bottomCenter, rotation, radius, false);
    }
    
    // 绘制半球
    private static void DrawHemisphereHandle(Vector3 center, Quaternion rotation, float radius, bool isTop)
    {
        // 确定半球的方向（上或下）
        Vector3 direction = isTop ? Vector3.up : Vector3.down;
        
        // 绘制三个半圆弧
        Handles.DrawWireArc(center, rotation * direction, rotation * Vector3.forward, 180, radius);
        Handles.DrawWireArc(center, rotation * Vector3.forward, rotation * direction, 180, radius);
        Handles.DrawWireArc(center, rotation * Vector3.right, rotation * direction, 180, radius);
    }
}
#endif
