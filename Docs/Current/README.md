# 当前文档

当前源码和用户最新决定优先。架构说明描述现行实现；审查和交付记录分别标明验证时间与边界。

## 开发分支整合

- [HyperGraph → main 整合整理](Main_Migration.md)：迁移分支基线、合并取舍、当前验证和后续按模块清理范围。

## 跨机器继续开发

- [项目拉取入口](../../README.md)：Unity 版本、分支和新电脑首次打开步骤。
- [跨机器交接与验证](Project_Transfer.md)：完整工作区同步范围、依赖核对、干净导入结果与已知限制。

## 项目结构

- [项目结构与归位约定](Project_Structure.md)：当前 Assets 目录职责、Data 与 Resources 边界、本轮兼容代码清理及验证范围。

## 开发就绪审查

- [Demo 开发就绪审查](Demo_Development_Readiness_Audit.md)：2026-10-18 的主链路问题快照、缺陷与能力缺口、GUID 扫描范围及验证边界；页首记录已实施范围，不代表发布验收通过。

## 玩家输入

- [玩家输入合同与本轮修复](Player_Input_Contract.md)：当前工作区已实现的设备采集、固定 Tick、当前 State / 纯 Event Sequence、方向基准、候选原子消费、生命周期与验证边界。
- [State / Sequence 方案与时间窗口调研](Player_Input_Sequence_Design.md)：已执行的设计与迁移、可调的 0.5 秒整段默认窗口、公开资料及证据限制；77 个选定纯逻辑用例通过，Unity 原生集成与手感验收待做。

## 战斗

- [胜负结算与整场重开](Battle_Restart_Flow.md)：胜利／死亡结果、倒计时进度线、默认 3 秒结算＋2 秒过渡后才加载、敌人绑定与验证边界。
- [Demo 空中连段次数限制](Demo_AirCombo_Limit.md)：每次滞空只能成功起手一套空中连段；组件／Condition 接入与人工验证步骤。
- [弹反代码合同与配置](Combat_Parry_Contract.md)：命中裁决、有效窗口与方向、被弹反事件、Unity 配置步骤、验证结果与未验收边界。

## 相机

- [CameraControl 职责与整理边界](Camera_Control_Architecture.md)：软硬锁模块、调度与切换、代理目标保护、旧入口清理及人工验证清单。

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
- [Action 区间特效、曲线震屏与隔离粒子预览](Action_Effect_Contract.md)：VFX 绑定 / 朝向与共用播放能力，原生 Impulse 的 Action 曲线、专用通道和严格清理，以及隔离粒子预览与实际画面验收边界。
- [Animation → Action → Actor 收尾审查](CombatSample_Animation_Action_Actor_Closure_Audit_2026-09-23_zh-CN.md)：该阶段的变更与验收边界。
- [Preview 确定性审查](ActionPreview_Determinism_Audit_2026-10-01_zh-CN.md)：Preview 问题与当时核对结果。
- [Timeline 吸附交接](ActionTimeline_Snapping_Handoff_2026-10-01_zh-CN.md)：对应交互调整的交接记录。

## NodeCanvas

- [自身 Agent 与显式 Actor Target](NodeCanvas_Actor_Target_Contract.md)：自定义节点参数、Get Combat Target 快照行为、旧图接线清单与验证边界。

## 场景

- [战斗训练大厅交付](Combat_Training_Arena.md)：最新 HyperGraph 玩法迁入、完成版大厅资源、清理结果及场景重开依赖。
- [场景归属基线](Scene_Ownership_Baseline_2026-08-02.md)：指定日期的场景职责快照，使用时以当前场景核对。
