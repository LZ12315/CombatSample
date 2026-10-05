# CombatSample Locomotion Implementation Roadmap v1

> 归档于 2026-10-05。正文中的状态、待办和验证结果保留为当时记录；当前实现见[现行架构](../../Current/CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)，最新验证与未完成检查见[提交审查](../../Current/CombatSample_Locomotion_Commit_Review_2026-10-05_zh-CN.md)。源码链接用于导航，历史成员和行号不代表当前代码。

> 对应[《Locomotion 最终架构 v1》](../../Current/CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)。阶段 0 是历史基线；阶段 1～3 的移动与基础动画链路保留，当前仅保留 Move 速度匹配；先前阶段 4 的脚相和阶段 5 的 Stop Distance Matching 代码已移除。Runtime、Editor（含测试）及非 Editor 静态编译通过；Unity Test Runner、角色素材接入和场景视觉验收待完成。

## 目标与执行原则

最终建立两条清晰链路：

```text
Player / AI
    → ActorLocomotion
    → LocomotionMotionRequest
    → ActorMotor
    → KCC

LocomotionRuntime
    → LocomotionAnimationRequest
    → ActorAnimation Layer 0
```

保持 Action Layer 1、Motion Policy、Velocity/RootMotion owner、KCC 和 Motor 仲裁的既有权威。正式角色使用同一套运行逻辑；有效基础 Move 独立支撑起步、停止和转向，Start/Stop/Pivot 各自可选。基础 Pose 缺失和已配置条目损坏记录为缺口，空可选列表合法。

实现按小阶段推进。纯数据、选择、积分和生命周期优先使用 EditMode 合同测试；依赖真实 Animancer、KCC、Prefab 和场景表现的部分由 Unity 编辑器与人工清单验证。本文不安排额外命令行 Unity 编译流程。

## 0. 锁定当前基线

**状态：已完成静态盘点。** 详见[阶段 0 实施基线](CombatSample_Locomotion_Stage_0_Baseline_2026-09-25_zh-CN.md)。该文档保留当时的 `LocomotionModeAsset`、Fallback 和资源名称，作为历史记录，不回写成新设计。

已锁定：

- Player/AI Intent 调用点与 Action 准入读取点；
- Motor 水平、垂直、旋转仲裁；
- Policy、TimeScale、HitStop、Abort/Disable 生命周期；
- Kiana FreeMove、Jaeger Strafe 与 Action Root Motion 样本；
- Kiana 缺少 Start/Pivot 等素材事实；
- 需要接入的 Locomotion AnimationAsset 和 Move Root Motion 轨迹。

## 1. 迁移 Intent 所有权与 Motion Request

**状态：主代码已落地；Unity 编译、场景回归结果尚待用户在编辑器中确认并回写。**

已实施目标：

1. Player 与两个 NodeCanvas 移动任务向 `ActorLocomotion` 提交 Intent；Control 锁定本 Tick 快照，Action 条件读取同一快照。
2. `LocomotionRunner` 归 ActorLocomotion 所有；Motor 通过只读 `LocomotionMotionContext` 提供有效 dt、Ground、Policy/owner 与上一 Tick 发布结果。
3. `LocomotionMotionRequest` 只提交世界水平速度和目标世界旋转；Motor 不读取 Locomotion 配置。
4. Motor 消费 Request 后应用 Locomotion/Air Scale，保留 Velocity、Action Root Motion、Locomotion+Impulse 和旋转优先级。
5. Motor 发布 Requested/Actual 与同一求解区间的来源、Policy、时间比例和外部运动事实。
6. dt 为 0 时不消费 Intent、不积分、不补帧；取消、控制对象切换和 Disable 清理对应快照与 owner。

阶段 2 不重新建立 Motor Intent 或第二条移动链路。

## 2. 重构 LocomotionAsset、显式 Facing 与 Runtime 边界

**状态：实现完成。** 代码、资源与场景序列化迁移已落地，静态合同已核对；Unity 编译和 Kiana/Jaeger 实际移动按约定由用户在编辑器中确认。

### 2.1 显式 Facing Intent

1. 将 `FacingDirection == Vector3.zero` 的语义改为保持当前朝向，不再隐式回退到 MoveDirection。
2. 普通 Player 移动显式提交移动朝向；Lock-on 提交目标朝向，并允许 Idle 时仅提交 Facing。
3. AI Task 显式选择面向移动方向、目标方向或保持朝向。
4. 从 Runner 移除 `LocomotionStyle.FreeMove/Strafe` 分支。1D/2D 动画类型不参与 Gameplay 旋转决策。

### 2.2 Asset 层次与序列化迁移

建立：

```text
abstract LocomotionAsset
├─ Priority / EntryConditions / SelfTags
├─ MovementConfig
└─ MoveDefinition

LocomotionMixerAsset : LocomotionAsset
LocomotionSetAsset   : LocomotionAsset
├─ TransitionBlendDuration
├─ Start[]
├─ Stop[]
└─ Pivot[]
```

迁移要求：

- 旧具体脚本 GUID 优先迁给 `LocomotionSetAsset`，使现有 Kiana/Jaeger 资源继续实例化为具体类型；抽象基类使用新 GUID；
- 保留 Kiana、Jaeger 资源 GUID 和 Actor 序列化引用；
- 将 `fallbackMode + candidateModes` 迁为单一候选列表；若字段重命名，使用明确的序列化迁移而不是让引用丢失；
- 删除单体 AnimationSet 中的 Jump/Fall/Land；Jump/Land 仍由 Action 负责；
- 不创建独立 MovementConfig、MoveDefinition 或 AnimationSet `.asset`。

### 2.3 MoveDefinition

1D：

```text
Parameter = HorizontalSpeed | VerticalSpeed
Sample = AnimationAsset + float Threshold + Sync(default true)
```

2D：

```text
Parameter = LocalVelocity
Sample = AnimationAsset + Vector2 Threshold + Sync(default true)
```

要求：

- 1D/2D 只描述 Move；
- Idle 由零阈值样本表达；
- Loop 读取源 Clip；
- 阈值有限、唯一，并按参数类型验证；
- Sync 是每个样本可见配置，Runtime 建图时映射到 Animancer 同步成员资格。

### 2.4 Set 过渡数据

```text
StartEntry = AnimationAsset + TargetLocalDirection
StopEntry  = AnimationAsset + SourceLocalDirection
PivotEntry = AnimationAsset + SourceLocalDirection + TargetLocalDirection
```

方向统一使用以当前 Facing 为基准的局部二维向量。Pivot 角度由 Source/Target 计算。第一版不加入每条动画的 SpeedRange、Foot、ExitTime、PlaybackSpeed、Priority、Condition 或单独 Fade。

### 2.5 Runtime 与共享移动状态

1. Asset 通过工厂创建 `LocomotionMixerRuntime` 或 `LocomotionSetRuntime`；ActorLocomotion 只调用共同 Runtime 接口，不判断具体子类。
2. 每 Actor、每 Asset 缓存一个 Runtime；切换调用 Exit/Enter，Disable/Destroy 统一 Dispose。
3. Gameplay 速度与旋转积分状态由 ActorLocomotion 共享，切换 Asset 不归零，也不在各 Runtime 中保留多份过期速度。
4. 保持阶段 1 已实现的加速、减速、反向输入、空中控制和有效 dt 合同。Ground 与 Air Asset 使用相同的 `MaxSpeed / Acceleration / Deceleration / RotateSpeed / TurnResponseTime` 结构，并分别配置自己的数值；MaxSpeed 是目标控制上限，不硬截断继承动量。

当前迁移值为：Kiana Ground `5 / 20 / 32`，Jaeger Ground `7 / 25 / 45`；Kiana Air `2 / 6 / 3`，Jaeger Air `2.8 / 6 / 3`。所有资产的 `RotateSpeed` 为 600，`TurnResponseTime` 为 0.08 秒。Air 的 MaxSpeed 已折算旧 0.4 控制系数，Motor 仍按 Policy 独立应用 `AirLocomotionScale`。

### 2.6 选择时序与无候选行为

1. Control 只锁定 Intent，不再选择 Asset。
2. Action 推进后先调用 `Motor.BeginMotion`，处理 `ForceUnground` 并得到当 Tick Ground/Policy 上下文。
3. Locomotion 使用该上下文选择一次 Asset，再构建 Request；不得在 Action 前后选择两次。
4. 选择遵守 Conditions、Priority、当前候选稳定保持和 authored order。
5. 删除全局 Fallback。无候选时退出当前 Runtime、释放 SelfTags、不给出普通移动贡献并报告可定位错误。
6. 同 Tick Action 使用 Tick 开始时已生效的 Locomotion SelfTags；本次选择的新标签从下一次 Action 决策起可见。
7. 空 EntryConditions 表示该显式候选无条件成立，但不获得 Fallback 的特殊优先级。

**代码验证：** 覆盖 Intent 显式 Facing、1D/2D 数据校验、Set 条目方向、同优先级稳定选择、起跳当 Tick 选择 Air、无候选清理、Runtime Enter/Exit/Dispose、Asset 切换速度连续、dt=0 与序列化迁移。

**阶段出口：** 工作区不再存在 Gameplay `LocomotionStyle`、隐式 Facing 或全局 Fallback；Kiana/Jaeger 引用保持；两类 Asset 能沿阶段 1 的唯一链产生相同或可解释迁移后的 Gameplay 运动。Unity 编译和 Kiana/Jaeger 实际移动由用户在编辑器中检查。

## 3. 基础 Locomotion 动画 Runtime

**状态：代码实现已落地，Unity 合同测试及角色视觉验收待完成（2026-09-28）。** 本阶段由用户在 Unity 配置角色动画样本；此次实现没有修改现有 Locomotion/Animation 资源、场景、Prefab、Importer 或资源 GUID。

### 3.1 ActorAnimation Layer 0 接口

1. 为 ActorAnimation 增加窄的 Locomotion Layer 0 提交接口；Animancer Graph、Layer 和最终状态所有权仍属于 ActorAnimation。
2. Locomotion Runtime 只提交播放目标、Mixer 参数、时间、淡入淡出和必要标识。
3. Action Layer 1 覆盖期间 Locomotion 状态继续运行；Action 退出后显露当前正确基础姿态，不重启整个 Locomotion。

### 3.2 Move Mixer

1. 根据 MoveDefinition 创建并缓存 `LinearMixerState` 或 `DirectionalMixerState`。
2. 按阈值添加 AnimationAsset 的 Clip；`Sync=false` 的子状态退出同步。
3. 非循环子 Clip 在重新变为有效样本时正确重启，不因零权重期间已播放完成而停在末帧。
4. Mixer 参数使用 Gameplay/合格反馈速度；1D/2D 不改变移动和 Facing 规则。

阶段 3 实际使用共享模型水平速度乘当前 Locomotion/Air Policy Scale；2D 使用 Tick 开始时的 Facing。Air VerticalSpeed 来自 Motor 已准备请求，并除去已应用的 MovementTimeScale。没有使用 ActualSolvedVelocity 做播放速度匹配；LinearMixer 的 ExtrapolateSpeed 显式关闭。默认 Clip 速度保持 1，Sync 仅使用 Animancer 基础同步。

### 3.3 Set 状态机

`LocomotionSetRuntime` 固定管理 `Move / Start / Stop / Pivot`：

- 从静止出现输入且有有效 Start 时，选择最近 `TargetLocalDirection` 的样本；
- 失去输入且有有效 Stop 时，以积分前的模型运动方向选择最近 Source；
- 强反向输入且有有效 Pivot 时，以 Source/Target 方向关系选择样本；
- 先提出下一状态，Runtime 选择并准备有效 Clip 后再提交；对应列表为空时继续 Move，不进入空状态、不记录待补播过渡；
- Start/Stop/Pivot 完成后均返回当前模型参数的 Move；无 Stop 时由 Move 随模型速度减速到 Idle，Stop 提前完成也不强制零速；
- 新输入可打断 Stop；Action/Asset 切换可中断瞬态；
- 触发阈值、优先级、中断与完成规则由代码统一维护，不暴露任意 Condition 图。

使用统一 `TransitionBlendDuration` 和明确的 Clip 完成边界，不要求相位、出口或刹车标记。Ground 基础播放需要零速和非零 Move 样本；Air 不强制地面 Idle。空过渡组合法，损坏组报告并停用该组；基础 Pose 失败时的安全保护不算视觉验收通过。

**阶段出口：** MixerAsset 和只有 Move 的 SetAsset 均能持续播放；部分及完整过渡配置按实际能力工作。Action 覆盖、HitStop、Asset 切换、Disable/Enable 无空白帧、重复 Enter 或遗留状态。角色按其有效基础 Pose 和已配置过渡验收，不要求配置所有过渡类型。

### 3.4 本次实施与验证记录

- Layer 0 使用独立会话 token；过期请求及释放不能影响新会话。Runtime 缓存动画状态，ActorAnimation 负责接入和一次 Graph 求值。
- Motion 清除 Control Intent 前保存动画快照，包括积分前速度；动画阶段不再次选择 Asset、积分或读取 Pending Intent。
- 代码阈值为 MoveStrength > 0.01、静止速度 <= 0.1 m/s、Pivot 旧速度 >= 0.5 m/s 且反转角 >= 120°。方向评分相同保持 authored order，Pivot 不因持续相同输入反复触发。
- 新 Action 覆盖结束瞬态，覆盖及释放时重建输入边沿；隐藏的 Move 继续更新。动画有效 dt 为 0 时不处理转换或 Clip 完成。
- Actor/Locomotion/Animation 单独 Disable、Driver abort 均撤销动画会话；ActorLocomotion 退出并 Dispose Runtime，Animation 单独 Disable 则重置动画状态并在 Enable 后重绑。Graph 销毁后重建缓存状态。
- 释放前使用 ActorAnimation 自有 Idle 状态保护姿态；没有 Idle 时独立复制最近基础姿态的 Clip 时间和 Mixer 权重，冻结保护状态。冷启动全空配置报告无有效基础姿态。
- 播放校验与轨迹覆盖校验分开：无效 Clip、方向或阈值拒绝整组相关样本，不静默剔除；Move 未烘焙轨迹报告为速度匹配缺口，不阻止有效 Clip 播放。
- 2026-10-01 可选过渡收敛：状态决策与提交分开，素材在绑定时验证并缓存；空 Start/Stop/Pivot 不报告缺失，基础 Stop 不要求轨迹。移除 Stop 完成后的零速锁存，返回 Move 时使用当前模型速度。Gameplay 接口、序列化字段和动画请求结构保持不变。

| 验证 | 结果 |
| --- | --- |
| Runtime 与 Editor（包括新增测试）静态 C# 编译 | Passed，使用现有 Unity/Animancer 引用；不替代 Unity 导入编译 |
| Unity 导入 / 编译 | Passed，2026-09-28 用户确认没有编译错误；不代表运行或视觉验收通过 |
| EditMode 决策与合成 Clip 合同测试 | 已新增到 `LocomotionAnimationContractTests`，未在 Unity Test Runner 执行 |
| 外部执行纯决策测试的尝试 | NUnit 与 .NET 环境不兼容，未完成；已停止该路径，不计入通过结果 |
| Kiana/Jaeger 视觉验收 | Pending，素材仍由用户接入 |

2026-10-01 磁盘检查：Kiana Normal 已接入 Idle/Walk/Run、一个 Start 和一个 Stop，Pivot 为空且合法；这些本地资源由用户配置，尚未计作视觉验收通过。Jaeger Normal 的 Move 为空，Start 有一个空动画、零方向条目；两个 Air Asset 的 Move 仍为空。Jaeger 的空 Stop/Pivot 不列为缺口。基础 Pose 与损坏条目仍待作者处理，Test Runner 和角色视觉验收未完成。

## 4. Move 速度匹配

**状态：代码已收敛；静态编译通过，Unity Test Runner 与角色视觉验收待完成。**

1. Move 参数继续来自共享模型速度；1D HorizontalSpeed 在样本有效范围内做 0.1s 的参数限速平滑，2D 与 VerticalSpeed 保持原语义，权重由 Animancer Mixer 计算。每个样本的 `Sync` 只配置 Animancer 原有同步成员资格，运行时不维护脚相时钟。
2. 绑定 Runtime 时校验 Move AnimationAsset 的累计 Root Motion 轨迹。循环样本用完整周期的局部位移；按当前权重合成参考速度，基础同步成员计入加权周期速率，非同步成员使用各自周期时长。Air VerticalSpeed 的 PlayRate 固定为 1。
3. 只读取上一 Tick 合格的 `ActualSolvedVelocity`，移除该结果所属 Tick 的 MovementTimeScale 后调整 PlayRate。倍率限制在 0.5–1.5，平滑时间为 0.1s；参考速度低于 0.1m/s、轨迹缺失或 Move 样本非循环时保持 1 并报告缺口。
4. Action 覆盖、Asset/Policy/Ground/owner 变化及暂停恢复使旧反馈失效。基础 Move 持续维护；普通 Locomotion 不提交动画 Root Motion 位移。

**阶段出口：** Walk/Run/Sprint 与方向样本的动画步速大体协调，战斗打断和恢复及时；贴墙时动画仍能推进。实际观感由角色素材人工验收。

## 5. 基础 Stop

阶段 5 曾实现 Stop Distance Matching，现已从当前代码和制作流程移除。Stop 是可选表现：有有效 Clip 时按积分前模型方向选择，从时间 0 按正常动画速度播放，完成后回到当前模型参数的 Move。无 Stop 时 Move 自然表现减速到 Idle；Gameplay 减速规则不变，不要求 Stop 轨迹、BrakeEnd、SettleEnd、脚相窗口或停止距离曲线。

## 6. 集成与验收

- 保留阶段 1～3 的唯一 Intent → Motion Request → Motor/KCC 链路、Layer 0 会话、Action Layer 1 覆盖及保护姿态。
- 代码检查包括输入释放后的正常刹车、Asset 选择、Motion 与动画时间域、Move 权重与速度匹配，以及只有 Move、部分过渡、完整过渡三类 Set 配置；保留过期 owner 拒绝、HitStop 和 Disable/Enable 检查。
- Runtime、Editor（含合同测试）和无 Editor 定义的静态编译通过；未运行 Unity Test Runner、命令行 Unity 构建或外部 NUnit。静态编译不代表 Animancer Graph、KCC 或角色视觉通过。
- 当前 Kiana Normal 已配置基础 Move 与 Start/Stop，无 Pivot；重点检查起步、松开减速、大幅转向、连续输入与战斗打断。Jaeger Ground 和两个 Air 的基础样本仍待配置。本轮不修改资源、场景、Prefab、Importer 或 GUID，也不覆盖已有本地资源改动。
- 2026-10-01 反向闪帧复查：数值推演发现 1D Move 直接使用反向刹车速度会短暂给 Idle 很高权重；新增水平参数平滑、连续积分后求值的合成 Clip 测试，以及 Editor 中可手动开启的诊断。第一份 60 Tick 日志只覆盖静止，诊断已改为等待反向/输入释放后录制 120 个有效 Tick，并在 Graph 求值后记录层权重和 Clip 时间。移除 Motion 绑定阶段的重复动画覆盖汇总，由 Runtime 报告具体配置与轨迹问题。静态编译和数值推演不作为本次闪帧的视觉修复验收；Test Runner、角色反向/松手/HitStop 检查仍待执行。
- 完成 Unity Test Runner 与 Kiana/Jaeger 的 Walk/Run/Sprint、不同初速停止、战斗打断、HitStop 和生命周期视觉检查后，记录真实缺口，再决定是否需要新的专项功能。
