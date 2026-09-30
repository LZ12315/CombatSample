# Actor Motion v3 验证清单

> Status: Verified v3 validation contract; E3-H acceptance passed
> Static structure verified: 2026-08-29
> Locomotion Stage 2 static structure updated: 2026-09-27; Unity compile and scene checks remain pending.
> Locomotion Stage 3 code updated: 2026-09-28; Runtime/Editor static C# compile passed. User confirmed Unity import/compile has no errors. Unity tests, resource coverage and visual acceptance remain pending.
> Locomotion 收敛：2026-10-01；保留基础动画 Runtime 与 Move 速度匹配，脚相和 Stop Distance Matching 已移除。Runtime、Editor（含测试）及非 Editor 静态编译通过；Unity Test Runner 与角色视觉验收待完成。
> 当前执行入口：ActionRuntime。运动 Domain 的历史设计来源见[归档 v3 架构](../Archive/CombatSample_ActionSequence_Final_Architecture_v3_zh-CN.md)；本文只保留仍与当前代码一致的运动合同。

本文是当前 Actor Motion 验证合同。`Docs/Archive/Actor_Motion_Validation.md` 描述的是已经退出 authority 的 `ActorMotionRuntime / MotionChannels / RootMotionBuffer / Animator Root Motion`，不得继续作为当前实现依据。

## 1. 当前权威链路

```text
Player / AI → ActorLocomotion pending LocomotionIntent
→ Control locks one Intent snapshot for Action and Motion
Action arbitration and ActionRuntime items → ActorMotor policies / owners
→ ActorMotor.BeginMotion consumes current ForceUnground and publishes a read-only Motion context
→ ActorLocomotion selects one LocomotionAsset from Conditions + Priority
→ the Asset's per-Actor Runtime uses the shared LocomotionRunner to submit LocomotionMotionRequest
→ ActorMotor applies Policy and prepares Translation / Rotation
→ ActorLocomotion updates its Runtime animation from the locked Motion snapshot
→ LocomotionAnimationRequest is submitted through an owned ActorAnimation Layer 0 session
→ ActorAnimation evaluates Layer 0 and Action Layer 1 once, before World solve
→ KCC simulates requested motion
→ ActorCollisionResolver applies deterministic actor separation
→ ActorMotor publishes requested/actual world result
```

- `ActorMotor` 是 Gameplay Actor movement authority，KCC 是实际 world solve。
- `TranslationDomain` 持有 HorizontalVelocity owner、Trajectory Root Motion、HorizontalImpulse、VerticalVelocity owner 与 Ballistic state。
- `RotationDomain` 持有 Scripted / Root owner；ActorLocomotion 的共享 `LocomotionRunner` 只提供最低优先级 locomotion yaw。
- `MotionPolicyState` 独立组合 LocomotionScale、AirLocomotionScale 与 GravityScale。
- Gameplay Root Motion 只来自 `RootMotionTrajectory` 当前查询区间，不读取 Animator delta。
- LocomotionAsset 当前分为 `LocomotionSetAsset` 与 `LocomotionMixerAsset`；两者在同一候选列表中通过 Conditions + Priority 选择，没有全局 Fallback。Gameplay Facing 由 Intent 显式提交，不由 1D/2D Move 动画类型推断。
- 上述 Locomotion 链已经静态落地；Unity 编译、Kiana/Jaeger 场景移动和 Ground/Air 切换结果尚未回写为通过。目标动画 Runtime 见[Locomotion 最终架构 v1](../Proposals/CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)。
- 基础 Runtime 使用 Linear/Directional Move Mixer 与代码驱动的 Move/Start/Stop/Pivot；Sync 仅配置 Animancer 基础同步。Move 参数来自模型速度；Air VerticalSpeed 来自已准备请求并去除时间缩放。
- Move 速度匹配按当前权重与 Clip 完整周期的 Root Motion 位移合成参考速度，只读取上一 Tick 合格 World 结果，并去除该结果所属 MovementTimeScale。Air VerticalSpeed 保持 PlayRate 1；普通 Locomotion 不向 Motor 提交 Root Motion。
- Stop 按方向选样本，从时间 0 正常播放到 Clip 结束，随后返回零速 Move。当前实现没有脚相元数据或 Stop Distance Matching。
- Animation 使用 Motion 保存的 Intent、积分前后速度和 Facing；状态完成判断与 Graph 使用同一动画时钟。资源错误维持独立有效基础姿态并报告缺口，不改 Gameplay 或增加第二套 Locomotion。

## 2. 必须保持的不变量

### Translation

```text
HorizontalVelocityOwner > Root Motion > Locomotion + HorizontalImpulse
VerticalVelocityOwner ? owner : BallisticVerticalVelocity
```

- 被覆盖 contribution 继续接收本帧数据或维护自身状态，但不得在恢复时 catch-up。
- Root Motion 是 actor-local interval displacement；其他水平通道是 world-planar m/s。
- requested motion 与 KCC actual result 必须分离；blocked displacement 永不偿还。

### Vertical / Grounding

- Vertical owner active 时 Ballistic temporal evolution 冻结。
- 最后一个 Vertical owner 释放时 Ballistic 归零；恢复下层 owner 时不得错误归零。
- Grounded 将最终 vertical 与 Ballistic 归零，但不自动结束 Vertical owner。
- 有效向上 Add/Set 必须触发 ForceUnground；撞顶只截断正向 Ballistic。

### Rotation

```text
Scripted Rotation > Root Rotation > Locomotion Rotation
```

- Root / Scripted 使用独立 LIFO owner；stale token 不得影响当前 owner。
- producer 解析 Target、Direction、Snap、RotateBySpeed；Motor 只接收 local yaw delta。
- covered yaw 按原帧消费并丢弃，不补偿 missed yaw。

### Time / Lifecycle

- `MovementTimeScale == 0` 时 Locomotion、Gravity、Impulse decay 与 Root interval 一致冻结。
- Pause/HitStop 不重复执行 Action gameplay frame side effects。
- Cancel、Stop、Restart、Disable、Dispose 与 Driver abort 必须幂等释放各自 owner。
- Actor disable/re-enable 后 owner、policy、Ballistic、requested/actual readout 与 KCC pose 不得残留旧帧状态。

## 3. 已继承的 E3-G 人工结果

用户已在 E3-G cutover 后确认以下当时的基础功能回归；这里的“locomotion 正常”仅指未发现基础移动故障，不代表已满足当前提案中的起步、刹停、急转和滑步质量目标：

- Jaeger/Kiana locomotion 正常；
- 普通攻击、DashAttack 和 RetreatAttack 动画速度正常；
- 攻击与受击 Root Motion 的明显滑动、无位移和 T-pose 问题已消失；
- 当前基础战斗链路没有已知 runtime exception。

这些结果不替代下面的 E3-H 差量检查。

## 4. E3-H 差量人工检查

### A. Actor registration / collision order

1. 在 `MiHoYo_Release` 中让两个 Actor 接触并发生侧向推挤，记录方向与质量分配。
2. Play Mode 中依次 disable/enable 两个 Actor，再以相反顺序重复一次。
3. 两次结果应一致；不得因为注册顺序改变 pair resolution。

### B. Root Motion cover / blocked displacement

1. 让有位移的正式 Action 贴墙播放，再离开墙体。
2. 被墙阻挡的 displacement 不得在后续帧补偿或产生瞬移。
3. 在 Velocity owner 或更高 rotation owner 覆盖结束后，应直接使用当前帧 Root/Rotation contribution，无 catch-up。

### C. HitStop time domain

1. 在 Root Motion、空中 Ballistic 或 HorizontalImpulse 存在时触发 HitStop。
2. Action Pose、Locomotion Base、crossfade、Gravity、Impulse decay 和 trajectory interval 应同时冻结。
3. 恢复后不得补帧；受击者现有 4/60 秒延迟冻结语义保持不变。

### D. Cleanup / re-enable

1. 连续切换或取消正式 Action，并在动作中 disable/re-enable Actor。
2. 确认没有 T-pose、空白帧、重复 hit、残留 Tag/HitBox 或 owner。
3. 检查 `ActorMotor > Runtime Debug`：Movement Time Scale 恢复 1，Ballistic/Impulse 与 owner readout 符合当前状态，Root/Scripted owner count 回到 0。

## 5. 结果记录

| 项目 | 状态 | 场景/备注 |
| --- | --- | --- |
| Actor registration / collision order | Passed | `MiHoYo_Release`，2026-08-29 用户差量验收 |
| Root Motion cover / blocked displacement | Passed | `MiHoYo_Release`，无补偿或 catch-up |
| HitStop unified freeze / resume | Passed | `MiHoYo_Release`，冻结与恢复未发现问题 |
| Cancel / handoff / disable-re-enable cleanup | Passed | `MiHoYo_Release`，未发现残留或姿态异常 |
| Reimport / compile / Missing Script / runtime exception | Passed | Unity Preview/Play 与用户跑测未发现问题 |

E3-H 差量验收于 2026-08-29 完成。后续回归若失败，只修复对应 v3 invariant，不顺手重构运动数学。

## 6. Locomotion 阶段 3 检查（待 Unity 验证）

以下项目不继承 E3-H 的 Passed。Runtime/Editor 已通过静态 C# 编译，用户于 2026-09-28 确认 Unity 导入后没有编译错误。新增的 EditMode 测试尚未在 Unity Test Runner 执行；Unity 外的纯决策测试尝试因 NUnit 环境兼容问题停止，不算通过。

1. 配置有效 Kiana 1D 与 Jaeger 2D Move（含零速 Idle），以及 Air VerticalSpeed 样本。确认 Policy 为 0、半速和 Ground/Air 切换时参数合理，无双重时间缩放。
2. 补全 Set 样本，检查静止起步、释放输入、Stop 中重新输入、强反转、Start/Pivot 播完、Stop 播完返回零速 Move。相同反转输入不能重复触发 Pivot；同分方向按资源列表顺序选取。
3. 用非循环 Move Clip 测试离开和重新进入混合区域：重新进入时从头播放，持续有效时不重复重启。
4. 在 Start/Stop/Pivot 和 Move 中覆盖 Action，包含连续切换 Action。基础 Move 隐藏期间继续更新，Action 退出后不恢复旧瞬态或重新起播整个 Locomotion。
5. 在 crossfade 与瞬态中触发 HitStop、暂停和零播放速度。基础动画、瞬态完成判断与 crossfade 同时冻结，恢复无补帧。
6. 分别 Disable/Enable Actor、ActorLocomotion、ActorAnimation，以及测试 Driver abort。验证 owner 清理、标签释放、缓存重建、无旧请求、空白姿态和重复 Enter；普通移动及 Action Root Motion 回归正常。
7. 测试缺失 Clip、重复阈值和非法方向：错误须定位 Actor/Asset/条目并去重，不生成删减样本的正常 Mixer。已有有效 Idle 或基础姿态时保持保护姿态；冷启动完全无有效样本时明确报告无法建立基础姿态。

当前角色样本仍未接入：四个 Locomotion Asset 的 Move 均空；Kiana Set 过渡均空，Jaeger Set 另有一个空 Start 条目。资源文件未由此次代码实现修改，角色视觉出口保持 Pending。

## 7. Move 速度匹配与基础 Stop 验证（待 Unity 执行）

代码及合同测试已静态编译；尚未运行 Unity Test Runner，也未完成角色视觉验收。四个 Locomotion Asset 的 Move 样本仍为空，Kiana Set 过渡均空，Jaeger Set 的 Start 有一个缺 Clip 且方向为零的条目。配置角色样本后才能检查观感。

### 制作检查

1. 为 Ground Move 配置 Idle、Walk、Run、Sprint 等实际拥有的样本，为 Air 配置 VerticalSpeed 样本；保持各样本原有 `Sync` 意图。Start、Stop、Pivot 配置有效 Clip 与方向，不需要脚相、出口标记或 Stop 刹车标记。
2. 需要速度匹配的 Move 样本，通过 AnimationAsset Inspector 或 Bake 窗口烘焙有效 Root Motion 轨迹。Clip、Rig 或烘焙设置改变后按原有工具状态重建；缺轨迹时样本仍播放，但匹配倍率为 1，并报告具体缺口。
3. 素材配置固定于 Runtime 生命周期。修改 Clip、Rig 或样本后重新运行，或禁用再启用 ActorLocomotion 使 Runtime 重建；不要求运行中热刷新。

### 行为检查

| 场景 | 预期 |
| --- | --- |
| Kiana 1D 与 Jaeger 2D | 模型速度决定权重；Sync 成员使用 Animancer 基础同步；不同权重下参考速度和播放倍率平滑变化。 |
| Walk／Run／Sprint 与不同移动速度 | PlayRate 在 0.5–1.5 内平滑调整；轨迹不足、非循环 Move 或参考速度过低时以倍率 1 播放并报告。 |
| 不同初速的松手停止 | Gameplay 正常减速；Stop 按 SourceDirection 选择，从 0 按时间播放至 Clip 结束并返回零速 Move。记录可见滑步，作为以后是否需要距离匹配的依据。 |
| Start／Pivot 完成与中断 | 正常完成返回当前 Move；输入、Action 或 Asset 打断不追播旧过渡。 |
| 非自主位移 | Action Root Motion、Velocity owner、冲量、平台、Actor 分离及未知位移不作为 Move 实际步速。 |
| 半速、HitStop、Action 覆盖与释放 | MovementTimeScale 只应用一次；零 dt 保持状态、倍率和 crossfade，恢复不补帧、不使用暂停前旧反馈。 |
| Asset／Ground／Policy／会话变化 | 旧反馈失效；共享 Gameplay 速度保留；旧 Graph 状态和过期 Layer 0 owner 不能复用。 |
| Disable／Enable 和 Driver abort | 清理幂等，保护基础姿态接管；新会话重新绑定，Action Layer 1 和 Root Motion 仲裁正常。 |

### 验证记录

Runtime、Editor（含合同测试）及无 Editor 定义的静态 C# 编译通过；未启动命令行 Unity 构建或外部 NUnit 宿主。合同测试包含权重与基础 Sync 周期速率、Root Motion 完整周期、反馈排除、时间缩放、零 dt、缺轨迹和原有 Set/生命周期合同。静态编译不能证明 Unity Test Runner、Animancer Graph、KCC 或角色视觉结果。角色资源和现有本地攻击资源改动未修改。
