#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>Behavior-first authoring shared by the Action defaults and Timeline window.</summary>
internal static class ActionMotionPolicyGUI
{
    private static readonly string[] MovementModes = { "Allow", "Brake", "Locked" };
    private static readonly string[] TurnModes = { "Normal", "Limited", "Locked" };
    private const string MoveHelp = "Allow: joystick-driven movement. Brake: ignore move drive and decelerate normally. Locked: stop autonomous movement immediately. Does not block RootMotion, Velocity or impulses.";
    private const string TurnHelp = "Only affects Locomotion facing. SelfRotation keeps its authority. Limited is a degrees/second cap, not a multiplier.";

    internal static void DrawDefaults(SerializedProperty policy)
    {
        if (policy == null) return;
        DrawMovement(policy, null);
        DrawTurning(policy, null);
        DrawAdvanced(policy, null);
        try
        {
            new ActionMotionPolicy
            {
                movement = (ActionMovementControl)policy.FindPropertyRelative("movement").intValue,
                locomotionScale = policy.FindPropertyRelative("locomotionScale").floatValue,
                turning = (ActionTurnControl)policy.FindPropertyRelative("turning").intValue,
                maxTurnSpeed = policy.FindPropertyRelative("maxTurnSpeed").floatValue,
                airLocomotionScale = policy.FindPropertyRelative("airLocomotionScale").floatValue,
                gravityScale = policy.FindPropertyRelative("gravityScale").floatValue,
            }.Capture();
        }
        catch (InvalidOperationException exception) { EditorGUILayout.HelpBox(exception.Message, MessageType.Error); }
    }

    internal static void DrawWindow(SerializedProperty config, ActionAsset action, MotionPolicyItem item)
    {
        var defaults = action.MotionPolicy ?? new ActionMotionPolicy();
        DrawMovement(config, defaults);
        DrawTurning(config, defaults);
        DrawAdvanced(config, defaults);
        try
        {
            defaults.Capture();
            var fields = new ActionMotionPolicyOverride(item.Config);
            if (!fields.HasAny)
                EditorGUILayout.HelpBox("This window inherits every field and makes no changes. Choose a field to override; it returns to the Action default automatically when the window ends.", MessageType.Info);
            else if (HasOverlap(action, item, fields))
                EditorGUILayout.HelpBox("Overlapping policy windows affect the same field. Movement/turning use the stricter restriction; gravity multipliers multiply. Lane order does not set priority.", MessageType.Warning);
        }
        catch (InvalidOperationException exception) { EditorGUILayout.HelpBox(exception.Message, MessageType.Error); }
    }

    internal static string MovementSummary(ActionMotionPolicy policy) =>
        policy.movement == ActionMovementControl.Allow && policy.locomotionScale > 0f
            ? $"Allow {policy.locomotionScale * 100f:0.##}%"
            : policy.movement == ActionMovementControl.Brake ? "Brake" : "Locked";

    internal static string TurningSummary(ActionMotionPolicy policy) => policy.turning == ActionTurnControl.Limited
        ? $"Limited {policy.maxTurnSpeed:0.##} °/s" : policy.turning.ToString();

    private static void DrawMovement(SerializedProperty config, ActionMotionPolicy defaults)
    {
        bool window = defaults != null;
        bool configured = !window || config.FindPropertyRelative("useMovementControl").boolValue
            || config.FindPropertyRelative("useLocomotionScale").boolValue;
        var mode = (ActionMovementControl)config.FindPropertyRelative("movement").intValue;
        float scale = config.FindPropertyRelative("locomotionScale").floatValue;
        if (window && !config.FindPropertyRelative("useMovementControl").boolValue)
            mode = scale == 0f ? ActionMovementControl.Locked : ActionMovementControl.Allow;
        if (mode == ActionMovementControl.Allow && scale == 0f) mode = ActionMovementControl.Locked;
        string[] choices = window ? WithInheritance(MovementModes, MovementSummary(defaults)) : MovementModes;
        int selected = configured ? (int)mode + (window ? 1 : 0) : 0;
        EditorGUI.BeginChangeCheck();
        int edited = EditorGUILayout.Popup(new GUIContent("Movement", MoveHelp), selected, choices);
        if (EditorGUI.EndChangeCheck())
        {
            configured = !window || edited != 0;
            mode = configured ? (ActionMovementControl)(edited - (window ? 1 : 0)) : ActionMovementControl.Allow;
            if (window)
            {
                config.FindPropertyRelative("useMovementControl").boolValue = configured;
                config.FindPropertyRelative("useLocomotionScale").boolValue = false;
            }
            if (configured) config.FindPropertyRelative("movement").intValue = (int)mode;
            if (configured && mode == ActionMovementControl.Allow && scale <= 0f)
                config.FindPropertyRelative("locomotionScale").floatValue = 1f;
        }
        if (configured && mode == ActionMovementControl.Allow)
            DrawPercent(config.FindPropertyRelative("locomotionScale"), "Move Speed (%)", "Autonomous speed only, 0–100%. Zero is a movement lock.");
    }

    private static void DrawTurning(SerializedProperty config, ActionMotionPolicy defaults)
    {
        bool window = defaults != null;
        bool configured = !window || config.FindPropertyRelative("useTurnControl").boolValue;
        var mode = (ActionTurnControl)config.FindPropertyRelative("turning").intValue;
        EditorGUI.BeginChangeCheck();
        int edited = EditorGUILayout.Popup(new GUIContent("Turning", TurnHelp),
            configured ? (int)mode + (window ? 1 : 0) : 0,
            window ? WithInheritance(TurnModes, TurningSummary(defaults)) : TurnModes);
        if (EditorGUI.EndChangeCheck())
        {
            configured = !window || edited != 0;
            mode = configured ? (ActionTurnControl)(edited - (window ? 1 : 0)) : ActionTurnControl.Normal;
            if (window) config.FindPropertyRelative("useTurnControl").boolValue = configured;
            if (configured) config.FindPropertyRelative("turning").intValue = (int)mode;
        }
        if (configured && mode == ActionTurnControl.Limited)
            EditorGUILayout.PropertyField(config.FindPropertyRelative("maxTurnSpeed"),
                new GUIContent("Max Turn Speed (°/s)", "Positive cap. Cannot speed up Locomotion beyond its normal turning rules."));
    }

    private static void DrawAdvanced(SerializedProperty config, ActionMotionPolicy defaults)
    {
        bool window = defaults != null;
        float air = config.FindPropertyRelative("airLocomotionScale").floatValue;
        float gravity = config.FindPropertyRelative("gravityScale").floatValue;
        bool useAir = !window || config.FindPropertyRelative("useAirLocomotionScale").boolValue;
        bool useGravity = !window || config.FindPropertyRelative("useGravityScale").boolValue;
        string summary = (useAir && (window || air != 1f) ? $" · Air {air * 100f:0.##}%" : "")
            + (useGravity && (window || gravity != 1f) ? $" · Gravity {gravity * 100f:0.##}%" : "");
        config.isExpanded = EditorGUILayout.Foldout(config.isExpanded, "Advanced" + summary, true);
        if (!config.isExpanded) return;
        if (!window || DrawOverride(config, "useAirLocomotionScale", "Air Movement", defaults.airLocomotionScale))
            DrawPercent(config.FindPropertyRelative("airLocomotionScale"), "Air Movement (%)", "Additional autonomous movement multiplier while airborne, 0–100%.");
        if (!window || DrawOverride(config, "useGravityScale", "Gravity", defaults.gravityScale))
            DrawPercent(config.FindPropertyRelative("gravityScale"), "Gravity (%)", "Non-negative acceleration multiplier. Zero does not erase existing vertical velocity; use Vertical Velocity to hover.");
    }

    private static bool DrawOverride(SerializedProperty config, string field, string label, float inherited)
    {
        var property = config.FindPropertyRelative(field);
        EditorGUI.BeginChangeCheck();
        int selected = EditorGUILayout.Popup(label, property.boolValue ? 1 : 0,
            new[] { $"Inherit Action ({inherited * 100f:0.##}%)", "Override" });
        if (EditorGUI.EndChangeCheck()) property.boolValue = selected == 1;
        return property.boolValue;
    }

    private static void DrawPercent(SerializedProperty property, string label, string tooltip)
    {
        EditorGUI.BeginChangeCheck();
        float percent = EditorGUILayout.FloatField(new GUIContent(label, tooltip), property.floatValue * 100f);
        if (EditorGUI.EndChangeCheck()) property.floatValue = percent / 100f;
    }

    private static string[] WithInheritance(string[] modes, string inherited)
    {
        var result = new string[modes.Length + 1];
        result[0] = $"Inherit Action ({inherited})";
        Array.Copy(modes, 0, result, 1, modes.Length);
        return result;
    }

    private static bool HasOverlap(ActionAsset action, MotionPolicyItem item, ActionMotionPolicyOverride fields)
    {
        foreach (var lane in action.Timeline.GameplayLanes)
        {
            if (lane == null || lane.Muted) continue;
            foreach (var other in lane.Items)
            {
                if (other is not MotionPolicyItem policy || policy == item || policy.Muted
                    || !ActionGameplayLaneOccupancy.Overlaps(item.StartFrame, item.EndFrameExclusiveLong,
                        policy.StartFrame, policy.EndFrameExclusiveLong)) continue;
                if (fields.OverlapsFields(new ActionMotionPolicyOverride(policy.Config))) return true;
            }
        }
        return false;
    }
}
#endif
