# HitStop 边界修正记录（2026-09-19）

## 范围

补齐 HitStop / SpeedEffect 复查发现的计时、组件失效、模拟停止和动画倍率边界。保留受击者四个完整战斗 Tick 延迟及双方独立 Duration，不调整生产动作、场景、Prefab、动画过渡或渲染设置。

## 本轮修改

| 文件 | 修改 |
|---|---|
| `Assets/Scripts/Impact/Effects/ActionSpeedEffect.cs` | 时长换算使用整数帧边界的 float 往返比较，替换固定误差容限；正时长至少一 Tick。年龄使用 long。检查 Actor 有效性；固定 Tick 发现组件失效后释放其 token 并丢弃引用，取消该组件尚未发生的延迟申请。销毁对象按 Unity null 语义处理。 |
| `Assets/Scripts/Actor/CombatSimulationDriver.cs` | 将步长与依赖检查纳入异常清理；前置检查失败也取消 Actor 操作并释放速度效果。故障或释放所有权时先关闭新效果准入。 |
| `Assets/Scripts/Impact/ImpactSystem.cs` | 只有存在有效、启用且未故障的战斗模拟所有者时，才接受新的持续速度效果。全局暂停不等于失去所有权，仍保持固定 Tick 暂停计时的语义。 |
| `Assets/Scripts/Actor/ActorSimulationRuntime.cs` | 用 nullable 倍率区分“没有 Motor”和“Motor 倍率为 1”；前者有动作时只采用 PlaybackSpeed。 |
| `Assets/Scripts/Actor/ActorAnimation.cs` | 删除仅供旧测试读取的姿势提交布尔属性；姿势提交与求值行为不变。 |
| `Assets/Tests/Editor/ActionSpeedEffectTests.cs` | 增加 float 边界、极小正时长、组件分别失效、Actor 禁用/销毁、无 Motor 倍率测试。原先没有 Driver 的 ImpactSystem 测试改为验证无时钟时拒绝申请，避免使用不符合生产前提的测试环境。 |
| `Assets/Tests/Editor/V1ActionPlaybackSessionTests.cs` | 冻结测试改为验证实际动画层权重、绝对采样时间及临时模型节点位置，并覆盖暂停、半速恢复和停止后回到底层姿势。 |

组件失效在固定 Tick 观察并处理；本轮未增加逐组件禁用事件订阅，不宣称能捕获两个 Tick 之间完成的禁用再启用。

## 已完成验证

- `dotnet build Assembly-CSharp-Editor.csproj --verbosity quiet`：通过，0 错误、51 警告。包含 Runtime、Editor 及上述测试源码；警告涉及旧 API、插件和未使用成员等。
- 本轮相关文件 `git diff --check`：通过。
- 未运行 Unity Test Runner。用户明确要求保留正在打开的 Unity，本轮只做编译检查。
- 未修改 TestAction、生产 Prefab、场景或模型导入配置。

## 待验证

以下全部为“未执行”，不能用编译结果代替：

1. 运行 `ActionSpeedEffectTests`、`V1ActionPlaybackSessionTests` 及相关 Runtime 回归。
2. 在临时对象/动作中，确认有效 Driver 下，空攻击 Effects 仍执行目标 Profile 的速度效果；显式 Clear 释放双方 token。
3. 有效果时停用 Driver，确认立即清理，后续命中不再申请速度 token；恢复 Driver 后仅接受新命中。
4. 临时环境中将战斗步长设为不合法值，确认 Driver 进入故障状态、取消动作、清理速度效果且不重复刷异常；测试后恢复步长。生产项目设置不作修改。
5. 单独禁用或销毁 Actor，以及在延迟期只禁用 Player 或 Motor，确认已失效部分不因重新启用而获得旧命中的冻结。
6. 实际画面核对冻结、恢复、连续命中和动作切换；旧 Sequence / Legacy Timeline 兼容性仍需运行检查。

本轮结论：代码修正及编译完成，运行验收待执行。
