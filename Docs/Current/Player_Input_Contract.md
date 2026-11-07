# 玩家输入与动作条件合同

状态：`FrameWork` 已实现 **当前 State / 纯 Event Sequence**、有限移动历史与方向识别，源码已本地提交于 `d3785413`。Manager Prefab 清理与文档另行提交；按用户要求，7 个普攻的资产迁移仍仅保留在本地工作区，不纳入提交。推送已停止，尚未确认成功。源码编译及选定纯合同测试已通过，用户反馈输入与编辑器手工测试未发现问题；完整 Unity 原生集成、设备覆盖、Shader 和 Player 验收仍未记录完成。方案与资料依据见 [方案及时间窗口调研](Player_Input_Sequence_Design.md)。

## 设计边界

**输入核心只提供事实与消费能力，条件适配核心；核心不认识 ActionCondition、锁定目标或招式。**

```text
Unity Input System（Dynamic Update，context.time）
    -> PlayerInputController（唯一设备/权限 owner）
    -> PlayerInputBuffer（待发布边沿、带时间移动变化、当前状态）
    -> Driver 固定 Input/Control 边界发布
    -> PlayerInputDirections（核心外，用固定边界基准解释方向）
    -> Actor.PlayerInput / PlayerInputDirections（当前角色 runtime-only view）
    -> 输入 State / Sequence + 普通玩法条件（只读）
    -> 本次 ActionCandidate 携带 ActionEntryMatch
    -> ASM 胜选且动作真正开始成功
    -> 使用候选凭据原子消费匹配事件
```

保留现有 ASM、CancelRule、ActionContext、Locomotion 和 AI 外部请求。不增加混合 State/Event Clip、嵌套检查、动作图 DSL、复杂搓招框架、按住连发、联网、新程序集或新技能。

## 条件怎么配置

### 事件序列：InputSequenceCondition

- `maxSequenceTime`：整段首尾事件的最大真实时间跨度，默认 **0.5 秒**，每个 Sequence 单独配置。
- `clips`：非空的 Event 列表；每项选择按钮按下、按钮松开，或进入方向/回中。
- 第一版支持整段窗口 **0～2 秒**。负数、非有限数及超出范围的配置拒绝，不悄悄截断。零窗口允许同一时间戳的不同事件，仍按采集顺序匹配。
- 不设独立逐步时限；允许夹杂其他记录，但所需事件必须有序、未消费，且一个事件不能满足两个 Clip。
- 多组都合法时选择最旧的有效终端事件，再向前选择最近的有效前置步骤，避免旧起点超时掩盖另一个合法组合；快速连点仍是独立请求。

编辑器按操作意图配置，不直接显示底层 Type；界面标签、选项及提示统一使用英文：

- 列表显示为 **Input Steps**，每项标题只显示实际内容，例如“Enter Back”“Light Attack Pressed”，不额外加序号前缀或分隔符号；展开后编辑。
- 先选 **Input Category: Button / Direction**。按钮再选 **Button** 及 **Event: Pressed / Released**；方向直接选 **Operation: Enter Forward / Enter Back / Enter Left / Enter Right / Return to Center**。
- 两步及以上显示 **Max Sequence Time (s)**。单步骤隐藏该字段，只受终端缓存限制；若旧时间值本身无效，仍显示以便修复，不把拒绝原因藏起来。
- 此次仅修改编辑器呈现；仍写入原 `type / button / direction / clips / maxSequenceTime`，不改匹配语义、不新增序列化分类字段，也不重迁移资产。切换类别时才设置对应事件类型，绘制界面本身不重写无关值。

```text
Kiana 普攻进入条件（全部满足）
  Sequence：LightAttack / ButtonPressed（单 Clip）
  Ground State：Ground

后 -> 前 -> 攻击
  Sequence：Enter Back -> Enter Forward -> LightAttack / ButtonPressed
```

单 Clip 合法，跨度为零，不必保留另一种单事件 Condition。按住不会产生新 Pressed；持续推前也不能凭一个事件完成两次“进入前”。需要回中时显式添加 Neutral Clip。

Sequence 可用于 Action Requirements、ActionList 的 Neutral 入口条件或 CancelRule 的条件。这些位置复用完整 ActionCondition 类型集，不新增平行条件体系；独立 ExitConditions 已移除。

### 当前状态：InputStateCondition

`type` 选择：

- **Button**：`button`、`held`、`minimumHoldSeconds`。检查现在按住/未按住及当前真实按住时长；Not Held 不使用最低时长，编辑器隐藏无关字段。
- **Direction**：`direction`。检查当前相对锁定目标的四主方向或 Neutral；无有效方向参考时，包括 Neutral 在内均拒绝，不把“没有目标”冒充一个有效目标相对状态。

State 不消费输入，可以连续多个 Tick 成立，可用于 Requirements、Neutral 入口或取消规则条件。输入不可用时，连 Not Held 也拒绝，不能把禁输入解释成主动松手。技能中断仍走动作生命周期。

```text
一直推着前也能发动：
  State：当前 Forward
  Sequence：LightAttack / ButtonPressed

要求最近刚进入前方向：
  Sequence：Enter Forward -> LightAttack / ButtonPressed
```

State 只检查判定时的当前状态，不回溯历史攻击时刻。外层“当前前方向 + 缓存攻击”明确允许先按攻击、随后推前再通过检查。移动行为/速度等玩法条件仍可使用现有 LocomotionIntentCondition，不塞进输入历史。

### 组合规则

- Requirements、ActionList Neutral 入口条件与 CancelRule 条件统一 **All：全部满足**，复用同一求值机制。普通状态保留 invertResult；输入使用明确选择，不支持反转，非法序列化反转值拒绝。
- 未选按钮、组合按钮掩码、未知枚举、空 Clip 列表、非法窗口/时长和 null 条件拒绝对应条件集，不能靠反转通过。
- 三处空条件列表均表示没有额外限制。ActionList 的 Allow From Neutral 决定是否提供 Poll 起手入口；取消规则仍需匹配窗口和目标。独立主动退出条件不再参与仲裁。
- Event 来源按 RequiresPlayerInput 类别跳过 Requirements 中的玩家条件，其他条件和 null 项不跳过；External 不伪造设备输入，取消请求同时检查匹配 CancelRule 的条件与目标 Requirements，Neutral 请求不检查 ActionList 的 Poll 入口条件。
- 每条候选携带本次路线条件与目标 Requirements 的合并输入凭据；被拒绝、落选或 Begin 失败不消费，实际开始后统一原子消费。同一事件被多层条件匹配时只消费一次。
- 已移除 overrideAll；前轮迁移前确认没有启用配置。旧 YAML 值为 0 的无效字段不为清理而批量重存。

## 方向参考、识别与会话

- 锁定键仍切换 SoftLock，不改为 HardLock。有效软锁/硬锁都能提供目标相对参考，无锁定时按钮及移动照常工作，只拒绝方向类输入条件。
- 先复用 PlayerLocomotionIntentResolver 的实际移动转换得到世界方向，再相对目标判断前后左右。HardLock 使用目标相对移动；其他情况下与现有相机相对移动一致，不把摇杆上直接认成前。
- **采样点：固定输入发布、处理 Lock 按下之后。** 该批移动变化使用本次固定边界的同一组基准，不是设备事件瞬间的世界姿态回放。保留源事件的原始实时 Time 和 Id，后续查询不以新基准重解释旧方向事件。
- 新参考建立时以当前轴值为基线，不产生进入事件；当步已经发布的操作不补认成新参考下的方向命令。建议先建立锁定，再输入方向序列。锁定前的普通按钮请求保留，但不能拼到新的方向序列中。
- 更换目标、失去目标、软硬锁规则切换、主相机参考对象替换，以及输入会话变化会切断方向历史。ActorCombater.LockRevision 只记录锁定状态/目标变化及组件停用，不改变选敌/锁定策略；同一步内解锁再锁回同一目标也会切断旧历史。
- 镜头旋转、角色/目标位置变化可以更新当前方向 State，但不能单独生成方向 Event。真实移动值变化时才比较分类变化；方向历史和当前状态分别维护。
- 四主方向按最近主方向分类；精确斜向且没有可保留的旧方向时优先前后。使用约六度的角边界容差，维持原分类直到明确越界，不用几帧延迟去抖。
- 中心识别采用 0.12 进入、0.10 回中阈值（轴强度）；Controller 已有可配置 dead zone 保持不变。输入先经过原 dead zone，方向识别再做中心容差，不修改移动意图的门限和手感。

## 时间、历史与蓄力事实

| 项目 | 当前规则 |
| --- | --- |
| 整段 Sequence | 末事件 Time - 首事件 Time ≤ maxSequenceTime；默认 0.5 秒，可配 0～2 秒 |
| 终端出招缓存 | 保留 Controller 的默认 0.2 秒；从最后事件原始发生时间算起 |
| 原始记录保留 | 2 秒上下文上限 + 当前终端缓存，默认约 2.2 秒；固定边界清理 |
| 时间轴 | Input System context.time / realtimeSinceStartupAsDouble；不按渲染帧、timeScale 或动画进度累加 |

前面步骤可以超过 0.2 秒；仅终端额外要求新鲜。识别成功不等待窗口走完，重复检查不续期。时间比较使用一微秒的数值误差余量，避免 float 配置与 double 时间边界表示差异；这不是额外手感宽容参数。

按钮边沿与移动变化共用采集 Id，按原始时间/Id 排序，同一时间戳保留采集顺序。Buffer.Events 仍是按钮记录，MoveEvents 独立保存移动变化；PublishedMoves 提供本固定步的新变化，避免重新扫描历史时再造方向事件。原始历史包含已消费/终端已过期的上下文，不代表其中所有记录仍可出招。

PlayerInputEvent 是不可变事实；按钮保留 Id、PressId、Button、Kind、Time、HeldSeconds，移动记录另带 IsMove / Move。

- 同次按下/松开有相同 PressId；Released 保留实际按住时长，即使同一输入更新内快速按松。
- 当前 held/时长独立于过期和消费；消费 Pressed 不消费 Released，不会停止保持状态或移动。
- 不合成 ShortPress/LongPress，不设帧数阈值，也不超时自动释放。若需要按释放时长判断技能行为，可读取 Released 事实，不在核心恢复兼容事件。
- 技能蓄力阶段、进度/伤害及是否受慢动作/顿帧影响由技能决定；本轮不新增蓄力技能。

## 匹配结果与消费

- 共享 Condition / ActionAsset 不存运行时命中记录；同一条件可以检查多个角色和候选。
- PlayerInputEventMatch 绑定原事件、owner、generation、Tick 和该组终端的有效截止时间；PlayerInputMatch 还绑定方向参考会话，ActionEntryMatch 保存本候选的多个组。
- 匹配只读；其他条件失败、仲裁落选、禁止重入或实际 Begin 失败均不消费。特殊技不合法时，合法普攻仍参加原有仲裁；不新增 Begin 失败后自动重试其他候选的机制。
- 开始前重新校验；开始成功后，先验证全部组的方向/输入凭据，再合并为一个原子提交集合。跨条件重复引用只消费一次；同一 Sequence 内不允许重复使用同一个事件。
- 成功消费全部匹配步骤，包括方向上下文；不消费夹杂的无关事件。已消费的后前操作不能反复配上新攻击重放特殊招式。
- 原始记录保留、匹配有效期和消费校验使用一致规则；前置步骤不会因为超过 0.2 秒而在提交时被误拒绝。
- 下一 Tick、清历史/reset、换角色、不同 owner 或新方向参考发布后，旧凭据失效。下一 Tick 可重新匹配尚有效的原事件，但不改变其时间。
- Begin 期间若清理/换 owner，旧凭据失效，不能误消费新会话。方向参考变化按固定输入边界发布；不承诺在两个发布点之间回放瞬时世界状态。
- 不恢复 ActionCondition.OnClaim / ActionAsset.ClaimEntry 或旧 InputCheck 套娃。

## 禁输入、切角色与相机

禁用、失焦、切角色、Gameplay 权限变化时清待发布事件、历史和按住计时，撤销旧 Actor view 和移动意图；Actor 自身禁用也清理。方向 view 随同撤销，后续发布以新会话建立基线。

恢复时原 held 的攻击/跳跃/闪避/锁定须先松再按；native initial-state check 保证后续释放可见，但初始化不变成新攻击。系统 Disable 的 canceled 不生成玩家释放事件。

移动可在恢复后的固定边界读取仍推着的轴，不借此伪造方向命令。RawMove 固定发布；FreeLook 读取同一个 Controller 的 Look，保留呈现时钟、原脚本 GUID、Prefab 引用和鼠标过滤参数。

集成入口保留：

- SetControlledActor(actor)：显式换角色；旧 controlledActor 字段仍在 Update/回调/固定边界同步。
- SetGameplayInputEnabled / SetLookInputEnabled / SetUIInputEnabled：分别控制权限。
- SetMenuInputActive：只切输入权限，不创建菜单或修改 Time.timeScale。
- InputActions：UI 可以接到同一实例；本轮不改 EventSystem/场景，启用 UI Map 不等于菜单接线完成。

## 迁移范围与保留内容

- 源码已在既有 FrameWork 分支本地提交于 `d3785413`，main、Release 未移动；推送已停止，未确认成功。Manager.prefab 与文档在随后独立的本地提交中归档，不重试推送。
- 7 个 Kiana 普攻初次从过渡 InputEventCondition 迁移为 InputSequenceCondition / 单个 LightAttack ButtonPressed；保留外层 RID、地面条件、Timeline 和其余参数。用户随后将 NormalAttack_1 改为后 -> 前 -> 轻攻击序列验证，原样保留。**7 个 ActionAsset 修改按用户要求不提交**，拉取代码后仍需另行迁移或配置旧输入资产，否则旧序列配置不能自动适配新字段。
- InputStateCondition / InputSequenceCondition 替换过渡的 InputButtonStateCondition / InputEventCondition，不长期保留两套入口；沿用原状态/序列脚本 GUID。旧 InputCheck / InputCheckHandler 不恢复。
- ActorLogicInput 的组件和既有序列化槽保留。Manager.prefab 延续前轮仅删除三个废弃阈值的结果，本轮不再改 Prefab、绑定、生成类、场景、项目设置或包。
- 当前轮备份、编译响应文件和验证日志：`C:\Users\20052\AppData\Local\Temp\combat-sequence-mserjbn0`。此前备份仍保留在 `combat-condition-migration-89f788qy`、`combat-input-development-novusz5a`。

## 命中特效 Shader（上一轮实现，本轮不扩大修改）

`Assets/Art/Shaders/HitConfirmOccluderStencil.shader` 根据现有 Renderer 合同重建，沿用缺失 GUID `770da3146d7444eb92a935c7eb85d4b8`，三套 Renderer 引用可解析。并非找回原始 Shader 源文件。

该 pass 不写颜色/深度，可见环境通过深度测试后写模板 bit 0 / Ref 1，与后续 NotEqual 1 的重绘配合。层筛选、Renderer 次序和特效资产不改。仍需检查三档画质、透明环境、模板位冲突和真实 Player Shader 编译/保留。

## 验证与人工清单

本轮使用 Unity 2022.3.62f3 配套编译器检查 Runtime、移除 UNITY_EDITOR 定义的 Runtime、Editor/全部测试源码，结果和最终纯合同用例数量见 [方案页当前验证状态](Player_Input_Sequence_Design.md#当前验证状态)。独立 Mono 执行的是选定纯逻辑测试，不是完整 Unity Test Runner。

已补方向窗口边界、终端时效、非相邻匹配、多起点、同时间顺序、保持/抖动、参考变化、当前状态与历史区别，以及原子消费/跨会话用例。ASM 集成测试新增方向特殊技与同按钮普通攻击的竞争/回落，原有开始失败与胜选消费用例保留；这些原生集成用例只编译，**尚未在 Unity Test Runner 执行**。

按用户最新要求，不继续触发复杂测试；临时 Unity 测试入口已移除并取消待执行回调，本轮没有启动 Unity Test Runner。以下集成、设备及发布验收由用户执行：

1. 打开/保存 7 个普攻，检查 Sequence 单 Clip、无缺失 managed reference、无输入反转/override 选项；新建按钮/方向 State、按下/松开/回中 Clip 检查字段显示。
2. Unity Test Runner 执行输入及 ActionStateManagerContextTests；实际确认落选、失败开始、取消窗口、方向特殊技与普攻回落不错误吃输入。
3. 键鼠/手柄，30/60/144 FPS，不同相机角度及 SoftLock/HardLock：验证方向与实际移动一致。既看主动出招成功率，也看后退/前进/普通攻击的误触率，再调具体招式窗口。
4. 换目标、目标失效、同 Tick 解锁重锁、镜头旋转、目标移动，不伪造方向操作、不继承旧序列；无目标时按钮/移动正常。
5. 按住时开关权限、禁用/启用、失焦/恢复、换角色和角色快速停用：操作键先松再按，移动可恢复且不伪造方向 Event。
6. 顿帧/慢动作下仍按真实时间过期；无效配置只拒绝对应动作，不停止其他角色仿真。
7. 三档画质及 Frame Debugger 检查遮挡，真实 Player Build 检查 Shader 与输入运行。

未关闭当前 Unity，未启动第二实例。审查第 5～8 项（AI 撤销、Overlap 容量、列表缓存刷新、Combat hub 自动补齐）仍不在本轮范围，见 [审查记录](Demo_Development_Readiness_Audit.md)。
