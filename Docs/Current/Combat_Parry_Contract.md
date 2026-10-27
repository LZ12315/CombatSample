# 弹反：代码合同与配置

## 当前范围

第一版实现主动弹反的逻辑接入：攻击可弹反标记、有效窗口、正面方向检查、免伤、攻击者被弹反事件，以及成功通知。

本轮没有创建或修改玩家／敌人的 Action、输入绑定、Tag 数据、动画、场景或 Prefab；这些配置完成后才能在游戏中体验。没有实现处决、架势条、多次弹反计数或专用音画反馈资源。

## 命中链路

```text
HitBoxItem 开启范围
→ ActorHitBoxRuntime 查询、按 IDamageable 去重
→ PendingHit / CombatHitBuffer 收集并有序结算
→ 目标 ActorCombater.TakeDamage
→ 返回 HitResolveResult
→ ImpactAllowed 为 true 时进入普通 ImpactSystem 反馈
```

`CombatHitIntent.cs` 无须修改：它依旧只负责队列与结算调用。弹反是目标对一次已检测命中的裁决，不是让 HitBox 查询失败。

`TakeDamage` 的顺序为：死亡 → 无敌 → 弹反 → 原有扣血、霸体受击与死亡处理。

弹反成功条件：

- 本次 `AttackHitData.CanBeParried` 为 true。
- 攻击者存在，且不是防守者自身。不要求攻击者仍存活，沿用已提交命中的结算规则。
- 防守者具有精确匹配的弹反窗口 Tag；与既有战斗状态一致，可从 Transient 或 Persistent 容器读取，制作时应使用 Transient。
- 从防守者指向攻击者的位置方向落在防守者正面圆锥内。使用世界 XZ 平面，忽略高度；同一水平位置等无有效方向的情况不弹反。
- 半角默认 60°（总角度 120°），限制在 0°～90°，含边界。

成功返回 `HitResolveOutcome.Parried`：不扣血，不发送普通受击事件，`ImpactAllowed = false`，因此不会播放原来攻击的砍肉、受伤等普通反馈。

现有攻击的 `canBeParried` 默认 false，旧 `AttackHitData` 构造调用仍可不传新参数。其他命中结果的伤害、受击、反馈和死亡布尔合同不变。

## Unity 配置步骤

### 1. 创建并加载 Tag

在项目现有 TagTree 数据中添加：

```text
State.Combat.ParryWindow
Event.Combat.Parried
```

`ActorCombater` 的 Parry 分区支持选择其他 Tag；未分配时按上述全名查找。回退查找不会自动创建 Tag，也不会自动创建事件 Action。

### 2. 配玩家主动弹反动作

- 创建非循环 Poll Action，配置弹反动画。
- 按当前输入合同，在 ActionList 的入场路线条件中绑定弹反按键；共通准入放 Action Requirements。
- 在有效区间添加 `TagItem`，Tag 选 `State.Combat.ParryWindow`，容器选 `Transient`。
- **不要放到 SelfTags**，否则整段前后摇都会被视为窗口。
- 可先用 `[6, 16)` 帧作为调试起点；时间轴 60 帧／秒，数值并非原版法环数据。
- 如需从其他动作取消进入弹反，在来源动作的 CancelRule 中开放相应窗口。
- TagItem 正常退出、被打断或 Abort 都会释放自身添加的 Tag，不需要额外协程或定时器。

### 3. 配敌人攻击

在要允许弹反的每一个 `HitBoxItem` 的 `AttackDataConfig` 中开启 `canBeParried`。抓取、爆炸等不允许弹反的攻击保持关闭。

### 4. 配敌人被弹反动作

- 创建非循环 Event Action。
- `EventTriggerTag` 选择 `Event.Combat.Parried`。
- 加入该敌人使用的 ActionList。
- 设置能够在候选仲裁中胜出的优先级，确认 Requirements 不会挡住它。
- 配置被弹开／硬直动画。Event 候选不经过来源动作的 CancelRule，但仍经过 Requirements、重入资格与优先级仲裁。

事件发送到**本次命中的攻击者**，不是玩家当前锁定目标。事件 ActionContext 的约定：

```text
Instigator = 防守者（弹反者）
Target     = 攻击者（被弹反者）
Point      = 原命中点
Direction  = 弹反者指向被弹反者
```

### 5. 后续接专用反馈

成功时防守者会触发：

```csharp
ActorCombater.ParrySucceeded
// Action<ActorCombater, AttackHitData>
```

第一个参数是弹反成功的防守者；第二个参数仍为**原始来袭攻击**，其中 `Attacker` 是敌人，不是玩家。可以据此接专用火花、金属音、顿帧等表现，不要把普通攻击反馈直接当弹反反馈。

## 时序与边界

- 免伤在当前 Hit 阶段立即确定；`SendEvent` 仅登记请求，硬直动作在后续 `DecideAction` 中仲裁，不在命中结算中直接切动作。
- 没有配置对应事件 Action 时仍可免伤和发成功通知，但敌人不会凭空播放硬直动作；必须完成上述配置。
- 不会清除同 Tick 已经提交的其他 PendingHit，也不会使整个攻击实例立即失效。死亡目标仍跳过，攻击者随后死亡不自动撤销其已提交命中。
- 同一窗口可以弹反多个符合条件的来袭攻击，不在成功时手工移除窗口 Tag。
- Action HitBox 保持“一次激活对一个 IDamageable 最多尝试一次”，包括弹反和无敌。
- 旧 `AttackHandler` Trigger 路径也传递可弹反属性，并记录已弹反的 Collider，防止窗口结束后回摆重入再伤害。普通命中 Start／Over 回调不对弹反触发；其原来的无敌接触重试和按 Collider 去重行为保持不变。

## 验证

已完成：

- 使用项目 Unity 编译参数和依赖编译当前运行时、编辑器及测试源码，无错误；运行中的 Unity Editor 也已自动刷新、成功编译并完成域重载。
- 独立 Mono 进程执行 24 个纯逻辑用例：17 个 `CombatParryContractTests`、7 个既有 `CombatHitBufferTests`，全部通过。
- 新测试覆盖攻击 opt-in、窗口准入、正面／背面、方向与高度、圆锥边界、退化输入、弹反结果，以及原命中结果布尔合同。
- `git diff --check` 通过。

这不是 Unity Test Runner 的执行结果，也不代表场景、Tag 配置或动作效果已经验收。Unity Test Runner 与以下手工检查仍待执行：

1. `[6, 16)` 窗口首帧和结束边界；动作被取消后不残留窗口。
2. 正面成功时不扣血、不发普通受击、不播放普通反馈；背面和不可弹反攻击正常扣血。
3. 同时具有无敌和弹反窗口时只按无敌处理；霸体不阻止成功弹反。
4. 攻击者为空时不弹反；成功事件指向实际攻击者而非锁定目标，上下文参与者正确。
5. 被弹反 Event Action 在下一次可用动作决策中胜出，退出原攻击并关闭后续判定。
6. 同一个 Action HitBox 的多 Collider 和武器回摆不重复命中；如仍使用旧 Trigger 路径，另验收其回摆重入。
7. 同 Tick 多命中、多名敌人和多个 HitBox 的结果符合上述已提交命中规则。
