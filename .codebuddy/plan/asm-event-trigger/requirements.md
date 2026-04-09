# 需求文档：ASM 事件触发与 EventContext

## 引言

本功能为现有的 ActionStateManager（ASM）引入**事件触发路径**，参考 UE GAS 的设计哲学，使 ASM 成为轮询（Poll）、事件（Event）、外部注入（External）三条 Action 来源的统一仲裁者。同时引入 `ActionEventContext` 数据结构，为事件触发的 Action 提供执行时所需的上下文参数（方向、力度、攻击者等）。

### 背景

- **当前问题**：受击等被动响应 Action 依赖 Transient Tag 轮询，存在 1 帧延迟且语义不清晰
- **设计理念**：Tag 负责路由（选谁），EventContext 负责传参（给选中的 Action 执行时用）
- **核心原则**：主动发起的 Action（攻击、闪避等）直接从 Actor 身上读取信息，不需要 EventContext；被动响应的 Action（受击、击飞等）通过 EventContext 接收外部施加的参数

### 涉及文件

| 文件 | 路径 | 改动类型 |
|------|------|---------|
| ActionAsset.cs | Assets/Scripts/ActionSystem/ActionAsset.cs | 修改 |
| ActionEventContext.cs | Assets/Scripts/ActionSystem/ActionEventContext.cs | **新建** |
| ActionStateManager.cs | Assets/Scripts/Actor/ActionStateManager.cs | 修改 |
| ActionPlayer.cs | Assets/Scripts/Actor/ActionPlayer.cs | 修改 |
| ActionInstance.cs | Assets/Scripts/ActionSystem/ActionInstance.cs | 修改 |
| ActorCombater.cs | Assets/Scripts/Combat/ActorCombater.cs | 修改 |

---

## 需求

### 需求 1：ActionAsset 新增触发模式配置

**用户故事：** 作为一名策划，我希望能在 ActionAsset 上配置触发模式（轮询/事件），以便区分主动发起的动作和被动响应的动作。

#### 验收标准

1. WHEN ActionAsset 被创建 THEN 系统 SHALL 提供 `ActionTriggerMode` 枚举字段，包含 `Poll`（默认）和 `Event` 两个选项
2. WHEN ActionAsset 的 TriggerMode 设为 `Event` THEN 系统 SHALL 显示 `EventTriggerTag`（TagReference）字段，用于配置匹配的事件 Tag
3. WHEN ActionAsset 的 TriggerMode 设为 `Poll` THEN 系统 SHALL 保持现有行为完全不变，`EventTriggerTag` 字段不生效
4. IF 已有的 ActionAsset 未配置 TriggerMode THEN 系统 SHALL 默认为 `Poll`，确保向后兼容

### 需求 2：新建 ActionEventContext 数据结构

**用户故事：** 作为一名开发者，我希望有一个统一的数据结构来承载事件触发时的上下文参数，以便被选中的 Action 在执行时能获取击退方向、力度、攻击者等信息。

#### 验收标准

1. WHEN 系统需要传递事件上下文 THEN ActionEventContext SHALL 包含以下字段：`Instigator`（施加者 GameObject）、`Target`（受击者 GameObject）、`HitPoint`（命中点 Vector3）、`Direction`（方向 Vector3）、`Magnitude`（力度 float）
2. WHEN ActionEventContext 被创建 THEN 系统 SHALL 将其定义为 `struct`（值类型），避免 GC 开销
3. IF EventContext 的 Instigator 不为 null 或 Direction 的平方长度大于 0.001 THEN `IsValid` 属性 SHALL 返回 true

### 需求 3：ASM 新增事件触发路径

**用户故事：** 作为一名开发者，我希望 ASM 能通过 `SendEvent` 方法接收事件触发的 Action 候选，以便受击等被动响应能立即参与仲裁，无需等待下一帧轮询。

#### 验收标准

1. WHEN ASM 初始化 THEN 系统 SHALL 从 ActionAssetList 中构建事件映射表（`Dictionary<int, List<ActionAsset>>`），将 TriggerMode 为 Event 的 Action 按 EventTriggerTag 的 Id 分组
2. WHEN 外部调用 `SendEvent(Tag eventTag, ActionEventContext context)` THEN 系统 SHALL 通过事件映射表找到匹配的 Action 列表，对每个 Action 执行 `CheckEntry` 条件检查（但跳过 InputStateCondition），通过检查的加入事件候选列表
3. WHEN ASM 的 `CheckForTransition` 执行仲裁 THEN 系统 SHALL 将三条路径的候选（Poll 候选、Event 候选、External 候选）统一纳入优先级仲裁
4. WHEN 轮询路径收集候选 THEN 系统 SHALL 跳过 TriggerMode 为 `Event` 的 Action，避免事件 Action 被轮询误触发
5. WHEN ASM 的 Update 结束 THEN 系统 SHALL 清空事件候选列表和暂存的 EventContext
6. WHEN SendEvent 被调用 THEN 系统 SHALL 暂存传入的 `ActionEventContext`，供后续 ActionPlayer 使用

### 需求 4：ActionInstance 接收 EventContext

**用户故事：** 作为一名开发者，我希望 ActionInstance 能持有 EventContext，以便 Timeline 中的 Behaviour 在执行时能读取击退方向、力度等参数。

#### 验收标准

1. WHEN ActionInstance 的 `OnEnter` 被调用 THEN 系统 SHALL 支持接收可选的 `ActionEventContext` 参数
2. WHEN EventContext 被传入 THEN ActionInstance SHALL 将其存储为公开属性 `EventContext`，供 Timeline Behaviour 读取
3. IF OnEnter 未传入 EventContext THEN ActionInstance SHALL 使用 `default(ActionEventContext)`，不影响轮询触发的 Action

### 需求 5：ActionPlayer 传递 Context

**用户故事：** 作为一名开发者，我希望 ActionPlayer 的 `BeginAction` 方法能接收并传递 EventContext，以便完成从 ASM 到 ActionInstance 的参数传递链路。

#### 验收标准

1. WHEN `BeginAction` 被调用 THEN 系统 SHALL 支持接收可选的 `ActionEventContext` 参数
2. WHEN BeginAction 创建 ActionInstance 并调用 OnEnter THEN 系统 SHALL 将 EventContext 传递给 `ActionInstance.OnEnter`
3. WHEN ActionPlayer 处理 Loop 重播 THEN 系统 SHALL 保留原始 EventContext 并在重新 OnEnter 时传入
4. IF BeginAction 未传入 EventContext THEN 系统 SHALL 使用 `default(ActionEventContext)`，保持向后兼容

### 需求 6：ActorCombater 构造 Context 并 SendEvent

**用户故事：** 作为一名开发者，我希望 ActorCombater 在受击时能构造 EventContext 并通过 SendEvent 发送事件，以便替代当前直接写入 Transient Tag 的方式。

#### 验收标准

1. WHEN `TakeDamage` 被调用且 `AttackHitData.HitEventTag` 不为 null THEN ActorCombater SHALL 构造 `ActionEventContext`，包含：Instigator（攻击者 GameObject）、Target（自身 GameObject）、HitPoint（命中点）、Direction（从攻击者指向受击者的归一化方向）、Magnitude（伤害值或力度）
2. WHEN EventContext 构造完成 THEN ActorCombater SHALL 调用 `ASM.SendEvent(hitEventTag, context)` 替代当前的 `_actor.AddTag(hitEventTag, Transient)`
3. IF Actor 上未挂载 ActionStateManager THEN 系统 SHALL 安全跳过 SendEvent 调用，不产生异常

### 需求 7：玩家与敌人双侧统一

**用户故事：** 作为一名开发者，我希望玩家和敌人共享同一套 ASM 事件触发机制，以便受击流程完全一致，BT 侧不受影响。

#### 验收标准

1. WHEN 玩家受击 THEN 系统 SHALL 通过 `ActorCombater → SendEvent → ASM 仲裁 → ActionPlayer` 的路径处理
2. WHEN 敌人受击 THEN 系统 SHALL 通过完全相同的路径处理，与玩家一致
3. WHEN 敌人 BT 通过 `RegisterExternalCandidate` 注入 Action THEN 系统 SHALL 保持现有行为不变
4. IF 敌人的 ActionAssetList 中包含 Event 模式的 Action THEN 系统 SHALL 在初始化时正确构建事件映射表
