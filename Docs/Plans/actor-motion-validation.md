# Actor Motion 行为基线验证清单

本文档用于第二轮整理前固定行为基线。当前阶段不追求重命名或继续拆分代码，目标是确认 `ActorMotor -> ActorMotionRuntime -> MotionChannels/GroundingRuntime/RootMotionBuffer/VelocityReadout` 这条链路在关键场景下的预期行为。

后续清理 `MotionChannels`、`GroundingRuntime` 或 facade 时，应先对照本文档，避免把“结构变清楚”变成“手感偷偷变了”。

## 总原则

- 先验证行为，再整理命名和边界。
- 不为了水平/垂直 API 表面形式对称而改语义。
- 接地、跳跃、RootMotion、HitStop 都属于高风险链路，任何相关改动后都要重跑对应场景。
- 如果发现现有行为不符合预期，先记录为问题，不在同一轮里顺手重构和修复。

## 核心不变量

### ActorMotor

- `ActorMotor` 是 KCC 回调入口，负责把 KCC tick 驱动到 runtime。
- `BeforeCharacterUpdate` 捕获本 tick 起点，并调用 `MotionRuntime.BeginMotorTick()`。
- `UpdateVelocity` 在 `MovementTimeScale <= 0` 或 `deltaTime <= 0` 时走暂停分支，避免 RootMotion delta / `deltaTime` 除法出问题。
- `AfterCharacterUpdate` 用 KCC 最终位移计算 solved velocity，再交给 `VelocityReadout` 发布。
- `_motorFrameStartWorldPosition`、`_pausedThisTick`、`_requestedVelocity` 仍归 `ActorMotor`，因为它们依赖 KCC 本 tick 的桥接上下文。

### ActorMotionRuntime

- runtime 是纯 C# 状态根，管理 MotionChannels、GroundingRuntime、RootMotionBuffer、VelocityReadout。
- `MovementTimeScale`、`GravityScale`、`RootMotionApplyMode` 是运行时策略状态。
- `ActorMovement` 只保留 facade、序列化配置、locomotion intent、facing pipeline。
- `ActorMovement` 的早期策略 setter 调用需要被 buffer，并在 `BindMotor` 后 replay。

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
- External 模式下，不用 root position 驱动 KCC velocity。
- root rotation 仍在 `ActorMotor.UpdateRotation` 中叠加。
- Y dead-zone 的累计状态属于 RootMotionBuffer。
- motor tick 结束后清空 pending root position / rotation。

### VelocityReadout

- `CurrentVelocity` 是 KCC 后处理后的 gameplay velocity，不是通道合成的原始请求速度。
- 稳定接地时 readout 的 Y 目标为 0。
- 垂直速度平滑用于动画/条件系统读数，不应该反向影响 KCC 物理解算。

## 手动验证场景

## 本轮验证结果

验证时间：2026-05-05

结果汇总：

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
- 下一次 `StepVerticalImpulseDecay` 清掉正向垂直 impulse。
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
- motor tick 结束后清空 pending root position / rotation。

记录：
- 当前结果：通过
- 备注：RootMotion 驱动 KCC 位移正常。

### 7. RootMotion External

操作：
- 播放同一 root motion 动作，模式为 External。

预期：
- root position 不接管 KCC velocity。
- locomotion / impulse / velocity owner 正常参与合成。
- root rotation 的预期行为需要单独确认：如果当前设计仍叠加 rotation，应记录为明确行为。

记录：
- 当前结果：通过
- 备注：External 模式下物理位移表现正常。root rotation 是否应受 External 排除仍保留为后续设计讨论点。

### 8. HitStop / MovementTimeScale = 0

操作：
- 触发攻击命中后的 HitStop，冻结攻击方或双方 movement time。

预期：
- `ActorMotor.UpdateVelocity` 走暂停分支，KCC 请求速度为 0。
- 不发生 root motion delta / `deltaTime` 除法。
- `ActorMovement.Update` 的 facing dt 使用 buffered/runtime `MovementTimeScale`。
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

## 第二轮整理顺序

1. 跑完上述场景，补充“当前结果”。
2. 整理 `MotionChannels` 的命名、注释和 API 边界，尽量不改数学行为。
3. 重跑 MotionChannels 相关场景：普通跳、二段跳、斜坡、撞天花板、Action 切换。
4. 整理 `GroundingRuntime` 的 forced unground / suppress 命名，让事件语义更直白。
5. 重跑 grounding 相关场景：普通跳、落地、forced unground 后 KCC stable -> unstable。
6. 再讨论 `ActorMovement` facade 是否继续保留或逐步收窄。

## 待讨论问题

- `RootMotionApplyMode.External` 下是否应该仍然叠加 root rotation，还是 position/rotation 都外部处理。
- `MotionChannels.AddVerticalImpulse` 是否需要更明确的命名来表达 launch/slam 语义。
- 是否需要公开 `ClearVerticalImpulse()`。当前倾向是不为了对称新增，除非出现明确 gameplay 需求。
- `GroundingRuntime` 的 suppress 标记是否只改名，还是抽成显式 transition reason。
