#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(LocomotionAsset), true)]
[CanEditMultipleObjects]
public sealed class LocomotionAssetEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        // Use the same card chrome as ActionAsset; field layout remains native IMGUI.
        ActionEditorTheme.Apply(root, "action-editor-asset-inspector");
        Undo.undoRedoPerformed -= Repaint;
        Undo.undoRedoPerformed += Repaint;

        AddCard(root, "Mode Selection", () =>
        {
            DrawProperty("priority");
            DrawProperty("entryConditions");
        });
        AddCard(root, "Tags", () => DrawProperty("selfTags"));
        AddCard(root, "Movement Config", () => DrawChildren("movementConfig"));
        AddCard(root, "Move", () => LocomotionMoveAuthoringGUI.Draw(serializedObject.FindProperty("move")));
        if (serializedObject.FindProperty("start") != null)
        {
            AddCard(root, "Transitions", () =>
            {
                DrawProperty("transitionBlendDuration");
                DrawProperty("stopPlaybackMode");
                DrawProperty("transitionDecisionConfig");
                DrawProperty("start");
                DrawProperty("stop");
                DrawProperty("pivot");
            });
        }
        root.Add(new IMGUIContainer(() => DrawCard(() =>
            DrawPropertiesExcluding(serializedObject, "m_Script", "priority", "entryConditions", "selfTags",
                "movementConfig", "move", "transitionBlendDuration", "stopPlaybackMode", "transitionDecisionConfig",
                "start", "stop", "pivot"))));
        return root;
    }

    private void OnDisable() => Undo.undoRedoPerformed -= Repaint;

    private void AddCard(VisualElement root, string title, Action draw)
    {
        var card = new VisualElement();
        card.AddToClassList("action-editor-section");
        var heading = new Label(title);
        heading.AddToClassList("action-editor-section-title");
        heading.AddToClassList("action-editor-card-title");
        card.Add(heading);
        card.Add(new IMGUIContainer(() => DrawCard(draw)));
        root.Add(card);
    }

    private void DrawCard(Action draw)
    {
        serializedObject.UpdateIfRequiredOrScript();
        draw();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProperty(string path)
    {
        var property = serializedObject.FindProperty(path);
        if (property != null) EditorGUILayout.PropertyField(property, true);
    }

    private void DrawChildren(string path)
    {
        var property = serializedObject.FindProperty(path);
        if (property == null) return;
        var child = property.Copy();
        var end = property.GetEndProperty();
        if (!child.NextVisible(true)) return;
        do
        {
            if (SerializedProperty.EqualContents(child, end)) break;
            EditorGUILayout.PropertyField(child, true);
        } while (child.NextVisible(false));
    }
}

/// <summary>
/// Authoring view for the Move space. Only native controls are used: popups for the blend mode and
/// the 2D axis sources, and a plain SerializedProperty for the samples list, so the list keeps
/// Unity's own frame, folding, reordering and multi-object editing. Nothing is written until an
/// explicit edit, so opening an asset never migrates it.
/// </summary>
internal sealed class LocomotionMoveAuthoringGUI
{
    private const string ParameterTooltip = "HorizontalSpeed: planar speed (m/s). VerticalSpeed: signed vertical speed (m/s). "
        + "InputStrength: 0–1. LocalInputX/Z: right/forward input components (-1–1). "
        + "LocalVelocityX/Z: right/forward velocity components (m/s). TurnLean: -1 left, +1 right. "
        + "A zero local component alone does not identify Idle.";
    private static readonly string[] Modes = { "1D", "2D Directional", "2D Cartesian" };
    private static readonly GUIContent ModeLabel = new GUIContent("Mode",
        "1D：一个数值。2D Directional：向量方向和长度。2D Cartesian：两个独立量，例如侧倾与输入强度。");

    internal static void Draw(SerializedProperty move)
    {
        if (move == null) return;
        int mode = ReadMode(move, out bool mixedMode);
        DrawMode(move, mode, mixedMode);
        if (mixedMode || mode < 0)
        {
            EditorGUILayout.HelpBox("所选资产的混合方式不同或无效，请先统一选择。", MessageType.Info);
            return;
        }

        bool twoD = mode != 0;
        SerializedProperty definition = move.FindPropertyRelative(twoD ? "twoDimensional" : "oneDimensional");
        if (twoD)
        {
            DrawAxis(definition, true);
            DrawAxis(definition, false);
        }
        else EditorGUILayout.PropertyField(definition.FindPropertyRelative("parameter"), new GUIContent("Parameter", ParameterTooltip));

        EditorGUILayout.PropertyField(definition.FindPropertyRelative("samples"), new GUIContent("Samples"), true);
        if (AnyTarget(move, UsesLean))
        {
            DrawLean(move.FindPropertyRelative("turnLean"));
            if (AnyTarget(move, HasInvalidLean))
                EditorGUILayout.HelpBox("Invalid Turn Lean settings in the selection. TurnLean outputs zero; Move samples are retained. "
                    + "Full Lean Acceleration must be finite and > 0. Minimum Speed and Smooth Seconds must be finite and >= 0. "
                    + "Runtime settings are captured on creation.", MessageType.Warning);
        }
    }

    private static void DrawMode(SerializedProperty move, int mode, bool mixed)
    {
        EditorGUI.showMixedValue = mixed;
        EditorGUI.BeginChangeCheck();
        int selected = EditorGUILayout.Popup(ModeLabel, mode, Modes);
        bool changed = EditorGUI.EndChangeCheck();
        EditorGUI.showMixedValue = false;
        if (!changed) return;

        move.FindPropertyRelative("blendType").intValue = selected == 0 ? 0 : 1;
        // Switching dimensions must not discard the other space's algorithm or axis sources.
        if (selected != 0) move.FindPropertyRelative("twoDimensional.algorithm").intValue = selected - 1;
    }

    private static int ReadMode(SerializedProperty move, out bool mixed)
    {
        static int Read(SerializedProperty entry)
        {
            int blendType = entry.FindPropertyRelative("blendType").intValue;
            if (blendType == 0) return 0;
            if (blendType != 1) return -1;
            int algorithm = entry.FindPropertyRelative("twoDimensional.algorithm").intValue;
            return algorithm is >= 0 and <= 1 ? algorithm + 1 : -1;
        }

        int value = Read(move);
        mixed = false;
        foreach (UnityEngine.Object target in move.serializedObject.targetObjects)
            if (target != move.serializedObject.targetObject)
                using (var single = new SerializedObject(target))
                    if (Read(single.FindProperty(move.propertyPath)) != value) mixed = true;
        return value;
    }

    private static void DrawAxis(SerializedProperty definition, bool x)
    {
        var label = new GUIContent(x ? "X Parameter" : "Y Parameter", ParameterTooltip);
        LocomotionMoveParameter value = EffectiveAxis(definition, x);
        bool mixed = false;
        foreach (UnityEngine.Object target in definition.serializedObject.targetObjects)
            if (target != definition.serializedObject.targetObject)
                using (var single = new SerializedObject(target))
                    if (EffectiveAxis(single.FindProperty(definition.propertyPath), x) != value) mixed = true;

        EditorGUI.showMixedValue = mixed;
        EditorGUI.BeginChangeCheck();
        var selected = (LocomotionMoveParameter)EditorGUILayout.EnumPopup(label, value);
        bool changed = EditorGUI.EndChangeCheck();
        EditorGUI.showMixedValue = false;
        if (!changed) return;

        SetAxis(definition, x, selected);
    }

    // One edit transaction: flush pending changes before per-target writes, then refresh the
    // multi-object view. Never write the untouched axis through a multi-target property.
    internal static void SetAxis(SerializedProperty definition, bool x, LocomotionMoveParameter selected)
    {
        var outer = definition.serializedObject;
        string path = definition.propertyPath;
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Change Move Axis");
        outer.ApplyModifiedProperties();
        foreach (UnityEngine.Object target in outer.targetObjects)
        {
            using var single = new SerializedObject(target);
            WriteAxis(single.FindProperty(path), x, selected);
            single.ApplyModifiedProperties();
        }
        outer.Update();
        Undo.CollapseUndoOperations(undoGroup);
    }

    private static void WriteAxis(SerializedProperty entry, bool x, LocomotionMoveParameter selected)
    {
        LocomotionMoveParameter oldX = EffectiveAxis(entry, true);
        LocomotionMoveParameter oldY = EffectiveAxis(entry, false);
        entry.FindPropertyRelative("xParameter").intValue = (int)(x ? selected : oldX);
        entry.FindPropertyRelative("yParameter").intValue = (int)(x ? oldY : selected);
        entry.FindPropertyRelative("axesConfigured").boolValue = true;
    }

    private static LocomotionMoveParameter EffectiveAxis(SerializedProperty definition, bool x)
    {
        if (definition.FindPropertyRelative("axesConfigured").boolValue)
            return (LocomotionMoveParameter)definition.FindPropertyRelative(x ? "xParameter" : "yParameter").intValue;
        return LocomotionMove2DDefinition.ResolveLegacyAxis(
            definition.FindPropertyRelative("parameter").intValue, x);
    }

    private static bool UsesLean(SerializedProperty move)
    {
        if (move.FindPropertyRelative("blendType").intValue == 0)
            return move.FindPropertyRelative("oneDimensional.parameter").intValue == (int)LocomotionMoveParameter.TurnLean;
        SerializedProperty definition = move.FindPropertyRelative("twoDimensional");
        return EffectiveAxis(definition, true) == LocomotionMoveParameter.TurnLean
            || EffectiveAxis(definition, false) == LocomotionMoveParameter.TurnLean;
    }

    private static bool HasInvalidLean(SerializedProperty move)
    {
        if (!UsesLean(move)) return false;
        var lean = move.FindPropertyRelative("turnLean");
        return !new LocomotionTurnLeanConfig
        {
            FullLeanAcceleration = lean.FindPropertyRelative("FullLeanAcceleration").floatValue,
            MinimumSpeed = lean.FindPropertyRelative("MinimumSpeed").floatValue,
            SmoothSeconds = lean.FindPropertyRelative("SmoothSeconds").floatValue,
        }.IsValid;
    }

    private static bool AnyTarget(SerializedProperty property, Func<SerializedProperty, bool> predicate)
    {
        if (predicate(property)) return true;
        foreach (UnityEngine.Object target in property.serializedObject.targetObjects)
            if (target != property.serializedObject.targetObject)
                using (var single = new SerializedObject(target))
                    if (predicate(single.FindProperty(property.propertyPath))) return true;
        return false;
    }

    private static void DrawLean(SerializedProperty lean)
    {
        lean.isExpanded = EditorGUILayout.Foldout(lean.isExpanded, new GUIContent("Turn Lean",
            "由移动模型的转弯响应推导侧倾强度。只影响动画，不改变角色移动。"), true);
        if (!lean.isExpanded) return;

        EditorGUILayout.PropertyField(lean.FindPropertyRelative("FullLeanAcceleration"),
            new GUIContent("Full Lean Acceleration", "单位 m/s²。越大越不容易倾满；越小，侧倾越明显。"));
        EditorGUILayout.PropertyField(lean.FindPropertyRelative("MinimumSpeed"),
            new GUIContent("Minimum Speed", "单位 m/s。低于此速度不侧倾，达到两倍速度时完全生效。"));
        EditorGUILayout.PropertyField(lean.FindPropertyRelative("SmoothSeconds"),
            new GUIContent("Smooth Seconds", "单位秒。越大越柔和也越滞后；0 为立即响应。"));
    }
}

/// <summary>
/// One compact row per sample: animation, threshold, sync. Only the element content is customized;
/// the array frame, element headers, reordering and +/- buttons stay native Unity UI.
/// </summary>
internal abstract class LocomotionMoveSampleDrawer : PropertyDrawer
{
    protected const float SyncWidth = 18f;
    protected const float Gap = 6f;
    // Breathing room above and below the single content row; Unity's element spacing alone is too tight.
    protected const float Padding = 4f;
    private static readonly GUIContent SyncLabel = new GUIContent(string.Empty, "对齐循环归一化时间。");

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        EditorGUIUtility.singleLineHeight + Padding * 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        position.yMin += Padding;
        position.yMax -= Padding;
        if (position.width <= SyncWidth + Gap * 2f) return;
        EditorGUI.BeginProperty(position, label, property);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        try
        {
            var sync = new Rect(position.xMax - SyncWidth, position.y, SyncWidth, position.height);
            Rect threshold = ThresholdRect(position, sync);
            float contentWidth = position.width - SyncWidth - Gap * 2f;
            // Preserve at least half the available content width for the animation reference.
            threshold.width = Mathf.Min(threshold.width, contentWidth * 0.5f);
            threshold.x = sync.x - Gap - threshold.width;
            var animation = new Rect(position.x, position.y, contentWidth - threshold.width, position.height);
            EditorGUI.PropertyField(animation, property.FindPropertyRelative("animation"), GUIContent.none);
            EditorGUI.PropertyField(threshold, property.FindPropertyRelative("threshold"), ThresholdLabel);
            EditorGUI.PropertyField(sync, property.FindPropertyRelative("sync"), SyncLabel);
        }
        finally
        {
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    protected abstract Rect ThresholdRect(Rect position, Rect sync);
    protected abstract GUIContent ThresholdLabel { get; }
}

[CustomPropertyDrawer(typeof(LocomotionMove1DSample))]
internal sealed class LocomotionMove1DSampleDrawer : LocomotionMoveSampleDrawer
{
    protected override GUIContent ThresholdLabel => new GUIContent(string.Empty,
        "Threshold：含义由 Parameter 决定，单位见该参数的 tooltip。");

    protected override Rect ThresholdRect(Rect position, Rect sync) => new Rect(
        sync.x - Gap - 90f, position.y, 90f, position.height);
}

[CustomPropertyDrawer(typeof(LocomotionMove2DSample))]
internal sealed class LocomotionMove2DSampleDrawer : LocomotionMoveSampleDrawer
{
    protected override GUIContent ThresholdLabel => new GUIContent(string.Empty,
        "Threshold：X、Y 分别对应 X Parameter 与 Y Parameter。");

    protected override Rect ThresholdRect(Rect position, Rect sync)
    {
        float width = Mathf.Clamp(position.width * 0.3f, 130f, 190f);
        return new Rect(sync.x - Gap - width, position.y, width, position.height);
    }
}
#endif
