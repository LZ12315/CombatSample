# CombatSample ActionSequence Stage E1：ASM 请求排队与固定 Tick 决策

> 状态：已落地；代码编译通过，等待 Unity Test Runner 全量验收
>
> 日期：2026-08-22
>
> 起点：`8e28a525 Implement Stage D5 simulation simplification`
>
> 范围：`ActionStateManager`、`ActorSimulationRuntime`、`CombatSimulationDriver`、ASM/Driver 测试

## 1. 与长期路线的关系

本文是 [`CombatSample_ActionSequence_Final_Architecture_v2_zh-CN.md`](CombatSample_ActionSequence_Final_Architecture_v2_zh-CN.md) 的 Stage E1 实施记录。主架构文档继续回答最终职责边界；本文只记录 E1 如何把 ASM 仲裁接入 D5 已简化的固定 Tick。

E1 不改变 ActionAsset 资产格式，不新增通用事件系统，不迁移 Locomotion，也不改 Sequence/HitBox 的 D5 职责。

## 2. 目标

E1 只解决一个问题：Action 决策必须和 Gameplay Frame 使用同一套 60 Hz 时钟。

完成后的 Driver 顺序为：

```text
Capture Actor Snapshot
    -> KCC Pre Interpolation
    -> Decide Actions
    -> Play Action Frames
    -> KCC / Actor Collision
    -> Physics.SyncTransforms
    -> Detect Hits
    -> Resolve Hits
    -> Finish Action Frames
    -> KCC Post Interpolation
```

`ActionStateManager` 不再在 `LateUpdate` 仲裁。`LateUpdate` 仍可被 Camera、UI、表现逻辑使用，但不再决定 Gameplay Action。

## 3. 请求语义

Poll 不排队。每次 `DecideAction` 都根据当前 Actor 状态、输入缓冲和 `LatestLocomotionIntent` 重新采样。

External 和 Event 使用 pending/deciding 双缓冲：

```text
RequestExternalAction / SendEvent
    -> 写入 pending

下一次 Driver Tick 的 DecideAction
    -> pending 交换为 deciding 快照
    -> 收集 Poll / External / Event 候选
    -> 按 PriorityLayer、PriorityValue、提交顺序选择唯一胜者
    -> BeginAction
    -> 完成 deciding External 回调

仲裁期间新提交的请求
    -> 继续写入 pending
    -> 只能参加下一 Tick
```

External 是一次性请求。它只参加下一次 `DecideAction`；如果没有胜出、Action 无效、Entry 不通过或当前 Action 不允许取消到它，回调 `false`，不会留到未来自动触发。

Event 也是一次性请求。它保留提交时的 `ActionContext`，继续使用 `CheckEntryForEvent`，不受当前 Action 的 CancelRule 限制，但仍参与统一优先级竞争。

## 4. 清理与异常

External 回调使用 exactly-once 保护。只有精确胜出的请求回调 `true`，其他请求回调 `false`。回调自身抛异常时只记录日志，不阻止其他请求完成，也不让 Driver fault。

ASM Disable、Actor Disable 或 Driver Abort 时：

- pending/deciding External 全部回调一次 `false`；
- Event 队列直接清空；
- 已清空请求不会在重新启用后继续执行。

`DecideAction` 核心仲裁异常向 Driver 传播。Driver 沿用 D5 的 fail-fast 策略：Abort 当前 actor 快照、清理 Action/HitBox/ASM 队列、配对 KCC interpolation，然后停止不可信模拟。

## 5. 保留规则

E1 保留以下既有行为：

- 当前 Action 先检查 `CheckExit`；
- External 不能绕过 CancelRule；
- 同 Action 是否重入继续由 `AllowReenterWhilePlaying` 决定；
- Event Action 继续通过 EventTag 映射与 `CheckEntryForEvent` 筛选；
- `ActionPlayer.BeginAction`、`RequestExternalAction`、`SendEvent` 和 `CurrentActionAsset` 的 public 签名不变；
- Legacy Timeline Action 从固定 Tick 边界开始，但播放载体仍由现有 `ActionPlayer.Update()` 推进。

## 6. 测试边界

E1 补充的自动化测试覆盖：

- `LateUpdate` 不再拥有 Action 仲裁；
- Poll 在 `DecideAction` 中采样固定 Tick 当前上下文；
- External 请求直到固定 Tick `DecideAction` 才开始 Action；
- 多个 External 请求只有精确胜者回调 `true`；
- Cancel 窗口外 External 只失败一次，不在未来自动触发；
- 回调中再次提交请求会延迟到下一 Tick；
- Disable 清空等待请求，并 exactly-once 回调 `false`；
- 回调异常不阻断其他回调；
- Driver 生产路径从 ASM 请求进入，并验证 `DecideAction` 发生在 `PlayActionFrame` 前。

## 7. 非目标

- 不实现 LocomotionController / LocomotionModeAsset；
- 不移除 ActorLogicInput 直接推 Motor 的兼容路径；
- 不新增 StateKind、Locomotion target 或 CancelRule Conditions；
- 不把 ASM 做成通用事件总线；
- 不修改 Scene、Graph、ActionAsset、AnimationConfig 或 ProjectSettings 内容资产。
