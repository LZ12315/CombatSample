# 实施计划

- [ ] 1. 新建 ActionEventContext 结构体
   - 在 `Assets/Scripts/ActionSystem/` 下新建 `ActionEventContext.cs`
   - 定义 `struct ActionEventContext`，包含字段：`Instigator`（GameObject）、`Target`（GameObject）、`HitPoint`（Vector3）、`Direction`（Vector3）、`Magnitude`（float）
   - 实现 `IsValid` 只读属性：当 `Instigator != null` 或 `Direction.sqrMagnitude > 0.001f` 时返回 true
   - _需求：2.1、2.2、2.3_

- [ ] 2. ActionAsset 新增触发模式字段
   - 在 `Assets/Scripts/ActionSystem/ActionAsset.cs` 中新增 `ActionTriggerMode` 枚举（`Poll = 0`、`Event = 1`），定义在文件顶部或单独区域
   - 在 ActionAsset 类中新增序列化字段 `_triggerMode`（默认 `Poll`）和 `_eventTriggerTag`（TagReference 类型）
   - 新增对应的公开属性 `TriggerMode` 和 `EventTriggerTag`
   - 确保未配置的已有 ActionAsset 默认为 `Poll`，向后兼容
   - _需求：1.1、1.2、1.3、1.4_

- [ ] 3. ActionAsset 新增事件路径条件检查方法
   - 在 `ActionAsset.cs` 中新增 `CheckEntryForEvent(Actor actor)` 方法
   - 该方法遍历 `_entryConditions`，跳过类型为 `InputStateCondition` 的条件（使用 `is` 类型检查），其余条件正常检查
   - 保持与 `CheckEntry` 相同的 `overrideAll` 和 `invertResult` 逻辑
   - _需求：3.2_

- [ ] 4. ActionInstance 支持接收 EventContext
   - 修改 `Assets/Scripts/ActionSystem/ActionInstance.cs`
   - 新增公开属性 `ActionEventContext EventContext { get; private set; }`
   - 修改 `OnEnter` 方法签名为 `OnEnter(Actor actor, ActionEventContext context = default)`，将 context 存储到 EventContext 属性
   - 在 `OnExit` 中重置 `EventContext = default`
   - _需求：4.1、4.2、4.3_

- [ ] 5. ActionPlayer 传递 EventContext
   - 修改 `Assets/Scripts/Actor/ActionPlayer.cs`
   - 修改 `BeginAction` 方法签名为 `BeginAction(ActionAsset actionAsset, ActionEventContext context = default)`
   - 新增私有字段 `_pendingContext` 暂存 context，在 `TryBindAndPlayTimeline` 成功后传递给 `CurrentAction.OnEnter(_actor, _pendingContext)`
   - 修改 `HandleDirectorStopped` 中 Loop 重播逻辑：保留原始 context，在重新 `OnEnter` 时传入
   - 新增私有字段 `_currentContext` 保存当前播放 Action 的 context，供 Loop 重播使用
   - `StopAction` 中重置 `_currentContext = default`
   - _需求：5.1、5.2、5.3、5.4_

- [ ] 6. ASM 构建事件映射表
   - 修改 `Assets/Scripts/Actor/ActionStateManager.cs`
   - 新增私有字段 `Dictionary<int, List<ActionAsset>> _eventActionMap`
   - 在 `Awake` 或 `Start` 中遍历 `_actionList.GetAllAvailableActions()`，将 `TriggerMode == Event` 的 Action 按 `EventTriggerTag.GetTag().Id` 分组存入映射表
   - _需求：3.1_

- [ ] 7. ASM 实现 SendEvent 方法与事件候选收集
   - 在 `ActionStateManager.cs` 中新增字段：`List<ActionAsset> _eventCandidatesThisFrame`（事件候选列表）和 `ActionEventContext _pendingEventContext`（暂存 context）
   - 实现 `public void SendEvent(Tag eventTag, ActionEventContext context)` 方法：通过 `_eventActionMap` 查找匹配的 Action 列表，对每个 Action 调用 `CheckEntryForEvent(_actor)`，通过的加入 `_eventCandidatesThisFrame`，同时暂存 context 到 `_pendingEventContext`
   - _需求：3.2、3.6_

- [ ] 8. ASM 修改 CheckForTransition 统一仲裁三条路径
   - 修改 `CheckForTransition` 方法中的轮询路径：遍历 `allActions` 时跳过 `TriggerMode == Event` 的 Action
   - 在轮询候选和分支候选收集之后、外部候选之前，将 `_eventCandidatesThisFrame` 中的候选加入 `_validCandidatesCache`
   - 修改 `Update` 方法：在末尾同时清空 `_eventCandidatesThisFrame` 和重置 `_pendingEventContext = default`
   - 修改 `PlayNewAction` 方法：将 `_pendingEventContext` 传递给 `_actionPlayer.BeginAction(actionToPlay, _pendingEventContext)`
   - _需求：3.3、3.4、3.5_

- [ ] 9. ActorCombater 改用 SendEvent 替代 Transient Tag
   - 修改 `Assets/Scripts/Combat/ActorCombater.cs`
   - 新增 `[SerializeField] private ActionStateManager _asm;` 字段，在 `Awake` 中尝试 `GetComponent<ActionStateManager>()`
   - 重写 `ApplyHitEventTag` 方法：构造 `ActionEventContext`（Direction 为从 Attacker 指向自身的归一化方向，Magnitude 为 attackData.Damage，HitPoint 为 attackData.HitPoint，Instigator 为 attackData.Attacker.gameObject，Target 为自身 gameObject）
   - 调用 `_asm.SendEvent(attackData.HitEventTag, context)` 替代 `_actor.AddTag(attackData.HitEventTag, Transient)`
   - 若 `_asm == null` 则安全跳过，不产生异常
   - _需求：6.1、6.2、6.3_

- [ ] 10. 配置受击 ActionAsset 并验证完整流程
   - 将现有受击 ActionAsset 的 `TriggerMode` 改为 `Event`，配置对应的 `EventTriggerTag`（如 `Event.Hit.Normal`）
   - 移除受击 ActionAsset 上的 `InputStateCondition`（如果有的话），保留 Tag 条件等其他条件
   - 验证：攻击命中 → ActorCombater.TakeDamage → SendEvent → ASM 仲裁选中受击 Action → ActionPlayer 播放 → ActionInstance 持有 EventContext
   - 验证轮询 Action（攻击、闪避等）行为不受影响
   - 验证敌人受击走相同路径
   - _需求：7.1、7.2、7.3、7.4_
