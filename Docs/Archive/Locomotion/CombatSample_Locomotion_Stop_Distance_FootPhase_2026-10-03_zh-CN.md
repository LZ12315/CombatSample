# Stop Distance Matching 与脚相：实现和接入记录

> 归档于 2026-10-05。正文中的状态、待办和验证结果保留为当时记录；当前实现见[现行架构](../../Current/CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)，最新验证与未完成检查见[提交审查](../../Current/CombatSample_Locomotion_Commit_Review_2026-10-05_zh-CN.md)。源码链接用于导航，历史成员和行号不代表当前代码。

日期：2026-10-03。当前实现已接通，实际角色的停止观感和原生预览 UI 仍需在编辑器中验收。

## 作者工作流

1. 在 `AnimationRigAsset` 设置用于 Bake 的动画骨架。Humanoid 左右脚路径留空即可自动使用 LeftFoot/RightFoot；Generic 骨架在 Rig 上一次性填写相对 Animator 的左右脚路径。
2. 在 Move 使用的 `AnimationAsset`（当前 Kiana 为 Run）和 Stop 素材上点击 **Rebuild Animation Data**。同一次图采样生成根轨迹和脚接触标记，保留原有根轨迹验证流程。
3. `AnimationAsset` Inspector 显示 Root X/Y/Z、到停止点的剩余平面路径长度、停止帧、左右脚接触帧。启用对应 Override 后可以修改停止帧，或添加／修改／删除脚标记。重烘焙同 Clip 保留覆盖；关闭 Override 恢复自动数据；换 Clip 后旧数据和覆盖不参与新 Clip。
4. 在 `LocomotionSetAsset` 将 **Stop Playback Mode** 设为 **Distance**。旧资产缺少这个新字段时保持 **Time**，需要作者显式切换。
5. 预览区复用 Unity 原生 AnimationClip Editor。标记编辑使用帧号列表，未实现自定义时间轴，也没有联动原生预览内部播放头。
6. Runtime 在绑定资源时建立缓存。在播放期间修改素材数据后，需要重新进入 Play Mode，或取消该 Actor 的 Simulation 并重新绑定。

此次未修改项目中作者的 Kiana 素材配置，也没有在原项目中重烘焙或自动切换模式。

## 核心运行模型

- Gameplay 移动仍由 `LocomotionRunner` 和 Motor 控制。
- 自动停止点：最早使所有后续采样的水平根位置，都位于最终水平根位置半径 **0.02 m（2 cm）** 内的采样时刻。只比较水平位置，不比较逐帧速度、垂直运动或根旋转。它估计轨迹最终稳定，作者可以覆盖为自己的刹车结束点；全程位于该区域内时返回 0，没有可用距离段时 Stop 使用 Time。
- 曲线不重复保存一份根轨迹：Runtime 绑定时根据有效停止点，建立到该点的累计平面路径长度，并对单调剩余距离做线性反查。
- 自动脚标记：沿烘焙开始时固定的地面上方向读取脚的世界高度，以高度范围的 **10%** 作为区分显著抬起／回落与小幅摆动的高度差，每次显著下降后的局部高度谷产生一个候选。不要求各步达到同一个绝对低高度；非循环动画开头已经着地的脚不产生新落脚，末端仍持续下降的脚也不产生未完成候选；循环素材跨接缝检测。脚骨骼缺失、脚不发生可观测高度变化时不产生该脚标记。这仍是骨骼高度谷估计，不是脚掌接触地面的测量；作者可以覆盖实际需要的接触帧，不增加素材质量警告。
- 脚相：左接触为 0，右接触为 0.5；相邻异侧接触之间插值，循环素材跨循环边界继续插值。非循环素材在标记范围外保持端点脚相；单标记表示该脚，空标记表示未知。比较使用循环距离，避免 0／1 接缝错误。
- 当前脚相来自权重最大的非 Idle Move 样本；Start/Pivot 中松键时来自当前瞬态 Clip。读取实际求值后的 Playable 时间，避免同一 Unity 帧内多次手动求值时 Animancer 的 TimeD 缓存滞后。
- Stop 先比较方向。方向同分时比较脚相；有可比较脚相的候选优先于未知候选；仍同分时采用配置顺序。缺少所有脚相数据时自然回到方向和配置顺序。只配一个 Stop 时正常使用这一片。
- Time 模式的候选起播点为 0。Distance 模式在**各候选距离选出的实际起播点**比较脚相，不把 Clip 的第零帧当作距离播放的入口。
- 预测距离采用当前 Motion 积分后的模型速度：`speed² / (2 × deceleration) × policy locomotion scale`。MovementTimeScale 改变停止耗时，不再乘一遍距离。当前 Policy 下的空中 LocomotionScale 也按现有规则纳入。
- 刹车期间向 `ActorAnimation` 提交显式 SampleTime，并把该次 Clip 时钟设为 0 倍率。时间只能前进；距离超范围取曲线边界。预测距离为零时采样停止点一次，下一 Tick 以正常倍率 1 播放收尾。
- 无有效曲线、原地素材或无法预测有限停止距离（例如减速度为零）时，这次 Stop 普通按时间播放。停止期间预测失效，也交回时间播放。
- 新输入、Action owner、资源切换和 Simulation 取消继续使用现有状态机及所有权规则。脚相只参与 Stop 选片；没有增加 Move 脚标记同步、IK、Warping、位移补偿或新的运动规则。

预测是连续线性刹车的近似；离散积分、碰撞和平台等世界运动可能产生偏差。自动停止点与脚接触检测也不能保证所有动画的语义，需要作者通过预览和场景观察覆盖结果。

## 改动文件

- `AnimationLocomotionData.cs`：停止距离曲线、自动停止点计算、脚标记及相位轨道。
- `AnimationAsset.cs`：自动数据、来源绑定和独立手工覆盖；`AnimationRigAsset.cs`、`RootMotionBakeSettings.cs`：共享脚骨骼上下文。
- `RootMotionBaker.cs`、`RootMotionBakeResult.cs`、`AnimationFootContactBaker.cs`、`AnimationAssetBakeWorkflow.cs`：同次采样与事务提交。
- `AnimationAssetEditor.cs`：曲线、帧标记编辑、原生 Clip 预览。
- `LocomotionRunner.cs`、`LocomotionSetAsset.cs`、`LocomotionRuntime.cs`、`LocomotionMovePlayback.cs`、`LocomotionStopDistancePlayback.cs`：距离预测、模式、选片和时钟。
- `LocomotionAnimationContracts.cs`、`ActorAnimation.cs`：显式采样时间提交和真实时间读取。
- `AnimationLocomotionDataTests.cs`、`LocomotionAnimationContractTests.cs`：数据、播放和选片合同；旧合成 Clip 测试显式关闭并恢复对应 Animancer 动态动画警告，在测量时间前零步长刷新初始 Playable。生命周期测试改为直接验证 Simulation 取消合同。

## 验证

- Unity 2022.3.62f3，在 `/tmp` 的代码副本中运行 EditMode，避免与当前项目已打开的 Unity 实例争用。
- Locomotion 和 RootMotionBaker 合同：**86／86 通过**。结果 `/tmp/locomotion-distance-tests-final.xml`，日志 `/tmp/locomotion-distance-isolated-unity-final.log`。
- Kiana 原素材的副本中执行完整 Bake 和 Runtime Stop 请求检查：Run 生成 4 个脚接触（0.0833、0.3833、0.6833、0.9833 秒）；Stop 生成 3 个（0.3500、0.7167、1.0667 秒）。
- Stop 根轨迹 114 个采样，自动停止点 **1.7167 秒**，完整平面路径长度 **4.0361 米**。Runtime 在不同模型速度下的显式采样与距离曲线反查一致，时间单调，零距离后收尾交回时间播放。日志 `/tmp/locomotion-distance-kiana-check-2.log`。
- Runtime（含去除 UNITY_EDITOR 的预处理配置）和 Editor 编译通过，未执行完整 Player Build；`git diff --check` 通过。
- 临时项目检查没有实际角色视觉验收。仍需在原项目重烘焙 Run／Stop、切换 Distance 后，检查不同释放脚相、方向、再次输入打断以及停止收尾；查看 Inspector 曲线、手工覆盖、Undo 和原生预览。

## 嵌入预览范围修复（2026-10-03）

作者截图中 Kiana Stop 的预览到 `1:00 / 100% / Frame 60` 即结束，而 Clip 数据和 Inspector 范围为 1.8667 秒／112 帧。此前仅核对 Clip 时长不足以解释截图。

原因是 Unity `TimeControl` 默认范围为 0～1 秒，`AnimationClipEditor.OnInspectorGUI` 会按 Clip Settings 设置起止时间；仅委托 HasPreviewGUI 和 OnInteractivePreviewGUI 没有执行该初始化。Stop 第 103 帧的烘焙数据仍在真实 Clip 范围内。

`AnimationAssetEditor` 现在显式创建 Unity 原生 Clip Editor，并在原生预览创建后按 Clip Settings 初始化预览范围。Unity 没有公开的预览范围 setter，因此这一小段 Editor 接入缓存反射字段；不设置播放头，不另建时钟或时间轴。Unity 升级时需要核对这段内部 API 适配；字段不可用时不展示未经初始化的预览。

Unity 2022.3.62f3 临时项目中的原生预览实例检查通过：原始终点为 1 秒，修复后范围为 0～1.86666679 秒，停止点位置为 91.964%，末端为第 112 帧。日志 `/tmp/locomotion-preview-range-check-2.log`。Editor 编译及 `git diff --check` 通过。批处理检查了真实预览时钟，但没有渲染交互截图；在原项目重新选择 AnimationAsset 后检查预览末端即可，无需为本次修复重新烘焙。

## 自动脚标记检测修正（2026-10-03）

作者反馈 Start 的脚标记同时存在漏标、多标和时机偏差。对 Kiana 原素材副本执行与正式 Bake 相同的图求值，确认 Humanoid 自动解析得到 `Bip001 L Foot`／`Bip001 R Foot`，Rig 上空脚路径不是问题。

原算法使用全 Clip 最低 10% 的绝对高度区间。Start 右脚第 22 帧的局部低点约 0.16240 m，略高于阈值 0.16223 m，因此漏标；开头双脚已经站立却各生成第 0 帧标记。新算法使用显著高度变化分隔局部谷，不再把所有步的落点高度与初始站姿比较。自动结果依然取局部最低采样帧，不能保证等于脚掌首次接触的帧。

临时项目的实际素材采样结果（60 FPS）：

| AnimationAsset | 原候选 | 修正后候选 |
| --- | --- | --- |
| Run_Start | 左 0、右 0、左 40、右 57 | 右 22、左 40、右 57 |
| Run_Stop | 右 21、左 43、右 64 | 左 3、右 21、左 43、右 64 |
| Run（循环） | 左 5、右 23、左 41、右 59 | 左 5、右 23、左 41、右 59 |

采样日志 `/tmp/locomotion-foot-candidates.log`。原项目素材未自动重烘焙；在 Inspector 点击 Rebuild Animation Data 才会更新自动结果。已有手工覆盖继续保留，需要关闭 Override Foot Markers 才能查看自动结果。作者仍需用预览确认接触语义和时机，必要时手工调整候选帧。

新增纯数据合同覆盖不同落点高度、初始站姿、小幅接触摆动、停止尾部、末端未完成下降和循环接缝。Unity 2022.3.62f3 临时项目中 Locomotion／RootMotionBaker EditMode **90／90 通过**（`/tmp/locomotion-foot-candidate-tests.xml`），`git diff --check` 通过。本次没有角色视觉验收，也没有更改 Runtime 脚相插值或 Stop 选片规则。


## 自动停止点改为最终位置稳定范围（2026-10-03）

作者同意以“轨迹最终稳定”作为自动 Stop Point 的含义；手工覆盖仍可表达“前进刹车结束、后续后撤和姿势调整正常播放”。两种含义不能从同一个速度阈值可靠推断。

`FindAutomaticStopTime` 现在从末尾向前扫描根轨迹：最终水平位置是稳定区域的中心，固定半径为 2 cm；第一次遇到区域外采样时，返回紧随其后的采样时刻。由此保证返回点及其后所有样本都在区域内，临时进入后又离开的时刻不会被误选。采样间采用现有线性位置插值，两端在圆内的段也在圆内。该算法不增加方向识别、最短驻留时间、平滑窗口或素材特例。

Kiana Stop 的原始轨迹在第 53 帧到达最远前进位置，之后沿前进轴回退约 17.5 cm。旧算法得到第 103 帧；新定义对应第 **92 帧／1.5333333 秒**。2 cm 容差不会把后续这段回退整体忽略为噪声。原项目素材需要点击 Rebuild Animation Data 才会更新自动数据，同 Clip 的停止点和脚标记手工覆盖继续保留。

验证：Unity 2022.3.62f3 临时项目完整 Bake 得到第 92 帧，后续根位置离最终位置最远 **0.018598 m**，到新停止点的距离曲线长度为 **4.015897 m**。同 Clip 重烘焙保留作者覆盖，关闭覆盖恢复新自动点；零距离采样停止点一次后交回正常时间收尾。实际素材检查日志 `/tmp/locomotion-stop-stability-content.log`。

新增纯数据合同覆盖小幅快速抖动、垂直运动、不同时间间隔、临时进入后离开稳定区、水平圆形范围和缓慢漂移／全程微小位移。Locomotion／RootMotionBaker EditMode **96／96 通过**（`/tmp/locomotion-stop-stability-tests.xml`），`git diff --check` 通过。原项目素材未自动重烘焙，角色停止收尾仍需在原项目预览和运行中验收。


## 速度方向响应与停止减速分离

作者同意先调整运动核心，再按运动目标适配停止素材。`LocomotionMovementConfig` 新增有限、非负的 `DirectionResponse`，单位为 1/s；默认新配置为 12。现有 Acceleration、Deceleration、RotateSpeed、TurnResponseTime 的字段名和引用保持。

统一积分规则：

1. 输入方向 × 输入强度 × MaxSpeed 得到目标水平速度。
2. 目标速度为零：以 Deceleration 将当前速度向零推进；停止距离预测仍为 `speed² / (2 × Deceleration)`，再应用现有 Policy Scale。
3. 目标速度非零：先以 `1 - exp(-DirectionResponse × dt)` 将当前速度与“同当前速率、沿输入方向”的速度向量混合；随后以 Acceleration／Deceleration 向目标速度推进。适用所有角度，反向向量自然抵消，不为 180° 引入单独的减速阶段。
4. DirectionResponse 为 0 时关闭额外方向对齐，普通向量加减速仍有效。全部运动速率和 DirectionResponse 都为 0 时，速度不变。朝向旋转继续由显式 FacingDirection、RotateSpeed／TurnResponseTime 控制。

Kiana 地面资产 `Locomotion_Kiana_Normal` 设置 Deceleration = 12、DirectionResponse = 12，保持 MaxSpeed = 5、Acceleration = 20 和作者的 Stop Point 手工覆盖。连续刹车模型满速停止约 0.417 秒、1.042 米；离散 Tick 积分与碰撞的实际结果可有偏差。其他角色与空中资产未重新调参。

纯 EditMode 合同覆盖多角度满输入转向与停止减速度的独立性、方向响应强度、松键刹车、朝向独立、减速中再次输入、模拟输入强度下降、零响应及非法数值。Unity 2022.3.62f3 临时项目中相关 Locomotion／RootMotionBaker 测试 **104／104 通过**，结果 `/tmp/locomotion-direction-response-tests.xml`。

实际 Kiana 资源副本通过运动 Runner、LocomotionSetRuntime、ActorAnimation 与 Animancer 图的联合检查：60 Hz 下满速松键约 0.4333 秒停止；先刹车 0.1 秒再反向输入，约 0.0667 秒后速度转为新方向。Stop 距离匹配进入动画约 0.5265 秒，随后采样单调推进到作者覆盖的 1.0 秒停止点；图的实际播放时间与请求一致，下一 Tick 解除显式采样并恢复正常时间播放。日志 `/tmp/locomotion-direction-response-content.log`。

本次检查没有覆盖原场景的 KCC 碰撞或渲染后的脚滑效果。重新进入 Play Mode 后，手工检查满速松键、连续左右反向和刹车中再次反向输入；这些运动参数修改无需重新烘焙动画。运行时缓存配置，因此运行中修改资产后应重新进入 Play Mode。

## Kiana Right Stop 导入修复

`Avatar_Kiana_C2_Ani_RunStopRight_fix.FBX` 原导入为 Generic／No Avatar。实际素材副本接到 Kiana Bake Rig 后，烘焙返回成功，但 121 个根轨迹采样全为零、停止点为 0、脚标记为空；轨迹验证只能确认采样与图求值一致，不能确认作者期望的动作已经绑定。

仅修正该 FBX 的 ModelImporter：Humanoid、Copy From Other Avatar，复用 Kiana 模型 Avatar；保留原 GUID、原压缩设置和素材自身 0～120 帧／2 秒范围。与现有 Left Stop 一样保留水平根位移、烘焙根旋转和高度。运行时与烘焙代码未修改。

Unity 2022.3.62f3 素材副本验证通过：Clip 为 Human Motion，121 个采样通过完整烘焙验证，末端前向位移约 5.1516 米，自动停止点 1.7667 秒，脚候选为右 0.35、左 0.65、左 1.10、右 1.6167 秒。日志 `/tmp/locomotion-right-stop-check-final.log`。这些自动候选仍需作者按预览确认。

当前 Locomotion 的两个 Stop 条目均引用 Left Stop 的 AnimationAsset；没有现存 AnimationAsset 引用 Right Stop。原项目自动重新导入后，可将其 Clip 赋给独立 AnimationAsset、选择 AnimRig_Kiana 并 Rebuild Animation Data，再配置到 Stop 条目。原场景视觉播放尚未验收。

## 左右 Stop 配置与入场脚相检查

作者创建 `Anim_Kiana_Run_Stop_Left`／`Anim_Kiana_Run_Stop_Right` 后反馈切入 Stop 时腿部切换异常。检查当前保存的资产发现两个 Stop 条目实际均引用 Right 的 GUID `6e82ec67bd9151ad3898868756a9599e`。已将第一个条目改为 Left 的 GUID `22b6162bdc40b446aa59a57a90680de7`，第二个保持 Right，使左右候选均参与现有脚相选片。运动参数、混合时长和运行时代码没有修改。

检查时 Right 的序列化覆盖字段仍来自复制前的 Left Clip；按现有来源绑定约定，`HasStopTimeOverride` 为 false，实际使用自动停止点 1.7667 秒，而非旧字段里的 1 秒。作者随后明确要求 Right 也使用 1 秒，已将该资产的停止点覆盖绑定到 Right Clip。此为作者指定的配置修正，没有改变不同 Clip 之间不自动迁移覆盖的约定。

在 `/tmp` 素材副本中重烘焙并用实际 Animancer 图验证：12 个 Run 播放时刻分别触发停止，Left／Right 各选中 6 次，均与距离入场帧的脚相差比较结果一致。满速第一刹车 Tick 的剩余距离约 0.96 米：Left（停止点 1 秒）进入 0.5265 秒，脚相 0.7406；Right（自动停止点 1.7667 秒）进入 0.8616 秒，脚相 0。日志 `/tmp/locomotion-stop-entry-phase-check-final.log`。

样本中最近候选的最大脚相差仍为 0.3611 周期。这说明选片确实生效，但现有素材的距离入场帧不能覆盖任意 Run 脚相；脚相只排序候选，不改变距离时钟，也不直接保证腿部姿势连续。副本验证未做原场景视觉验收。重新进入 Play Mode 后检查重复引用修正的效果；再根据作者对具体异常时段的反馈核对停止点、自动脚标记和混合观感。

Right 的 1 秒覆盖生效后再次检查：`HasStopTimeOverride` 为 true，满速第一刹车 Tick 的 Right 入场时间改为 0.7967 秒，脚相仍为 0；12 个 Run 时刻仍为 Left／Right 各选中 6 次。逐 Tick 刹车采样与所选距离曲线一致，两条素材都在零距离采样 1 秒停止点一次，随后恢复正常时间收尾。日志 `/tmp/locomotion-stop-entry-phase-one-second.log`。本轮修改仅涉及 Locomotion 两个候选的引用、Right 停止点覆盖绑定和本记录；相关差异检查通过。

## 行为与配置绑定一致性（2026-10-04）

本轮落实脚相参考、执行配置绑定、状态决策阈值和 Stop 距离时钟四项约定。沿用现有目录和职责布局，Kiana 的运动参数、素材引用及左右 Stop 的 1 秒作者停止点没有修改。

### 脚相参考来自实际基础层姿势

ActorAnimation 接收请求附带的叶节点与已绑定脚相轨道关系，保留淡出动画的关系。读取时遍历基础层有效动画叶节点，最终贡献为层权重 × 各级父节点权重 × 叶节点权重；选择最大贡献，权重相同时保留图遍历顺序。用该节点实际已求值的播放时间读取脚相，而非按 Runtime 是否处于 Start／Pivot 来决定来源。

主要动画没有轨道或轨道不能采样时返回未知；Idle、保护姿势也不跳过。不会为取得已知脚相而选择贡献较小的动画。ActorLocomotion 在本 Tick 的 UpdateAnimation 和新请求提交前取得参考，因此新 Stop 不会参与自己的选片。候选仍按方向优先、实际距离入场帧的脚相择优、作者顺序选择，距离曲线继续决定起播时间。

会话开始／结束、播放图更换或销毁时清除对应关系，失效节点不参与读取；Action 覆盖期间参考未知，覆盖与释放规则保持。脚相仍是候选排序的近似，并不承诺任意入场姿势完全无缝。

### Runtime 创建即绑定执行配置

Runtime 构造时复制 MovementConfig、Move 参数源与样本数值、Start／Stop／Pivot 条目、混合时长、Stop 模式和决策阈值。每份动画资源绑定 Clip、独立脚相轨道、停止距离曲线，以及周期时长和根位移速度匹配数据。列表和标记复制为绑定数据，不保留作者配置容器作为后续执行来源。

Mixer 延迟创建、Tick、退出再进入同一 Runtime、动画图重建均使用这些数据。`Runtime.Asset` 保留身份；`CurrentMovementConfig` 展示绑定值，候选的执行配置有效性也使用已捕获结果。模式选择继续按当前角色状态求值，输入、Action 和模式切换保持动态。只有创建新 Runtime 才读取新作者配置；不提供刷新入口或热更新。Disable／CancelSimulation 会释放 Runtime，之后可重新绑定。

### 状态决策参数

`LocomotionSetAsset.TransitionDecisionConfig` 开放以下参数，新字段初始化为默认值，未批量改写现存资产。

| 参数 | 默认 | 比较规则 |
|---|---:|---|
| StationarySpeed | 0.1 m/s | Start 使用速度 ≤ 阈值；释放输入进入 Stop 使用速度 > 阈值 |
| PivotMinimumSpeed | 0.5 m/s | 速度 ≥ 阈值 |
| PivotMinimumAngleDegrees | 120° | 转角 ≥ 阈值 |

两项速度必须有限且非负，角度必须有限且在 0～180°；沿用资产核心有效性检查，无 Inspector 告警。无参状态机保持默认行为，输入边沿、Pivot 锁存、完成和 Action 中断规则沿用。

### Stop 距离时钟边界

Stop 距离匹配使用当前模型速度、绑定的 Deceleration 和现有运动倍率预测刹车距离。碰撞造成角色实际提前停下，不改写 Stop 的距离时钟；模型继续减速，动画继续按模型距离推进。Move 速度匹配仍使用符合条件的实际运动反馈，两者各用其约定的数据来源。

零距离只采样作者停止点一次，随后恢复普通时间播放收尾；再次输入仍能打断。不增加碰撞特例、脚锁定或姿势矫正。贴墙提前停下可能出现可见的运动／姿势差异，这是当前模型的边界。

### 验证与待验收

- Runtime、Editor／测试程序集编译通过；另行去除 `UNITY_EDITOR` 条件的编译通过（并非完整 Player Build）。使用 Unity 现有编译响应文件读取依赖，编译输出仅写入 `/tmp`。
- 独立 Mono 执行现有 NUnit 断言，38 个不依赖原生图求值的决策、动画数据、运动反馈和倍率合同用例通过。日志 `/tmp/locomotion-binding-pure-contracts.log`。这是独立执行结果，不等同于 Unity Test Runner 全套回归。
- 新增实际 Animancer 图合同：Run 主导的 Pivot 混合、Start 主导的 Move 返回混合、嵌套权重、同权重及未知脚相、会话／图生命周期，以及资产修改、重入和重建后的配置一致性。速度匹配合同补充周期数据、时长和样本标记的绑定。删除旧的“最大非 Idle Move 样本”脚相测试。
- Unity 2022.3.62f3 批处理启动因没有有效 Editor 许可证退出，测试未进入执行；新增图合同、完整 Locomotion／动画数据／RootMotionBaker EditMode 回归尚未通过本轮运行验证。启动日志 `/tmp/locomotion-binding-tests.log`。上文 2026-10-03 的通过数保留为历史记录。
- 原项目人工验收：快速启动后松键、转向混合中松键、Start 返回 Run 后立即停止、刹车中再次输入。另检查贴墙提前停止；已有 Runtime 修改资产后应保持原配置，重建 Runtime 后应使用新配置。停止点覆盖与脚标记修改也遵循此约定。

## 交接统一保护已求值姿势（2026-10-05）

作者确定交接时一律冻结当前基础姿势。Idle 仍是 Move 的普通样本，没有独立后备配置；ActorAnimation 不再缓存历史 Idle，也不在保护时优先取最后 Move。

保护时复制基础层当前有贡献的动画树，包括未结束 crossfade 的两侧；保留混合权重，使用实际已求值时间创建独立静止快照。移除进入瞬态前的 Move 预先快照；后续会话结束或有效 owner 的无效请求都使用同一保护规则。重复保护复用已有快照，旧 Runtime 的销毁不影响快照。Action 覆盖继续单独处理，保护基础层时不把 Action 姿势复制进去。正常 Stop、Move 参数和速度匹配保持现有规则。

更新原保护合同的期望为最后播放的 Move 姿势；增加地面 Idle 后进入无 Idle 空中模式的保护合同，以及变化曲线下混合中结束会话／请求失效、销毁旧节点、Action 覆盖与释放后保护姿势不变的合同。

验证：Runtime 和 Editor／测试程序集编译通过，相关代码差异检查通过。Unity 2022.3.62f3 临时项目批处理因没有有效 Editor 许可证退出，新图测试未执行；日志 `/tmp/locomotion-pose-check/unity-tests.log`，编译日志位于同目录。尚需原项目确认地空交接、转向混合中交接，以及 Action 覆盖／释放时基础姿势的连续性。本次没有改动资产、Prefab、场景或运动参数。
