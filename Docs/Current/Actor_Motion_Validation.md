# Actor Motion v3 验证清单

> Status: Verified v3 validation contract; E3-H acceptance passed
> Static structure verified: 2026-08-29
> Authority baseline: `CombatSample_ActionSequence_Final_Architecture_v3_zh-CN.md`

本文是当前 Actor Motion 验证合同。`Docs/Archive/Actor_Motion_Validation.md` 描述的是已经退出 authority 的 `ActorMotionRuntime / MotionChannels / RootMotionBuffer / Animator Root Motion`，不得继续作为当前实现依据。

## 1. 当前权威链路

```text
LocomotionIntent
→ ActorLocomotion selects profile in Control
→ Action arbitration and ActionSequence contributions
→ ActorMotor prepares Translation / Rotation
→ KCC simulates requested motion
→ ActorCollisionResolver applies deterministic actor separation
→ ActorMotor publishes requested/actual world result
```

- `ActorMotor` 是 Gameplay Actor movement authority，KCC 是实际 world solve。
- `TranslationDomain` 持有 HorizontalVelocity owner、Trajectory Root Motion、HorizontalImpulse、VerticalVelocity owner 与 Ballistic state。
- `RotationDomain` 持有 Scripted / Root owner；`LocomotionRunner` 只提供最低优先级 locomotion yaw。
- `MotionPolicyState` 独立组合 LocomotionScale、AirLocomotionScale 与 GravityScale。
- Gameplay Root Motion 只来自 `RootMotionTrajectory` 当前查询区间，不读取 Animator delta。

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
- Pause/HitStop 不重复执行 Sequence gameplay frame side effects。
- Cancel、Stop、Restart、Disable、Dispose 与 Driver abort 必须幂等释放各自 owner。
- Actor disable/re-enable 后 owner、policy、Ballistic、requested/actual readout 与 KCC pose 不得残留旧帧状态。

## 3. 已继承的 E3-G 人工结果

用户已在 E3-G cutover 后确认：

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

1. 让有位移的 Sequence 动作贴墙播放，再离开墙体。
2. 被墙阻挡的 displacement 不得在后续帧补偿或产生瞬移。
3. 在 Velocity owner 或更高 rotation owner 覆盖结束后，应直接使用当前帧 Root/Rotation contribution，无 catch-up。

### C. HitStop time domain

1. 在 Root Motion、空中 Ballistic 或 HorizontalImpulse 存在时触发 HitStop。
2. Action Pose、Locomotion Base、crossfade、Gravity、Impulse decay 和 trajectory interval 应同时冻结。
3. 恢复后不得补帧；受击者现有 4/60 秒延迟冻结语义保持不变。

### D. Cleanup / re-enable

1. 连续切换或取消 Sequence，并在动作中 disable/re-enable Actor。
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
