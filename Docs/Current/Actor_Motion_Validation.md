# Actor Motion 当前合同与验证清单

更新：2026-10-05。本文描述现行运动与动画协作的回归合同，不将历史验收或当前编译结果当作全部测试通过。

最新编译、纯逻辑测试、原生测试阻碍和 Kiana 配置见[阶段提交审查](CombatSample_Locomotion_Commit_Review_2026-10-05_zh-CN.md)。2026-08-29 E3-H 结果与后续阶段调查完整保存在[验证历史](../Archive/Locomotion/Actor_Motion_Validation_2026-10-01.md)。

## 1. 当前执行链

```text
Player / AI → ActorLocomotion 提交 Intent
Control → 锁定本 Tick 输入
Action → 决策并更新运动 owner / policy
Motor.BeginMotion → ForceUnground 与只读上下文
Locomotion → 选择资产、共享 Runner 积分、缓存运动请求和动画快照
Motor → 仲裁并准备 Translation / Rotation
Animation → 使用快照和当前 Action owner、垂直速度、已求值脚相
          → 提交基础层与 Action 层，求值动画图
World → KCC 与 Actor 分离
Finish → 发布请求、实际运动与来源事实，供后续 Tick 反馈
```

[ActorLocomotion](../../Assets/Scripts/Actor/ActorLocomotion.cs)负责输入、选择和基础请求；[ActorMotor](../../Assets/Scripts/Actor/ActorMotor.cs)负责运动仲裁，KCC 负责世界求解；[ActorAnimation](../../Assets/Scripts/Actor/ActorAnimation.cs)负责最终图、层与混合。[当前架构](CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)描述详细数据与生命周期。

## 2. 必须保持的运动合同

| 范围 | 合同 |
| --- | --- |
| 水平仲裁 | HorizontalVelocity owner ＞ Action 轨迹 Root Motion ＞ Locomotion + HorizontalImpulse。被覆盖贡献不追偿遗漏位移。 |
| 垂直 | VerticalVelocity owner 优先，否则 Ballistic；owner 存在时冻结 Ballistic 时间演化。最后一个垂直 owner 释放时清零 Ballistic；有效向上 Add／Set 应解除接地，撞顶只截断正向 Ballistic。 |
| 旋转 | Scripted ＞ Root ＞ Locomotion；Root／Scripted owner 按已有栈规则恢复，失效 token 不影响当前 owner，被覆盖的旋转不补播。 |
| Root Motion | Action 使用 actor-local 轨迹区间位移；普通 Locomotion 的根轨迹用于动画匹配，不直接驱动 Capsule，不读取 Animator delta 作为 Gameplay authority。 |
| 世界反馈 | Requested 与 ActualSolved 分开；碰撞阻挡的位移不偿还。反馈必须保留对应求解区间的来源与时间倍率。 |
| 时间 | MovementTimeScale 为零时运动与相关动画时钟冻结，恢复不补帧、不重复执行 Action gameplay 副作用。 |
| 清理 | Cancel、Disable、Dispose、Action 中断与 Driver abort 配对释放 owner、policy、标签和其他临时状态；旧 owner 请求不进入新会话。 |

## 3. Locomotion 与匹配合同

- 一个 Tick 只锁定一次输入、选择一次资产并积分一次运动。重复 Build 返回缓存；无 BeginControlTick 时采用无输入。冻结保留可恢复的一次性输入，更新或清空优先；零动画时间不消费决策边沿。
- Mixer 与 Set 使用同一个候选列表。条件、优先级、当前合法候选稳定保持和作者顺序决定选择；没有隐含 Fallback。Facing 来自显式输入，不由 Move 类型猜测。
- Move 参数源可为模型速度或输入。输入为零时输入模式参数为零，速度模式继续读取模型速度；播放倍率匹配独立使用合格的上一 Tick 实际运动反馈。VerticalSpeed 保持中性倍率。
- Start／Stop／Pivot 独立可选。缺少过渡时使用 Move；未能进入的过渡不排队。新输入可以打断 Stop，Action 覆盖与释放不排队补播旧瞬态。
- Stop 方向来自积分前模型速度。脚相参考来自基础层贡献最大的已求值动画，包含所有父节点与层权重；主动画无标记时返回未知，不改选较小权重的动画。
- Stop 候选按方向、实际入场脚相、作者顺序选择。Time 从零播放；Distance 由积分后模型速度预测刹车距离并反查绑定曲线，碰撞后的实际提前停止不改变这个时钟。零距离采样停止点一次，再按普通时间播放收尾。
- 交接保护冻结实际已求值的基础层混合姿势；快照独立于旧 Runtime，Action 层保持独立，新基础动画正常混合接入。没有显式 Idle 配置也不推断另一个保护动画。
- Runtime 配置构造时固定；重入和图重建不刷新。禁用／取消 Simulation 会释放 Runtime；新 Runtime 才读取新配置。绑定、会话、节点和标签清理各自保持幂等。

## 4. 自动验证入口

在可运行的 Unity Editor 中执行现有 EditMode 合同：

| 套件 | 主要范围 |
| --- | --- |
| LocomotionContractTests | 输入、运动积分、选择、冻结、重复 Build 与生命周期。 |
| LocomotionSetDecisionTests | Start／Stop／Pivot 决策、阈值、输入边沿和 Action 中断。 |
| LocomotionAnimationContractTests | 真实 Animancer 图、脚相参考、交接保护、owner、重建与绑定。 |
| LocomotionMatchingContractTests | 反馈资格、参数／权重准备、Sync、非循环重入和倍率。 |
| AnimationLocomotionDataTests | 停止点、曲线、脚标记、脚相、覆盖与距离时钟。 |
| RootMotionTrajectoryTests、RootMotionBakerTests | 轨迹采样、验证与烘焙。 |

编译不替代 Test Runner。独立 Mono 进程只用于可执行的纯逻辑断言，不提供 Unity 原生对象或动画图，不能将其通过数视为完整套件结果。

## 5. 人工回归

| 场景 | 检查 |
| --- | --- |
| 快速启停、跑稳后松键、混合中松键 | Stop 候选、脚相与入场时间符合配置；观察是否仍出现换脚、倒腾腿或姿势跳变。 |
| 连续转向、刹车中再次输入 | 运动响应正常，Stop 可打断，相同反转不重复锁存 Pivot。 |
| 地空切换与 Action 结束 | 基础层交接保留当前混合姿势，未恢复旧瞬态或重复 Enter；缺可选过渡仍正常运行。 |
| 贴墙或碰撞提前停止 | 实际位移可以小于模型预测；没有追偿位移。Stop 距离时钟继续遵循模型，按约定评估观感。 |
| HitStop、暂停与恢复 | 运动、动画和 Action 副作用冻结一致；恢复没有补帧。 |
| Actor／Locomotion／Animation 独立 Disable、图销毁与重建 | owner、节点、标签与请求清理正确；旧 Runtime 销毁不改变独立保护快照。 |
| Action Root Motion 贴墙、运动／旋转 owner 覆盖与释放 | 不补偿被覆盖或阻挡的区间，不瞬移，不接受失效 token。 |
| Actor 碰撞与注册顺序 | 改变 Enable 顺序后重复接触，分离结果保持确定，不依赖注册顺序。 |

每次记录明确配置、场景、日期和实际检查项。最新 Kiana 运行体感良好不表示 Jaeger、所有地空模式或全部生命周期路径已验收；脚相择优也不以任意姿势完全无缝为通过条件。
