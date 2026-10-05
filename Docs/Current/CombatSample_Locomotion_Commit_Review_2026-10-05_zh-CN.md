# Locomotion 阶段提交审查（2026-10-05）

## 结论

当前修改可以整理为 Locomotion 开发分支的阶段提交。用户已反馈结构整理后的运行体感正常，并确认两轮 Inspector 调整。完整 Unity 原生合同仍待执行，停止衔接的偶发腿部调整尚未完成最新配置的场景验收；此提交不代表全部视觉问题已经解决。

审查阶段尚未创建提交或推送。审查时本地分支为 `Locomotion`，HEAD 为 `3490ecb5c13203cb763b833eb3c1f1df51e2020f`，远端与其一致。2026-10-06 用户授权按下述两个范围使用中文提交信息提交并推送；执行前重新 fetch，确认本地与 `origin/Locomotion` 没有领先或落后提交。上述未完成验收继续保留。

## 建议提交范围

按可独立审阅的两个范围提交，顺序如下。第一项同时包含功能、后续结构整理与编辑器：这些修改在同一批尚未提交的文件中，拆分成历史实施阶段需要重新构造中间实现，因此本次以当前完整实现为单位。旧 Locomotion Inspector 仍引用已删除的覆盖告警接口，必须随核心代码一起更新，不能仅将它的移动和新界面留到后续提交。

| 提交 | 内容与文件范围 |
| --- | --- |
| `整理 Locomotion 运行链、动画数据与资产编辑器` | ActorLocomotion、ActorAnimation、ActorMotor、Motion/LocomotionRunner；Locomotion/Configuration、Runtime、Contracts、Editor；旧位置脚本删除与相应新位置 `.meta`；AnimationAsset、AnimationRigAsset、RootMotionBakeSettings、AnimationLocomotionData、AnimationFootContactBaker、烘焙工作流与结果、AnimationAssetEditor；相关合同测试；架构、开发记录、本审查记录与文档索引 |
| `完善 Kiana Locomotion 动画资源与导入配置` | Motion 下 Idle、Walk、Run、Start、Left/Right Stop 新 AnimationAsset 及 `.meta`；Jump 重烘焙；Kiana Normal 配置；RunBS、Left/Right Stop、StandBy、Walk 的现有 FBX `.meta` 调整 |

每次提交均应包含对应新增／移动文件的 `.meta`，包括新增目录的 `.meta`。本次涉及的资源修改单独呈现，保留当前作者配置。

## 本次不纳入的工作区差异

- `Assets/Create/Animation/Kiana/Combat/Anim_Kiana_Attack_3A.asset`：Combat 动画的重烘焙、Locomotion 派生数据及旧字段清理，超出本轮 Locomotion 素材提交范围。保留工作区修改，另行确认与提交。
- `Assets/Settings/Input/PlayerInputControl.inputactions.meta`：Importer 的 `script` 从持久 GUID 引用变成 `{instanceID: 0}`；不是本轮输入或 Locomotion 代码修改所需。推送中应保留 HEAD 版本，工作区现有修改不回退。

## 当前实现摘要

- Move 支持速度与输入强度参数源；Kiana Normal 使用 InputStrength。方向响应与松键减速分别配置。
- Runtime 构造时绑定执行配置与动画派生数据；构造期按 AnimationAsset 去重。重入和图重建使用原绑定，新 Runtime 才读取新配置。
- Tick 锁定输入，运动只积分一次；动画阶段读取当前 Action owner、垂直速度与已求值基础层脚相。
- Stop 按方向、脚相、作者顺序选片，模型刹车距离决定采样时间。零距离采样作者停止点一次，随后恢复普通时间收尾；再次输入可以打断。
- 会话、Runtime、图和 owner 生命周期各自清理。交接冻结基础层实际已求值的混合姿势，快照独立于旧 Runtime。
- Move 准备统一按参数、权重、非循环样本重新入场、倍率的顺序执行；保留实际运动反馈速度匹配。
- Locomotion 代码归入 Configuration、Runtime、Contracts、Editor；组件和共享 Motion 合同保留原目录。Editor Trace 为条件编译 partial。
- Locomotion Inspector 使用 Mode Selection、Tags、Movement Config、Move、Transitions 卡片；Movement 与 Move 字段平铺，列表保留原样。
- AnimationAsset Inspector 使用 Animation、Bake、Root Motion、Stop Point、Foot Markers 卡片。显示 Clip 来源文件，正常烘焙状态采用简洁状态行；Root Motion 曲线亮色，Stop Time 和 Remaining Distance 随 Override 保持一致；去除 Foot Markers 下方说明。

## 当前资源与已知问题

审查时 `Locomotion_Kiana_Normal` 的 Stop 列表已包含独立 Left 与 Right 两个候选，方向均为 `(0, 1)`。Left 覆盖停止点为 `0.8333333 s`，Right 为 `1 s`；运动参数为 MaxSpeed 5、Acceleration 20、Deceleration 12、DirectionResponse 12，混合时长为 0.1 秒。

此前诊断时保存的配置只有 Left Stop，脚相排序无法发挥选片作用。本次审查时 Right 已加回。尚未在场景验证加回后的停止衔接，不将旧配置发现视为最新配置下问题已经解决的证据。即使两条候选都可用，脚相仍是近似排序，距离入场帧未必能覆盖所有跑步姿势。

Walk 的 Importer 改为保留水平原始位置，与 Run 的根轨迹配置不同；该资源调整单独保留在素材提交中，不声称 Walk 已验证距离／速度匹配资格。RunBS 缩短到 35 帧并保留原始朝向；Jump 重烘焙包括旋转轨迹变化，需保留地空交接检查。

## 本次验证

- 使用 Unity 2022.3.62f3 自带 Roslyn 和当前工程依赖重新编译 Runtime，以及 Editor／测试程序集：均通过。编译产物全部写入 `/tmp/locomotion-submit-review/`，未编辑原项目生成目录。
- Runtime 编译仍有已有的 Impact 旧类型、隐藏成员和 ActorMotor 兼容字段警告；没有编译错误。
- 使用实际新编译程序集，在独立 Mono 进程执行现有可用的纯逻辑合同：35 项断言通过，覆盖输入 Buffer、决策、Stop 曲线与时钟、自动脚标记、脚相、配置校验、运动反馈与倍率。
- 独立进程额外尝试的两项合同因缺少 Unity 原生调用而未能完成：旧资产默认配置需要 ScriptableObject／JsonUtility；插值停止点需要 Quaternion.SlerpUnclamped。它们不是已通过的合同。其他图、对象与引擎合同未在该进程执行。
- 尝试创建隔离项目补跑 EditMode 套件，准备包副本时 `/tmp` 空间不足；直接启动当前 Unity 二进制又缺少 `libxml2.so.2`，Editor 未启动、没有生成测试结果 XML。已删除本次创建的隔离项目副本并释放空间，保留编译和纯逻辑日志。此轮阻碍不同于旧记录里的许可证问题。
- 10 个已有脚本移动后的 `.meta` 与 HEAD 原文件逐字节一致；Assets 扫描未发现重复 GUID，新增脚本／资源没有缺失 `.meta`。Locomotion 新目录的 `.meta` 齐全。
- 拟提交的 Kiana AnimationAsset 与 LocomotionAsset 引用 GUID 均能解析到 Assets 中现存的 `.meta`。此静态检查不替代 Unity 的 Clip 导入与烘焙资格检查。
- 提交前差异检查发现 Left Stop FBX `.meta` 一处新增尾随空格；本次仅去除该行空格，没有改变 Importer 参数。

历史验证与实施细节见[停止距离与脚相记录](../Archive/Locomotion/CombatSample_Locomotion_Stop_Distance_FootPhase_2026-10-03_zh-CN.md)、[运行审查](../Archive/Locomotion/CombatSample_Locomotion_Runtime_Audit_2026-10-03_zh-CN.md)和[结构整理交付记录](../Archive/Locomotion/CombatSample_Locomotion_Structure_Refactor_2026-10-05_zh-CN.md)。历史临时日志不作为本轮新测试结果。

## 剩余验收

在可运行的 Unity Editor 中执行 LocomotionContractTests、LocomotionSetDecisionTests、LocomotionAnimationContractTests、LocomotionMatchingContractTests、AnimationLocomotionDataTests、RootMotionTrajectoryTests 与 RootMotionBakerTests。

人工检查当前 Left/Right 配置的快速启停、连续转向、混合中松键、刹车中再次输入、地空交接与 Action 结束。确认偶发腿部调整是否消失，再决定是否需要进一步分析素材标记或播放混合。完整原生套件通过前，保留上述验证边界。

## Docs 整理（2026-10-06 完成）

本批提交同时包含以下文档整理，未因此改变代码或资源：

- 现行架构从 Proposals 移至 Current；更新参数语义、Set 配置、功能范围与验证状态，保留原文件名以便追溯。
- 增加[制作工作流](CombatSample_Locomotion_Authoring_zh-CN.md)和 Current 索引；根索引明确架构、制作与最新验证的阅读顺序。
- 路线图、阶段 0、转向交接、运行审查、距离与脚相接入、结构审查和阶段交付移至 Archive/Locomotion；补充归档说明并重建相对链接。
- 原 Actor Motion 验证文档完整保存在归档快照中；Current 验证清单按现行输入参数、Stop 模式、绑定与生命周期规则重写。历史通过结果不转写为本轮通过。
- Proposals 清除已落地方案的活动入口；旧路线图中的“下一步”不再作为现行待办。

文档检查覆盖现行入口与本次迁移记录的本地链接，以及差异格式；没有因文档修改重复运行代码测试。最新停止衔接与 Unity 原生检查仍保留在上述剩余验收中。
