# CombatSample Locomotion Implementation Roadmap v1

> 对应[《Locomotion 最终架构 v1》](CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)。阶段 0 是历史基线；阶段 1/2/3/4 代码已在工作区落地。阶段 3/4 Unity 编译由用户确认通过，阶段 4 Runtime/Editor 静态编译通过；Unity Test Runner、角色素材接入和场景视觉验收仍待整体验收，不具备阶段 3/4 完整出口。

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

保持 Action Layer 1、Motion Policy、Velocity/RootMotion owner、KCC 和 Motor 仲裁的既有权威。正式角色使用同一套运行逻辑；素材不足记录为制作缺口，不建设另一套简化 Locomotion。

实现按小阶段推进。纯数据、选择、积分和生命周期优先使用 EditMode 合同测试；依赖真实 Animancer、KCC、Prefab 和场景表现的部分由 Unity 编辑器与人工清单验证。本文不安排额外命令行 Unity 编译流程。

## 0. 锁定当前基线

**状态：已完成静态盘点。** 详见[阶段 0 实施基线](CombatSample_Locomotion_Stage_0_Baseline_2026-09-25_zh-CN.md)。该文档保留当时的 `LocomotionModeAsset`、Fallback 和资源名称，作为历史记录，不回写成新设计。

已锁定：

- Player/AI Intent 调用点与 Action 准入读取点；
- Motor 水平、垂直、旋转仲裁；
- Policy、TimeScale、HitStop、Abort/Disable 生命周期；
- Kiana FreeMove、Jaeger Strafe 与 Action Root Motion 样本；
- Kiana 缺少 Start/Pivot 等素材事实；
- 需要补做的 Locomotion AnimationAsset、轨迹和脚相数据。

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

- 从静止出现输入时选择最近 `TargetLocalDirection` 的 Start；
- 失去输入时，以进入 Stop 前的运动方向选择最近 Source；
- 强反向输入时，以 Source/Target 方向关系选择 Pivot；
- Start/Pivot 完成后返回 Move；Stop 完成后返回 Move 的零速样本；
- 新输入可打断 Stop；Action/Asset 切换可中断瞬态；
- 触发阈值、优先级、中断与完成规则由代码统一维护，不暴露任意 Condition 图。

阶段 3 先使用统一 `TransitionBlendDuration` 和明确的 Clip 完成边界，随后接入 AnimationAsset 的相位/标记数据。缺失样本报告资源缺口；运行时安全保护不算正式视觉验收通过。

**阶段出口：** MixerAsset 能持续播放 Move；SetAsset 能完成基础 Move/Start/Stop/Pivot 生命周期；Action 覆盖、HitStop、Asset 切换、Disable/Enable 无空白帧、重复 Enter 或遗留状态。目标角色只有在对应素材接入后才能通过视觉出口。

### 3.4 本次实施与验证记录

- Layer 0 使用独立会话 token；过期请求及释放不能影响新会话。Runtime 缓存动画状态，ActorAnimation 负责接入和一次 Graph 求值。
- Motion 清除 Control Intent 前保存动画快照，包括积分前速度；动画阶段不再次选择 Asset、积分或读取 Pending Intent。
- 代码阈值为 MoveStrength > 0.01、静止速度 <= 0.1 m/s、Pivot 旧速度 >= 0.5 m/s 且反转角 >= 120°。方向评分相同保持 authored order，Pivot 不因持续相同输入反复触发。
- 新 Action 覆盖结束瞬态，覆盖及释放时重建输入边沿；隐藏的 Move 继续更新。动画有效 dt 为 0 时不处理转换或 Clip 完成。
- Actor/Locomotion/Animation 单独 Disable、Driver abort 均撤销动画会话；ActorLocomotion 退出并 Dispose Runtime，Animation 单独 Disable 则重置动画状态并在 Enable 后重绑。Graph 销毁后重建缓存状态。
- 释放前使用 ActorAnimation 自有 Idle 状态保护姿态；没有 Idle 时独立复制最近基础姿态的 Clip 时间和 Mixer 权重，冻结保护状态。冷启动全空配置报告无有效基础姿态。
- 播放校验与轨迹覆盖校验分开：无效 Clip、方向或阈值拒绝整组相关样本，不静默剔除；未烘焙轨迹报告为后续阶段缺口，不阻止有效 Clip 播放。

| 验证 | 结果 |
| --- | --- |
| Runtime 与 Editor（包括新增测试）静态 C# 编译 | Passed，使用现有 Unity/Animancer 引用；不替代 Unity 导入编译 |
| Unity 导入 / 编译 | Passed，2026-09-28 用户确认没有编译错误；不代表运行或视觉验收通过 |
| EditMode 决策与合成 Clip 合同测试 | 已新增到 `LocomotionAnimationContractTests`，未在 Unity Test Runner 执行 |
| 外部执行纯决策测试的尝试 | NUnit 与 .NET 环境不兼容，未完成；已停止该路径，不计入通过结果 |
| Kiana/Jaeger 视觉验收 | Pending，素材仍由用户接入 |

当前磁盘资源缺口：Kiana Normal 的 Move/Start/Stop/Pivot 均空；Jaeger Normal 的 Move/Stop/Pivot 为空，Start 有一个空动画、零方向条目；两个 Air Asset 的 Move 样本为空。至少有效 Ground Idle/Move 和 Air Move 接入后，才能检查基础姿态；完整 Set 验收还需所承诺方向的 Start/Stop/Pivot。缺口未关闭时不得标记阶段 3 完成。

## 4. AnimationAsset 运动元数据、脚相与 Move 匹配

**状态：代码已实现，2026-09-28 Runtime/Editor（含新增合同测试）静态 C# 编译通过，同日用户确认 Unity 导入/编译无错误。Test Runner、角色烘焙与视觉出口 Pending。**

1. 扩展 `AnimationAsset` 的可复用烘焙数据：保留累计 Root Motion 轨迹，并增加 Move 周期、左右脚接触/脚相、循环映射和必要的过渡衔接点。能稳定计算的数据自动烘焙；不可靠的语义允许少量人工修正。
2. Move 样本按同一脚步语义同步。`Sync` 只决定某个样本是否加入同步组；Idle 通常退出同步。Start/Pivot 返回 Move 时接入兼容周期，不默认跳到归一化时间 0。
3. 根据 `RootMotionTrajectory.TrySample/TryExtract` 计算各子样本的局部参考速度，按 Mixer 权重和同步速率合成参考速度。
4. 只使用来源合格的上一 Tick `ActualSolvedVelocity` 做有界、平滑 PlayRate 微调。Action Root Motion、Velocity owner、显著冲量、平台携带、Actor 分离和 Unknown 来源均不可冒充自主步速。
5. Asset、Policy、Ground 或 owner 改变时丢弃失效反馈，先用模型速度和中性 PlayRate；取得新合格结果后再恢复匹配。
6. 普通 Locomotion 不调用 Motor 的 `SubmitTrajectoryRootMotion`，Action Root Motion 行为保持不变。

**代码验证：** 周期跨界、Sync 成员资格、相位接入、参考速度合成、反馈资格、零速、时间比例换算和 Action 覆盖解除后的首 Tick。

**阶段出口：** Walk/Run/方向 Mixer 在速度变化时脚步连续；贴墙近零不会永久冻结跑姿；半速、暂停恢复和 Action 覆盖无双重缩放。实际落脚质量由目标角色人工验收。

### 当前落地边界

- AnimationAsset 内嵌自动脚接触/周期候选与独立人工修正；Inspector/Bake 窗口提供分析、候选复制、出口确认和过期状态。复用现有 Humanoid Rig 采样，分析不提交或重建 Action Root Motion 数据，失败不覆盖有效结果。
- 左/右触地对应共同脚相 0/0.5，通过分段映射跨完整 Clip 周期；自动识别首版要求一个可靠左右脚周期。Generic 自动骨骼识别不在本阶段范围。
- 项目内的 Linear/Directional Mixer 扩展只替换有效脚相组的同步，保留 Animancer 权重算法与 Sync 成员资格；相位在 Graph pre-update 随实际有效 dt 推进，不叠加内置归一化同步。Start/Pivot 正常结束使用已确认出口，打断不使用旧出口，Stop 保持阶段 3。
- 参数仍由模型速度选择；参考速度结合当前权重、共同周期速率和循环感知轨迹区间。合格上一 Tick 实际速度去除所属 MovementTimeScale 后计算倍率，默认 0.5–1.5、平滑时间 0.1s、参考速度下限 0.1m/s；Air VerticalSpeed 固定 1。
- Asset/会话、Ground、Policy、Action 覆盖变化及暂停恢复使旧反馈失效；覆盖期间仍维护 Move。缺脚相的组整体保持基础同步和中性匹配，缺轨迹保留 Clip，诊断去重，不算制作或视觉完成。
- 新增 `LocomotionMatchingContractTests`，未在 Unity Test Runner 运行；没有启动命令行 Unity 构建或外部 NUnit 测试宿主。资源文件、Importer、Prefab、Scene 和现有 GUID 未改。

制作步骤和整体验收清单见[当前 Actor Motion 验证文档](../Current/Actor_Motion_Validation.md#7-locomotion-阶段-4-检查整体验收时执行)。四个 Locomotion Asset 的空样本缺口继续保留；阶段 5 的 BrakeEnd/SettleEnd 与停止距离反查尚未实施。

## 5. Stop Distance Matching

1. 由共享 Gameplay 积分器提供与当前 Deceleration 完全一致的停止距离预测；Animation 阶段只读预测，不提前运行 Motion Tick。
2. 在 Stop AnimationAsset 中提供或校正 `BrakeEnd` 与 `SettleEnd`。从累计水平路径建立“时间 → 到 BrakeEnd 的剩余距离”查询。
3. 先按 SourceDirection 和当前脚相选择兼容 Stop，再在有效窗口内反查起播位置。后续采样只向前推进，不倒播。
4. 距离进入零阈值后推进到 BrakeEnd，再按动画时间播放原地收势到 SettleEnd，避免零距离平台卡帧或跳过尾段。
5. 新输入、Asset/Ground/Policy/owner 改变、碰撞阻挡或数据无效时安全退出/重建；不修改 Gameplay 刹车，不追偿被覆盖或碰撞损失的位移。
6. Start 不做 Distance Matching；Pivot 不用动画 Root Motion 反向驱动 Gameplay 转向。

**阶段出口：** 不同初速和减速度下，Stop Pose 与真实刹停在素材覆盖范围内一致；不倒播、不跳末帧、不在零距离卡住；Stop 中重按、墙体、Action 覆盖和 Asset 切换无瞬移。

## 6. 集成、资源闭合与文档归档

| 检查 | 必须看到的结果 |
| --- | --- |
| Intent | Player/AI 都经过 ActorLocomotion；移动与 Facing 语义明确；Action 读取同 Tick 快照。 |
| Asset 选择 | Ground/Air、Lock-on 等均由 Conditions + Priority 选择；起跳当 Tick 使用 Air 上下文；无隐式 Fallback。 |
| Runtime | Mixer/Set 平行运行；切换不丢速度；SelfTags、Enter/Exit/Dispose 数量正确。 |
| Motion | Accel/Decel/Turn 改变 KCC 移动；Requested 与 Actual 分开；Motor Policy 和 owner 优先级保持。 |
| Animation | Move 1D/2D、Start/Stop/Pivot 与 Air Mixer 正常；Jump/Land Action 正常；Action 覆盖退出后基础姿态正确。 |
| 匹配 | Move 不驱动 Capsule；外部位移不冒充步速；Stop 不修改 Gameplay 刹车或追偿碰撞。 |
| 时间 | HitStop 不消费 Intent、不推进 Runtime；恢复无补帧。时间比例变化无双重缩放。 |
| 资源 | Kiana、Jaeger 所承诺的方向覆盖、Clip、轨迹、脚相和标记齐全；缺口未关闭时不宣告角色完成。 |

完成代码合同与 Unity 人工验收后：

1. 更新 `Docs/Current/Actor_Motion_Validation.md` 的最终数据流和回归结论；
2. 将最终架构从 `Docs/Proposals` 移入 `Docs/Current`；
3. 将本 Roadmap 标记完成并归档；
4. 记录仍延后的 Warping、Foot IK/Lock、Motion Matching、复杂 Trajectory Matching 和 Start Distance Matching。
