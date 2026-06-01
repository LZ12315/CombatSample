using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActorMotor))]
public class ActorMotorEditor : Editor
{
    private static readonly Color LabelColor = new(0.85f, 0.85f, 0.85f);
    private static readonly Color ValueColor = Color.white;
    private static readonly Color GroundedColor = new(0.3f, 1f, 0.4f);
    private static readonly Color AirborneColor = new(1f, 0.45f, 0.35f);
    private static readonly Color SectionColor = new(0.55f, 0.75f, 1f);

    private bool _showOverview = true;
    private bool _showVelocityComposition = true;
    private bool _showChannels = true;
    private bool _showLocomotion = true;
    private bool _showFacing = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying)
            return;

        ActorMotor motor = (ActorMotor)target;
        if (motor == null)
            return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("═══ Runtime Debug (Play Mode) ═══", EditorStyles.boldLabel);

        DrawMotionOverview(motor);
        DrawVelocityComposition(motor);
        DrawMotionChannels(motor);
        DrawLocomotion(motor);
        DrawFacing(motor);

        // Keep refreshing while playing
        Repaint();
    }

    #region === 运动总览 ===

    private void DrawMotionOverview(ActorMotor m)
    {
        _showOverview = EditorGUILayout.BeginFoldoutHeaderGroup(_showOverview, "运动总览 / Motion Overview");
        if (!_showOverview) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        EditorGUI.indentLevel++;

        Vector3 vel = m.CurrentVelocity;
        Row("速度 (m/s)", $"({vel.x:F2}, {vel.y:F2}, {vel.z:F2})");
        Row("水平速度", $"{m.CurrentHorizontalSpeed:F2} m/s");
        Row("垂直速度", $"{m.CurrentVerticalSpeed:F2} m/s");

        ActorGroundState gs = m.GroundState;
        bool grounded = m.IsGrounded;
        Color gc = grounded ? GroundedColor : AirborneColor;
        GUI.color = gc;
        Row("地面状态", gs.ToString());
        GUI.color = Color.white;

        Row("跳跃次数", $"{m.JumpCount} / {m.MaxJumpCount}");

        GUI.color = new Color(0.7f, 0.7f, 0.7f);
        Vector3 reqVel = m.RequestedVelocity;
        Row("请求速度 (KCC)", $"({reqVel.x:F2}, {reqVel.y:F2}, {reqVel.z:F2})");
        Row("时间缩放", $"{m.MovementTimeScale:F2}x");
        GUI.color = Color.white;

        EditorGUI.indentLevel--;
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    #endregion

    #region === 速度构成 ===

    private void DrawVelocityComposition(ActorMotor m)
    {
        _showVelocityComposition = EditorGUILayout.BeginFoldoutHeaderGroup(_showVelocityComposition, "速度构成 / Velocity Composition");
        if (!_showVelocityComposition) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        EditorGUI.indentLevel++;

        var loco = m.DebugLocomotion;
        var ch = m.DebugChannels;

        // Locomotion
        Vector3 locoVel = loco.CachedVelocity;
        Row("Locomotion", $"({locoVel.x:F2}, 0, {locoVel.z:F2})  [{locoVel.magnitude:F2} m/s]");

        // Impulse
        Vector3 imp = ch.DebugHorizontalImpulse;
        Row("Impulse (水平)", $"({imp.x:F2}, 0, {imp.z:F2})  [{imp.magnitude:F2} m/s]");

        // Owner
        if (ch.HasHorizontalVelocityOwner)
        {
            Vector3 ownH = ch.DebugOwnerHorizontalVelocity;
            GUI.color = new Color(1f, 0.75f, 0.3f);
            Row("Owner (水平)", $"({ownH.x:F2}, 0, {ownH.z:F2})  [{ownH.magnitude:F2} m/s]");
            GUI.color = Color.white;
        }
        else
        {
            Row("Owner (水平)", "无");
        }

        if (ch.HasVerticalVelocityOwner)
        {
            float ownV = ch.DebugOwnerVerticalVelocity;
            GUI.color = new Color(1f, 0.75f, 0.3f);
            Row("Owner (垂直)", $"{ownV:F2} m/s");
            GUI.color = Color.white;
        }
        else
        {
            Row("Owner (垂直)", "无");
        }

        // Composed output
        Vector3 req = m.RequestedVelocity;
        Row("→ 合成请求速度", $"({req.x:F2}, {req.y:F2}, {req.z:F2})  [{req.magnitude:F2} m/s]");

        EditorGUI.indentLevel--;
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    #endregion

    #region === 运动通道 ===

    private void DrawMotionChannels(ActorMotor m)
    {
        _showChannels = EditorGUILayout.BeginFoldoutHeaderGroup(_showChannels, "运动通道 / Motion Channels");
        if (!_showChannels) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        EditorGUI.indentLevel++;

        var ch = m.DebugChannels;

        Vector3 imp = ch.DebugHorizontalImpulse;
        Row("水平冲量", $"({imp.x:F3}, 0, {imp.z:F3})  |mag|={imp.magnitude:F3}");

        float vImp = ch.DebugVerticalImpulse;
        GUI.color = vImp > 0.01f ? new Color(0.3f, 1f, 0.5f) : (vImp < -0.01f ? new Color(1f, 0.4f, 0.3f) : Color.white);
        Row("垂直冲量", $"{vImp:F2} m/s");
        GUI.color = Color.white;

        float grav = ch.DebugGravityAccumulator;
        GUI.color = grav < -0.5f ? new Color(1f, 0.55f, 0.3f) : Color.white;
        Row("重力累积", $"{grav:F2} m/s");
        GUI.color = Color.white;

        Row("水平 Owner 活跃", ch.HasHorizontalVelocityOwner ? "是" : "否");
        Row("垂直 Owner 活跃", ch.HasVerticalVelocityOwner ? "是" : "否");

        EditorGUI.indentLevel--;
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    #endregion

    #region === 移动意图 ===

    private void DrawLocomotion(ActorMotor m)
    {
        _showLocomotion = EditorGUILayout.BeginFoldoutHeaderGroup(_showLocomotion, "移动意图 / Locomotion");
        if (!_showLocomotion) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        EditorGUI.indentLevel++;

        var loco = m.DebugLocomotion;
        var intent = m.LocomotionIntent;

        Vector3 dir = intent.WorldMoveDirection;
        Row("世界方向", $"({dir.x:F2}, {dir.y:F2}, {dir.z:F2})");

        Vector3 facing = intent.FacingDirection;
        Row("朝向方向", $"({facing.x:F2}, {facing.y:F2}, {facing.z:F2})");

        Row("移动强度", $"{intent.MoveStrength:F2}");
        Row("基础速度", $"{m.DebugBaseSpeed:F1} m/s");

        bool airborne = m.IsAirborne;
        float effectiveSpeed = intent.MoveStrength * m.DebugBaseSpeed;
        if (airborne) effectiveSpeed *= m.DebugAirControlFactor;
        string speedNote = airborne ? $" (空中 ×{m.DebugAirControlFactor:F2})" : "";
        Row("生效速度", $"{effectiveSpeed:F2} m/s{speedNote}");

        GUI.color = loco.IsSuppressed ? new Color(1f, 0.5f, 0.3f) : new Color(0.5f, 1f, 0.5f);
        Row("已抑制", loco.IsSuppressed ? "是" : "否");
        GUI.color = Color.white;

        Vector3 cached = loco.CachedVelocity;
        Row("缓存速度", $"({cached.x:F2}, 0, {cached.z:F2})  [{cached.magnitude:F2} m/s]");

        EditorGUI.indentLevel--;
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    #endregion

    #region === 朝向 ===

    private void DrawFacing(ActorMotor m)
    {
        _showFacing = EditorGUILayout.BeginFoldoutHeaderGroup(_showFacing, "朝向 / Facing");
        if (!_showFacing) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        EditorGUI.indentLevel++;

        var facing = m.DebugFacing;
        Transform t = m.transform;

        float currentYaw = t.rotation.eulerAngles.y;
        float targetYaw = facing.TargetRotationYaw;
        float pendingYaw = facing.PendingRotation.eulerAngles.y;

        Row("当前 Yaw", $"{currentYaw:F1}°");
        Row("目标 Yaw", $"{targetYaw:F1}°");
        Row("Pending Yaw", $"{pendingYaw:F1}°");

        float yawDelta = Mathf.DeltaAngle(currentYaw, targetYaw);
        GUI.color = Mathf.Abs(yawDelta) > 1f ? new Color(1f, 0.8f, 0.3f) : new Color(0.5f, 1f, 0.5f);
        Row("Δ Yaw (→目标)", $"{yawDelta:F1}°");
        GUI.color = Color.white;

        Row("角速度", $"{m.DebugRotateSpeed:F0}°/s");

        if (facing.HasFacingOverride)
        {
            GUI.color = new Color(1f, 0.75f, 0.3f);
            Vector3 ovr = facing.OverrideDirection;
            Row("Facing 覆盖", $"({ovr.x:F2}, {ovr.y:F2}, {ovr.z:F2})");
            GUI.color = Color.white;
        }
        else
        {
            Row("Facing 覆盖", "无");
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    #endregion

    #region === 工具 ===

    private static void Row(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        GUI.color = LabelColor;
        EditorGUILayout.LabelField(label, GUILayout.Width(160));
        GUI.color = ValueColor;
        EditorGUILayout.LabelField(value);
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    #endregion
}
