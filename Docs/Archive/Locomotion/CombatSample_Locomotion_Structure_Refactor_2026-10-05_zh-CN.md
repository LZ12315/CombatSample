# Locomotion 结构整理交付记录（2026-10-05）

> 归档于 2026-10-05。正文中的状态、待办和验证结果保留为当时记录；当前实现见[现行架构](../../Current/CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)，最新验证与未完成检查见[提交审查](../../Current/CombatSample_Locomotion_Commit_Review_2026-10-05_zh-CN.md)。源码链接用于导航，历史成员和行号不代表当前代码。

## 范围与结果

按绑定 → 生命周期 → Tick → Move 准备 → 文件布局实施。保持原有操控与选择规则、速度匹配、Stop 距离时钟，以及交接冻结基础层实际已求值混合姿势的规则。未修改 Kiana 动画、运动参数、停止点、场景或 Prefab；不增加配置、刷新入口、素材特殊规则和 Inspector 告警。

本轮开始前工作区已有动画数据、资源和合同测试修改；这些保留为基线。以下记录针对本轮结构整理。

## 当前调用链

```text
控制提交 → IntentBuffer
BeginControlTick → 锁定输入
BuildMotionRequest → 选择 Asset / 缓存 Runtime → Runner 积分一次
                  → 缓存请求 + 输入 / 前后速度 / Motor 快照
ActorMotor → 仲裁及提交运动请求
UpdateAnimation → 读取 Action owner / 垂直速度 / 实际播放脚相
                → Runtime 决策及准备动画请求
ActorAnimation → 挂接 / 重播 / 显式采样
               → MovePlayback 更新参数、权重、非循环重入、倍率
               → 混合与最终求值
World / Motor → 发布实际运动结果，供下一 Tick 合格反馈使用
```

待提交输入独立于 Tick。重复 Build 返回同一请求；没有 Begin 时使用无输入；冻结只恢复被消费的一次性输入，更新或清空优先。零动画时间不提交，不推进输入决策边沿。

## 变更入口

| 文件／目录 | 本轮变化 |
| --- | --- |
| [ActorLocomotion](../../../Assets/Scripts/Actor/ActorLocomotion.cs) | Tick 值结构、固定运动快照、会话结束与 Runtime Reset 分开、匹配组件与 owner 的失效通知 |
| [ActorAnimation](../../../Assets/Scripts/Actor/ActorAnimation.cs) | 独立关闭／图替换单向通知；挂接后统一 Move 准备；保留独立保护快照 |
| [ActorLocomotion.Debug](../../../Assets/Scripts/Actor/ActorLocomotion.Debug.cs) | Editor 条件编译 partial，保留 Trace 入口 |
| [ActorMotor](../../../Assets/Scripts/Actor/ActorMotor.cs) | 隐藏三个已有迁移字段，保留原名与序列化值 |
| [Configuration](../../../Assets/Scripts/Actor/Locomotion/Configuration) | 原 Asset 与条件搬入；序列化字段、工厂及类型名不变 |
| [Runtime](../../../Assets/Scripts/Actor/Locomotion/Runtime) | 只读 Move/Set 绑定与构造期去重；基类、共同动画、Mixer/Set 分文件；Tick、输入 Buffer 和 Move 准备归入此处 |
| [Contracts](../../../Assets/Scripts/Actor/Locomotion/Contracts) | 公共上下文、输入、请求、状态与 owner；Runner 和共享 Motor 合同保留原 Motion 目录 |
| [Editor](../../../Assets/Scripts/Actor/Locomotion/Editor) | 原 Locomotion Inspector 搬入 |
| [AnimationAsset](../../../Assets/Scripts/Animation/AnimationAsset.cs) | 增加接受已确认轨迹的内部派生入口，避免绑定重复查询资格；原调用保留 |

绑定定义复制数值和列表，不保留作者容器。每个 AnimationAsset 在该次绑定中获取一次轨迹资格结果；同一资源在 Move 和多个过渡中复用数据。Runtime 保留身份和执行状态；延迟创建、退出重入或图重建均消费原绑定，新 Runtime 才读取新配置。

生命周期顺序为保护并结束会话 → Runtime.Exit → 释放标签。独立图失效通知只对匹配会话生效，不反向清理发起端。首次会话创建不再次 Reset 已 Enter 的 Runtime。新动画正常混合接入，旧 Runtime 销毁不影响独立保护快照。

## 验证结果

- 五个实施阶段均通过 Unity 2022.3.62f3 Roslyn 的 Runtime 与 Editor／测试程序集编译。
- 最终 Runtime、Editor／测试与去除 UNITY_EDITOR 定义的条件编译检查通过。后者用于确认 Trace 隔离，不代表 Player 构建。
- 使用实际程序集在独立 Mono 进程运行 38 项纯逻辑合同断言，全部通过：输入 Buffer、决策边界、Stop 时钟／曲线、脚相数据和反馈／倍率规则。该进程不提供 Unity 原生调用，并产生原生 Quaternion 调用未解析提示；此结果仅覆盖实际执行的纯逻辑，不替代 Unity Test Runner。
- 新增合同覆盖锁定输入、重复 Build、冻结恢复与新提交／清空优先、无 Begin、取消、零动画时间后的单次提交与前后速度快照、会话失效 Reset 与旧 owner 拒绝、更新权重后的倍率和非循环重入。新增及既有图合同编译通过，运行状态如下。
- 在 `/tmp` 隔离项目中尝试运行 Locomotion、AnimationLocomotionData 与 RootMotionBaker EditMode 套件；Unity 启动后报 `No valid Unity Editor license found`、`No ULF license found`，未执行测试，也未生成测试结果 XML。
- 11 个已有脚本移动时 `.meta` 字节不变；所有新脚本具备 `.meta`，扫描 Assets 未发现重复 GUID。
- 对比本轮开始的 7109 个资源文件哈希，均未变化；原项目生成目录未用于写入编译或测试输出。

验证日志位于本机 `/tmp/locomotion-structure-check/`；临时目录不是持久交付文件。

## 待完成的运行验收

许可证可用后，执行现有 EditMode 合同套件，包括真实 Animancer 图的同步、首次挂接、混合脚相、会话失效、图重建与保护快照测试；编译不替代这些验证。

人工检查：快速启停、连续转向、混合中松键、刹车后再次输入、地空交接和 Action 结束。确认交接没有姿势跳变、刹车仍可打断、旧会话不影响新动画，标签退出后正确释放。脚相择优继续是近似，保持原验收边界。
