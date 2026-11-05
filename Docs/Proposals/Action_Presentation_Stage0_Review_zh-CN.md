# Action 表现：阶段 0 核对结果

## 结论与停点

**阶段 0 完成。未修改功能代码、测试代码或资产，未启动 Unity、编译或测试。下一阶段尚未开始。**

目标与范围见[实施计划](Action_Presentation_Plan_zh-CN.md)。当前三类功能均有候选实现；后续应收口兼容和实际制作 / 播放验收，不从零重写，也不再扩充系统。

优先顺序：

1. 阶段 1 先验证旧震屏配置是否保留原方向 / 距离，再验证代表动作与相机过渡。
2. 阶段 2 验证真实挂点、朝向和 VFX 播放，避免以临时粒子夹具代替美术效果验收。
3. 阶段 3 验证编辑器操作与预览成像；当前原生粒子计数证据不能证明画面可用。

## 一、差距表

“有证据”仅指表内列出的范围，不表示整项验收通过。

| 目标 | 当前实现与证据 | 尚缺什么 | 归属 |
| --- | --- | --- | --- |
| 区间独立持有、取消与释放 | Buffer + Range runtime 已实现；合同测试覆盖完成、打断、Abort、待创建取消及独立清理 | 真实角色动作切换、循环、Driver 停止的串联观察；现有实对象集成主要验证 Actor Disable | 阶段 1 / 2 |
| 原生曲线震屏 | Directional / IndependentAxes、位置 / 旋转曲线、Action 时钟、原生 event 和专用 Listener 已有；原生接收 / 取消有集成证据 | 主场景 Brain 的真实过渡、实际幅度、不同帧率下的短区间表现 | 阶段 1 |
| 暂停 / 慢速 | 连续 Action 位置参与采样；合同验证不足整数帧的推进与暂停保持，原生事件保持采样值 | 代表动作的暂停、恢复、实际相机画面 | 阶段 1 |
| 旧震屏数据 | 有旧 Point 时间迁移、enabled / intensity 兼容及曲线默认值 | 巨人资产存在“已有方向但无迁移标记”的真实旧配置，现有用例未覆盖该组合；详见下节 | 阶段 1 优先 |
| VFX 位置 / 朝向独立 | 根 / 骨骼 / 路径，留置 / 跟随，三种朝向与更新策略已实现；纯数学和配置快照有证据 | 真实 Humanoid / 武器挂点、世界 / 屏幕朝向及连续更新的可见结果 | 阶段 2 |
| Damage 适用能力共用 | 初始化、遮挡、粒子速度与 facing helper 已共享；Damage 仍保留原命中位置 / Lifetime 路径 | 实际缩放、遮挡、材质与 Damage / 弹反外观回归 | 阶段 2 |
| 可制作的 Details | 条件字段、单位、曲线编辑、配置 / 端点 / 极短区间诊断已有 | 实际交互、Undo / Redo、复制、保存重载和诊断是否易理解 | 阶段 3 |
| 隔离粒子预览 | Prefab 组件预检、独立场景、固定种子、Start 姿态留置和退出清理已有；有粒子重采样集成证据 | 主工程 Preview 纹理、实际材质、视角与挂点对应关系；不是任意脚本回放 | 阶段 3 |
| 验证可复现性 | 合同测试在 Assets；原生 / 预览 / Play Mode 夹具和结果目前主要在 `.git/action-effect-validation/` | 必要集成证据应有可版本控制的最小复现入口，而不是依赖本机 `.git` 工件；不另建完整测试框架 | 阶段收口 |

未发现需要新增万能编排、资源池或 Actor 业务组件才能完成目标的理由。现有 CameraControl / Combat / Locomotion 等大范围工作区改动不属于本次收口范围，不回滚或顺带整理。

## 二、参数与兼容核对

### 1. 旧震屏方向：待验证的高优先级风险，不先判定已发生故障

现有资产：`Assets/Data/Action/Giant/Attack/Action_Giant_Attack01.asset`。

- ScreenShake 区间为 `[103,107)`，强度 `0.6`，`direction: 1`（Horizontal）、`frequency: 12`、`maxDistance: 20`。
- 没有 `_hasDirectionalSettings` / `_hasCurveSettings` 和新曲线数据。
- 当前 `ScreenShakeItemConfig.OnAfterDeserialize()` 在 `_hasDirectionalSettings == false` 时会统一设置 Vertical / 20。
- 现有 `LegacyPlaybackDefaults_DoNotRepairExplicitInvalidNewData` 的旧方向样本**显式带了 `_hasDirectionalSettings: true`**，不能证明上述真实资产组合正确。

**下一步先在隔离副本中验证实际反序列化结果。**字段初始化与 Unity 资产反序列化行为尚未在本轮执行确认，不声称该资产已经改向。若需要修正，只处理兼容：保留确实已写出的方向 / 距离、enabled / intensity 和时间；没有方向信息的更早数据仍使用默认值。不得把明确的新配置非法零值当旧数据修复。

旧 Hz 参数不再驱动新曲线，也不承诺等价手感；不要批量重写原资产来掩盖兼容问题。

### 2. 曲线进度与退出

当前公式是 `(ContinuousPosition - StartFrame) / (DurationFrames - 1)`，夹到 `[0,1]`，单帧只取 `t=0`。Scheduler 在最后有效帧 Finish 释放资源。

这表示从首个到最后一个有效逻辑帧映射曲线，而不是另增一个 End 帧供渲染。首尾零的单帧波形可以完全没有可见震动；很短区间也可能错过渲染。**按已确认的严格区间规则保留，不改调度器、不补最少可见帧。**在长短两个样本中分别验证，勿只拿四帧旧资产判断新曲线是否可用。

### 3. VFX 速度、缩放与朝向

- 粒子速度目前是把各 ParticleSystem 的 `main.simulationSpeed` **设为配置值**，不是再乘各粒子原值；Action 要求至少 `0.01`。制作说明应保持这个含义，避免“倍率”被误解。
- 根缩放包含 Prefab 原缩放、配置倍率及挂点缩放；粒子 Scaling Mode 统一为 Hierarchy。非均匀缩放的实际美术结果仍需看画面。
- MatchPositionBinding 在跟随位置时连续更新朝向，否则只在进入时计算；OnEntry / Continuous 可独立选择。没有理由再增加一种朝向策略。
- ScreenOriented 与 Damage 一样是屏幕平面的发射主轴约定，不是任意面片的通用 Billboard。
- Runtime 路径从 Actor 根求值，而 Preview 从 presentation-only 角色实例根求值。默认 Rig 的 Preview 引用模型 FBX，不能假定二者完整路径相同；明确路径样本应使用层级一致的夹具，不做静默 fallback 或通用层级映射系统。

### 4. Preview 边界

预检拒绝脚本、Animator、Camera 等不支持的效果组件，要求已保存的 Prefab 资产。年龄按 nominal 1x 时间轴秒数 × 配置速度求值；不重放暂停历史或整段 world-space 发射轨迹。现有约束符合本次限定范围，不扩展到任意脚本 Prefab。

## 三、验收样本清单

本轮仅选定样本，**没有修改、复制或声称已运行这些资产**。已有工作区修改和未跟踪资源保持原样。

| 样本 | 现有入口 / 最小夹具 | 验什么 |
| --- | --- | --- |
| S1：真实旧震屏 | `Assets/Data/Action/Giant/Attack/Action_Giant_Attack01.asset`，`[103,107)`、Horizontal / 0.6 | 缺迁移标记时保留原参数；短区间表现和警告 |
| S2：可观察的曲线震屏 | S1 动画的临时 Action 副本，独立配置约 24 帧正负波形；两份独立区间、可选旋转 | 曲线方向、暂停 / 慢速、各轴、分别取消；不拉长 S1 原区间 |
| S3：真实骨骼跟随 | `Assets/Data/Action/Katana/Attack/Combo01/Action_Katana_Combo01_1.asset`，已有 Effect `[30,75)`、RightHand、scale 0.4、Follow | 沿用现有 `Assets/Prefabs/Effect/AttackTip.prefab`，核对角色移动 / 转身、挂点和缩放 |
| S4：留置 / 屏幕朝向 | S3 的临时副本，改用 `Assets/Prefabs/Effect/HitVFX.prefab`；分别测试留置与屏幕朝向 / 更新策略 | 位置与旋转独立、预览相机、遮挡；HitVFX 也被现有 GreatSword 命中配置引用，可用于对照 |
| S5：持续粒子与通用路径 | 复用最小循环 ParticleSystem 夹具；ActorRoot 下建明确 `Model/Weapon` 路径，预览层级保持一致 | 活到退出边界、路径缺失、立即隐藏 / 销毁；不增加美术或正式 Prefab |
| S6：镜头 / 命中串联 | 主场景候选 `Assets/Scenes/HyperGraph.unity`；实际使用 `Main Camera`、FreeLook / SoftLock / HardLock 和 Manager 的隔离副本或人工播放 | 真实 Brain 过渡、叠加普通命中 / 弹反，不只检查 CameraState 数值 |

补充：`AttackTip` 的序列化内容有四组自动播放、非循环粒子，不能以它代替所有持续粒子生命周期验收；持续性由 S5 覆盖。`AttackWarning.prefab` 可作备选；尚未验收其画面。

相机接入核对：

- `ActorCameraControl.Awake` 已为其三个持有的 rig 安装专用 Action Listener；本任务只涉及这处接入，不整理相机控制架构。
- 三个 `Assets/Prefabs/Function/Camera/CM_*.prefab` 的共享 Listener 都是 mask 1；`Manager.prefab` 的命中 Source 也是 channel 1。Action 使用 mask 2。
- `Main Camera.prefab` 已有 Brain，默认 blend 0.4 秒；HyperGraph 及 CombatTrainingArena 序列化了三个 rig 引用。检索未发现这两个场景的 channel / camera-space 属性覆盖，但实际运行时组件与路由仍需验收。
- 不为本任务修改新训练场的环境、墙壁、灯光或相机调参；S6 默认从 HyperGraph 路径核对，场景当前已有修改，后续验证不得直接保存覆盖。

## 四、已有证据的边界

本轮读取并复核了既有 `polish-final-verified.xml`：**123 通过、0 失败、0 跳过**。这是此前执行结束后留下的结果，本轮没有重跑。

- 115：选定合同回归（Action 表现、VFX、Scheduler、速度、Hit Buffer、弹反、反馈路由）。
- 5：原生 Impulse / 专用 Listener，包含位置与旋转、距离、来源移动、独立取消、FreeLook 与 State 混合。
- 2：隔离粒子实例、留置入点、前后重采样和不支持组件诊断。
- 1：进入 Play Mode 后的真实对象初始化、跟随与 Actor Disable 清理。

核对的 14 个关键源码 / 测试文件与隔离工程副本 SHA-256 一致，覆盖两个 camera 文件、Action 数据 / runtime / VFX / buffer、Actor 阶段接入、CameraControl、粒子预览 helper 以及两个表现合同测试文件。

**不能据此声称：**

- 当前整个工作区所有测试都通过，或所有 Editor 文件、实际资产都受上述副本覆盖。
- `CameraState.Lerp` 已验证真实 Brain 的 FreeLook ↔ 软 / 硬锁过渡画面。
- 原生粒子数量 / 坐标已证明 Preview 纹理、材质、遮挡和美术质量。
- 直接调用 Buffer 的 Play Mode 夹具已替代完整 Actor 动作 / Driver / 命中串联。

## 五、下一阶段的有限范围

阶段 1 只收口震屏，不提前修 VFX 或 Preview：

1. 为 S1 的真实旧字段组合建立隔离加载 / 序列化复现，先查是否丢方向与距离；确有问题才做最小兼容修正。
2. 保留现有 native event / Listener / Action 时钟；仅修复复现出的采样、取消或接收问题。不重写 Scheduler 或相机路由。
3. 用 S2 的较长波形分离验证曲线可用性，再回看 S1 短区间；按需做有限参数 / 提示调整。
4. 用 S6 验证实际过渡和命中叠加。没有可观察画面时明确记为待人工，不宣布阶段全部验收通过。
5. 将必需原生复现用例整理到可保存的独立 / 显式验证入口，复用现有设施，不把完整集成套件加到默认 Test Runner。

允许触及的生产位置（按缺陷需要，不保证全部修改）：

- `Assets/Scripts/Action/Data/ActionTimelineData.cs`：仅 ScreenShake 配置兼容。
- `Assets/Scripts/Camera/ActionScreenShake.cs` / `ActionScreenShakeListener.cs`：仅有复现依据的原生适配修正。
- `Assets/Scripts/Action/Editor/Windows/ActionDetailsWindow.cs`：仅震屏字段 / 诊断。
- 对应表现合同测试、必要的独立集成夹具和验证记录。

`ActorCameraControl` 三处 Install 已存在，默认不再改；`ActionRuntime`、Driver、VFX 播放、Preview 以及正式 Action / 相机资产也默认不改。若必须突破上述范围，先交代具体原因。

**阶段 0 到此停止；本记录不是阶段 1 的实施完成报告。**
