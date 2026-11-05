# 战斗训练大厅交付

场景：**`Assets/Scenes/CombatTrainingArena.unity`**。直接在 Project 中打开即可；不需要制作工具或中转场景。

## 当前内容

- 最新 `HyperGraph` 的 `Manager`、`CameraSet`、`Player`、`Enemy` 已整体迁入，保留对象间引用和 Prefab 连接；旧停用 `Characters` 组不再保留。
- 出生点沿用本次迁入配置：玩家 `(0,0,-10)`、敌人 `(0,0,0)`；死亡重开延迟为源场景配置的 5 秒。
- 保留完成版大厅：28 × 28 m、12 m 净高、完整树脂地坪、四墙 72 块模块、天花灯具、七个环境碰撞体和离线反射。
- 保留训练场相机背景与后处理开关；其余镜头、角色和玩法参数以迁入的 HyperGraph 配置为准。
- 这是一次迁移快照，不会自动同步 HyperGraph 之后的新修改。

## 资源与清理

- 环境：`Assets/Prefabs/Environment/CombatTrainingArena/CombatTrainingArena.prefab`。
- 后处理：同目录 `FinishedArenaVolume.asset`。
- 材质、模型、纹理：`Assets/Art/` 各自的 `CombatTrainingArena` 目录；共享表面贴图已移出 Lookdev 目录，GUID 保留。
- 已移除制作/迁移脚本、Lookdev 样板场景及其独立资源、中转场景，以及本训练场范围内不再被场景或 Prefab 引用的资产。
- 历史对照图片和重复样板文档已清理，只保留完成版环境参考图。参考图摄于玩法迁移前，不代表最新出生点或运行镜头。

## 检查与已知边界

- 迁入玩法在保存并重开后，对象和组件序列化比对一致；场景不保留重复玩法根对象。
- 未覆盖 HyperGraph 或修改共享玩法/相机资源。没有为迁移更改 Build Settings、Packages 或全局渲染配置。
- **死亡重开需要将训练场加入启用的 Build Settings 场景列表**；仅迁入组件不会解除此要求。
- 没有新增截图、实战/性能验收或 Test Runner 测试。既有动作资产缺失类型、动画清理等告警未在此任务中修改或宣称修复。

环境参考：[大厅整体](../Media/CombatTrainingArena/Finished/after-room-overview.png)、[中央构图](../Media/CombatTrainingArena/Finished/after-combat-view.png)。
