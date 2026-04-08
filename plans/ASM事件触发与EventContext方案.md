# ASM 事件触发与 EventContext 方案

## 一、核心理念来源：UE GAS 的设计哲学

参考 UE 的 GameplayAbilitySystem，提炼出以下关键设计思想并适配到本项目：

| UE GAS 概念 | 本项目对应 | 状态 |
|---|---|---|
| AbilityTrigger（事件触发） | `ActionTriggerMode` + `EventTriggerTag` | **待新增** |
| Input 绑定（独立激活路径） | `ActionCondition` 里的 `InputCheck`（轮询） | **已有，保持不变** |
| GameplayEventData | `ActionEventContext` | **待新增** |
| ActivateAbility() / ActivateAbilityFromEvent() | `OnEnter(actor)` / `OnEnter(actor, context)` | **待修改** |

---

## 二、架构决策（已确认）

### 1. Input 保持"条件配置 + 轮询"，不改为事件驱动

**理由**：
- 本系统是 **1个按键 → N个动作**（取决于上下文），GAS 是 1个按键 → 1个 Ability
- Branches 连招机制天然适配轮询
- 输入缓冲在轮询模型下更自然
- 敌人 AI 与玩家共享相同的仲裁逻辑

### 2. 新增事件触发路径，与轮询并行

**理由**：
- 受击等被动响应 Action 用轮询有 1 帧延迟且语义不清晰
- 事件驱动的 Action 应该由 `SendEvent` 直接注入候选，而非依赖 Tag 轮询

### 3. ASM 成为三条路径的唯一仲裁者

```
ASM 的三条 Action 来源
├── 1. Poll（轮询）    → ActionList + Branches，每帧 CheckEntry
├── 2. Event（事件）   → SendEvent() 注入，跳过 InputCheck
└── 3. External（外部）→ RegisterExternalCandidate()，如 BT 注入
        ↓
    统一进入 SelectHighestPriorityAction() 仲裁
        ↓
    唯一出口：PlayNewAction()
```

---

## 三、Tag 与 EventContext 的职责分离

- **Tag 负责路由**：决定触发哪个 Action（如 `Event.Hit.Knockback` → 击退 Action）
- **EventContext 负责传参**：给选中的 Action 执行时使用（方向、力度、攻击者等）
- 两者职责完全分离

### 两种 Action 的信息获取方式

```
┌──────────────────────────────────────────────────────────┐
│  事件触发 Action（受击、击飞、击退、弹反...）              │
│  信息来源：外部施加者                                     │
│  获取方式：EventContext（由 ActorCombater 构造并传入）     │
│                                                          │
│  context.Direction  → 击退/击飞方向                       │
│  context.Magnitude  → 力度                                │
│  context.Instigator → 攻击者                              │
│  context.HitPoint   → 命中点                              │
├──────────────────────────────────────────────────────────┤
│  轮询触发 Action（攻击、翻滚、跳跃、冲刺...）             │
│  信息来源：自身状态                                       │
│  获取方式：直接从 Actor 及其组件上读取                     │
│                                                          │
│  actor.LogicInput.MoveInput  → 移动/翻滚方向              │
│  actor.LockedTarget          → 攻击目标                   │
│  actor.transform.forward     → 当前朝向                   │
└──────────────────────────────────────────────────────────┘
```

**核心原则**：主动发起的 Action 不需要别人告诉它该做什么，它自己知道；被动响应的 Action 才需要 context，因为信息来自外部。

---

## 四、具体改动清单

### 改动 1：ActionAsset 新增触发模式配置

文件：`Assets/Scripts/ActionSystem/ActionAsset.cs`

```csharp
// 新增枚举
public enum ActionTriggerMode
{
    Poll,   // 轮询触发（攻击、翻滚、跳跃...）
    Event,  // 事件触发（受击、击飞、弹反...）
}

// ActionAsset 新增字段
[Header("Trigger")]
[SerializeField] private ActionTriggerMode _triggerMode = ActionTriggerMode.Poll;
[SerializeField, Tooltip("仅 Event 模式生效，匹配 SendEvent 传入的 Tag")]
private TagReference _eventTriggerTag;

public ActionTriggerMode TriggerMode => _triggerMode;
public TagReference EventTriggerTag => _eventTriggerTag;
```

- **Poll 模式**：行为与现在完全一致，每帧轮询 `CheckEntry`
- **Event 模式**：不参与每帧轮询，只在 `SendEvent` 匹配到 Tag 时才成为候选

### 改动 2：新建 ActionEventContext 数据结构

新建文件：`Assets/Scripts/ActionSystem/ActionEventContext.cs`

```csharp
public struct ActionEventContext
{
    public GameObject Instigator;  // 施加者
    public GameObject Target;      // 受击者
    public Vector3 HitPoint;       // 命中点
    public Vector3 Direction;      // 方向（击退方向等）
    public float Magnitude;        // 力度
    
    public bool IsValid => Instigator != null || Direction.sqrMagnitude > 0.001f;
}
```

### 改动 3：ASM 新增事件触发路径

文件：`Assets/Scripts/Actor/ActionStateManager.cs`

```csharp
// 新增：事件映射表（初始化时构建）
private Dictionary<int, List<ActionAsset>> _eventTagToActions;

// 新增：事件候选列表（SendEvent 时填充，Update 末尾清空）
private readonly List<ActionAsset> _eventCandidatesThisFrame = new List<ActionAsset>(4);

// 新增：暂存的 EventContext
private ActionEventContext _pendingEventContext;

// 初始化时构建映射表
private void BuildEventMap()
{
    _eventTagToActions = new Dictionary<int, List<ActionAsset>>();
    foreach (var action in _actionList.GetAllAvailableActions())
    {
        if (action.TriggerMode != ActionTriggerMode.Event) continue;
        var tag = action.EventTriggerTag?.GetTag();
        if (tag == null) continue;
        
        if (!_eventTagToActions.TryGetValue(tag.Id, out var list))
        {
            list = new List<ActionAsset>();
            _eventTagToActions[tag.Id] = list;
        }
        list.Add(action);
    }
}

// 外部调用入口
public void SendEvent(Tag eventTag, ActionEventContext context = default)
{
    if (eventTag == null) return;
    if (!_eventTagToActions.TryGetValue(eventTag.Id, out var actions)) return;
    
    _pendingEventContext = context;
    for (int i = 0; i < actions.Count; i++)
    {
        var action = actions[i];
        if (action.CheckEntry(_actor))  // 仍然走条件检查（但跳过 InputCheck）
            _eventCandidatesThisFrame.Add(action);
    }
}
```

`CheckForTransition` 中将事件候选也纳入仲裁：

```csharp
private ActionAsset CheckForTransition()
{
    _validCandidatesCache.Clear();

    // 1. Poll 候选（只收集 TriggerMode == Poll 的）
    // ...现有逻辑，增加 TriggerMode 过滤...

    // 2. Event 候选
    for (int i = 0; i < _eventCandidatesThisFrame.Count; i++)
        TryAddCandidate(_eventCandidatesThisFrame[i]);

    // 3. External 候选
    for (int i = 0; i < _externalCandidatesThisFrame.Count; i++)
        TryAddCandidate(_externalCandidatesThisFrame[i]);

    // 统一仲裁
    if (_validCandidatesCache.Count > 0)
        return SelectHighestPriorityAction(_validCandidatesCache);
    return null;
}
```

Update 末尾清理：

```csharp
// Update 末尾
_externalCandidatesThisFrame.Clear();
_eventCandidatesThisFrame.Clear();
```

### 改动 4：ActionInstance 接收 EventContext

文件：`Assets/Scripts/ActionSystem/ActionInstance.cs`

```csharp
public ActionEventContext EventContext { get; private set; }

public void OnEnter(Actor actor, ActionEventContext context = default)
{
    _actor = actor;
    EventContext = context;
    // ...现有 Tag 逻辑不变...
}
```

- **事件触发的 Action**：context 由 `ActorCombater` 构造，沿 `SendEvent → ASM → ActionPlayer → ActionInstance.OnEnter` 传入
- **轮询触发的 Action**：context 为 `default`，Action 执行时直接从 `Actor` 身上读取所需信息

### 改动 5：ActionPlayer 传递 Context

文件：`Assets/Scripts/Actor/ActionPlayer.cs`

```csharp
public void BeginAction(ActionAsset actionAsset, ActionEventContext context = default)
{
    StopAction();
    if (!TryBindAndPlayTimeline(actionAsset))
        return;
    CurrentAction?.OnEnter(_actor, context);  // 传递 context
}
```

### 改动 6：ActorCombater 构造 Context 并 SendEvent

文件：`Assets/Scripts/Combat/ActorCombater.cs`

```csharp
private void ApplyHitEventTag(AttackHitData attackData)
{
    if (_actor == null || attackData.HitEventTag == null) return;

    // 构造 EventContext
    var context = new ActionEventContext
    {
        Instigator = attackData.Attacker.gameObject,
        Target = gameObject,
        HitPoint = attackData.HitPoint,
        Direction = (transform.position - attackData.Attacker.transform.position).normalized,
        Magnitude = attackData.Damage  // 或专门的力度字段
    };

    // 不再写入 Transient Tag，改为直接发送事件
    _actor.GetComponent<ActionStateManager>().SendEvent(attackData.HitEventTag, context);
}
```

---

## 五、玩家 / 敌人双侧统一

玩家和敌人共享 ASM，三条路径的使用方式为：

| 路径 | 玩家侧 | 敌人侧 |
|------|--------|--------|
| **Poll** | LogicInput 驱动 InputCheck | 敌人无 InputCheck，靠 TagCheck 等 |
| **Event** | `ActorCombater.SendEvent` | `ActorCombater.SendEvent`（完全一致） |
| **External** | 一般不用 | BT 的 `BT_PlayAction` 调用 `RegisterExternalCandidate` |

受击流程玩家和敌人**完全一致**，BT 侧**不受任何影响**。

---

## 六、改动影响范围

| 文件 | 改动类型 | 改动量 |
|------|---------|--------|
| `ActionAsset.cs` | 新增字段 | 小 |
| `ActionEventContext.cs` | **新建文件** | 小 |
| `ActionStateManager.cs` | 新增事件映射 + SendEvent | 中 |
| `ActionPlayer.cs` | BeginAction 加参数 | 极小 |
| `ActionInstance.cs` | OnEnter 加参数 | 极小 |
| `ActorCombater.cs` | 构造 context + 调 SendEvent | 小 |

总体改动量不大，且**所有现有的轮询 Action 不需要任何修改**，完全向后兼容。

---

## 七、信息流示例：击退效果

```
Lucy 重击命中敌人
    │
    ▼
AttackHandler 组装 AttackHitData
    hitEventTag: "Event.Hit.Knockback"
    │
    ▼
ActorCombater.TakeDamage()
    → 构造 ActionEventContext { Direction=击退方向, Magnitude=力度, ... }
    → ASM.SendEvent("Event.Hit.Knockback", context)
    │
    ▼
ASM.SendEvent()
    → 通过 _eventTagToActions 映射找到 Hit_Knockback Action
    → CheckEntry 通过 → 加入 _eventCandidatesThisFrame
    │
    ▼
ASM.Update() → CheckForTransition()
    → 统一仲裁 → Hit_Knockback 胜出
    │
    ▼
ActionPlayer.BeginAction(Hit_Knockback, context)
    → ActionInstance.OnEnter(actor, context)
    │
    ▼
Timeline 中的 KnockbackBehavior
    → 读取 context.Direction → 击退方向
    → 读取 context.Magnitude → 击退力度
    → 驱动角色位移
```
