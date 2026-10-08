# 当前文档

当前源码和用户最新决定优先。架构说明描述现行实现；审查和交付记录分别标明验证时间与边界。

## Scripts 组织

- [Scripts 整理记录](CombatSample_Scripts_Organization_2026-10-08_zh-CN.md)：目录职责、文件归位、反馈与 HitBox 边界、取消匹配共用规则、验证边界，以及输入历史和 Details 配置绘制的独立后续事项。

## Locomotion

建议按以下顺序阅读：

1. [当前架构](CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)：组件协作、Tick、绑定、动画选择、匹配与生命周期。
2. [资产与动画制作工作流](CombatSample_Locomotion_Authoring_zh-CN.md)：Inspector 分区、参数源、烘焙、停止点、脚标记和修改生效方式。
3. [阶段提交审查](CombatSample_Locomotion_Commit_Review_2026-10-05_zh-CN.md)：拟提交范围、最新 Kiana 配置、验证和待验收问题。
4. [Actor Motion 验证清单](Actor_Motion_Validation.md)：共享运动合同与当前回归入口。

实施过程与旧路线图见[Locomotion 历史索引](../Archive/Locomotion/README.md)。

## Action 与 Preview

- [Action 编辑器与播放架构](CombatSample_Action_Editor_Architecture_2026-09-19_zh-CN.md)：现行 ActionAsset／ActionRuntime 与编辑器职责。
- [Animation → Action → Actor 收尾审查](CombatSample_Animation_Action_Actor_Closure_Audit_2026-09-23_zh-CN.md)：该阶段的变更与验收边界。
- [Preview 确定性审查](ActionPreview_Determinism_Audit_2026-10-01_zh-CN.md)：Preview 问题与当时核对结果。
- [Timeline 吸附交接](ActionTimeline_Snapping_Handoff_2026-10-01_zh-CN.md)：对应交互调整的交接记录。

## 场景

- [场景归属基线](Scene_Ownership_Baseline_2026-08-02.md)：指定日期的场景职责快照，使用时以当前场景核对。
