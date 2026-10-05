# Locomotion 历史记录

整理于 2026-10-05。这里保留实施过程、当时的审查发现和验证结果；正文中的“下一步”“未关闭”只代表对应阶段。

当前入口是[现行架构](../../Current/CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)、[制作工作流](../../Current/CombatSample_Locomotion_Authoring_zh-CN.md)和[最新提交审查](../../Current/CombatSample_Locomotion_Commit_Review_2026-10-05_zh-CN.md)。

| 记录 | 用途 |
| --- | --- |
| [阶段 0 基线](CombatSample_Locomotion_Stage_0_Baseline_2026-09-25_zh-CN.md) | 迁移前的输入、Motor、素材与场景盘点。 |
| [实施路线图 v1](CombatSample_Locomotion_Implementation_Roadmap_v1_zh-CN.md) | 最初分阶段方案与阶段收敛，包含后来被取代的“移除距离匹配／脚相”结论。 |
| [转向闪帧交接](CombatSample_Locomotion_Turn_Flash_Handoff_2026-10-01_zh-CN.md) | 当时的现场 Trace、参数来源调查与后续决定。 |
| [运行逻辑审查](CombatSample_Locomotion_Runtime_Audit_2026-10-03_zh-CN.md) | 参数语义、覆盖告警移除与速度匹配讨论过程。 |
| [Stop 距离与脚相接入](CombatSample_Locomotion_Stop_Distance_FootPhase_2026-10-03_zh-CN.md) | 自动数据、Kiana 素材修复、距离时钟与脚相参考的演进和历史测试。 |
| [结构审查](CombatSample_Locomotion_Structure_Review_2026-10-05_zh-CN.md) | 整理前的代码证据、风险分析和交接姿势决定。 |
| [结构整理交付](CombatSample_Locomotion_Structure_Refactor_2026-10-05_zh-CN.md) | 五个实施阶段的变更、验证与当时未执行的原生检查。 |
| [Actor Motion 验证历史](Actor_Motion_Validation_2026-10-01.md) | 旧版验证清单完整快照，包括 2026-08-29 E3-H 结果和后续 Locomotion 调查。 |

归档保留旧配置、旧成员名和临时日志路径。源码链接用于查找现有位置，不保证历史行号或成员仍存在。历史测试通过数不代表当前工作区全部测试已通过；最新验证边界见 Current 的提交审查。
