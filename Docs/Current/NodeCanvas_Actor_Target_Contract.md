# NodeCanvas：自身 Agent 与显式 Actor Target

## 当前接口

`Assets/Scripts/Graph/` 下的自定义 Action / Condition 使用 `ActionTask<Actor>` / `ConditionTask<Actor>`。自身通过 `agent` 获取，不再配置 `actor` 黑板参数。默认从 Graph Owner 所在 GameObject 解析 Actor，不会自动搜索父物体；跨对象执行可以使用 NodeCanvas 的 Override Agent。

下列节点的目标均为 `BBParameter<Actor> target`，位置计算在内部使用该 Actor 的 Transform：

- **Move Toward Target**（保留脚本类型 `MoveTowardCombatTargetTask`）
- **Strafe Around Target**（保留脚本类型 `StrafeAroundCombatTargetTask`）
- **Check Target Angle**
- **Check Target Distance**

这些节点不读取 `ActorCombater.CombatTarget`。Target 为空时失败，不进行隐式目标解析。移动／意图写入任务要求自身 Actor 与 ActorLocomotion 均启用；运行期间失效返回 Failure，沿任务停止路径释放连续意图。朝向目标移动与绕行也拒绝 GameObject 已停用的目标，不把角色停用后拒收意图误报为 Running / Success。距离／角度条件仍只查询显式目标，不进行自动选敌。

Move Toward Target 的 Timeout 只适用于 Move Until Distance，Push Once 忽略从连续模式留下的隐藏超时配置。现有移动模式、距离比较模式、水平距离选项、暂停与连续意图释放语义不变。

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

## Enter Soft Lock

Action 菜单：**Custom / Combat / Enter Soft Lock**（`EnterSoftLockTask`）。

- 一次性调用自身 `agent.combater.TryEnterSoftLock()`，立即返回 Success / Failure；不模拟按键，不依赖玩家输入单例，也不等待目标出现。
- 未锁定时，优先使用 Combater 当前有效目标；否则沿用现有自动选敌。节点没有额外 Target 参数。
- 已软锁且目标有效时幂等 Success，不解锁、不更换目标，也不增加 LockRevision。软锁目标失效时同样检查／尝试自动选敌，无可用目标则 Failure。
- 已 HardLock 时 Failure，不降级；Actor / Combater 缺少或禁用时 Failure。
- 节点完成、停止或离开图分支不会解除锁定；后续保持、补选与丢失目标后的解锁仍由 Combater 负责。
- `TryEnterSoftLock` 现在表达“确保有效软锁”：已软锁也可成功；原按键 `ToggleSoftLock` 仍单独检查软锁并解除，按键行为不变。

在玩家自身的图中直接添加即可。若从其他 Actor 的图操作玩家，使用 NodeCanvas **Override Agent** 指向玩家 Actor；默认 Agent 不会自动替换为玩家。若后续节点需要黑板 Target，可在成功后执行 Get Combat Target 获取快照。

人工验收：无按键进入软锁并检查相机／移动响应；重复执行保持锁定；无目标返回失败；HardLock 不被降级；目标丢失按既有流程补选或解锁。

## With Actor Tag：子树的临时状态作用域

`Assets/Scripts/Graph/Decorator/WithActorTag.cs` 是通用 BT 装饰节点，Inspector 只配置一个 `Tag`。自身 Actor 从执行 Agent 同物体解析；固定使用 Transient 容器，不搜索父物体，不选择 LocomotionAsset，也不修改 `LocomotionIntent` 或 Strafe 的运动算法。

- 第一次执行子树前添加一次标签；Running 期间保留，不按 Tick 重复增加计数。
- 返回子树状态；成功、失败、Optional / Error 等非 Running 结果和抛出异常都释放标签。
- Reset、中断和图停止释放；图暂停释放，恢复后的首次执行重新持有。
- 清理使用实际获取时保存的 Actor / 容器 / Tag，不重新读取可能变化的配置。Actor 或其 Transient 容器在运行中被替换时终止本次作用域并重置子树。
- 未配置标签、标签未注册、Actor / 容器缺失或 Actor 禁用时返回 Failure，不执行子树；没有子节点时返回 Optional。
- 使用现有 TagContainer 引用计数，只移除本节点添加的那一次，不清空整个标签或容器。不要在作用域运行期间绕过成对移除而强行 Clear / RemoveCompletely 同一容器；现有计数接口不提供跨强制清空的所有权恢复。

标签表 `Assets/Resources/Tags/ActionTag.tags` 注册 `State.Movement.Strafe`。大剑的候选优先级仍为 0，通过现有条件互斥：

| 资源 | EntryConditions |
| --- | --- |
| GreatSword Normal | Grounded / JustLanded；Transient 中没有 `State.Movement.Strafe`（反转 Tag 条件）。 |
| GreatSword Strafe | Grounded / JustLanded；Transient 中具有该标签。 |
| GreatSword Air | Airborne / JustLeftGround，不依赖该标签。 |

Normal / Strafe 均使用现有 `LocomotionActorTagCondition` 的 Exact 匹配。该标签是资源选择的输入，不由 Locomotion SelfTags 反向产生。速度、加减速、混合时长和动画条目保留作者配置。标签变化后，资源在下一次有效的 Locomotion 选择中切换。

大剑绕行分支：

```text
Conditional：满足绕行条件
└─ Sequence
   ├─ With Actor Tag：State.Movement.Strafe
   │  └─ Strafe Around Target：3 秒
   └─ HitCount = 0
```

攻击分支保留 Wait For Completion。绕行任务的持续时间仍按原来的 Time.time 截止时间计算，暂停不延长；标签作用域本身不改变任务计时。

## 当前样例与旧图恢复边界

当前代表性图为训练场引用的 `Data/Graph/Giant/FSM_Giant.asset` 与 `BT_Giant_Combat.asset`，保留显式 Actor 目标获取、距离／角度判断、动作请求及绕行标签的用法。

资源收敛已将 Enemy_Test_1、Jaeger、GreatSword 草稿图与对应旧场景／角色入口共同裁剪；Lucy 的旧绑定 FSM 和带失效嵌套图的 David 预览角色也不再保留。没有把空目标／空 Action 的草稿自动补成新玩法。上文绕行结构及下文攻击计数说明是节点使用示例，不承诺当前训练场采用完全相同的计数和时长。

存续节点仍保留脚本类型与 GUID；旧 `actor` / `targetOverride` 参数已移除，不保留兼容回退。若从 HyperGraph 历史恢复旧样例，必须同时迁移动作引用和 Actor 黑板目标，在参与判断的作用域明确获取／刷新时机，并核对嵌套图黑板传递；不能只改字段名、使用旧 Transform／Self 变量，或将初始化时的一次快照当实时绑定。旧 `PlayActionTask.actor` 数据不再读取，FSM Owner 必须与自身 Actor 的解析规则一致。

## 节点 Inspector 字段显隐

自定义节点只使用 NodeCanvas 的属性装饰，不新增 `OnTaskInspectorGUI`，也不拆分程序集。约定：

- `[Header]` 只在确有分组必要时使用，每个节点最多 1–2 个；不使用 `Settings` 这类无信息量标题。插件该属性的实际画法只是加粗的 “- 标题”，没有分隔线与缩进，标题过多会视觉粗糙。含 `[Header]` 的文件用 `HeaderAttribute = ParadoxNotion.Design.HeaderAttribute` 别名避开 Unity 同名属性。
- 只在特定模式生效的字段用 `[ShowIf]` 隐藏：`untilStarted`、`moveMode`、`source`、`runMode`、`checkMode`、`checkSelf`。
- 同一字段需要多个条件才能判断时，用不参与序列化的私有只读属性供 `ShowIf` 读取（例如 `timeoutApplies`、`stopDistanceApplies`），不为此增加公开 API。
- 工具提示使用 Unity `[Tooltip]`，NodeCanvas 的反射 Inspector 会显示它；`[MinValue]` / `[SliderField]` 只对原生 `float` / `int` 字段生效，`BBParameter<float>` 上不添加这两个属性。
- 未给目标、标签、黑板输出添加 `[RequiredField]`：这些字段为空时仍按运行时失败 / false 处理，避免节点初始化直接报错，保持此前约定的“不硬失败”。
- `TagReference` 不能仅依赖 Unity 的 `[CustomPropertyDrawer]`：NodeCanvas 使用自己的反射 Inspector，且 TagId 为内部字段，默认只会显示一个空折叠项。`Assets/Scripts/Graph/Editor/NodeCanvasTagReferenceDrawer.cs` 通过 `ObjectDrawer<TagReference>` 适配，复用 TagTree 的 `TagReferencePropertyDrawer.DrawField` 标签选择器和编辑器按钮。选择回调捕获原字段 / Graph 上下文，在修改前记录 Undo，并同步图序列化与 dirty；仅查看和重复选择当前标签不改数据。未注册的非零 ID 显示为 `Missing Tag (ID: ...)`，与未选择的 None 区分。
- 该适配仅是 Editor 代码，不改变 TagReference 数据格式、运行时标签逻辑或大剑资源配置。人工检查 With Actor Tag、Actor Has Tag、Current Action Has Self Tag 的标签选择、切换节点后的正确写入、Undo / Redo、图重载，以及普通 Unity Inspector 的原标签选择器。

## Play Action Via ASM 的完成规则

启动等待与动作完成等待分别配置，默认仍为**动作真正开始播放即成功**：

- 不勾选 `Until Started`：只提交一次启动请求，下一次 ASM 仲裁未启动则 Failure。
- 勾选 `Until Started`：每次仲裁未抢到后重新提交，保持 Running，直到动作开始或超时。
- `Timeout` 仅在 `Until Started` 下显示，且只限制启动等待；动作启动后不再受此截止时间影响。
- `Timeout = 0`：只等待下一次仲裁，不重试；负数与 NaN 按 0 处理。
- 不勾选 `Wait For Completion`：启动即 Success，保持原有行为。
- 勾选 `Wait For Completion`：启动后保持 Running，只在**本次 ActionInstance 正常结束**后 Success；停止、异常中断、播放器失效或实例被替换则 Failure。同一 ActionAsset 的新实例不算本次动作；循环动作不会正常结束。
- Actor / ASM / ActionAsset 缺失，或 ASM 未启用，均按 Failure 处理。旧执行轮次的启动回调仍按 execution id 丢弃。
- Stop 只撤销待决请求、解除完成监听，不强停已开始的动作。
- Pause 时未启动的请求会被撤销，Resume 重新提交但不重置启动截止时间；已启动的动作不会重播，其完成 / 中断结果保留到 Resume 后处理。

`GreatSword_BT_Combat` 的攻击列表为顺序执行：启用 `Wait For Completion` 的攻击 → `HitCount += 1`。三次完整攻击后进入持续 3 秒的 Strafe，完成后执行 `HitCount = 0`，再开始下一轮。条件仍受图中的目标距离 / 角度约束；动作中断不增加计数。

旧 `RequestMode` / `ResultMode` 已删除。历史 Jaeger 草稿曾将 `requestMode: 1` 迁为 `untilStarted: true`，但该图现已随旧入口裁剪；恢复历史图时仍须迁移启动等待规则。

## 验证与人工检查

- `NodeCanvasActorTargetTests`：内存对象 EditMode 用例，覆盖默认 Agent 解析、显式目标的距离 / 角度 / 移动方向、空目标不回退，以及获取目标的黑板写入、快照和失败清空。Enter Soft Lock 用例额外覆盖当前目标锁定、重复执行／停止不解锁、HardLock 不降级、缺少／禁用 Combater，以及无有效目标时失败（包括已有软锁但目标失效）。自动选敌的实际取舍及相机／移动联动仍需 Unity 人工检查。
- `ActionStateManagerContextTests`：PlayActionTask 生命周期测试的构造已移除自身参数，并覆盖单次仲裁失败、Until Started 重试后成功、Timeout 0 获得一次仲裁机会。新增完成等待用例覆盖完整播放前阻止后续计数、启动超时不影响完成等待、停止 / 同资源实例替换返回失败、暂停期间完成后恢复不重播，以及取消旧等待不误完成下一次执行。
- `WithActorTagTests`：10 个内存对象 EditMode 用例，覆盖标签先于子树生效、连续 Tick 不重复增加计数、完成 / 失败 / 重置 / 停图清理、暂停恢复、配置 / 容器改变后的原引用清理、无效输入、异常释放，以及同优先级 Normal / Strafe 与 Air 的条件选择。仅在测试框架边界使用反射：临时注册并原样恢复 TagTree 全局表、打开 NodeCanvas 的执行状态以绕过 PlayMode-only StartGraph；不反射本节点私有实现。完整 GraphOwner 停止 / 暂停路由仍需手工验收。
- 使用 Unity 2022.3.62f3 自带编译器和现有引用，在系统临时目录重建 Runtime 与 Editor / Tests 程序集：均 0 error、0 warning；图结构、标签 ID 与资源条件静态检查通过，未修改 Unity 生成文件。
- 上述为原接口整理时的编译／测试设计记录。主线整合后已在隔离 Unity 2022.3.62f3 原生 EditMode 中运行：NodeCanvasActorTargetTests 20 / 20、WithActorTagTests 10 / 10、CombatStrafeMovementTests 10 / 10 通过；PlayActionTask 合同随 ASM 56 项一起通过。最新组合范围与结果见 [主线迁移记录](Main_Migration.md)。没有运行完整 GraphOwner 的 PlayMode 停止／暂停路由或图资产接线验收。
- 人工检查：完成仍需迁移的旧图接线；检查各节点 Inspector 的条件字段是否随模式正确显隐、Header / Tooltip 是否按预期显示；检查 `Actor Has Tag` 的 Check Self 开关、目标字段显隐与两种检查对象；验证运行中丢失目标时移动失败并释放连续意图，以及暂停 / 恢复后仍使用显式配置目标。
- 当前训练场人工检查：标签选择器显示 `State.Movement.Strafe`；Giant 绕行期间 Transient 容器具有该标签并选择 Strafe，结束后移除并返回 Normal。中途丢失目标、打断子树、停止 / 暂停 GraphOwner、禁用拥有者后均不残留标签；空中和刚离地时选择 Air。原大剑三次攻击计数例子不再作为现存资产验收项。
