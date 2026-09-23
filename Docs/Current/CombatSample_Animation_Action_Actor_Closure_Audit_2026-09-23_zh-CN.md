# Animation → Action → Actor 执行链收尾检查

> 代码复核：2026-09-24。用户已完成本阶段人工检查并确认通过；Unity Test Runner 不再作为本项目当前阶段的验收方式。未修改生产 Action、Prefab、场景或项目设置。

## 结论

正式路径已连通：AnimationClip 与 AnimationRigAsset 经烘焙产生 AnimationAsset；ActionAsset 由 Inspector、Timeline、Details 编辑，Preview 读取同一会话；请求由 ActionStateManager 仲裁，ActionPlayer 启动 ActionRuntimePlaybackSession，ActionRuntime/Scheduler 进入固定阶段并管理资源释放。旧 ActionSequence / Legacy Timeline 后端不再参与这条路径。

本轮解决了原审查中唯一的必须收口问题：Timeline 的整数 Frame N 现在展示 Runtime 第 N 帧 **Motion、World 提交后、Hit 阶段**的位置。Pose 仍在 N 的时间采样；HitBox 与角色处于同一世界位置。小数 PreviewPosition 是相邻 Hit 阶段位置的视觉插值；Duration 终点保留最终位移并隐藏已结束的 HitBox。轨迹只画到已到达的边界和当前插值点。

**Animation → Action → Actor 阶段已收口，可以推进 Locomotion 和 Camera 路线。** 代码与编译结果由本记录列出；人工检查的通过结论由项目负责人提供。没有单独运行 Unity Test Runner，也不将其记作“测试通过”。

## 正式职责与所有权

| 部分 | 职责与边界 |
|---|---|
| AnimationAsset / AnimationRigAsset | AnimationAsset 存 Clip、烘焙轨迹及 Editor Rig 引用；Rig 是唯一 BakeSettings 来源，保存 BakeRigPrefab 和默认 Preview Prefab |
| AnimationAssetBakeWorkflow | 先计算并验证，成功后一次提交；stale 提示留在烘焙工具，不阻止保存轨迹的播放 |
| ActionAsset | 保存准入、取消、标签与 Timeline；不保存窗口状态 |
| ActionEditorContext / Document | 共享 Action、选择、连续时间与唯一只读文档；Document 只做条目索引、身份和安全显示，不再全量扫描配置或 Bake 依赖 |
| Inspector / Timeline / Details | 分别编辑资产属性、时间编排和所选条目；Commands、时间操作、Undo 与编辑锁防止写错资产 |
| PreviewWindow / Renderer | 从 Context 获取输入；Renderer 持有 PreviewRenderUtility、角色实例、Graph、绘制资源并重复安全地释放；不执行 gameplay replay |
| ActionStateManager / ActionPlayer | 准入、仲裁、真实启动结果、成功后 ClaimEntry；不预扫未来 Item 的 Context 需求 |
| ActionRuntime / Scheduler / Item Runtime | 唯一固定帧执行路径；Begin 检查动画快照，异常 Abort；Item 持有并释放自身 Motor、Tag、HitBox 等 owner/handle |
| CombatSimulationDriver / ImpactSystem | Control → Action → Animation → Motion → World → Hit → Finish；SpeedEffect 按固定战斗 Tick 计时和释放 |

ActorAnimation 由 ActionRuntime 取得覆盖所有权并提交绝对采样时间；ActorMotor 接受运动贡献。Preview 与 Runtime 共用 SourceWindow、TranslationDomain、HitBox Anchor/形状计算，但 Preview 不模拟 SelfRotation、碰撞后的世界位移、伤害或 HitStop。

## 本轮修正

- 原 A-M3：Preview 空间求值从帧前边界改为帧后边界；帧 0、中间帧、末帧、小数插值和 Duration 的合成 RootMotion 测试已更新。另用 Runtime 同一 SourceWindow 累积位移对照 Preview 的 HitBox 世界中心；跨轨测试仍验证 TranslationDomain 当前 owner，避免简单相加。
- 移除 ActionAuthoringValidator、ActionValidationIndex、Issues 分类与 Validation 通知；Document 保留 EditorId 唯一性、异常时间安全几何和当前选择定位。烘焙窗口的 stale 提示保留。
- 移除 ActionStateManager、ActionPlayer、ActionRuntime 三层 Timeline Context 需求扫描，以及 ActionPlayer 的临时 Scheduler 动画快照预检。真正 Begin 失败由现有 Abort/Finalize 处理；后续帧失败仍走执行异常路径。更新了“启动失败回调 false / 不 ClaimEntry”和“未来条目缺 Context 不提前拒绝”的测试预期。
- 移除一次性 HitBox Enter 与 Impulse Execute 的无效日志布尔值；保留 SelfRotation 每帧检查目标的去重。
- AnimationAsset 的旧内嵌 BakeSettings 与迁移按钮、无调用的 Transition Clip Resolver 已删除。未迁移旧资产。
- 旧 ActionSequence、旧 Timeline 和 Action V1 阶段记录从 Docs/Current 移入 Archive；导航以正式架构和本检查记录为现行入口。

原 A-M1 的“启动前全量检查 Gameplay 配置”和 A-M2 的“stale RootMotion 阻断”均已撤回：确定性的配置结果按保存数据运行。编辑时防止写坏资产、固定帧顺序、Abort 与资源所有权仍是需要保留的保证。

## 验证记录

- 2026-09-24：使用临时 MSBuild target 排除生成工程列表中两个已删除源码，并加入改名后的 Identity 文件；未编辑 Unity 生成的 csproj/sln。Assembly-CSharp-Editor 构建完成，包含 Runtime、Editor 与当前 12 个 Editor 测试源码，**0 error、48 warnings**。警告来自既有项目和包源码。
- 静态检查：已删除类型的源码引用为零；Context 重复扫描与启动前 Scheduler 已移除；Preview 帧终点不绘制 HitBox；新 Identity 文件保留原 .meta。
- 2026-09-24：项目负责人表示已完成本阶段检查并接受结果；具体操作清单和日志未提供，因此本记录不逐项声称某个场景或时长测试已通过。
- Unity Test Runner 未运行。项目负责人决定不继续使用它作为当前开发流程的验收门槛；已有测试源码保留，不把“未运行”表述为“运行通过”。

## 后续接点

Locomotion 的基础动画呈现仍由独立路线实现；不要恢复已删除的 AnimationConfig 字符串键。Camera 可从 Actor 根、CameraTarget 或 Motor 已发布世界结果选择跟随来源，不应读取 Preview 或模型动画偏移作为运动权威。用户按当前 Action 格式重新配置需要的动作；本轮不迁移旧资产。

源码、测试与文档已在提交 `434c7ff7` 中形成阶段基线；用户重配的动作资产、场景和备份未混入该提交。后续发现具体缺陷时按缺陷修复，不重启旧迁移路线图。
