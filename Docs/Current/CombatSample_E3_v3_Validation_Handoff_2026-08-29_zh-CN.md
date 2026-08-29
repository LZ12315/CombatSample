# CombatSample E3 / Final Architecture v3 Validation Handoff

> Status: **Completed** — E3-H static, asset and differential acceptance gates passed
> Baseline commit: `0872ac03 feat(combat): complete E3-G gameplay authority cutover`
> Active scope: enabled Build Settings dependency closure rooted at `Assets/Scenes/MiHoYo_Release.unity`

## 1. 当前结论

E3-A 至 E3-H 已完成。代码静态结构符合 v3 的 Domain、Authority、Arbitration 与 7-Phase 边界；E3-H 修复了 `ActorCollisionResolver` 依赖注册顺序的确定性缺口，并完成 active content 与差量人工验收。

本轮静态资产闭包发现：39 个 reachable ActionAsset 全部使用 Sequence backend，Legacy count 为 0，Gameplay frame rate 与 managed-reference type 完整。另有 38 个 E3-G 自动生成资产共 302 个空 Track/Clip editor ID；migrator 已改为生成基于 Action GUID 和序列化索引的确定性 ID。2026-08-29 Unity Apply 已重建 38 个资产，Preview 确认 active=39、Legacy=0、AnimationConfig issues=0、Sequence validation issues=0。

2026-08-29 用户完成多 Actor、Root Motion cover/resume、HitStop 与生命周期差量跑测，当前未发现问题。E3 Completion Gate 已满足。

## 2. v3 第 18 章证据映射

| Invariant | 当前静态证据 | 状态 |
| --- | --- | --- |
| Authority | `DecideAction` 只由 Runtime 路由；`PlayActionFrame` 只由 Action Phase 调用；runtime Animancer Evaluate 只在 ActorAnimation；Animator delta 仅 Editor oracle | Static pass |
| Translation | ActorMotor 固定执行 Horizontal owner → trajectory root → locomotion + impulse；covered root 每 Motion tick snapshot 后清空 pending | Static + manual pass |
| Rotation | RotationDomain 固定 Scripted → Root → Locomotion；Root/Scripted owner stack 独立 | Static + manual pass |
| Grounding | Grounded 归零 final vertical/Ballistic；Vertical owner 不由 grounding 结束；ForceUnground 保留 | Static + manual pass |
| Animation | ActorAnimation 独占 Manual graph、Base/Override、stale owner 与每 Tick Evaluate | Static + manual pass |
| Root data | Runtime 只读取 baked trajectory interval，XZ/Yaw 分通道；Animator delta 无 gameplay path | Static + manual pass |
| Hit | Driver 在 KCC + ActorCollisionResolver + SyncTransforms 后统一 Query，再由 CombatHitBuffer stable sort Resolve | Static + manual pass |
| Time Domain | Sequence 使用 fixed delta/accumulator；MovementTimeScale 同步冻结 Locomotion、Ballistic、Impulse 与 requested motion；Animation dt 归零 | Static + manual pass |
| Action/Locomotion | Control selection 位于 Action arbitration 前；Action contribution 不反向重跑 Mode selection | Static pass |
| Driver Boundary | 显式 Control → Action → Animation → Motion → World → Hit → Finish；Actor 子系统只经 ActorSimulationRuntime | Static pass |

## 3. Active content / compatibility 边界

- Enabled build active content：Jaeger/Kiana、39 个 reachable Sequence ActionAsset、对应 AnimationConfig/trajectory 与 Locomotion profile。
- Inactive Boxing/Sword Timeline assets不属于本轮迁移范围，不得重新接入 enabled-build authority。
- `ActionMotionConfig` 只由数据模型和 editor migrator读取；Sequence runtime 不消费它。
- `ActorLogicInput` 可继续作为 prefab 上的只读 compatibility shell，不生产 locomotion authority。
- Timeline Animancer 的直接 Evaluate 只允许 `!Application.isPlaying` editor preview；Timeline runtime 不属于 active backend。
- `RootMotionOracleRecorder.OnAnimatorMove` 被 `UNITY_EDITOR` 包围，只服务 bake/validation。
- 当前 HitBox 使用 post-World final-pose overlap。只有未来出现明确高速连续判定需求时才增加 previous→current sweep；这不是当前 E3 缺口。

## 4. E3-H 已确认并修复的缺口

### Actor collision order

旧实现按 `ActorMotor.OnEnable` 注册顺序遍历 resolver pair，多 Actor 重叠时可能产生不同迭代顺序。E3-H 在移除失效引用后按 `GetInstanceID()` 固定排序，再执行原有 pair solve；没有改变 push、mass、top-slide 或 KCC sweep 数学。

### Generated Sequence identity

旧 E3-G conversion 创建 Track/Clip 时没有 editor ID。E3-H 让 migrator：

- 以 Action asset GUID + Track/Clip serialized index 计算 32 位确定性 ID；
- 在 Preview 中报告 active Sequence validator errors；
- 在 Apply 完成后以 validator errors 作为 hard failure。

Unity 已完成资产重写，并由 active-build Preview/Validator 确认通过。

## 5. Completion Record

- Unity Apply：`generatedOrRebuilt=38`、`directDependencies=19`、`duplicatedSharedLegacy=0`。
- Active Preview：39 actions、Legacy=0、AnimationConfig issues=0、Sequence validation issues=0。
- 302 个生成 Track/Clip editor ID 全部非空且无重复。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`：0 warning、0 error；未运行 TestRunner。
- `Actor_Motion_Validation.md` 的差量人工项目于 2026-08-29 通过。
- Scene Ownership 角色与 build ownership 未变化，已复核 `Scene_Ownership_Baseline_2026-08-02.md`，无需改写。
- ActionSequence Editor 编辑模型未变化；新增 Clip 继续使用既有 Track、validator 与 managed-reference 扩展机制，无需改写 Editor Architecture 文档。

提交链：

- E3-A～C：`bfb241d6`
- E3-D：`6b3140c2`
- E3-E：`94cf8051`
- E3-F：`e2c9f7d1`
- E3-G：`0872ac03`
- E3-H：本次 final validation / documentation handoff 提交。
