# EnemyAI_MinimalVerify（最小验证图）

## 用途

`EnemyAI_MinimalVerify.asset` 是按方案 **§6 验证建议** 搭建的 **FSM GraphAsset**，用于在敌人 Actor 上验证：

- 远距离：`CheckTargetDistance` → `MoveTowardCombatTarget`（PushOnce）
- 近距离：`PlayActionTask`（LightAttack_1，`EnterOnce`）
- 连段：`CurrentActionIs` + 距离 → `PlayActionTask`（LightAttack_2，`UntilStarted`，timeout 0.4）

## 使用方法

1. 敌人 Prefab 上挂 **FSM Owner**（或现有 NodeCanvas 图组件），将 **Graph** 指到本资源。
2. Blackboard 的 **Actor** 变量绑定为 **Owner / Graph 所在 Actor**（与玩家图一致：`_value: 1` 表示由运行时绑定）。
3. 确保 `actor.combater.CombatTarget` 指向玩家（或战斗目标）。
4. 敌人 **ActionList** 需包含 `Sword_LightAttack_1` / `Sword_LightAttack_2`（本图 `_objectReferences` 已引用这两条 ActionAsset）。

## Play Mode 检查清单

- `PlayActionTask` 仅通过 ASM `RequestExternalAction`，不直接 `BeginAction`。
- `UntilStarted` 在取消窗口未开时保持 Running，超时再 Failure。
- 玩家侧 FSM / 输入驱动行为与改前一致。
