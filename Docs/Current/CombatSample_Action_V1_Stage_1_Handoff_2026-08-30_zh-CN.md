# CombatSample Action V1 — Stage 1 Handoff

> 状态：**Implementation complete; Unity acceptance pending**  
> 日期：2026-08-30  
> 前置：Stage 0 E3 baseline 已完成 Unity 人工复验  
> 架构目标：[Action Final Architecture v1](../Proposals/CombatSample_Action_Final_Architecture_v1_zh-CN.md)  
> 路线图：[Action Implementation Roadmap v1](../Proposals/CombatSample_Action_Implementation_Roadmap_v1_zh-CN.md)

## 1. 本 Stage 交付

- `ActionAsset` 新增独立的 inline `Timeline` (`ActionTimelineData`)；Legacy Timeline、Sequence data、PlaybackBackend 和当前 `ActionPlayer` 路径未改。
- 既有 `ActionAsset` Inspector 新增 **Action V1 Timeline** 折叠区，作为 Stage 1 的基础序列化数据编辑入口；完整 Timeline/Details/Preview 工作流仍属于 Stage 5。
- 新增 `AnimationSegment`、`GameplayLane`、`GameplayItem` Point/Range 基类，以及 Impulse、HitBox、RootMotion、SelfRotation、VelocityOverride、MotionPolicy、Tag 七种 V1 Item 数据与 inline Config。
- Duration 使用固定 60Hz 自动推导，最短为 1 Frame；所有内容（包括 Muted）参与 Duration。
- 新增 Editor-only `ActionAuthoringIdentity` 和 `ActionAuthoringValidator`：覆盖稳定 EditorId、时间/SourceRange、Animation overlap、Config、及需要 RootMotionData 的 Item。
- 新增 `AnimationAsset`（Clip + `RootMotionTrajectory`）和 Bake/Rebuild Inspector；Bake 设置抽为 `RootMotionBakeSettings`，旧 `AnimationConfig` 仍通过同一 Baker/Hash/Oracle/Validator 核心工作。

## 2. 明确未做的内容

- 不创建 `ActionRuntime` 或 Scheduler，不执行 V1 Item，不播放 V1 AnimationSegment。
- 不修改 `ActionPlayer`、`ActionStateManager`、`ActorAnimation`、`ActorMotor` 或 E3 phase order。
- 不迁移任何正式 ActionAsset / AnimationAsset，不删除 Sequence、Legacy Timeline 或旧 bake API。

## 3. 自动验证

| 检查 | 结果 | 说明 |
| --- | --- | --- |
| Stage 1 Runtime + Editor + 新测试源编译 | Passed | `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`，以临时 MSBuild include 覆盖 Unity 尚未刷新生成项目文件的新增源；0 warning、0 error |
| 旧 / 新 bake settings 同采样合同 | Compiled | `RootMotionBakerTests.NewBakeSettings_ProduceTheSameSamplingContractAsLegacyConfig` 已加入，待 Unity Test Runner 执行 |
| Timeline serialize/identity/validator tests | Compiled | `ActionAuthoringDataTests` 已加入，待 Unity Test Runner 执行 |

## 4. 待 Unity 工作站验收

本环境的 Unity batch Test Runner 启动后未产出 test result 或 log，因此未将它计为通过。请在 Unity 2022.3.62f3 中完成：

1. 运行全部 EditMode tests，重点确认 `ActionAuthoringDataTests` 和 `RootMotionBakerTests`。
2. 创建临时 `ActionAsset`，在 Inspector 添加全部七种 Item；保存、Domain Reload、Lane/Item reorder、Undo/Redo 与 EditorId repair 后确认数据和 ID 不丢失。
3. 创建临时 `AnimationAsset`，指定持久化 Clip 与 reference rig；验证 Bake → Ready、再次 Rebuild、以及修改 Clip/Rig/settings 后变为 Stale。
4. 运行 `MiHoYo_Release`，确认旧 Action、Root Motion、HitStop、cancel/switch 无行为差量和新增 Console 错误。

完成以上验收前，不进入 Stage 2。
