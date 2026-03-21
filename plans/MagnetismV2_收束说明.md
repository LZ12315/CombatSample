# Magnetism（正式版）说明

## 职责划分

| 内容 | 由谁负责 |
|------|-----------|
| **攻击片段内朝向敌人**（+ 水平距离门控 `maxDistance`） | Timeline **Action Magnetism** → `MagnetismConfig` + `ActionMagnetismSession`（仅旋转） |
| **常态站位、顶开可推动体** | 根运动 + **`CombatSample.PhysicsInteraction`** |

## 脚本（全局类型，无嵌套命名空间，便于 Timeline 添加轨道）

- `ActionMagnetismTrack` / `ActionMagnetismClip` / `ActionMagnetismBehavior` — `Assets/Scripts/TimelinePlayable/Magnetism/`
- `MagnetismConfig` / `ActionMagnetismSession` — `Assets/Scripts/Magnetism/`

## 已移除的历史内容

- 旧版 `ActionMagnetismClip`（Instant/Continuous 吸附位移）、`CombatAnchors`、根表面间隙几何、`ActionMagnetismV2*` 命名等。

## 旧文档

- `攻击吸附_根节点到表面_方案与迁移.md`、`攻击吸附与战斗锚点_最终实现方案.md` 等可能与当前代码不一致，仅作备忘。
