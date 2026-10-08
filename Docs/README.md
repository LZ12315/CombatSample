# 项目文档索引

更新：2026-10-08。当前仓库源码和用户最新决定优先；阶段记录中的旧配置、待办与测试结果只代表对应时间。

| 目录 | 内容 |
| --- | --- |
| [Current](Current/README.md) | 当前架构、制作工作流、验证合同与最新审查入口。 |
| [Proposals](Proposals/README.md) | 尚未实施的方案。本次整理后没有活动方案正文。 |
| [Archive](Archive/README.md) | 历史计划、阶段交接、调查与验证快照。 |

## Locomotion：先读这几份

1. [当前架构](Current/CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)：理解 ActorLocomotion、Runtime、Motor 和 Animation 的协作。
2. [配置与制作工作流](Current/CombatSample_Locomotion_Authoring_zh-CN.md)：配置 Move／过渡、烘焙动画、修改停止点与脚标记。
3. [阶段提交审查](Current/CombatSample_Locomotion_Commit_Review_2026-10-05_zh-CN.md)：本批修改、Kiana 最新配置、已验证内容与剩余检查。
4. [Actor Motion 验证清单](Current/Actor_Motion_Validation.md)：共享运动合同与回归方法。

旧路线图、转向闪帧调查、距离与脚相接入、结构审查和阶段交付见[Locomotion 历史索引](Archive/Locomotion/README.md)。

## Action、Preview 与场景

- [Scripts 整理记录](Current/CombatSample_Scripts_Organization_2026-10-08_zh-CN.md)：当前目录约定、本轮职责调整及独立后续事项。
- [Action 编辑器与播放架构](Current/CombatSample_Action_Editor_Architecture_2026-09-19_zh-CN.md)：现行 ActionAsset／ActionRuntime 链路。
- [Animation → Action → Actor 收尾审查](Current/CombatSample_Animation_Action_Actor_Closure_Audit_2026-09-23_zh-CN.md)：该阶段的实施与验收记录。
- [Preview 确定性审查](Current/ActionPreview_Determinism_Audit_2026-10-01_zh-CN.md)、[Timeline 吸附交接](Current/ActionTimeline_Snapping_Handoff_2026-10-01_zh-CN.md)：对应编辑器问题的核对记录。
- [场景归属基线](Current/Scene_Ownership_Baseline_2026-08-02.md)：指定日期的职责快照，使用前核对当前场景。

## 历史来源

- [Action V1 阶段记录](Archive/ActionV1/)、[Action 实施路线图](Archive/CombatSample_Action_Implementation_Roadmap_v1_zh-CN.md)：历史实施过程。
- [ActionSequence v3 架构](Archive/CombatSample_ActionSequence_Final_Architecture_v3_zh-CN.md)、[E3 验收交接](Archive/CombatSample_E3_v3_Validation_Handoff_2026-08-29_zh-CN.md)：旧后端与运动 Domain 的来源。
- [HitStop 边界修正](Archive/CombatSample_HitStop_Boundary_Fixes_2026-09-19_zh-CN.md)：当时实施与回归结果。

ActionSequence、Legacy Timeline 与 AnimationConfig 的旧路径已退出当前实现依据。Locomotion 和 Camera 开发各自以当前源码与明确任务为准。
