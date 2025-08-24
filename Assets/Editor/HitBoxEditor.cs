#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using System.Collections.Generic;

namespace HitBoxEditorNamespace
{
    /// <summary>
    /// Editor utility for visualizing and editing HitBoxes in Timeline
    /// </summary>
    [InitializeOnLoad]
    public static class HitBoxEditor
    {
        private static bool isInitialized = false;
        private static bool _debugMode = true;
        private static bool _showHandles = true; // 新增：控制手柄显示
        private static readonly List<string> _debugMessages = new List<string>();
        private static Vector2 _debugScrollPos;
        private static readonly GUILayoutOption[] debugWindowOptions = { GUILayout.Width(300), GUILayout.Height(200) };

        static HitBoxEditor()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (!isInitialized)
            {
                SceneView.duringSceneGui += OnSceneGUI;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                EditorApplication.update += OnEditorUpdate;
                isInitialized = true;
                DebugLog("HitBoxEditor initialized");
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingPlayMode:
                    SceneView.duringSceneGui -= OnSceneGUI;
                    SceneView.duringSceneGui += OnSceneGUI;
                    DebugLog("Exiting play mode, reset editor state");
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    ClearDebugLog();
                    break;
            }
        }

        private static void OnEditorUpdate()
        {
            // 强制Scene视图刷新
            if (_debugMode || _showHandles)
            {
                SceneView.RepaintAll();
            }
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (!_showHandles && !_debugMode) return;

            DrawTimelineHitBoxes();
        }

        private static void DrawTimelineHitBoxes()
        {
            // 确保Timeline导演有效
            if (TimelineEditor.inspectedDirector == null)
            {
                DebugLog("No Timeline Director selected");
                return;
            }

            // 获取当前Timeline资源
            var timeline = TimelineEditor.inspectedDirector.playableAsset as TimelineAsset;
            if (timeline == null)
            {
                DebugLog("Selected PlayableAsset is not a TimelineAsset");
                return;
            }

            // 获取当前PlayableDirector
            var director = TimelineEditor.inspectedDirector;
            DebugLog($"Processing timeline: {timeline.name}, current time: {director.time:F3}");

            // 处理所有HitBox轨道
            ProcessHitBoxTracks(timeline, director);
        }

        private static void ProcessHitBoxTracks(TimelineAsset timeline, PlayableDirector director)
        {
            foreach (var track in timeline.GetOutputTracks())
            {
                if (!(track is ActionHitBoxTrack hitboxTrack)) continue;
                
                DebugLog($"Processing HitBox track: {hitboxTrack.name}");
                ProcessHitBoxClips(hitboxTrack, director);
            }
        }

        private static void ProcessHitBoxClips(TrackAsset hitboxTrack, PlayableDirector director)
        {
            foreach (var clip in hitboxTrack.GetClips())
            {
                if (!(clip.asset is ActionHitBoxAsset hitboxAsset)) continue;

                double currentTime = director.time;
                bool isClipActive = currentTime >= clip.start && currentTime <= clip.end;
                DebugLog($"Clip: {clip.displayName}, Active: {isClipActive}, Time: {currentTime:F3}");

                if (isClipActive && hitboxAsset.hitbox != null && _showHandles)
                {
                    DrawHitBoxHandles(hitboxAsset.hitbox);
                }
            }
        }

        // 绘制编辑器
        private static void DrawHitBoxHandles(CapsuleCollider collider)
        {
            if (collider == null) return;

            var transform = collider.transform;
            Vector3 center = transform.TransformPoint(collider.center);
            Quaternion rotation = transform.rotation;

            // 1. Position handle
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.cyan.WithAlpha(0.8f);
            Vector3 newPosition = Handles.PositionHandle(center, rotation);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(collider, "Move HitBox");

                // 正确计算新的中心点位置
                Vector3 newLocalCenter = transform.InverseTransformPoint(newPosition);

                // 应用变化到碰撞体中心（局部空间）
                collider.center = newLocalCenter;

                MarkAsDirty(collider);
                DebugLog($"HitBox position updated: {newLocalCenter}");
            }

            // 2. Rotation handle
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.yellow.WithAlpha(0.8f);
            Quaternion newRotation = Handles.RotationHandle(rotation, center);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(transform, "Rotate HitBox");
                transform.rotation = newRotation;
                MarkAsDirty(transform);
                DebugLog("HitBox rotation updated");
            }

            // 3. Radius handle
            EditorGUI.BeginChangeCheck();

            // 使用更鲜艳的颜色和更大的尺寸
            Handles.color = new Color(1, 0.2f, 0.2f, 1f); // 更鲜艳的红色
            float handleSize = HandleUtility.GetHandleSize(center) * 0.5f; // 增大50%（原为0.2f）

            // 添加轮廓效果
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always; // 始终显示在最前面

            // 创建更醒目的半径手柄
            float newRadius = Handles.ScaleValueHandle(
                collider.radius,
                center + rotation * Vector3.right * collider.radius,
                rotation,
                handleSize,
                (controlID, position, rotation, size, eventType) =>
                {
                    // 自定义绘制：红色球体+白色轮廓
                    Handles.color = new Color(1, 0.2f, 0.2f, 0.8f);
                    Handles.SphereHandleCap(controlID, position, rotation, size * 1.2f, eventType);

                    Handles.color = Color.white;
                    Handles.SphereHandleCap(controlID, position, rotation, size * 0.7f, eventType);
                },
                0.1f
            );

            // 添加文字标签
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = new Color(1, 0.3f, 0.3f);
            Handles.Label(center + rotation * Vector3.right * (collider.radius + handleSize * 0.5f),
                         "Radius", labelStyle);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(collider, "Resize HitBox Radius");
                collider.radius = Mathf.Max(0.01f, newRadius);
                MarkAsDirty(collider);
                DebugLog($"HitBox radius updated: {newRadius:F3}");
            }

            // 4. Height handle
            EditorGUI.BeginChangeCheck();

            // 使用更鲜艳的颜色
            Handles.color = new Color(0.2f, 1, 0.2f, 1f); // 更鲜艳的绿色
                                                          // handleSize 使用相同的值（0.5f）

            Vector3 direction = GetCapsuleDirectionVector(collider.direction);
            Vector3 heightHandlePos = center + rotation * direction * (collider.height / 2);

            // 创建更醒目的高度手柄
            float newHeight = Handles.ScaleValueHandle(
                collider.height,
                heightHandlePos,
                rotation,
                handleSize,
                (controlID, position, rotation, size, eventType) =>
                {
                    // 自定义绘制：绿色立方体+白色轮廓
                    Handles.color = new Color(0.2f, 1, 0.2f, 0.8f);
                    Handles.CubeHandleCap(controlID, position, rotation, size * 1.2f, eventType);

                    Handles.color = Color.white;
                    Handles.CubeHandleCap(controlID, position, rotation, size * 0.7f, eventType);
                },
                0.1f
            );

            // 添加文字标签
            labelStyle.normal.textColor = new Color(0.3f, 1, 0.3f);
            Handles.Label(heightHandlePos + rotation * direction * (handleSize * 0.7f),
                         "Height", labelStyle);


            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(collider, "Resize HitBox Height");
                collider.height = Mathf.Max(0.01f, newHeight);
                MarkAsDirty(collider);
                DebugLog($"HitBox height updated: {newHeight:F3}");
            }

            // 绘制胶囊体线框
            DrawCapsuleWireframe(center, rotation, collider.radius, collider.height, collider.direction);
        }

        #region 绘制Collider轮廓

        private static void DrawCapsuleWireframe(Vector3 center, Quaternion rotation, float radius, float height, int direction)
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            // 线框颜色
            Color wireColor = new Color(0.2f, 0.8f, 1f, 0.95f); // 亮蓝色

            float lineWidth = 2.0f;

            Vector3 upVector = GetCapsuleDirectionVector(direction);
            float cylinderHeight = Mathf.Max(0.01f, height - radius * 2);
            Vector3 topCenter = center + rotation * upVector * (cylinderHeight / 2);
            Vector3 bottomCenter = center - rotation * upVector * (cylinderHeight / 2);

            // 1. 绘制圆柱部分
            DrawCylinderOutline(topCenter, bottomCenter, rotation, radius, wireColor, lineWidth);

            // 2. 绘制顶部半球（完整半球）
            DrawFullHemisphere(topCenter, rotation * upVector, radius, wireColor, lineWidth);

            // 3. 绘制底部半球（完整半球）
            DrawFullHemisphere(bottomCenter, rotation * -upVector, radius, wireColor, lineWidth);
        }

        // 绘制圆柱体轮廓
        private static void DrawCylinderOutline(Vector3 topCenter, Vector3 bottomCenter, Quaternion rotation, float radius, Color color, float lineWidth)
        {
            // 绘制顶部和底部圆环
            Handles.color = color;
            Handles.DrawWireDisc(topCenter, rotation * Vector3.up, radius, lineWidth);
            Handles.DrawWireDisc(bottomCenter, rotation * Vector3.up, radius, lineWidth);

            // 绘制四条连接线
            Vector3[] directions = {
                rotation * Vector3.right,
                rotation * Vector3.forward,
                rotation * Vector3.left,
                rotation * Vector3.back
            };

            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 topPoint = topCenter + directions[i] * radius;
                Vector3 bottomPoint = bottomCenter + directions[i] * radius;
                Handles.DrawAAPolyLine(lineWidth * 2.5f, topPoint, bottomPoint);
            }
        }

        // 绘制半球
        private static void DrawFullHemisphere(Vector3 center, Vector3 direction, float radius, Color color, float lineWidth)
        {
            Handles.color = color;
            Quaternion rot = Quaternion.LookRotation(direction);

            // 绘制两个完整方向的360度圆弧
            // 1. 左右方向的圆
            Handles.DrawWireArc(center, rot * Vector3.left, rot * Vector3.down, 180, radius, lineWidth);

            // 2. 前后方向的圆
            Handles.DrawWireArc(center, rot * Vector3.down, rot * Vector3.right, 180, radius, lineWidth);
        }

        #endregion

        private static Vector3 GetCapsuleDirectionVector(int direction)
        {
            return direction switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                2 => Vector3.forward,
                _ => Vector3.up
            };
        }

        private static void MarkAsDirty(Object obj)
        {
            EditorUtility.SetDirty(obj);
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }


        private static void DebugLog(string message)
        {
            if (!_debugMode) return;
            _debugMessages.Insert(0, $"[{System.DateTime.Now:HH:mm:ss.fff}] {message}");
            if (_debugMessages.Count > 50)
            {
                _debugMessages.RemoveRange(50, _debugMessages.Count - 50);
            }
        }

        private static void ClearDebugLog()
        {
            _debugMessages.Clear();
        }

    }

    public static class ColorExtensions
    {
        public static Color WithAlpha(this Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    }
}
#endif