# Action V1 Stage 5R 历史记录

> 归档日期：2026-09-08
> 权威级别：历史证据，不代表当前 Preview 实现或下一步执行授权

本目录保存 Stage 5 原始实现、5R.0–5R.5 Timeline/Details 迭代、开源 Timeline 调研、两份 HTML 交互原型，以及已经撤下的两轮 Preview 尝试。

当前可用边界请阅读 [Preview 前编辑器检查点](../../../Current/CombatSample_Action_V1_Editor_PrePreview_Checkpoint_2026-09-08_zh-CN.md)。Timeline / Details 的行为事实仍可从这些实施记录追溯，但发生冲突时以当前代码和 Current 检查点为准。

## 归档分组

- `CombatSample_Action_V1_Stage_5_Handoff_*`：第一次 Stage 5 实现，视觉和交互已被 5R 系列取代。
- `CombatSample_Action_V1_Stage_5R_0_*` 至 `Stage_5R_5_*`：当前 Timeline / Details 的渐进实施与验收记录。
- `CombatSample_Action_V1_Stage_5_Open_Source_Timeline_Audit_*`：第三方与开源方案调研；项目未引入第三方 Timeline 编辑器源码。
- `VisualReferences/`：Point 和 Time Viewport 的已确认 HTML 原型，仅作为设计参考。
- `Stage_5R_6_*`：已撤下的 PreviewRenderUtility 与 SceneView / Animancer 试验。不得据此继续实现。

## Preview 失败证据

- 早期独立 Preview 视口造成 Unity Editor 明显卡顿。
- SceneView / Animancer P1 在 `Avatar_Kiana_C2_Ani_Attack_3_fix` 和 `Avatar_Kiana_C2_Ani_Attack_5_fix` 上出现四肢或身体明显变形。
- 根因调查被有意停止，现有证据不足以认定具体原因；归档文档中的设计假设不等于结论。
