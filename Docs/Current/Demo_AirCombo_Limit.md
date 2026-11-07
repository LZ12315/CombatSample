# Demo：每次滞空只能起手一套空中连段

## 规则

- 限制 `Action_Katana_AirCombo_1` 的成功起手次数，不限制第 2～4 段接招。
- 第一段成功启动就消耗一次；挥空、提前停止、切换动作、受击打断都不返还。
- 输入未通过、候选未选中、播放初始化失败不消耗。
- 同次滞空中的二段跳、空中受击不重置；`ActorMotor.OnLanded` 清零。
- 状态归属每个 Actor 的组件实例，不写入共享 Action 或 Condition。

## Unity 接入（需要手动配置）

1. 在玩家 **Actor 所在的 GameObject** 上添加 `DemoAirComboUsage`（菜单 `CombatSample / Demo / Air Combo Usage`）。
2. 将 `Air Combo Start` 指定为 `Action_Katana_AirCombo_1`。
3. 在该 Action 的 **Requirements** 中新增 `Demo / Air Combo Available`，保持 `Invert Result` 关闭。
4. 保留已有的输入和空中状态条件。不要将新条件加到 `AirCombo_2 / 3 / 4`。

组件从 Actor 引用获取 Motor 和 ActionPlayer；引用尚未在 Awake 填充时，分别查找同物体 Motor 和子级 ActionPlayer。
缺少组件、未配置起手资产或组件被禁用时，Condition 拒绝进入，不默认为无限次数。
`StartsThisAirtime` 是运行时只读次数。此实现固定限制一次，不是通用次数／Action 历史系统。

禁用组件不会返还空中已消耗的机会。组件应在整段游戏过程中保持启用；不支持通过中途添加／换配置／替换 Actor 依赖来追溯此前的起手历史。若禁用期间落地再离地，由于没有接收落地事件，重新启用时保守保留已消耗状态，直到下一次落地。

## 成功起手通知

`ActionPlayer.OnActionStarted` 在播放会话启动、公开播放状态同步成功后发布。

- 初始化失败不发布；Loop 内部重播不再次发布。
- 观察者异常被逐个捕获并记录，不会将已经成功的起手改判为失败，也不阻止其他观察者记账。
- 仅用于观察，不要在回调中停止／切换 Action。
- `TryBeginAction` 仍是播放入口，不代替 ASM 的 Requirements 仲裁；直接调用该入口可以绕过准入，但成功起手仍会记录。

## 验证边界

`DemoAirComboUsageTests` 使用临时 Action 和纯计数规则，覆盖可用性检查不消耗、起手即消耗、接招不重复消耗、落地恢复、角色间隔离，以及启动失败／Loop 不重复发送成功通知。另覆盖缺少或未配置组件时的条件拒绝。

本轮使用 Unity 2022.3.62f3 的编译器和项目依赖离线编译了运行时及 Editor 程序集，均无错误；未执行 Unity Test Runner，也没有自动修改场景、Prefab 或 Action 资产。

手工验收：

1. 起跳，第一段成功播放，正常接完整套；同次滞空不能重新起手。
2. 只打一刀不接、挥空或被打断，同次滞空仍不可重新起手。
3. 二段跳不恢复机会；真正落地后再次起跳可重新起手。
4. 只按下但没有成功启动的攻击不扣机会。
5. 两个挂有该组件的 Actor 不互相影响。
