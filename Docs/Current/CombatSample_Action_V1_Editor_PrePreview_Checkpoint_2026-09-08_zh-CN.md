# CombatSample Action V1 — Preview 前编辑器检查点

> 日期：2026-09-08
> 状态：Stage 0–4 基础与 Timeline / Details 已保存；Editor Preview 已撤下并暂停设计
> 分支：`FrameWork`

## 当前可用能力

- Stage 0–4 的 Action V1 数据、校验、Gameplay runtime、Scheduler 和动画姿势旁路仍然保留。Runtime 动画采样不是 Editor Preview，本次没有撤回。
- Action Timeline 是正式的 V1 时间与结构编辑入口，保留创建、选择、固定行 overlap 导航、拖动、Resize、Trim、缩放、Time Range Navigator、Undo/Redo 和会话剪贴板。
- Action Details 只编辑 Primary Selection，保留时间草稿、显式应用、七种 Config、Action rules、原生 SerializedProperty/IMGUI Drawer 及局部刷新。
- ActionAsset Inspector 保留 V1 打开入口、Duration/Validation 摘要和显式 EditorId Repair。
- Legacy Timeline、Sequence Editor、双击路由和正式战斗播放没有切换到 V1。

## Preview 当前状态

`ActionV1PreviewWindow` 仅保留兼容占位窗口。Timeline、Details 和菜单仍可打开它，但窗口不会创建 Preview Scene、角色副本、AnimancerGraph、相机交互、资源监听或 Editor update 回调。

Preview 仍是后续需求，但实现路线已经暂停，必须重新讨论后再形成新计划。不得根据已归档的 P0/P1 文档自动继续 P2、5R.7 或资产迁移。

撤下原因和已知现象：

- 原 Preview 实现曾导致 Unity Editor 明显卡顿。
- SceneView / Animancer P1 使用 `Avatar_Kiana_C2_Ani_Attack_3_fix` 和 `Avatar_Kiana_C2_Ani_Attack_5_fix` 时出现四肢或身体明显变形。
- 问题根因尚未证实；本检查点不声称问题已经修复，也不把某种 Preview 技术路线标记为当前方案。

## 文档权威

- 冻结的 Action V1 最终架构和 Stage 0–7 Roadmap 仍是目标与迁移边界的权威。
- Stage 5 Editor Redesign 仍是 Timeline / Details 的设计权威；其中 Preview 章节只保留为产品目标，不再指定当前技术实现。
- Stage 5R.0–5R.5 的实施记录、原 Stage 5 handoff、开源审查、HTML 原型及两轮 Preview 试验均位于 `Docs/Archive/ActionV1/Stage5R/`，只作为历史证据。
- 当前实现状态以本文、Stage 0–4 Current handoff 和仓库代码为准。

## 验证状态

- Stage 3–4 基础提交：`de2bc0d5`。
- Preview 撤下后的 Unity 生成工程 `dotnet build`：0 errors；现有包和旧代码警告不属于本次改动。
- Timeline / Details 已由用户在此前迭代中接受；本次整理后的 Unity 快速回归仍需用户确认。
- Unity Test Runner 不作为本检查点门槛。

## 继续开发前

先在 Unity 中确认 Timeline、Details、Undo/Redo、Legacy 运行和 Preview 占位窗口无新增异常。之后可以继续讨论 Preview 的最小目标、角色来源和复用策略；未经新设计确认，不恢复归档实现。
