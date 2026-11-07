# 弹反：代码合同与配置

## 当前范围

第一版实现主动弹反的逻辑接入：攻击可弹反标记、有效窗口、正面方向检查、免伤、攻击者被弹反事件，以及成功通知。现已补齐结果驱动的反馈路由：防守者可在 HitFeedbackReceiver 上配置独立的 Parry Profile，复用现有四种 Effect 执行器。

本轮没有创建或修改玩家／敌人的 Action、输入绑定、Tag 数据、动画、场景或 Prefab；这些配置完成后才能在游戏中体验。没有实现处决、架势条、多次弹反计数或专用音画反馈资源。

## 命中链路

```text
HitBoxItem 开启范围
→ ActorHitBoxRuntime 查询、按 IDamageable 去重
→ PendingHit / CombatHitBuffer 收集并有序结算
→ 目标 ActorCombater.TakeDamage
→ 返回 HitResolveResult
→ ImpactSystem.HandleResolvedHit 按结果选择普通／弹反反馈
```

`CombatHitIntent.cs` 依旧只负责队列与结算调用，将结果交给 ImpactSystem，不在 Buffer 中选择具体特效。旧 AttackHandler Trigger 路径也使用同一结果入口。弹反是目标对一次已检测命中的裁决，不是让 HitBox 查询失败。

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

### 5. 配置弹反反馈

1. 通过 `Create → CombatSample → Feedback → Hit Feedback` 创建一个 HitFeedbackProfile，例如 `Feedback_Parry`。
2. 在 `effects` 中添加启用的 `Hit VFX (Unified)`、Hit Sound、Speed Effect 或 Screen Shake；填写实际 Prefab／音频等参数。不会自动附加顿帧、震屏。
3. 在**防守者**的 HitFeedbackReceiver 上，将它赋给 `Parry Profile`。原 `Profile` 仍用于普通受击，不需要改引用。
4. 场景需要已有 ImpactSystem。震屏需要配置 CinemachineImpulseSource；Speed Effect 需要正在运行的 CombatSimulationDriver 固定时钟。

结果路由：

| 命中结果 | 播放来源 |
|---|---|
| 普通／霸体命中 | 原攻击 Effects + 防守者普通 Profile |
| 弹反成功 | **仅防守者 Parry Profile** |
| 无敌／已死亡 | 无 |

弹反 Profile 为空、列表为空或全部禁用时不播放，也不会回退到普通 Profile 或攻击 Effects。旧 Trigger 路径没有 Clip Effects 列表，但同样选择防守者对应 Profile。

弹反 VFX 固定使用**原始世界空间 HitPoint**；即使共享 Profile 的 `anchorMode` 配成 HitColliderCenter，弹反也不使用碰撞体中心。不新增盾牌／武器挂点、不改写原命中数据，生成后不挂在角色下跟随。普通命中的既有 anchorMode 行为不变。

Effect 上下文保留来袭攻击的身份：`Attacker` 是敌人，`Target` 是防守者。SpeedEffect 的 `affectBothParties = false` 影响来袭攻击者；true 则同时影响双方（沿用现有目标侧延迟规则）。

`ActorCombater.ParrySucceeded` 仍保留为玩法通知，参数是防守者和原始 AttackHitData。与死亡通知共用逐订阅者异常隔离：一个观察者报错会记录异常，但不阻断后续观察者或 Parried 结果返回。**标准弹反反馈已经由结果入口自动播放，不要再订阅该通知重复播放同一组 Effects。**

## 时序与边界

- 免伤在当前 Hit 阶段立即确定；`SendEvent` 仅登记请求，硬直动作在后续 `DecideAction` 中仲裁，不在命中结算中直接切动作。
- 没有配置对应事件 Action 时仍可免伤和发成功通知，但敌人不会凭空播放硬直动作；必须完成上述配置。
- 不会清除同 Tick 已经提交的其他 PendingHit，也不会使整个攻击实例立即失效。死亡目标仍跳过，攻击者随后死亡不自动撤销其已提交命中。
- 同一窗口可以弹反多个符合条件的来袭攻击，不在成功时手工移除窗口 Tag。
- Action HitBox 保持“一次激活对一个 IDamageable 最多尝试一次”，包括弹反和无敌。
- 旧 `AttackHandler` Trigger 路径也传递可弹反属性，并记录已弹反的 Collider，防止窗口结束后回摆重入再伤害。普通命中 Start／Over 回调不对弹反触发；其原来的无敌接触重试和按 Collider 去重行为保持不变。反馈统一调用 HandleResolvedHit，不通过 Start／Over 或 ParrySucceeded 额外触发。

## 验证

以下为反馈接入时的历史验证；主线整理后的最新组合回归见 [主线迁移记录](Main_Migration.md)。纯弹反规则 17 项、反馈路由 12 项、命中队列 7 项及速度效果 33 项仍通过；实际弹反窗口／事件 Action 与原生音画组合不在这些纯合同的覆盖范围内。

反馈接入时已完成：

- 使用主工程 Unity 编译参数和依赖编译当前运行时、编辑器及测试源码，无错误。过程中遇到的无关 Action 编辑器／旧测试接口阻塞已由工作区其他改动解除，本轮没有修改那些文件。
- 在 `.git/parry-feedback-validation/project` 隔离工程中，复制当前运行时源码、KCC Core、所需插件依赖和四组合同测试，使用相同 Unity 2022.3.62f3 的真实 EditMode Test Runner 执行：**69 个通过，0 失败**。
- 分组：`ImpactFeedbackRoutingTests` 12 个、`CombatParryContractTests` 17 个、`CombatHitBufferTests` 7 个、`ActionSpeedEffectTests` 33 个。结果保存在 `.git/parry-feedback-validation/source-editmode-results.xml`。
- 反馈合同覆盖按结果选择防守者 Profile、忽略无敌／死亡、无配置或禁用时不回退、弹反不认普通攻击 Effects、普通反馈兼容、原攻击双方／伤害／命中点保留，以及父级 Receiver 查找。
- `git diff --check` 通过。

上述 Test Runner 结果是无场景、无仓库资源依赖的隔离合同验证，不代表主工程完整套件或实际战斗画面已验收。以下手工检查仍待执行：

1. `[6, 16)` 窗口首帧和结束边界；动作被取消后不残留窗口。
2. 正面成功时不扣血、不发普通受击、不播放普通反馈；背面和不可弹反攻击正常扣血。
3. 同时具有无敌和弹反窗口时只按无敌处理；霸体不阻止成功弹反。
4. 攻击者为空时不弹反；成功事件指向实际攻击者而非锁定目标，上下文参与者正确。
5. 被弹反 Event Action 在下一次可用动作决策中胜出，退出原攻击并关闭后续判定。
6. 同一个 Action HitBox 的多 Collider 和武器回摆不重复命中；如仍使用旧 Trigger 路径，另验收其回摆重入。
7. 同 Tick 多命中、多名敌人和多个 HitBox 的结果符合上述已提交命中规则。
8. 配置 Parry Profile 后，火花在原命中点生成且不跟随角色；实际音效、震屏与顿帧符合配置，不重复播放、不混入受伤音效。
