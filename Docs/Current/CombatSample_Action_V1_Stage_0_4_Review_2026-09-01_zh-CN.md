# CombatSample Action V1 — Stage 0–4 Review

> 状态：**审查、修正与项目工作站复编译已确认通过**
> 日期：2026-09-01
> 权威依据：Action Final Architecture v1 与 Action Implementation Roadmap v1

## 结论

Stage 0–4 的实现边界与冻结架构一致：新数据和 Runtime 保持旁路，旧 Action/Sequence 仍是正式播放路径；Gameplay composition 仍属于现有 Receiver/Domain；Pose 与 Root Motion 保持分离。审查没有发现需要回滚 Stage 1–4 设计的结构性错误。

| Stage | 审查结论 | 关键证据 |
| --- | --- | --- |
| 0 | 满足 | Baseline、Legacy dependency、75 个迁移候选与 Stage 7 residual seed 已记录；人工 E3 基线复验已确认。 |
| 1 | 修正后满足 | 60 Hz 自动 Duration、七种 Item、SerializeReference、EditorId、Validator、AnimationAsset 与共享 Bake 核心均存在；补齐 Inspector 验证/显式 ID 修复和遗漏的 Config 校验。 |
| 2 | 修正后满足 | Snapshot、lazy runtime、Point/Range 半开区间、Speed/Pause、三种终止结果和 Action-phase Interrupt 均符合；补上 Range Enter 失败时的 best-effort Abort。 |
| 3 | 满足 | 七种 Item 均通过多态 factory 接入现有 Receiver；Scheduler 没有具体 Item type switch；Root Motion/SelfRotation 使用 baked trajectory；Complete/Interrupt/Abort 各自释放 owner/handle。 |
| 4 | 满足 | Continuous Position 解析 Clip/SourceTime，首段前不提交、段间/末段后 hold；ActorAnimation 直接 Clip 入口不依赖旧 Action animation key；ActionRuntime 全终止路径释放 animation owner。 |

## 本次修正

- Authoring Validator 现在拒绝非法 Item 枚举、非法 BoneReference、非有限/非法 HitBox shape、空 target layer、负或非有限 damage、空 effect、无效 Tag 与无效 AnimationClip duration。
- ActionAsset Inspector 现在直接显示 V1 validation 结果，并提供显式、单 Undo transaction 的 missing/malformed/duplicate EditorId 修复按钮；加载和 OnValidate 仍不会静默改 ID。
- Scheduler 在 Range Runtime 的 `Enter` 抛异常时先调用该 Runtime 的 `Abort`，避免 Enter 已取得部分 owner 后无法释放。
- Stage 2 Snapshot 测试改用合法 AnimationAsset/AnimationClip，并增加 Enter failure cleanup 合同用例。
- 收紧 Stage 4 内部 API：动画采样值保持 internal；Runtime 防御校验同时拒绝无效 Clip duration。
- 修正 Stage 0、Stage 1、Stage 4 handoff 状态，以及 Stage 4 对后续顺序的错误描述：Stage 5 是新编辑器，Stage 6 是资产迁移，Stage 7 才正式 cutover。

## 保持不变的边界

- 未修改 `ActionPlayer`、`ActionStateManager`、`ActorSimulationRuntime`、Combat phase、Legacy/Sequence session 或正式资产分派。
- 未迁移 Action/Animation 资产，未删除 Legacy API，未创建 V1 正式运行资产或开发 harness。
- Unity Test Runner 不作为验收门槛；已有测试源码只保留为稳定合同参考。

## 验证与剩余检查

- 已完成 source diff/whitespace 检查、Scheduler concrete-item switch 检查、ActorAnimation V1/Legacy dependency 边界检查，以及正式播放/Combat 文件零改动检查。
- 本地 `dotnet build --no-restore` 缺少 Unity 生成的 project assets；改用仓库外临时中间目录时，Unity 生成的 csproj graph 在 `UnityEditor.TestRunner` 出现循环引用，跳过 project reference 后又缺少 Unity 的临时输出 DLL。因此这些失败没有进入本次源码编译阶段，也不能作为代码错误或通过证据。
- 项目负责人已确认本次 review 修正后的 Unity 编译与运行无错误；Stage 0–4 review gate 已关闭。
- 仓库中只有 `Assets/Create/New Sequence Action/New Sequence Action.asset` 已序列化 V1 Timeline，其中四个 EditorId 当前为空。该文件属于用户现有改动，审查未直接重写；如要保留其 V1 内容，应在 Inspector 使用显式 Repair 按钮。
