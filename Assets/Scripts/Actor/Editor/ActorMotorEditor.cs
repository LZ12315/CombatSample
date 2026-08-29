using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActorMotor))]
public class ActorMotorEditor : Editor
{
    private bool _showRuntime = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying)
            return;

        ActorMotor motor = (ActorMotor)target;
        if (motor == null)
            return;

        EditorGUILayout.Space(8);
        _showRuntime = EditorGUILayout.BeginFoldoutHeaderGroup(_showRuntime, "Runtime Debug");
        if (!_showRuntime)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUI.indentLevel++;
        DrawVector("Current Velocity", motor.CurrentVelocity);
        DrawVector("Requested Velocity", motor.RequestedVelocity);
        DrawVector("Locomotion Velocity", motor.DebugLocomotionVelocity);
        DrawVector("Horizontal Impulse", motor.Translation.DebugHorizontalImpulse);
        DrawVector("Horizontal Owner", motor.Translation.DebugOwnerHorizontalVelocity);
        EditorGUILayout.LabelField("Ground State", motor.GroundState.ToString());
        EditorGUILayout.LabelField("Vertical Velocity", motor.CurrentVerticalSpeed.ToString("F2"));
        EditorGUILayout.LabelField("Ballistic Vertical", motor.Translation.BallisticVerticalVelocity.ToString("F2"));
        EditorGUILayout.LabelField("Vertical Owner", motor.Translation.DebugOwnerVerticalVelocity.ToString("F2"));
        EditorGUILayout.LabelField("Move Scale", motor.MotionPolicy.LocomotionScale.ToString("F2"));
        EditorGUILayout.LabelField("Air Move Scale", motor.MotionPolicy.AirLocomotionScale.ToString("F2"));
        EditorGUILayout.LabelField("Gravity Scale", motor.MotionPolicy.GravityScale.ToString("F2"));
        EditorGUILayout.LabelField("Movement Time Scale", motor.MovementTimeScale.ToString("F2"));
        EditorGUILayout.LabelField("Root Rotation Owners", motor.Rotation.DebugRootRotationOwnerCount.ToString());
        EditorGUILayout.LabelField("Scripted Rotation Owners", motor.Rotation.DebugScriptedRotationOwnerCount.ToString());
        EditorGUILayout.LabelField("Locomotion Target Yaw", motor.DebugLocomotionTargetYaw.ToString("F1"));
        EditorGUI.indentLevel--;
        EditorGUILayout.EndFoldoutHeaderGroup();

        Repaint();
    }

    private static void DrawVector(string label, Vector3 value)
    {
        EditorGUILayout.LabelField(label, $"({value.x:F2}, {value.y:F2}, {value.z:F2})");
    }
}
