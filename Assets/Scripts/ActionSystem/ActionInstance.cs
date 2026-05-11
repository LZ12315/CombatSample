using System;
using UnityEngine;
using DeiveEx.TagTree;

public class ActionInstance
{
    public ActionAsset Config { get; }

    public ActionData RuntimeData { get; private set; }

    /// <summary>本次 Action 开始时的上下文快照；无快照需求时为 default。</summary>
    public ActionEventContext EventContext { get; private set; }

    /// <summary>当前持有此 ActionInstance 的 Actor，OnEnter 时赋值，OnExit 时清空。</summary>
    public Actor Actor { get; private set; }

    public ActionInstance(ActionAsset config)
    {
        Config = config;
        ResetRuntimeData();
    }

    public void OnEnter(Actor actor, ActionEventContext context = default)
    {
        Actor = actor;
        EventContext = context;

        // 应用运动策略（必须在 Tag 之前，因为压制需要立即生效）
        ApplyMotionConfig(context);

        var selfTags = Config.SelfTags;
        if (Actor != null && selfTags != null)
        {
            for (int i = 0; i < selfTags.Count; i++)
            {
                var tagRef = selfTags[i];
                if (tagRef == null) continue;
                Tag tagObj = tagRef.GetTag();
                if (tagObj != null)
                    Actor.AddTag(tagObj, ActorTagContainerType.Transient);
            }
        }
    }

    public void OnExit()
    {
        var selfTags = Config.SelfTags;
        if (Actor != null && selfTags != null)
        {
            for (int i = 0; i < selfTags.Count; i++)
            {
                var tagRef = selfTags[i];
                if (tagRef == null) continue;
                Tag tagObj = tagRef.GetTag();
                if (tagObj != null)
                    Actor.RemoveTag(tagObj, ActorTagContainerType.Transient);
            }
        }

        // 恢复运动策略（必须在 Tag 之后，因为恢复后 Locomotion 才能重新生效）
        RestoreMotionConfig();

        Actor = null;
        EventContext = default;
    }

    public void UpdateNormalizedTime(double normalizedTime)
    {
        var currentData = RuntimeData;
        currentData.normalizedTime = normalizedTime;
        RuntimeData = currentData;
    }

    public void ResetRuntimeData()
    {
        RuntimeData = new ActionData
        {
            normalizedTime = 0
        };
    }

    #region === 运动策略 ===

    private void ApplyMotionConfig(ActionEventContext context)
    {
        if (Actor?.actorMotor == null) return;
        var motion = Config.MotionConfig;
        var motor = Actor.actorMotor;

        // Action 入场时清理旧 velocity owner，再按配置继承动量。
        motor.ClearVelocityOwners();
        motor.ApplyMotionHandoff(
            motion.horizontalMomentumInheritance,
            motion.verticalMomentumInheritance);
        motor.SetRootMotionApplyMode(motion.rootMotionMode);
        motor.SetLocomotionSuppressed(motion.suppressLocomotion);
        if (motion.gravityScale >= 0f)
            motor.SetGravityScale(motion.gravityScale);
        ApplyFacingOnStart(motion.facingOnStart, context);
    }

    private void RestoreMotionConfig()
    {
        if (Actor?.actorMotor == null) return;
        var motor = Actor.actorMotor;
        motor.SetRootMotionApplyMode(RootMotionApplyMode.External);
        motor.SetLocomotionSuppressed(false);
        motor.SetGravityScale(1f);
    }

    private void ApplyFacingOnStart(ActionFacingOnStart mode, ActionEventContext context)
    {
        if (mode == ActionFacingOnStart.None) return;
        Vector3 dir = Vector3.zero;

        if (mode == ActionFacingOnStart.SnapToInputOrTarget)
        {
            // 优先朝目标
            var target = Actor.combater?.CombatTarget?.transform;
            if (target != null)
            {
                dir = target.position - Actor.transform.position;
                dir.y = 0f;
            }
            // 没有目标 → 用 context 方向
            if (dir.sqrMagnitude < 0.001f)
                dir = context.Direction;
            // context 也没有 → 用当前移动意图
            if (dir.sqrMagnitude < 0.001f)
                dir = Actor.actorMotor.LocomotionIntent.WorldMoveDirection;
        }
        else if (mode == ActionFacingOnStart.SnapToInput)
        {
            dir = context.Direction;
            if (dir.sqrMagnitude < 0.001f)
                dir = Actor.actorMotor.LocomotionIntent.WorldMoveDirection;
        }

        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            Actor.actorMotor.SnapFacing(dir.normalized);
    }

    #endregion
}
