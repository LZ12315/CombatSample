# Actor Motion 行为基线验证清单

> Status: Current validation checklist  
> Code structure verified: 2026-08-02  
> The recorded PlayMode results below are historical results from 2026-05-05 and must be rerun before being treated as current.

本文档用于固定 Actor Motion 行为基线。目标是确认 `ActorMotor -> ActorMotionRuntime -> MotionChannels/GroundingRuntime/RootMotionBuffer/VelocityReadout` 这条链路在关键场景下的预期行为。

后续清理 `ActorMotor`、`MotionChannels` 或 `GroundingRuntime` 时，应先对照本文档，避免把结构调整变成未记录的手感变化。

## 总原则

- 先验证行为，再整理命名和边界。
- 不为了水平/垂直 API 表面形式对称而改语义。
- 接地、跳跃、RootMotion、HitStop 都属于高风险链路，任何相关改动后都要重跑对应场景。
- 如果发现现有行为不符合预期，先记录为问题，不在同一轮里顺手重构和修复。

## 核心不变量

### ActorMotor

- `ActorMotor` 是 KCC 回调入口，负责把 KCC tick 驱动到 runtime。
- `BeforeCharacterUpdate` 捕获本 tick 起点，并调用 `MotionRuntime.BeginMotorTick()`。
- `UpdateVelocity` 在 `deltaTime <= 0` 时走暂停分支；`MovementTimeScale == 0` 则通过统一的速度出口倍率把请求速度归零。
- `AfterCharacterUpdate` 用 KCC 最终位移计算 solved velocity，再交给 `VelocityReadout` 发布。
- `_motorFrameStartWorldPosition`、`_kccPaused`、`_requestedVelocity` 归 `ActorMotor`，因为它们依赖 KCC 本 tick 的桥接上下文。

### ActorMotionRuntime

- runtime 是纯 C# 状态根，管理 MotionChannels、GroundingRuntime、RootMotionBuffer、VelocityReadout。
- `MovementTimeScale`、`GravityScale`、`RootMotionApplyMode` 是运行时策略状态。
- `ActorMotor` 是唯一公开运动入口，并持有序列化配置。
- locomotion intent 和 facing 分别由 `LocomotionRuntime`、`FacingRuntime` 管理；项目中已不存在 `ActorMovement` facade。

### MotionChannels

- 水平 impulse 是平面外部动量，可以叠加。
- 垂直 impulse 不是简单叠加通道：上冲取最大值，下冲覆盖，用于表达 jump / launch / slam 这类互斥意图。
- velocity owner 是强控制通道，Action/Timeline 持有 owner 时覆盖对应轴。
- gravity accumulator 是垂直内部演化状态，不属于外部 impulse。
- 接地时允许 KCC 做斜坡/地面贴合；本地只负责把请求速度投影到稳定地面切线，并归零垂直请求速度。

### GroundingRuntime

- KCC 的 `IsStableOnGround` 是事实来源。
- forced unground 是主动离地请求，用于让跳跃当帧摆脱 KCC ground snapping。
- forced unground 已经主动发出 `OnLeftGround` 时，下一次 KCC stable -> unstable 只同步状态，不重复发事件。
- `JustLanded` 和 `JustLeftGround` 是一帧过渡态，下一次 grounding update 推进到稳定态。

### RootMotionBuffer

- Managed 模式下，pending root position 转成 KCC velocity，驱动物理位移。
- External 模式下，不用 root position 驱动 KCC velocity，也不叠加 root rotation。
- Managed 模式下，root rotation 在 `ActorMotor.UpdateRotation` 中叠加。
- Animator delta 的跨 Update/KCC-tick 累积与 snapshot 状态属于 `RootMotionBuffer`。
- `BeginMotorTick` 使用 snapshot 模式取得并清空累积缓冲，避免旧 delta 泄漏到下一 tick。

### VelocityReadout

- `CurrentVelocity` 是 KCC 后处理后的 gameplay velocity，不是通道合成的原始请求速度。
- 稳定接地时 readout 的 Y 目标为 0。
- 垂直速度平滑用于动画/条件系统读数，不应该反向影响 KCC 物理解算。

## 历史验证结果（需要重跑）

验证时间：2026-05-05

以下结果只说明 2026-05-05 当时的版本曾通过，不代表 2026-08-02 当前代码已经重新验证：

| 场景 | 结果 | 备注 |
| --- | --- | --- |
| 普通跳 | 通过 | 起跳、离地、落地链路正常。 |
| 二段跳 | 本轮跳过 | 用户明确暂不测试。 |
| 斜坡移动 | 通过 | 斜坡移动与贴地表现正常。 |
| 落地 | 通过 | 落地、状态恢复、速度读数表现正常。 |
| 撞天花板 | 通过 | 撞顶后上冲截断并正常下落。 |
| RootMotion Managed | 通过 | RootMotion 驱动 KCC 位移正常。 |
| RootMotion External | 通过 | External 模式下物理位移表现正常。 |
| HitStop / MovementTimeScale = 0 | 通过 | 冻结与恢复表现正常，无残留错位。 |
| Action 切换与 Velocity Owner | 通过 | Action 切换后速度 owner 未出现泄漏。 |

### 1. 普通跳

操作：
- 地面站立，触发一次普通跳。

预期：
- `AddVerticalImpulse(positive)` 产生 pending force unground。
- `ActorMotor.UpdateVelocity` 当 tick 消费 force unground，并调用 `Motor.ForceUnground(...)`。
- `GroundingRuntime` 当帧进入 `JustLeftGround`，只触发一次 `OnLeftGround`。
- 起跳帧不被 KCC stable ground snap 拉回地面。
- `jumpCount` 增加，落地后归零。

记录：
- 当前结果：通过
- 备注：起跳、离地、落地链路正常。

### 2. 二段跳

操作：
- 起跳后在空中再触发一次跳跃。

预期：
- 第二次上冲仍然通过垂直 impulse 生效。
- `jumpCount` 不超过 `maxJumpCount`。
- 空中二段跳不依赖 KCC `IsStableOnGround`。
- 垂直上冲取最大/覆盖语义不会被旧的较小上冲叠坏。

记录：
- 当前结果：本轮跳过
- 备注：二段跳暂不纳入本轮验证。

### 3. 斜坡移动

操作：
- 在稳定可站立斜坡上移动、停止、转向。

预期：
- locomotion 速度在接地时投影到地面切线。
- 请求垂直速度在稳定接地时归零。
- KCC 仍负责 stable ground / slope snapping。
- `CurrentVelocity.y` 稳定接近 0，水平速度符合坡面移动结果。

记录：
- 当前结果：通过
- 备注：斜坡移动与贴地表现正常。

### 4. 落地

操作：
- 从空中自然下落到稳定地面。

预期：
- KCC 从 unstable -> stable 时触发一次 `OnLanded`。
- `GroundState` 进入 `JustLanded`，下一次 grounding update 进入 `Grounded`。
- `jumpCount` 重置为 0。
- `VelocityReadout` 的垂直速度平滑回 0，不影响 KCC 实际贴地。

记录：
- 当前结果：通过
- 备注：落地、状态恢复、速度读数表现正常。

### 5. 撞天花板

操作：
- 起跳或上冲时撞到头顶碰撞体。

预期：
- `ActorMotor.OnMovementHit` 判断 hit normal 指向下方后调用 `SignalCeilingHit()`。
- 下一次 `StepVerticalImpulse` 清掉正向垂直 impulse。
- gravity accumulator 继续正常演化，不阻止后续下落。
- 不触发落地事件。

记录：
- 当前结果：通过
- 备注：撞顶后上冲截断并正常下落。

### 6. RootMotion Managed

操作：
- 播放一个 root motion 位移动作，模式为 Managed。

预期：
- `RootMotionBuffer` 累积 animator delta。
- `ComposeKccVelocity` 使用 pending root position / `deltaTime` 生成 KCC velocity。
- 接地时 root motion velocity 仍投影到稳定地面切线。
- `BeginMotorTick` 消费本 tick 的 root-motion snapshot；下一 tick 不应重复消费同一 delta。

记录：
- 当前结果：通过
- 备注：RootMotion 驱动 KCC 位移正常。

### 7. RootMotion External

操作：
- 播放同一 root motion 动作，模式为 External。

预期：
- root position 不接管 KCC velocity。
- locomotion / impulse / velocity owner 正常参与合成。
- root rotation 不由 ActorMotor 应用，避免与外部旋转处理重复叠加。

记录：
- 当前结果：通过
- 备注：旧版本只验证了物理位移；当前“External 不应用 position/rotation”的完整语义需要重跑。

### 8. HitStop / MovementTimeScale = 0

操作：
- 触发攻击命中后的 HitStop，冻结攻击方或双方 movement time。

预期：
- `MovementTimeScale == 0` 时统一速度出口倍率为 0，KCC 请求速度为 0。
- RootMotion 位移换算仍使用真实 KCC `deltaTime`，结果再乘时间倍率，不发生除零。
- `ActorMotor.Update` 的 facing dt 使用 `MovementTimeScale`。
- 恢复 time scale 后，速度、ground state、root motion buffer 不出现明显残留错位。

记录：
- 当前结果：通过
- 备注：冻结与恢复表现正常，无残留错位。

### 9. Action 切换与 Velocity Owner

操作：
- 在一个持有 horizontal/vertical velocity owner 的 Action 中打断切换到另一个 Action。

预期：
- 新 Action 入口调用 `ClearVelocityOwners()`，旧 owner 不泄漏。
- `ApplyMotionHandoff()` 按配置继承 impulse/gravity。
- 新 Action 的 velocity owner 可以正常接管对应轴。

记录：
- 当前结果：通过
- 备注：Action 切换后速度 owner 未出现泄漏。

## 下一次验证顺序

1. 在 `Assets/Scenes/Test/KCC_Migration_Test.unity` 重跑普通跳、二段跳、斜坡、落地和撞天花板。
2. 在包含相应 ActionAsset 的战斗场景重跑 RootMotion Managed、RootMotion External、HitStop 和 Action 切换。
3. 将上表结果改为当前日期，并记录使用的场景、角色和 ActionAsset。
4. 若结果失败，先单独记录问题；不要在验证过程中顺手重构运动数学。

## 仍需确认的设计问题

- 是否需要公开 `ClearVerticalImpulse()`；当前没有明确 gameplay 调用需求。
- `GroundingRuntime` 的 `_leftGroundEventAlreadyEmittedByForceUnground` 是否需要改成显式 transition reason。当前实现行为明确，重命名优先级较低。
