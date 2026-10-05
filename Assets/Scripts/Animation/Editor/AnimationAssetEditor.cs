#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(AnimationAsset))]
public sealed class AnimationAssetEditor : Editor
{
    // Unity initializes the preview range from AnimationClipEditor.OnInspectorGUI, which
    // isn't drawn in this inspector. Its preview-only API has no public range setter.
    private static readonly System.Type NativeClipEditorType = typeof(Editor).Assembly.GetType("UnityEditor.AnimationClipEditor");
    private static readonly FieldInfo AvatarPreviewField = NativeClipEditorType?.GetField("m_AvatarPreview", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo TimeControlField = AvatarPreviewField?.FieldType.GetField("timeControl", BindingFlags.Instance | BindingFlags.Public);
    private static readonly FieldInfo PreviewStartField = TimeControlField?.FieldType.GetField("startTime", BindingFlags.Instance | BindingFlags.Public);
    private static readonly FieldInfo PreviewStopField = TimeControlField?.FieldType.GetField("stopTime", BindingFlags.Instance | BindingFlags.Public);

    private Editor _clipEditor;
    private AnimationClip _previewClip;
    private VisualElement _rootMotionCard;
    private VisualElement _stopPointCard;
    private VisualElement _footMarkersCard;
    private Editor ClipEditor
    {
        get
        {
            var clip = ((AnimationAsset)target).Clip;
            if (_previewClip != clip)
            {
                if (_clipEditor != null) DestroyImmediate(_clipEditor);
                _previewClip = clip;
                _clipEditor = clip != null && NativeClipEditorType != null ? CreateEditor(clip, NativeClipEditorType) : null;
            }
            return _clipEditor;
        }
    }
    private void OnDisable()
    {
        Undo.undoRedoPerformed -= Repaint;
        if (_clipEditor != null) DestroyImmediate(_clipEditor);
        _clipEditor = null;
        _previewClip = null;
        _rootMotionCard = _stopPointCard = _footMarkersCard = null;
    }
    public override bool HasPreviewGUI() => ClipEditor != null && ClipEditor.HasPreviewGUI() && SetPreviewRange();
    public override void OnInteractivePreviewGUI(Rect rect, GUIStyle background)
    {
        if (SetPreviewRange()) ClipEditor.OnInteractivePreviewGUI(rect, background);
    }
    public override void OnPreviewGUI(Rect rect, GUIStyle background) => ClipEditor?.OnPreviewGUI(rect, background);
    public override void OnPreviewSettings() => ClipEditor?.OnPreviewSettings();

    private bool SetPreviewRange()
    {
        if (_clipEditor == null || PreviewStartField == null || PreviewStopField == null) return false;
        object preview = AvatarPreviewField.GetValue(_clipEditor);
        object timeControl = preview != null ? TimeControlField.GetValue(preview) : null;
        if (timeControl == null) return false;
        var settings = AnimationUtility.GetAnimationClipSettings(_previewClip);
        PreviewStartField.SetValue(timeControl, settings.startTime);
        PreviewStopField.SetValue(timeControl, settings.stopTime);
        return true;
    }

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        ActionEditorTheme.Apply(root, "action-editor-asset-inspector");
        Undo.undoRedoPerformed -= Repaint;
        Undo.undoRedoPerformed += Repaint;

        AddCard(root, "Animation", DrawAnimation);
        AddCard(root, "Bake", DrawBake);
        _rootMotionCard = AddCard(root, "Root Motion", DrawRootMotion);
        _stopPointCard = AddCard(root, "Stop Point", DrawStopPoint);
        _footMarkersCard = AddCard(root, "Foot Markers", DrawFootMarkers);
        UpdateCardVisibility((AnimationAsset)target);
        return root;
    }

    private VisualElement AddCard(VisualElement root, string title, System.Action<AnimationAsset> draw)
    {
        var card = new VisualElement();
        card.AddToClassList("action-editor-section");
        var heading = new Label(title);
        heading.AddToClassList("action-editor-section-title");
        heading.AddToClassList("action-editor-card-title");
        card.Add(heading);
        card.Add(new IMGUIContainer(() => DrawCard(draw)));
        root.Add(card);
        return card;
    }

    private void DrawCard(System.Action<AnimationAsset> draw)
    {
        var asset = (AnimationAsset)target;
        if (asset == null) return;
        serializedObject.UpdateIfRequiredOrScript();
        draw(asset);
        serializedObject.ApplyModifiedProperties();
        UpdateCardVisibility(asset);
    }

    private void UpdateCardVisibility(AnimationAsset asset)
    {
        bool hasClip = asset != null && asset.Clip != null;
        if (_rootMotionCard != null)
            _rootMotionCard.style.display = hasClip && asset.RootMotionData != null ? DisplayStyle.Flex : DisplayStyle.None;
        if (_stopPointCard != null)
            _stopPointCard.style.display = hasClip ? DisplayStyle.Flex : DisplayStyle.None;
        if (_footMarkersCard != null)
            _footMarkersCard.style.display = hasClip ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void DrawAnimation(AnimationAsset asset)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_clip"));
        serializedObject.ApplyModifiedProperties();
        if (asset.Clip == null) return;
        string path = AssetDatabase.GetAssetPath(asset.Clip);
        EditorGUILayout.LabelField(new GUIContent("Source File"),
            new GUIContent(string.IsNullOrEmpty(path) ? "—" : Path.GetFileName(path), path));
    }

    private void DrawBake(AnimationAsset asset)
    {
        var rig = serializedObject.FindProperty("_animationRigAsset");
        // Draw the same reference field without the old standalone bake-context header.
        EditorGUI.ObjectField(EditorGUILayout.GetControlRect(), rig, new GUIContent(rig.displayName, rig.tooltip));
        serializedObject.ApplyModifiedProperties();
        AnimationAssetBakeStatus status = AnimationAssetBakeWorkflow.GetStatus(asset);
        if (status.Code == AnimationAssetBakeStatusCode.Ready)
            EditorGUILayout.LabelField(new GUIContent("Status"),
                new GUIContent($"Ready · {asset.RootMotionData.SampleCount} samples", status.Message));
        else
            EditorGUILayout.HelpBox(status.Message, ToMessageType(status.Code));

        using (new EditorGUI.DisabledScope(asset.Clip == null))
        {
            string label = status.Code == AnimationAssetBakeStatusCode.Missing ? "Bake Animation Data" : "Rebuild Animation Data";
            if (GUILayout.Button(label))
            {
                if (!AnimationAssetBakeWorkflow.TryBake(asset, out AnimationAssetBakeOperationResult result))
                    Debug.LogError($"[AnimationAsset Bake] {result.Message}", asset);
                else
                    Debug.Log($"[AnimationAsset Bake] {result.Message}", asset);
            }
        }
    }

    private static float ClipFrameRate(AnimationAsset asset) => asset.Clip.frameRate > 0f ? asset.Clip.frameRate : 60f;

    private static void DrawRootMotion(AnimationAsset asset)
    {
        var trajectory = asset.RootMotionData;
        if (asset.Clip == null || trajectory == null) return;
        DrawCurve("Root X", trajectory.SampleTimes, trajectory.CumulativePositions, 0);
        DrawCurve("Root Y", trajectory.SampleTimes, trajectory.CumulativePositions, 1);
        DrawCurve("Root Z", trajectory.SampleTimes, trajectory.CumulativePositions, 2);
    }

    private static void DrawStopPoint(AnimationAsset asset)
    {
        if (asset.Clip == null) return;
        float fps = ClipFrameRate(asset);
        bool stopOverride = EditorGUILayout.Toggle("Override Stop Point", asset.HasStopTimeOverride);
        float stop = asset.StopTime;
        using (new EditorGUI.DisabledScope(!stopOverride))
        {
            int frame = Mathf.RoundToInt(stop * fps);
            int edited = EditorGUILayout.IntField("Stop Frame", frame);
            if (edited != frame) stop = Mathf.Clamp(edited / fps, 0f, asset.Clip.length);
            EditorGUILayout.LabelField("Stop Time", $"{stop:F3} s");
        }
        if (stopOverride != asset.HasStopTimeOverride || stop != asset.StopTime)
        {
            Undo.RecordObject(asset, "Edit Stop Point");
            asset.EditorOverrideStopTime(stopOverride, stop);
            EditorUtility.SetDirty(asset);
        }

        var trajectory = asset.RootMotionData;
        if (trajectory != null && AnimationStopDistanceCurve.TryCreate(trajectory, asset.StopTime, out var curve))
        {
            var keys = new Keyframe[curve.Times.Count];
            for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(curve.Times[i], curve.RemainingDistances[i]);
            using (new EditorGUI.DisabledScope(!stopOverride))
                EditorGUILayout.CurveField("Remaining Distance (m)", LinearCurve(keys), GUILayout.Height(60));
        }
    }

    private static void DrawFootMarkers(AnimationAsset asset)
    {
        if (asset.Clip == null) return;
        float fps = ClipFrameRate(asset);
        bool footOverride = EditorGUILayout.Toggle("Override Foot Markers", asset.HasFootMarkerOverrides);
        var markers = new System.Collections.Generic.List<AnimationFootMarker>(asset.FootMarkers);
        using (new EditorGUILayout.HorizontalScope())
        { EditorGUILayout.LabelField("Frame"); EditorGUILayout.LabelField("Foot"); GUILayout.Space(28); }
        bool changed = footOverride != asset.HasFootMarkerOverrides;
        using (new EditorGUI.DisabledScope(!footOverride))
        {
            for (int i = 0; i < markers.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int frame = Mathf.RoundToInt(markers[i].Time * fps);
                    int edited = EditorGUILayout.IntField(frame);
                    var foot = (AnimationFoot)EditorGUILayout.EnumPopup(markers[i].Foot);
                    if (edited != frame || foot != markers[i].Foot)
                    { markers[i] = new AnimationFootMarker(Mathf.Clamp(edited / fps, 0f, asset.Clip.length), foot); changed = true; }
                    if (GUILayout.Button("−", GUILayout.Width(24))) { markers.RemoveAt(i--); changed = true; }
                }
            }
            if (GUILayout.Button("Add Foot Contact"))
            { markers.Add(new AnimationFootMarker(0f, AnimationFoot.Left)); changed = true; }
        }
        if (changed)
        {
            Undo.RecordObject(asset, "Edit Foot Contacts");
            asset.EditorOverrideFootMarkers(footOverride, markers.ToArray());
            EditorUtility.SetDirty(asset);
        }
    }

    private static void DrawCurve(string label, System.Collections.Generic.IReadOnlyList<float> times,
        System.Collections.Generic.IReadOnlyList<Vector3> positions, int axis)
    {
        int count = Mathf.Min(times.Count, positions.Count);
        var keys = new Keyframe[count];
        for (int i = 0; i < count; i++) keys[i] = new Keyframe(times[i], positions[i][axis]);
        EditorGUILayout.CurveField(label, LinearCurve(keys), GUILayout.Height(50));
    }

    private static AnimationCurve LinearCurve(Keyframe[] keys)
    {
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }

    private static MessageType ToMessageType(AnimationAssetBakeStatusCode code)
    {
        return code switch
        {
            AnimationAssetBakeStatusCode.Ready => MessageType.Info,
            AnimationAssetBakeStatusCode.Missing => MessageType.Warning,
            _ => MessageType.Error,
        };
    }
}
#endif
