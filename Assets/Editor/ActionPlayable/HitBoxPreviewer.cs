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
    public static class ActionHitBoxPreviewer
    {
        private static bool isInitialized = false;
        private static bool _debugMode = true;
        private static bool _showHandles = true;
        private static readonly List<string> _debugMessages = new List<string>();
        private static Vector2 _debugScrollPos;
        private static readonly GUILayoutOption[] debugWindowOptions = { GUILayout.Width(300), GUILayout.Height(200) };

        static ActionHitBoxPreviewer()
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
            // 获取当前选中的所有Clip
            var selectedClips = new HashSet<TimelineClip>(TimelineEditor.selectedClips);

            foreach (var track in timeline.GetOutputTracks())
            {
                if (!(track is ActionHitBoxTrack hitboxTrack)) continue;

                DebugLog($"Processing HitBox track: {hitboxTrack.name}");
                ProcessHitBoxClips(hitboxTrack, director, selectedClips);
            }
        }

        private static void ProcessHitBoxClips(TrackAsset hitboxTrack, PlayableDirector director, HashSet<TimelineClip> selectedClips)
        {
            foreach (var clip in hitboxTrack.GetClips())
            {
                if (!(clip.asset is ActionHitBoxAsset hitboxAsset)) continue;

                double currentTime = director.time;
                bool isClipActive = currentTime >= clip.start && currentTime <= clip.end;
                DebugLog($"Clip: {clip.displayName}, Active: {isClipActive}, Time: {currentTime:F3}");

                if (isClipActive && hitboxAsset.behavior.hitbox != null)
                {
                    // 总是绘制胶囊体线框
                    DrawCapsuleWireframeForClip(hitboxAsset.behavior, clip);

                    // 只绘制选中Clip的Handle
                    if (_showHandles && selectedClips.Contains(clip))
                    {
                        DrawHitBoxHandles(hitboxAsset.behavior.hitbox, hitboxAsset.behavior, clip);
                    }
                }
            }
        }

        private static void DrawCapsuleWireframeForClip(ActionHitBoxClip clip, TimelineClip timelineClip)
        {
            if (clip == null || clip.config == null || timelineClip == null) return;
            if (clip.hitbox == null) return;

            var transform = clip.hitbox.transform;
            Vector3 center = transform.TransformPoint(clip.config.center);
            Quaternion rotation = transform.rotation * clip.config.rotation;
            rotation = NormalizeQuaternion(rotation);

            // 绘制胶囊体线框
            DrawCapsuleWireframe(center, rotation, clip.config.radius, clip.config.height, clip.hitbox.direction);
        }

        #region Handle绘制
        private static void DrawHitBoxHandles(CapsuleCollider collider, ActionHitBoxClip clip, TimelineClip timelineClip)
        {
            if (collider == null || clip == null || clip.config == null || timelineClip == null) return;

            var transform = collider.transform;
            Vector3 center = transform.TransformPoint(clip.config.center);
            Quaternion rotation = transform.rotation * clip.config.rotation;
            rotation = NormalizeQuaternion(rotation);

            // 获取关联的ActionHitBoxAsset
            var hitboxAsset = timelineClip.asset as ActionHitBoxAsset;
            if (hitboxAsset == null) return;

            // 获取当前选中的工具
            Tool currentTool = Tools.current;

            // 1. Position handle (仅在移动工具选中时显示)
            if (currentTool == Tool.Move)
            {
                EditorGUI.BeginChangeCheck();
                Handles.color = Color.cyan.WithAlpha(0.8f);
                Vector3 newPosition = Handles.PositionHandle(center, rotation);

                if (EditorGUI.EndChangeCheck())
                {
                    // 记录ActionHitBoxAsset而不是clip
                    Undo.RecordObject(hitboxAsset, "Move HitBox");

                    // 计算新的中心点位置（局部空间）
                    Vector3 newLocalCenter = transform.InverseTransformPoint(newPosition);

                    // 添加取整功能 - 解决浮点精度问题
                    newLocalCenter = RoundVector3(newLocalCenter, 5);

                    clip.config.center = newLocalCenter;

                    // 标记为脏
                    EditorUtility.SetDirty(hitboxAsset);
                    DebugLog($"HitBox position updated: {newLocalCenter}");
                }
            }

            // 2. Rotation handle (仅在旋转工具选中时显示)
            if (currentTool == Tool.Rotate)
            {
                EditorGUI.BeginChangeCheck();
                Handles.color = Color.yellow.WithAlpha(0.8f);
                Quaternion newRotation = Handles.RotationHandle(rotation, center);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(hitboxAsset, "Rotate HitBox");

                    // 计算相对于骨骼的旋转
                    Quaternion relativeRotation = Quaternion.Inverse(transform.rotation) * newRotation;

                    // 标准化四元数
                    relativeRotation = NormalizeQuaternion(relativeRotation);

                    // 添加取整功能 - 解决浮点精度问题
                    relativeRotation = RoundQuaternion(relativeRotation, 5);

                    clip.config.rotation = relativeRotation;

                    EditorUtility.SetDirty(hitboxAsset);
                    DebugLog("HitBox rotation updated");
                }
            }

            // 3. Radius handle (始终显示)
            EditorGUI.BeginChangeCheck();
            Handles.color = new Color(1, 0.2f, 0.2f, 1f);
            float handleSize = HandleUtility.GetHandleSize(center) * 0.5f;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            // 使用config.radius值
            float newRadius = Handles.ScaleValueHandle(
                clip.config.radius,
                center + rotation * Vector3.right * clip.config.radius,
                rotation,
                handleSize,
                (controlID, position, rot, size, eventType) =>
                {
                    // 确保使用的四元数是标准化的
                    rot = NormalizeQuaternion(rot);

                    Handles.color = new Color(1, 0.2f, 0.2f, 0.8f);
                    Handles.SphereHandleCap(controlID, position, rot, size * 1.2f, eventType);
                    Handles.color = Color.white;
                    Handles.SphereHandleCap(controlID, position, rot, size * 0.7f, eventType);
                },
                0.1f
            );

            // 标签
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = new Color(1, 0.3f, 0.3f);
            Handles.Label(center + rotation * Vector3.right * (clip.config.radius + handleSize * 0.5f),
                         "Radius", labelStyle);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(hitboxAsset, "Resize HitBox Radius");

                // 添加取整功能 - 解决浮点精度问题
                newRadius = RoundFloat(newRadius, 5);

                clip.config.radius = Mathf.Max(0.01f, newRadius);
                EditorUtility.SetDirty(hitboxAsset);
                DebugLog($"HitBox radius updated: {newRadius:F3}");
            }

            // 4. Height handle (始终显示)
            EditorGUI.BeginChangeCheck();
            Handles.color = new Color(0.2f, 1, 0.2f, 1f);

            Vector3 direction = GetCapsuleDirectionVector(collider.direction);
            Vector3 heightHandlePos = center + rotation * direction * (clip.config.height / 2);

            // 使用config.height值
            float newHeight = Handles.ScaleValueHandle(
                clip.config.height,
                heightHandlePos,
                rotation,
                handleSize,
                (controlID, position, rotation, size, eventType) =>
                {
                    Handles.color = new Color(0.2f, 1, 0.2f, 0.8f);
                    Handles.CubeHandleCap(controlID, position, rotation, size * 1.2f, eventType);
                    Handles.color = Color.white;
                    Handles.CubeHandleCap(controlID, position, rotation, size * 0.7f, eventType);
                },
                0.1f
            );

            // 标签
            labelStyle.normal.textColor = new Color(0.3f, 1, 0.3f);
            Handles.Label(heightHandlePos + rotation * direction * (handleSize * 0.7f),
                         "Height", labelStyle);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(hitboxAsset, "Resize HitBox Height");

                // 添加取整功能 - 解决浮点精度问题
                newHeight = RoundFloat(newHeight, 5);

                clip.config.height = Mathf.Max(0.01f, newHeight);
                EditorUtility.SetDirty(hitboxAsset);
                DebugLog($"HitBox height updated: {newHeight:F3}");
            }
        }

        // 四元数标准化方法
        private static Quaternion NormalizeQuaternion(Quaternion q)
        {
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);

            if (length > Mathf.Epsilon)
            {
                return new Quaternion(
                    q.x / length,
                    q.y / length,
                    q.z / length,
                    q.w / length
                );
            }

            return Quaternion.identity;
        }

        // 取整工具函数
        private static Vector3 RoundVector3(Vector3 vector, int decimals)
        {
            return new Vector3(
                RoundFloat(vector.x, decimals),
                RoundFloat(vector.y, decimals),
                RoundFloat(vector.z, decimals)
            );
        }

        private static Quaternion RoundQuaternion(Quaternion quaternion, int decimals)
        {
            return new Quaternion(
                RoundFloat(quaternion.x, decimals),
                RoundFloat(quaternion.y, decimals),
                RoundFloat(quaternion.z, decimals),
                RoundFloat(quaternion.w, decimals)
            );
        }

        private static float RoundFloat(float value, int decimals)
        {
            // 如果值非常接近0，则直接返回0
            if (Mathf.Abs(value) < Mathf.Pow(10, -decimals))
            {
                return 0f;
            }
    
            // 使用Mathf.Round进行四舍五入
            float multiplier = Mathf.Pow(10, decimals);
            return Mathf.Round(value * multiplier) / multiplier;
        }


        #endregion

        #region Collider轮廓绘制

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