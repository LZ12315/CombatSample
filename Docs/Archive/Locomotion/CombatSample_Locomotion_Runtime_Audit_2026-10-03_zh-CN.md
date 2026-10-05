# Locomotion 当前运行逻辑与配置语义审查

> 归档于 2026-10-05。正文中的状态、待办和验证结果保留为当时记录；当前实现见[现行架构](../../Current/CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)，最新验证与未完成检查见[提交审查](../../Current/CombatSample_Locomotion_Commit_Review_2026-10-05_zh-CN.md)。源码链接用于导航，历史成员和行号不代表当前代码。

## 审查后处理（2026-10-03）

以下审查主体保留修改前的行为快照与当时的代码行号。本节说明后续决定，优先于主体中的“当前行为”和待办建议。

- A1 已修正：InputStrength 直接取输入强度；LocalInput 直接取局部输入向量；无输入为零，不使用残余速度或状态机的 0.01 输入门槛。
- A2 已修正：HorizontalSpeed 直接读取政策缩放后的水平模型速度；移除样本相关的截断、0.1 秒平滑及缓存。
- A3 经用户澄清，速度匹配是既定且有用的核心功能，保留现有实现。本次没有发现其算法故障；原先将“配置表达”疑问列为问题的判断收回，不为此新增开关。输入选择样本，实际速度调整播放快慢，职责清楚。
- A4 此轮仅解释：VerticalSpeed 运行时不使用轨迹，但覆盖检查仍要求非零样本有轨迹。尚未修改校验。
- 已更新输入归零、小强度输入、速度直接取值及暂停恢复的契约测试，移除将速度低谷隐藏视为正确性的旧断言。Unity Roslyn 运行时及 Editor/test 程序集编译通过；未执行 Test Runner 或新的场景验证，详情见交接记录。

## 后续决定：移除配置诊断与覆盖门槛

用户确定：LocomotionAsset 无需 Inspector 检查；能运行的配置直接运行，表现由作者自行调整。本轮移除了覆盖检查 API、Inspector HelpBox、Asset/ActorLocomotion 的配置告警及 Move 播放匹配提示。A4 的垂直轨迹误报随覆盖检查整体删除而消除。

Runtime 不再强制 Idle/移动样本配对、不限制输入模式的样本阈值范围，不因一个坏条目拒绝整组素材。绑定时跳过空 Clip、无有效时长、非有限数值和重复阈值；重复阈值保留第一条可用样本。零方向按现有角度运算参与选片，零加速度/减速度/转速允许运行。

速度匹配算法不变，轨迹不足保持倍率 1。无可用 Move 时仍走姿势保护，缺少可选过渡仍回 Move。数值与生命周期契约、动作 owner 冲突等代码错误保护保留；手动 Trace 保留。

检查：运行时与 Editor/test 程序集使用 Unity Roslyn 编译通过（输出位于 `/tmp/locomotion-quiet-compile-6v7zap2l`），未执行 Test Runner 或场景验证。需重绑后手动确认单样本、缺 Idle、空条目及缺轨迹素材的播放与 Inspector/Console 表现。

## 修改前审查快照

日期：2026-10-03。范围：当前工作区中的 LocomotionAsset、两个具体 Asset、Runtime、状态机、运动模型、动画提交、Motor 接口和相关 EditMode 测试。本文描述当前代码，包括尚未提交的修改；建议均未实施。

本轮只新增本文，没有修改生产代码、测试或素材。验证方式为源码与现有测试断言交叉检查，未执行 Unity Test Runner 或新的场景复现。历史交接文档不作为当前实现的替代依据。

## 结论

配置的基本结构可以保留：移动规则、Move 混合空间、可选 Start/Stop/Pivot、基于方向的统一最近匹配。主要问题集中在参数含义和播放策略：同一个输入参数会悄悄改读速度；部分速度参数内置平滑；播放倍率匹配默认启用且无配置入口。作者仅看 Inspector 无法推导全部行为。

审查标准：规则有明确职责、输入和输出，且能用稳定契约说明，就是正常模型的一部分；为修补某个表现而改变参数含义、增加未声明的行为，才是需要收敛的地方。无需把每个内部常量都变成配置。

## 发现与建议

### A1：输入参数混用了残余速度——优先修正

位置：`Assets/Scripts/Actor/LocomotionRuntime.cs:136`、`:157`；输入有效判定在 `LocomotionAnimationContracts.cs:30`。

- InputStrength：存在有效移动输入时取输入强度；否则取政策缩放后的模型速度 / MaxSpeed，并限制在 0～1。
- LocalInput：存在有效输入时取世界方向 × 强度，转换到角色局部空间；否则取模型速度 / MaxSpeed，再转换到局部空间。
- “有效输入”还要求强度 > 0.01 和非零平面方向，因此微小输入也可能切到速度分支。
- 例如输入已归零、模型速度仍为 2.5 m/s、MaxSpeed=5 时，InputStrength 得到 0.5。

这就是明确的隐含表现补偿。原注释说明其目的是避免松键后站立滑行，但它破坏了输入模式的单一含义。反向按满输入时仍为 1，所以能解决此前反向减速经过零速而混入 Idle 的问题；这不能证明松键分支也合理。

建议：输入模式只读取输入，速度模式继续保留。若要平滑，以独立且明确的处理规则表达，不能悄悄更换信号源。现有测试 `InputStrength_FullPressRunsThroughReversalAndReleaseUsesResidualSpeed` 和 `LocalInput_UsesFacingRelativeIntentInsteadOfVelocity` 都断言了松键残速行为，清理时必须连同测试、旧说明一起更新。

### A2：HorizontalSpeed 隐含截断和平滑——配置语义不完整

位置：`LocomotionRuntime.cs:95`、`:165`。

当前先把政策缩放后的模型速度截断到最高样本阈值，再用 MoveTowards 平滑。变化速率 = 最高样本阈值 / 0.1 秒；首次采样直接采用目标值。这里的 0.1 秒是走完整个阈值范围所需时间，并非每次变化都持续 0.1 秒。

因此添加一个更高阈值的样本，也会改变原有范围内的参数响应速度。LocalVelocity 没有同样的平滑，输入模式也没有。Threshold 同时承担样本位置和滤波速率依据，作者难以预期。

建议：参数源只负责取值；若确实需要平滑，用统一且可说明的参数处理策略。不要继续在各个输入来源里分别补偿。样本插值边界与参数平滑应分开理解。

### A3：Move 播放倍率匹配是默认行为——需要明确产品契约

位置：`LocomotionMovePlayback.cs:49`、`:91`、`:145`。

非 VerticalSpeed 的 Move 会尝试读取循环素材的烘焙轨迹，结合混合权重和 Sync 状态计算参考速度，再用上一帧合格的物理结算速度修正整个 Mixer 的播放倍率。倍率限制为 0.5～1.5，平滑时间常量为 0.1 秒，参考速度下限为 0.1 m/s；这些都是代码常量。

- Sync 控制样本间的归一化播放进度同步，关闭 Sync 不会关闭倍率匹配。
- 当前参与混合的任一非 Idle 样本缺少可用循环轨迹，会令整个 Mixer 本次使用倍率 1。
- 非循环素材保留基本播放、倍率 1；VerticalSpeed 始终倍率 1。
- 参考位移向量相互抵消时可能得到很小的参考速度，进而回到倍率 1。

倍率匹配本身有独立职责，可以保留；问题是作者无法从配置明确选择是否使用它，也难以区分“动画选择”与“动画快慢”。建议明确其启用方式和缺资料时的行为，再决定哪些数值确实需要配置，不机械暴露所有常量。

### A4：Inspector 的垂直动画轨迹校验与 runtime 不一致

位置：`LocomotionAsset.cs:259` 对照 `LocomotionMovePlayback.cs:107`、`:150`。

覆盖检查对所有非零 1D 样本要求烘焙轨迹，未排除 VerticalSpeed；runtime 却跳过垂直样本的轨迹读取和倍率匹配。这会提示作者提供当前运行根本不需要的数据。该问题影响诊断，并不意味着缺少轨迹会禁止垂直动画基本播放，因为运行时播放校验关闭了轨迹检查。

建议：覆盖检查遵循实际启用的播放功能；若垂直模式不用轨迹，就不要要求它。

### B1：配置更新有“部分立即生效、部分缓存”的边界

位置：`LocomotionRuntime.cs:128`、`:206`、`:402`；`ActorLocomotion.cs:391`。

Move 样本、阈值、Sync、轨迹和过渡条目绑定后会缓存；参数枚举、BlendType、MovementConfig、过渡混合时长等仍从 Asset 读取。普通退出再进入同一 Asset 会复用 Runtime，不会完整重绑。

因此 Play Mode 修改配置不能视为完整热更新，甚至切换 BlendType 后可能仍持有旧类型 Mixer。现有测试只保证 ActorLocomotion Disable/Enable 后重建绑定。当前调素材应重新进入 Play Mode，或禁用再启用 ActorLocomotion。

这首先是需要声明的生命周期边界。以后若支持热更新，应采用完整配置快照或统一重绑机制，避免逐字段加特例。不能仅凭该现象推断历史某次 Trace 与磁盘差异的原因。

### B2：其他需要明示的固定规则

- 状态机写死：有效输入 > 0.01、静止速度 ≤ 0.1 m/s、Pivot 最低速度 0.5 m/s、反向夹角 ≥ 120°。
- MixerAsset 的状态切入混合时长固定 0.1 秒；SetAsset 使用可配置 TransitionBlendDuration。
- Move 中非循环子动画从近零权重重新获得权重时，会自动重置时间；持续有权重时不会反复重播（`ActorAnimation.cs:150`）。这是一项播放策略，需要明示，不能把它误认为素材已循环。
- Start/Pivot 正常等 clip 时间到末尾才完成，不按实际速度、朝向到位、脚步事件或距离完成。
- 一个 Start/Stop/Pivot 组内任一条目无效，会报错并禁用整个组；不会跳过坏条目后继续选其他条目。

这些并非全部需要删除。先决定它们是否属于统一契约，再决定哪些是作者需要调整的配置。

## 配置的四层含义

| 配置 | 职责 |
| --- | --- |
| EntryConditions / Priority / SelfTags | 哪个 Asset 当前生效；激活期间拥有哪些标签 |
| MovementConfig | 最大速度、加速、减速、朝向响应与转速上限 |
| Move | 选择 1D/2D 参数源；定义样本阈值、动画、Sync |
| Set 的 Start / Stop / Pivot | 可选的过渡素材；方向元数据；统一过渡混合时长 |

MixerAsset 只输出 Move。SetAsset 在 Move 基础上增加三种过渡。两者共用同一个运动模型，Start/Stop/Pivot 不负责角色的实际位移或转向。

## 每个 Tick 的实际顺序

入口：`CombatSimulationDriver.cs:119`。顺序是 Input/Control → Action → Motion → Animation → World → Hit/Finish。

1. 输入生产者提交世界移动方向、0～1 强度、独立的 FacingDirection。ActorLocomotion 锁定本 Tick 输入；一次性输入消费一次，持续输入保留至替换。
2. Action 先运行，可能取得动画层、速度或根运动的控制权，以及改变运动策略。
3. Motor 记录本帧起始朝向、接地状态、策略、上一帧物理结果。ActorLocomotion 从合法且所有条件满足的 Asset 中选最高 Priority；同优先级优先保持当前，否则取列表中第一项。
4. 切换 Asset 时释放旧标签并启用新标签，重置动画状态；两个有效 Asset 之间保留共享模型速度。没有候选时不自动选默认 Asset：清空模型速度并停止提供 Locomotion 运动请求。
5. LocomotionRunner 保存积分前速度，然后根据输入更新模型速度和朝向。目标速度 = 平面单位方向 × 输入强度 × MaxSpeed。反向点积 < 0 时先按 Deceleration 刹到零，再用 Tick 剩余时间按 Acceleration 加速。FacingDirection 单独控制旋转，TurnResponseTime 控制响应曲线，RotateSpeed 控制转速上限。
6. Motor 对 Locomotion 请求应用运动策略、动作运动所有权、冲量、重力等，得到准备交给 KCC 的运动。速度所有权优先，其次轨迹根运动，再其次 Locomotion 与普通水平运动合成。
7. 动画阶段使用同一 Tick 的输入、积分前/后模型速度、帧初朝向和 Motor 请求速度：更新 Set 状态机、选过渡或 Move，提交 Layer 0。Action 姿势使用 Layer 1。动画图本 Tick 求值一次。
8. World 阶段 KCC 和碰撞处理实际移动角色，再发布物理结果。因此本 Tick 的倍率匹配只能使用上一 Tick 已发布的物理速度。

冻结 Tick 的输入保留、动画时间为零时不消费状态边沿，是时钟规则。碰撞后反馈筛选和会话所有权保护是正常工程边界，不应为“减少分支”而删除。

## 不同“速度”的来源

| 信号 | 来源 | 用途 |
| --- | --- | --- |
| 输入强度/方向 | 输入生产者 | 目标运动；输入模式的 Move 参数 |
| 积分前模型速度 | Runner 上次缓存 | Start/Stop/Pivot 触发与来源方向 |
| 积分后模型速度 × 运动策略倍率 | 本 Tick Runner 输出 | HorizontalSpeed / LocalVelocity；当前输入模式的松键回退 |
| Motor 请求垂直速度，除去运动时间倍率 | 本 Tick Motion 合成结果 | VerticalSpeed 参数 |
| 上一 Tick 实际结算速度，除去运动时间倍率 | KCC/碰撞后的发布结果 | 符合连续自主运动条件时的 Move 播放倍率 |

HorizontalSpeed 和 LocalVelocity 不是碰撞后真实速度。例如撞墙时，模型仍可能要求跑动，实际位移却为零：混合参数与播放倍率会受到不同影响。

2D 参数的 x 为角色局部左右，y 为局部前后；用本 Tick 起始朝向转换。LocalVelocity 的长度有速度单位，LocalInput 的长度表示输入强度。过渡条目的 Vector2 则只表达方向，长度不参与评分。

## Set 状态机完整规则

状态只有 Move / Start / Stop / Pivot；Idle、Walk、Run 是 Move 的混合样本，不是三个独立状态。

判定顺序先看 Action，再看是否有输入。下表以无 Action 覆盖为前提，速度均为积分前模型速度。

| 当前情况 | 提议下一状态 |
| --- | --- |
| Move，输入刚从无变有，速度 ≤ 0.1 | Start |
| Move，新出现速度 ≥ 0.5 且与输入夹角 ≥ 120° 的条件 | Pivot |
| Start，出现上述反向边沿 | Pivot；优先于同 Tick 的 clip 完成 |
| Start，有输入且无反向边沿 | clip 完成才回 Move，否则继续 Start |
| Pivot，仍有输入 | clip 完成才回 Move，否则继续 Pivot；不会因新的反向再次重选/重播 |
| 输入刚消失，或 Start/Pivot 中没有输入 | 速度 > 0.1 则 Stop，否则 Move |
| Stop，无输入 | clip 未完成则保持，完成回 Move；不会因速度先到零而提前结束 |
| Stop，重新有输入 | 速度 ≤ 0.1 则 Start；否则有反向边沿则 Pivot；其余 Move |
| Move，无输入且无输入变化 | 继续 Move，由参数决定 Idle 等样本权重 |

“反向边沿”表示反向条件刚从不满足变为满足；条件持续成立不会每帧触发。空组或无效组不能准备目标动画时，本次直接回到 Move，边沿已消费，不排队补播。

Action 动画 owner 存在期间，以及刚退出后的第一个有效动画 Tick，Set 都回到 Move 并更新输入/反向基线，避免动作结束后补播旧 Start/Stop/Pivot。基础 Move 可以在覆盖期间继续更新。

Move 配置必须先能创建 Mixer，否则 Set 不播放单独的过渡；动画提交会进入姿势保护。运动配置有效与动画覆盖完整是两项独立检查，所以动画错误不一定使移动失效。

## 过渡选片：当前 Vector2 模型是统一的

位置：`LocomotionRuntime.cs:451`。

- Start：当前目标输入的局部方向，与每条 TargetLocalDirection 比夹角。
- Stop：积分前速度的局部方向，与每条 SourceLocalDirection 比夹角。
- Pivot：来源方向夹角 + 目标方向夹角，取总分最小的条目。
- 同分取作者列表中最前的条目；Vector2 长度不参与评分。
- 没有最大允许夹角、素材覆盖半径、速度区间、左右转特殊判断或“单素材专属”分支。
- 因此只配置一条前向 Start 时，向后启动也会选它。这符合最近匹配的统一规则，但不保证素材视觉自然。
- 仅在进入另一状态时选一次素材；同一 Start/Pivot 播放中不会持续重选方向。

选片只决定播放哪条 clip；它不会把片中前向位移重新定向成玩家想去的方向，也不会从 Start/Stop/Pivot 轨迹驱动角色。真实转向与加减速仍由运动层处理。

## 混合、时钟与保护

ActorAnimation 对状态切换做 crossfade；Set 使用 TransitionBlendDuration。状态机已经是 Move 时，画面中仍可能留有正在淡出的 Stop/Pivot。因此 Trace 的状态名不等于最终唯一可见姿势。

Set 每个动画 Tick 都计算 MoveRequest，但只有最终提交 Move 时才把参数和倍率应用到 Move Mixer。播放 Start/Stop/Pivot 时，淡出的 Move 子权重和倍率不会继续按新请求更新；clip 的播放时间仍可随图求值推进。

Start/Stop/Pivot 进入时从头播放，通常倍率 1，通过动画时钟到末尾判定完成。动画时钟受暂停、MovementTimeScale 和 Action 播放速度限制，不等同于无条件使用真实时间。

动画请求无效、Runtime 退出或销毁时，ActorAnimation 会用缓存 Idle 或独立冻结的姿势保护基础层。这与正常 Move 参数经过零值混入 Idle 是不同路径，诊断时必须分别观察。

速度反馈排除冲量、平台携带、角色分离、动作接管和不连续策略等结果，是为了不把外部位移解释成自主步速；这些检查应保留。

## 当前 Kiana Normal 的具体含义

本次读取磁盘 `Assets/Create/Locomotion/Kiana/Locomotion_Kiana_Normal.asset`：

- MaxSpeed=5、Acceleration=20、Deceleration=32、RotateSpeed=600、TurnResponseTime=0.08。
- Move 使用 1D InputStrength，只有阈值 0 的 Idle 和阈值 1 的 Run；两条 Sync 都关闭。
- Start 一条，目标方向前；Stop 一条，来源方向前；Pivot 为空；过渡混合 0.1 秒。
- Walk 素材存在，但未放入此 Asset 的 Move 样本。

从静止按键会先触发 Start，完成后进入 Move；键盘满输入使 Move 取 Run。运动中强反向满足 Pivot 条件时，因为 Pivot 为空，会回到 Move；持续满输入保持参数 1，所以模型刹到零也不会让 Move 混入 Idle。松键触发 Stop；Stop 被打断或完成后回到 Move。当前代码的松键 Move 参数仍可能使用残速，这正是 A1 待清理部分。

以上是磁盘配置和代码推导，不代表已经确认当前 Play Mode 实例完成重绑。

## 建议的收敛顺序

1. 保留现有 Asset 结构、方向元数据和统一最近选片；速度参数仍作为可选来源保留。
2. 先修正输入模式的信号语义，并同步更新相关测试与说明。
3. 明确参数平滑与播放倍率匹配的职责、配置入口及默认行为，清理各来源内的隐含补偿。
4. 修正校验与 runtime 的不一致，写清状态机触发/中断、空组与非法组规则。
5. 明确配置绑定生命周期；在未实现完整热更新前，调试素材后完整重绑。

后续验证应以稳定契约为主：相同输入不因残速而改变输入参数；方向最近匹配对单/多素材统一；空过渡消费边沿回 Move；配置校验与实际功能相符。再手动验证静止启动、持续反向、松键重按、Stop 中重按及 Action 覆盖/退出的可见混合。
