# CombatSample ActionSequence Stage E2：玩家输入所有权与固定 Tick LocomotionIntent

> 状态：已落地，等待 Unity Test Runner 与 Jaeger 手动验收
>
> 日期：2026-08-22
>
> 起点：`8cb96205 Implement Stage E1 ASM fixed tick decision`
>
> 范围：`PlayerInputController`、`ActorMotor`、`CombatSimulationDriver`、输入 Conditions、`ActorLogicInput` 兼容壳

## 1. 与长期路线的关系

E2 只解决玩家输入所有权和 LocomotionIntent 的固定 Tick 消费。

它不实现 `LocomotionController`、`LocomotionModeAsset`、AI 固定 Tick 化、Camera 输入重构或内容资产迁移。E3 仍负责正式 Locomotion 域。

## 2. 最终职责

```text
PlayerInputController
    = 玩家真实输入、raw move/look、按钮状态、输入历史 owner

PlayerLocomotionIntentResolver
    = 把玩家 raw move + Camera/Lock 上下文解释成 LocomotionIntent

Enemy AI / BehaviorTree
    = 继续直接生产 LocomotionIntent

ActorMotor
    = LocomotionIntent 的唯一消费者，不知道来源是 Player 还是 AI
```

`ActorLogicInput` 不再是 runtime 权威，只保留脚本和序列化字段，避免 Scene/Prefab 产生 Missing Script 或大范围 diff。

## 3. Driver 顺序

E2 后固定 Tick 顺序为：

```text
Capture Actor Snapshot
-> KCC Interpolation Pre-Step
-> PlayerInputController.SubmitLocomotionIntentForFixedTick
-> Decide Actions
-> Play Action Frames
-> KCC / Actor Collision / Physics.SyncTransforms
-> Detect Hits
-> Resolve Hits
-> Finish Action Frames
-> KCC Interpolation Post-Step
```

Driver 只认识唯一 `PlayerInputController` 入口，不读取 rawMove、Camera、LockMode 或具体 intent 字段。

## 4. Intent 生命周期

`ActorMotor.SetLocomotionIntent` 写入 pending intent。

下一次 KCC Motor Tick：

- 有 pending：复制为 effective，本 Tick 产生 locomotion/facing，随后清空 pending；
- 没有 pending：effective 自动变为 Idle；
- 同一 Tick 多次提交：最后一次生效。

`ActorMotor.LocomotionIntent` 继续作为公开兼容 API，返回最近一次 Motor Tick 实际使用的 effective intent。

ASM 的 `StartContextMode.LocomotionIntent`、`LocomotionIntentCondition` 与 `ActionInstance` 起手朝向改读 pending intent，避免把旧 effective 值当作本 Tick 输入。

## 5. 输入历史

输入历史迁入 `PlayerInputController`，使用 `Time.unscaledTime` 记录和过期。

`InputSequenceCondition` 保持 Check 与 Claim 分离：

- `Check` 只读，不消费；
- `OnClaim` 才通过 PlayerInputController 原子消费；
- 条目不存在或已消费时，Claim 不做部分修改；
- Condition 必须确认 `controlledActor == actor`，Enemy 错配玩家输入条件时安全返回 false。

旧 Timeline input buffer cleanup 也改为清理当前 controlled actor 的 PlayerInputController history。

## 6. 非目标

- 不物理删除 `ActorLogicInput.cs` 或它的 `.meta`。
- 不批量修改 Scene、Prefab、Graph、ActionAsset、AnimationConfig 或 ProjectSettings。
- 不新建 Input Provider、通用 Command Bus、Intent Source Manager 或 Player/AI 公共输入模型。
- 不改变现有 Camera/Cinemachine look 路径。
- 不把 NodeCanvas AI 迁入 Driver；AI 仍通过现有 Task 直接提交 Motor intent。

## 7. 验收边界

自动化重点覆盖：

- Player resolver 的 world fallback、HardLock 目标相对移动和强度 clamp；
- Motor pending/effective 的 once-only 消费与无提交自动 Idle；
- InputSequence 的只读 Check、胜选 Claim 消费和重复 Claim 无副作用；
- PlayerInputController singleton 销毁清理；
- ASM context 从 pending intent 采样；
- Driver 生产路径中玩家提交早于 ASM 决策。

手动 Jaeger 回归重点：

- Idle/Run；
- 自由相机移动；
- HardLock 八向移动；
- 攻击起手方向；
- 连招与输入 Buffer；
- HitStop 期间输入；
- 取消后恢复移动；
- 敌人追击与攻击。
