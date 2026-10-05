# Locomotion 实现与协作结构审查（2026-10-05）

> 归档于 2026-10-05。正文中的状态、待办和验证结果保留为当时记录；当前实现见[现行架构](../../Current/CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)，最新验证与未完成检查见[提交审查](../../Current/CombatSample_Locomotion_Commit_Review_2026-10-05_zh-CN.md)。源码链接用于导航，历史成员和行号不代表当前代码。

## 审查结论

当前实现的核心模型可以继续使用：输入定义目标运动，共享 Runner 积分模型速度，Motor 仲裁并解决实际位移；动画 Runtime 决定基础动画，ActorAnimation 管理最终图播放。Move 的速度匹配、Stop 的距离时钟和脚相择优分别有明确的数据来源。

主要整理空间集中在配置绑定、播放执行和生命周期交叉的部分。单个算法大多较短，但理解一个 Tick 或一次切换，仍需跨多个方法追踪状态。本轮是当前代码的静态审查，不修改生产代码，不将历史测试结果算成本轮验证，也不将下面的潜在边界风险视为已经复现的视觉故障。

## 当前执行链路

固定顺序见 [CombatSimulationDriver.cs](../../../Assets/Scripts/Actor/CombatSimulationDriver.cs)：Control → Action → Motion → Animation → World → Hit／Finish。

| 环节 | 当前职责 | 关键约定 |
|---|---|---|
| Player／AI 输入生产者 | 提交方向、0～1 强度、独立朝向 | Locomotion 不读取相机或 CombatTarget |
| ActorLocomotion.Control | 锁定本 Tick 输入 | Action、选模式和 Motion 共用输入；冻结时可保留一次性输入 |
| Action | 更新动画覆盖、运动所有权与倍率 | 先于 Motion，因此本 Tick 能采用 Action 更新后的策略 |
| ActorLocomotion.Motion | 选 Asset、切换 Runtime／标签，提交运动请求 | 同优先级保持当前；有效模式之间继承模型速度；没有候选则停止贡献并清模型速度 |
| Runtime → Runner | 用绑定运动配置积分速度、朝向 | Runner 属于 ActorLocomotion，模式各有 Runtime，但不各存一份模型速度 |
| Motor | 合成所有者、根运动、Locomotion、冲量、重力 | Locomotion 请求不是最终物理速度 |
| ActorLocomotion.Animation | 读上一求值姿势脚相，提供同 Tick 运动上下文 | 在新请求提交前获取参考，使用帧初朝向作局部方向转换 |
| Set Runtime | 提议状态、选素材、准备节点，再提交状态 | 空过渡回 Move；输入边沿不会排队补播 |
| ActorAnimation | 应用请求、参数／倍率、显式时间、Action 层，并求值 | Layer 0 为基础，Layer 1 为 Action；最终图播放权集中 |
| World → Motor | KCC／碰撞，发布实际结果 | 动画阶段早于 World，Move 使用上一 Tick 的合格运动反馈 |

Asset 的作者数据在 Runtime 构造时绑定。Exit／Enter、普通 Tick、图重建复用绑定值；新 Runtime 读取新配置。Priority／条件仍用于当前模式选择，SelfTags 在模式激活期间取得并释放；这里不增加运行时改配置能力。

## 发现与建议

### 1. 姿势保护隐含地依赖此前使用过的 Idle

**类型：需要明确约定的行为边界，整理优先级较高。**

证据见 [ActorAnimation.cs](../../../Assets/Scripts/Actor/ActorAnimation.cs)：`SubmitLocomotion` 的 153～154 行只有请求携带可用 Idle 时才更新 `_idleClip`；`EndLocomotionSession` 不清它；`HoldLocomotionPose` 的 230 行优先使用这个缓存，再考虑当前 Move 的独立姿势快照。

因此可从代码推导：先播放带 Idle 的地面模式，再播放没有 Idle 的空中模式，后者结束时可以保护此前地面 Idle；同一个空中模式若从未见过 Idle，则走冻结 Move 快照。这是对历史状态的依赖，不是当前素材配置就能完整说明的行为。现有 `AirWithoutIdle_RetainsAnIndependentFrozenBaseAfterRuntimeDisposal` 只覆盖从没有 Idle 的初始状态进入空中模式。

建议分别表达“当前会话提供的 Idle”和“为会话结束保留的独立姿势”。保留旧会话的保护结果用于新会话首次提交前的间隙；新会话收到有效请求后，其 Idle 是否存在应明确替换，而非只更新非空值。若确实要将最近 Idle 作为角色级后备，也应明确命名、约定优先级及覆盖测试。这一项涉及可见行为，实施前需固定保护合同，不能仅作为字段清理悄悄改变。

### 2. 保护快照与其他姿势观察使用了不同的时间来源

**类型：潜在一致性风险，尚未做原生图复现。**

`ActorAnimation.CreatePoseSnapshot` 第 275 行复制 `source.TimeD`；脚相与过渡完成判断却使用 `LocomotionAnimationUtility.EvaluatedTime`，后者直接读有效 Playable 的 `RawTime`。当前 Animancer 源码中 `TimeD` 按 Graph.FrameID 缓存，`RawTime` 直接取底层时间。

这并不证明每次保护都会取错帧，但同一帧手动求值、提前读取缓存或显式采样后再保护时，两种观察口径存在偏离风险。建议统一“观察已求值姿势”的读取入口；时间写入继续使用 Animancer 正常设置接口，不能把所有 TimeD 写入也机械替换为 RawTime。

验证应使用有时间变化的探针曲线：读取缓存、继续手动求值、结束会话并保护，比较保护前后实际姿势。现有保护测试大多使用常量姿势，不能证明冻结帧正确。

### 3. 生命周期正确性依赖交叉回调和重复 Reset

**类型：结构问题，可在不改变播放规则的情况下整理。**

证据：`ActorLocomotion.ApplyAsset` 第 351 行调用 `ResetAnimationSession`，其中重置旧 Runtime；随后旧 Runtime.Exit 又经 `LocomotionAnimationRuntime.OnExit` 重置动画。`ClearCurrentAsset` 有同样的路径。另在 `ActorAnimation.ClearAnimationState` 第 427～428 行，先结束会话，再回调 ActorLocomotion；后者又请求 ActorAnimation 结束会话。

当前依靠所有权检查和幂等清理避免重复操作产生错误。保护和幂等本身应保留，但同一次事件由两端相互通知、多个入口重复 Reset，会增加维护成本。

建议分清三个操作：

1. 结束播放会话：ActorAnimation 保留独立保护姿势并撤销播放 owner。
2. 退出 Runtime：清状态决策和播放反馈，不重新读取配置。
3. 图失效：使旧播放节点失效，下一次从绑定数据重建。

每种事件指定一个协调入口。组件独立 Disable 仍需通知另一端，但通知应清本端引用，而非再循环调用发起端的清理方法。普通 Reset 与图失效恢复仍需区分，避免把生命周期简化成一个无条件销毁方法。

### 4. ActorLocomotion 的 Tick 状态分散，字段组合承载了协议

**类型：可读性和维护风险。**

第 20～34 行同时保存 Control 输入、请求缓存、动画输入、运动前速度、Motor 上下文和多个阶段标志。`BeginControlTick`、`ConsumeControlTick`、`HoldControlTick`、`CancelControlTick` 分别清不同子集。阅读者需要记住 `_controlTickOpen`、`_motionRequestBuilt`、`_hasAnimationSnapshot`、`_animationUpdated` 如何组合才有效。

缓存运动请求和输入快照是必要的：它们保证一次积分，以及 Action／Motion／Animation 使用同一 Tick 数据；不应为减少字段删除。建议将当 Tick 状态集中成内部值结构，明确输入锁定、运动已准备、动画已提交等阶段；保留待提交输入缓冲独立存在。动画快照集中保存 Intent、前后模型速度和 Motor 上下文，减少从多个活字段临时拼上下文。

只提炼一个职责集中的 Tick 状态，不为每个布尔字段新建类。优先保护冻结、重复 Build、取消、新输入覆盖旧输入等已有合同。

### 5. Runtime 将配置绑定、状态决策和播放节点管理集中在一起

**类型：职责可以进一步收紧。**

`LocomotionRuntime.cs` 中共同动画 Runtime 既复制／过滤／排序作者样本，又转换参数、创建 Mixer、处理图销毁和 Dispose。Set Runtime 又负责绑定三组条目、状态机编排、方向／脚相评分、Stop 距离预测以及 ClipState 缓存。

当前继承层数本身合理，状态机与 Stop 时钟也已独立，问题不只是文件较长。建议把绑定结果作为简单的内部只读定义，构造时完成复制、资格检查和资源去重；Runtime 后续只消费定义并维护可变执行状态。先提炼绑定职责，不创建额外 ScriptableObject，不增加通用插件式注册框架，也不强行把每项能力拆成接口。

`LocomotionBoundAnimation` 构造还分别经脚相、Stop、周期轨迹路径查询同一动画的轨迹资格；在 Editor 中每条路径可能重复验证烘焙依赖。共同绑定可以获取一次有效轨迹后派生所需数据。此处是重复工作发现，没有性能测量，不能据此声称已经造成卡顿。

### 6. 动画请求跨越了“请求描述”和“执行对象”两种职责

**类型：协作边界不够直观，需保持执行顺序。**

`LocomotionAnimationRequest` 除 State、参数、BlendDuration 和 SampleTime，还携带 `LocomotionMovePlayback` 与整份播放上下文。`ActorAnimation.SubmitLocomotion` 第 192～196 行先应用 Mixer 参数／权重，再调用 LocomotionMovePlayback.Prepare 做倍率计算。于是 Move 的执行规则分散在 Runtime、ActorAnimation 和 MovePlayback 三处。

现有顺序有实际原因：倍率参考必须使用本次参数更新后的权重；刚创建的节点还需要挂到图上。不能仅删除请求中的对象，或提前按旧权重算倍率。

建议统一 Move 参数、子权重、非循环重新入场和倍率计算的归属，把播放层的通用提交与 Move 特有准备区分清楚。若最终将请求收紧为播放数据，必须先明确新图首次挂接和准备阶段，而非另加一个隐藏回调替代当前对象。本项可放在绑定与生命周期清理之后，单独验证实际图，避免与其他大改合并。

### 7. 调试与兼容代码占据主阅读路径

**类型：低优先级清理。**

- ActorLocomotion 的 Trace 实现位于主要 Tick API 前；可移至独立的调试 partial 文件，仍保留 Editor 条件编译与现有入口。
- ActorMotor 第 11～18 行仍展示不再驱动当前 Locomotion 的旧运动字段。保留序列化兼容，但隐藏或集中兼容区；删除要另做资产迁移，不直接移除字段。
- `LocomotionRunner.ClearIntent` 同时清模型速度，实际含义接近运动 Reset；`LocomotionMovementConfig.Sanitize` 实际验证并抛异常，不进行数值修正。整理内部命名／说明时保留现有公共 API 兼容。
- Runtime／绑定／播放相关类型可按职责分文件；文件移动必须保留 Unity GUID 和资产引用，不把搬目录本身当作结构改进。

## 建议保留的设计

- 共享 Runner：模式切换时保留动量，不让各 Runtime 的运动状态彼此过期。
- 固定 Control → Action → Motion → Animation → World 顺序，以及上一 Tick 实际运动反馈。
- Action owner／Locomotion owner：防止旧请求更改新会话，保护复制不引用将被销毁的 Runtime 节点。
- 输入方向、强度和 FacingDirection 分离；一次性与持续输入共用 Buffer。
- Set 状态机只维护决策，不积分速度、不读资产、不操作动画图；提议／准备／提交避免缺素材进入空状态。
- 方向优先 → 脚相择优 → 作者顺序；不加入素材缺失特例。
- Move 参考速度与实际反馈分离；Stop 按模型距离推进，零距离后普通时间收尾。
- 绑定一次与新 Runtime 更新配置的约定；不增加热更新入口和 Inspector 配置告警。
- 标签取得／释放机制。底层 TagContainer 是计数容器，不是普通集合；当前配对操作不会因别的系统同名标签就当然错误删除。

## 整理顺序与验证边界

1. 先固定姿势保护的跨会话约定，并统一已求值时间读取；新增变化曲线和跨模式保护合同。
2. 集中 Runtime 的只读绑定数据，保持过滤、排序、最近方向选片和资源身份；补绑定一次、重入和图重建合同。
3. 整理会话／退出／图失效清理，再集中 Tick 状态。分别保持冻结输入、重复请求、Action 覆盖／释放、Disable 和取消行为。
4. 统一 Move 播放准备的职责，验证更新权重后的倍率、非循环重新入场、首次图挂接以及同步样本。
5. 最后整理调试、兼容区与目录。旧字段／GUID／公共入口的迁移单独处理。

本轮只增加这份审查记录。静态核对了当前生产代码、相关合同测试、底层 Animancer 时间接口和标签计数实现，没有运行新的 Unity 回归或修改角色配置。当前良好的运行体感是用户反馈；上述边界风险仍需相应合同或人工场景验证，不能用体感反馈替代所有生命周期验证。

## 审查后确定的交接规则

作者明确选择：交接时一律冻结当前基础姿势；Idle 没有独立显式配置，不作为保护时的后备资源。上述第 1 项的行为约定已确定，第 2 项的求值时间读取随实现统一。

ActorAnimation 已移除历史 Idle 和最后 Move 的保护缓存，保护时复制基础层有贡献的动画树及其权重，使用实际求值时间。混合未完成时保留整个基础层混合结果；Action 层保持独立。旧 IdleClip 请求入口仍保留兼容，但不参与保护决策。保护修改阶段的运行时和 Editor／测试程序集编译通过；实际图测试因 Unity Editor 许可证不可用未执行。

## 后续结构整理已实施

本次按确认计划完成绑定定义、生命周期、Tick 状态、Move 准备与文件布局五个阶段。上文保留为审查时的证据，旧位置与行号不再代表当前文件布局。当前职责、合同验证和未执行的原生图／人工检查见[结构整理交付记录](CombatSample_Locomotion_Structure_Refactor_2026-10-05_zh-CN.md)；架构说明同步更新。
