# TurnToMove Start：方向基准与验证

## 范围

修复非锁定自由移动的“转身起跑 → 前向 RunLoop”方向交接。Motor 继续拥有实际位移和旋转；不开启 Animator Root Motion、不改 KCC、不按动画文件名或具体方向写固定补偿。

旧 Start 默认为 `Directional`，保持原来的仅选片行为。新的 `TurnToMove` 是显式选择，要求源匹配、依赖未过期的烘焙轨迹和非循环 Humanoid Clip；缺少条件时在 Runtime 绑定时警告并跳过。它不是通用 Generic、Strafe 或 Pivot 朝向扭曲系统。

## 原因与数据合同

2026-10-25 在当前 Unity 2022.3.62f3 Editor 中，用临时 Katana Rig／PlayableGraph 测量：

- Animator Apply Root Motion 关闭时，前向 Start 末尾及 RunLoop 开头的身体局部 yaw 约为 +3.02°。
- 左／右／后左／后右 Start 的末尾约为 -45.64°，相差约 48.66°。
- 方向性素材把一部分转向提取进根旋转。关闭应用 Root Motion 并不会自动将余下姿态转换为与前向循环相同的基准。
- 原运行时只用目标方向选片，没有恢复提取的旋转，也没有在切换前统一基准。

48.66° 是这组资源的观测值，不是代码常量。输入方向和根旋转总量也不是同一个概念：右起步的作者结束方向可以是 +90°，但烘焙的累计 yaw 约为 +138.66°。直接把累计 yaw 当作目标转角会再次混淆姿态基准。

每个 TurnToMove 条目填写素材结束时相对入场前方的行进／面向角度：0、左右 ±90、后方左右 ±180 等。`targetLocalDirection` 保留供旧 Directional 使用，Inspector 按 Facing Mode 显示对应字段。

## 运行规则

1. 按入场输入方向匹配候选；方向同分时优先 Motor 已选择的转向侧，区分 -180／+180；之后保留作者顺序。
2. 绑定时从有效烘焙旋转复制展开的 yaw 曲线。逐段展开，避免后方右转越过 180°后被插值为左转；不在运行时读取可变资产列表。
3. Start 采用显式动画时钟，Clip 与 yaw 在同一时间取样。暂停不推进，不在恢复时补帧。
4. 每个 Start Clip 有自己的动画 Job，在基础层混合前修正 Humanoid 身体的局部位置／旋转；不旋转 Actor、Animator Transform 或 Action 层。
5. 从入场世界基准平滑转换到 Motor-forward 基准。记提取 yaw 为 `R(t)`，作者结束角为 `A`，入场后 Motor 的实际累计转角为 `M(t)`，归一化动画时间为 `p`：

   ```text
   poseCorrection = R(t) - Lerp(M(t), A, SmoothStep(p))
   ```

   入场时抵消 Motor 已发生的旋转，恢复素材的入场世界姿态；结束时修正为 `R(end) - A`，消除不同提取基准的差异。结束修正不一定为零。慢转速时也不强制 Motor 瞬间转向目标，仍遵守 Gameplay 旋转限制。
6. `PoseWorldRotation` 使用本 Tick Motor 已合成的旋转，不使用积分前朝向，也不使用被其他 owner 覆盖的 Locomotion 提案。
7. 淡出的 Start 继续自己的对齐与时钟，到末帧后冻结。Action 依旧在独立覆盖层。
8. 普通松键／强反向／Action 使用原状态机规则。另外，输入偏离入场方向超过 45°、Facing 与移动不再一致、或旋转锁定／接管时，未完成的 TurnToMove 返回 Move，不逐帧重选或排队重播 Start。
9. 姿态保护保留实际已求值的 Clip 时间和 Job 修正，不能将新请求中尚未求值的修正写入快照。保护 Mixer 附图时不向子节点传播初始时间零，避免擦掉各叶的捕获时间。快照不引用旧 Runtime。

## 当前 Katana 配置

`Assets/Data/Locomotion/Katana/Locomotion_Katana_Normal.asset` 的 Start 配置为五个 TurnToMove 条目：前向 0、左 -90、右 +90、后左 -180、后右 +180。

任务开始时磁盘列表仍只有前向；本次将已经存在的四份 AnimationAsset 接入。运行时修复本身没有重写源动画；提交独立性复核发现，这四份资源依赖工作区已有的 `Run_Start_Fast_L/R/B_L/B_R.anim` 修改（包括关闭循环），因此后续补交这四个源 Clip 的现有内容。不修改模型、场景、Move 参数或运动响应参数。其他未提交资源修改保留；Stop 开关和 Turn 重烘焙不属于本次修复。

## 验证（2026-10-25）

- Unity Editor 原生编译完成，无本次 C# 编译错误。
- 新增 `LocomotionStartAlignmentTests`：13 项通过。覆盖入场／结束坐标合同、任意初始朝向、展开曲线与不可变绑定、后方左右择侧、Strafe／旋转接管排除、旧配置兼容。
- 通过临时只读预览对象执行真实 Katana SetRuntime、Runner、ActorAnimation 和 Animancer 图：初始 yaw 0／137°，输入 0、±90、±135、±179、180、45°，加一个低转速场景，共 19 个场景。所有 Start 均取样到末帧并回到 Move；末帧相对前向 Start 参考末帧的髋部旋转差均为输出精度下的 0°。其中 +179°实际选到后右，-179°选到后左。
- 同一原生预览链检查 90°改向、松键、Action owner 上下文打断、暂停恢复、结束会话后销毁 Runtime、未求值新请求后的快照、同 Runtime 重入。检查通过；三种快照／重入保护测得旋转和位置误差均为输出精度下的零。该检查不等于完整场景 Action gameplay 回归。
- 较大 EditMode 回归运行 196 项：185 通过、11 失败。新增 13 项全部通过，不能据此声称现有图生命周期套件全绿。

尚未解决的 11 项：

| 套件／用例 | 数量 | 现象 |
| --- | ---: | --- |
| LocomotionAnimationContractTests.FootPhaseBindings_EndSessionAndGraphDestructionReturnUnknown | 1 | 图销毁后访问无效 PlayableGraph |
| MotionAnimationPipeline_FollowsComposedMotorRotationInsteadOfLocomotionProposal | 2 | 测试 SendMessage("Awake") 产生 ShouldRunBehaviour 断言 |
| MotionAnimationPipeline_SessionLossResetsOnceAndRejectsStaleNotifications | 1 | 会话重置计数不符 |
| RuntimeSnapshot_AssetEditsReentryAndGraphRebuildKeepBoundExecutionData | 1 | 已销毁图上的姿态捕获 |
| Set_SelectsClosestDirectionsCompletesClipsAndDiscardsAnExitedTransient | 1 | TearDown 访问无效图 |
| SetReentry_RestartsTheCachedTransitionAndRetainsTheProtectedPose | 1 | 重入保护姿态不符 |
| StopDuringTransitionBlend_UsesTheDominantEvaluatedPose(True) | 1 | 混合姿态不符 |
| LocomotionMatchingContractTests.MovePreparation_* | 3 | 未附图 Mixer 的 Playable 为空 |

这些失败没有全部完成与任务开始前版本的对照归因，不宣称都是既有问题，也没有为使本任务测试全绿扩大修改范围。首次回归有 13 项失败；修复本次所需的快照子节点时间保留后，已有两项 PoseProtection 测试恢复通过。

临时测量脚本／测试驱动在交付前移除，不加入默认套件。原始本机结果位于 `%TEMP%/CombatSample-StartRuntime-v3.txt`、`CombatSample-StartTests-v2.xml`；本地临时文件不是仓库的持久验证依赖。

## 提交独立性复核（2026-10-27）

### 提交范围修正

`b0a516b1` 包含 Start 运行时实现，但遗漏了四个方向性 Start 的源 Clip 修改：提交前版本仍为循环动画，无法满足 TurnToMove 的绑定条件。后续补交这四个 `.anim` 的现有工作区内容，保留原 `.meta`／GUID，不把 Combat Start、大剑、攻击或 Air 资源顺带提交。左右源 Clip 还包含 Unity 序列化的展开曲线，不能只提交 `loopTime` 开关而让源 Clip 与既有烘焙哈希失配。

同时将以下不相关内容从提交结果中剥离，**只改 Git 暂存版本，不覆盖工作区文件**：

- Stop 的 `_overrideStopTime` 和 Turn_L／Turn_R 烘焙内容恢复为搬迁前版本；`Normal/` 目录和原 GUID 保留。
- `MotionStateSnapshot` 只保留 Start 使用的 `LockLocomotionRotation` 扩展；`IsValid`、`BrakeLocomotion`、`LocomotionTurnSpeedLimit` 及完整 Action 策略实现留作独立的本地修改。
- 当前已提交 Motor 没有 Action 锁转向策略的生产者，此字段默认 false；已有 Root／Scripted rotation owner 排除仍有效。保留锁标记是上下文输入合同，不代表本次交付了 Action 锁转向功能。

未根据文件日期或旧归档删除所谓“第一次失败尝试”；现有证据不足以给每份未提交修改作历史归因。

### 隔离验证与限制

在仓库 `.git/start-audit/project` 中导出已提交源码、准备补交的暂存内容、第三方依赖和 Katana 引用闭包；不带入本地 Action 策略、AI、场景等未提交修改。

- 使用 Unity 2022.3.62f3 Roslyn 和现有第三方引用，Runtime／Editor 源码快照分别编译通过；隔离 Unity 原生导入也没有 C# 编译错误。
- 隔离 EditMode：`LocomotionStartAlignmentTests` **14/14**、`LocomotionSetDecisionTests` **16/16**，合计 **30/30**。新增一项显式 Motor 锁转向排除合同测试。
- 原工作区诊断中，五个 Start 的源依赖哈希均与提交的烘焙数据匹配；隔离项目中，四个补交 Clip 的导入依赖哈希也与原工作区一致。
- **精简隔离项目的新导入 Rig 依赖哈希不同**（原工作区 `7ba6d576607b6e24cf89308c4359746a`，隔离项目 `61df4bf19a63b6fca09b5e50742fddd8`）。Rig 还依赖 URP Lit shader；具体差异未完成归因。严格 freshness 检查会拒绝包括前向 Start 在内的这些旧烘焙数据。因此不能宣称“全量干净克隆、无需 Rebuild 即验证通过”。
- 只在可丢弃隔离项目执行正常 Rebuild：五个 Start 的每个累计位移／旋转采样与待提交数据比较，最大位置误差 **0**、最大旋转误差 **0°**。这些环境专用重烘焙结果没有复制回工作区或提交。
- 在该环境重烘焙后，用不含 Action 策略改动的 Runner／SetRuntime／ActorAnimation 复跑 **19 个真实 Katana 图场景**：初始 0／137°，输入 0、±90、±135、±179、180、45°及低转速；全部取样至 Start 末帧并返回 Move，末帧髋部旋转相对前向参考差为输出精度下的 **0°**。

由此确认了独立代码编译、必要源 Clip 和烘焙采样正确性，尚未关闭跨导入环境的烘焙 freshness 风险。新环境若出现 `Root Motion dependencies changed`／TurnToMove 条目跳过警告，应先在对应 AnimationAsset 正常 Rebuild，再退出／重新进入绑定会话；不要关闭验证或将此视为方向修复自动失效。此次没有扩大修改共享烘焙哈希算法，也没有重新运行前述全部 196 项回归。

临时审计脚本不加入默认测试套件，原始编译日志、`isolated-tests.xml` 和原生图报告保存在本机 `.git/start-audit/`；上述结果摘要随文档提交。

## 剩余人工检查与边界

重新进入 Play Mode 后，从不同初始朝向测试前、后、左、右及斜向起步，观察 Start→RunLoop 是否还发生原来的方向拉回；再测试起步中改向、松键、攻击和暂停。尤其检查入场混合期间的视觉朝向和真实胶囊轨迹，而不只看末帧。

本次没有实现 Start 距离／速度匹配、脚锁定或通用步幅扭曲，因此加速度与步频不一致导致的滑脚仍可能存在。Pivot 保持原来的播放语义；本次不声称它也获得了相同的根旋转重建。图销毁、重新启用和实际场景 Action 接管仍需结合上述失败继续验收。
