# NodeCanvas：自身 Agent 与显式 Actor Target

## 当前接口

`Assets/Scripts/Graph/` 下的自定义 Action / Condition 使用 `ActionTask<Actor>` / `ConditionTask<Actor>`。自身通过 `agent` 获取，不再配置 `actor` 黑板参数。默认从 Graph Owner 所在 GameObject 解析 Actor，不会自动搜索父物体；跨对象执行可以使用 NodeCanvas 的 Override Agent。

下列节点的目标均为 `BBParameter<Actor> target`，位置计算在内部使用该 Actor 的 Transform：

- **Move Toward Target**（保留脚本类型 `MoveTowardCombatTargetTask`）
- **Strafe Around Target**（保留脚本类型 `StrafeAroundCombatTargetTask`）
- **Check Target Angle**
- **Check Target Distance**

这些节点不读取 `ActorCombater.CombatTarget`。Target 为空时失败，不进行隐式目标解析。现有移动模式、距离比较模式、水平距离选项、暂停和连续移动意图释放逻辑不变。

**Actor Has Tag** 默认启用 `Check Self`，检查 `agent`。关闭后显示 `target`，检查指定 Actor；目标为空时返回 false。标签容器和精确 / 模糊匹配选项不变。

## Get Combat Target

新 Action **Get Combat Target**：

1. 每次执行读取一次 `agent.combater.CombatTarget`，不发起搜索或重新锁定。
2. 从存储的 GameObject 同物体上获取 Actor，写入 `Save As` 指定的 Actor 黑板变量。
3. 获取成功立即返回 Success。
4. 没有 Combater、没有目标或目标没有 Actor 时，清空输出并返回 Failure；未指定输出时返回 Failure。

`Save As` 是快照，不是绑定到 Combater 的实时属性。Combater 改变目标不自动改变黑板，行为树再次执行获取节点才刷新输出。

`ActorCombater.CombatTarget` 的 GameObject 类型不变。旧 `HasCombatTargetTask` 和隐式解析工具 `NodeCanvasCombatTargetUtility` 已删除。

建议接线：

```text
Sequence
├─ Get Combat Target    Save As → Target（Actor）
├─ Check Target Distance Target ← Target
└─ Move Toward Target    Target ← Target
```

需要刷新目标时重新执行获取 Action；运行中的移动节点读取自己配置的 Target 参数，不会自行切换到 Combater 的新目标。

## 旧图迁移：尚需在 NodeCanvas 编辑器接线

本次保留存续节点的脚本类型和 GUID，但明确移除了旧 `actor` / `targetOverride` 参数，不保留兼容回退。旧 Transform 类型目标变量不能直接作为新的 Actor 参数使用。

扫描当前 `Assets/` 中的序列化资源，以下图仍有旧 `targetOverride`，本次未擅自修改其图结构：

| 资源 | 需要检查 |
| --- | --- |
| `Assets/Data/Graph/Enemy_Test_1.asset` | 新增 Actor 黑板目标和获取步骤，重绑距离条件与移动节点。 |
| `Assets/Data/Graph/Jaeger/Jaeger_Combat.asset` | 同上；核对嵌套图的黑板作用域。 |
| `Assets/Data/Graph/Jaeger/Jaeger_FSM.asset` | 距离条件位于 FSM 连线，需明确安排获取 Action 的执行 / 刷新位置，不能仅在初始化时取一次而长期沿用旧目标。 |
| `Assets/Data/Graph/GreatSword/GreatSword_BT_Combat.asset` | 当前工作区未提交的新图；配置 Actor 目标和获取步骤，不覆盖现有草稿布局与接线。 |

上述目标节点在重新配置前会因为 Target 为空而失败。迁移时应在参与判断的黑板作用域里创建 Actor 变量，并明确获取步骤的执行时机；不要用旧 Self 变量替代目标变量。

此外，`Assets/Prefabs/Actor/Characters/Lucy.prefab` 的绑定 FSM 仍序列化有 `PlayActionTask.actor`。脚本已不再读取该字段，重新保存会清理旧数据；需确认实际 FSM Owner 与自身 Actor 同物体。其余旧图中的自身参数亦同理。

本次资源扫描没有发现 `HasCombatTargetTask` 的序列化引用，不需要替换已有实例。

## 节点 Inspector 字段显隐

自定义节点只使用 NodeCanvas 的属性装饰，不新增 `OnTaskInspectorGUI`，也不拆分程序集。约定：

- `[Header]` 只在确有分组必要时使用，每个节点最多 1–2 个；不使用 `Settings` 这类无信息量标题。插件该属性的实际画法只是加粗的 “- 标题”，没有分隔线与缩进，标题过多会视觉粗糙。含 `[Header]` 的文件用 `HeaderAttribute = ParadoxNotion.Design.HeaderAttribute` 别名避开 Unity 同名属性。
- 只在特定模式生效的字段用 `[ShowIf]` 隐藏：`untilStarted`、`moveMode`、`source`、`runMode`、`checkMode`、`checkSelf`。
- 同一字段需要多个条件才能判断时，用不参与序列化的私有只读属性供 `ShowIf` 读取（例如 `timeoutApplies`、`stopDistanceApplies`），不为此增加公开 API。
- 工具提示使用 Unity `[Tooltip]`，NodeCanvas 的反射 Inspector 会显示它；`[MinValue]` / `[SliderField]` 只对原生 `float` / `int` 字段生效，`BBParameter<float>` 上不添加这两个属性。
- 未给目标、标签、黑板输出添加 `[RequiredField]`：这些字段为空时仍按运行时失败 / false 处理，避免节点初始化直接报错，保持此前约定的“不硬失败”。

## Play Action Via ASM 的完成规则

节点只有两个模式字段：`Until Started` 与仅在该模式显示的 `Timeout`。成功条件统一为**动作真正开始播放**，不等待动作播完。

- 不勾选 `Until Started`：提交一次请求，在下一次 ASM 仲裁后完成。抢到并开始则 Success，否则 Failure。
- 勾选：每次仲裁未抢到后重新提交，节点保持 Running，直到动作开始或超时。
- `Timeout = 0`：只等待下一次仲裁，不重试；负数与 NaN 按 0 处理。
- Actor / ASM / ActionAsset 缺失，或 ASM 未启用，均按 Failure 处理。旧执行轮次的回调仍按 execution id 丢弃。
- Stop / Pause 只撤销待决请求，不强停已开始的动作；Resume 重新提交但不重置截止时间。

旧 `RequestMode` / `ResultMode` 已删除。`Jaeger_Combat.asset` 中原本序列化的 `requestMode: 1` 已改为 `untilStarted: true`，保持该节点持续重试的原有意图；其他图未序列化该字段，行为不变。

## 验证与人工检查

- `NodeCanvasActorTargetTests`：8 个内存对象 EditMode 用例，覆盖默认 Agent 解析、显式目标的距离 / 角度 / 移动方向、空目标不回退，以及获取目标的黑板写入、快照和失败清空。
- `ActionStateManagerContextTests`：PlayActionTask 生命周期测试的构造已移除自身参数，并覆盖单次仲裁失败、Until Started 重试后成功、Timeout 0 获得一次仲裁机会。
- 使用 Unity 2022.3.62f3 自带编译器和现有引用，在系统临时目录重建 Runtime 与 Editor / Tests 程序集：均 0 error、0 warning；未修改 Unity 生成文件。
- 当前项目正在 Unity Editor 中打开，未尝试启动第二个实例执行 Test Runner，以上新增用例尚未在 Unity 内运行。
- 人工检查：完成上表接线；检查各节点 Inspector 的条件字段是否随模式正确显隐、Header / Tooltip 是否按预期显示；检查 `Actor Has Tag` 的 Check Self 开关、目标字段显隐与两种检查对象；验证运行中丢失目标时移动失败并释放连续意图，以及暂停 / 恢复后仍使用显式配置目标。
