# 项目结构与归位约定

状态：当前仓库目录说明。核对日期：2026-10-16。以当前文件、源码和用户最新决定为准；历史快照见 [Archive/Project_Structure.md](../Archive/Project_Structure.md)。

## 仓库根目录

- `Assets/`：Unity 导入的代码和资产。
- `Packages/`：包清单、锁文件及仓库内的包。
- `ProjectSettings/`：Unity 项目设置。
- `Docs/`：当前说明、历史记录和未实施方案。
- `Tools/`：本地检查与维护工具。
- `Bin/`：留在 Unity 导入边界之外的历史资源。
- `AGENTS.md`：项目协作约束。

`Library/`、`Temp/`、`Logs/`、`obj/` 及 Unity/IDE 生成的工程文件不是整理对象，不提交生成输出。

## Assets 目录职责

| 目录 | 内容 |
| --- | --- |
| `Art/` | 按类型组织的模型、动画、贴图、材质、Shader、特效；保留资源包内部结构。 |
| `Audio/` | 音频资产和音效资源包。 |
| `Data/` | 自制玩法数据资产，不放原始美术或运行时代码。 |
| `Prefabs/` | 项目使用的角色、特效、相机和环境等预制体。资源包内部 Prefab 可随包保留。 |
| `Resources/` | 确实需要 `Resources.Load` / `Resources.LoadAll` 按路径加载的资产。 |
| `Scenes/` | 构建入口及功能验证场景。 |
| `Scripts/` | 按功能域组织的项目代码；Editor 代码放在所属域的 `Editor/`。 |
| `Settings/` | 项目、引擎与包的配置资产和输入配置。 |
| `Tests/Editor/` | 现有 EditMode 测试。 |
| `Plugins/` | 存放在 Assets 下的第三方插件。 |

`Art/` 当前采用 `Animations/`、`Effects/`、`Material/`、`Models/`、`Shaders/`、`Textures/`，不要求改成按角色的另一套结构。资源包的空格、大小写及内部 Scripts/Scenes 不在本轮统一改名或拆分。

### Data

- `Action/Jaeger/`、`Action/Kiana/`：当前动作与角色 Action List；Kiana 普攻位于 `Kiana/Attack/`。
- `Animation/Jaeger/`、`Animation/Kiana/`：AnimationAsset、AnimationRigAsset 和烘焙运动数据。
- `Locomotion/Jaeger/`、`Locomotion/Kiana/`：Locomotion 配置。
- `Graph/`：NodeCanvas 等行为图资产；适配代码仍在 `Scripts/Graph/`，插件本体不在这里。

`Assets/Create/` 已改为 `Assets/Data/`；`Assets/Other/` 与旧的资产归档目录已删除。Unity 菜单 `Assets/Create/CombatSample/Action/Action` 是菜单入口，不是磁盘目录，不随 Data 改名。

### Resources

当前只保留：

- `Resources/TagTree_Settings.asset`：TagTree 加载的设置。
- `Resources/Tags/ActionTag.tags`：TagTree 通过 `Resources.LoadAll<TextAsset>("Tags")` 加载的标签数据。

普通美术和音频使用 `Art/`、`Audio/`，通过场景、Prefab、数据资产等直接引用；文件夹位置不决定其能否被正常使用。Resources 内的资产会纳入构建，不能把它当作一般资源仓库；这不代表所有 Resources 资产都会在启动时一次性加载进内存。

### 场景入口

- `Scenes/MiHoYo_Release.unity`：当前唯一启用的 Build Settings 场景。
- `Scenes/Test/Combat_Test.unity`：战斗验证。
- `Scenes/Test/KCC_Migration_Test.unity`：运动与 KCC 验证。
- `Scenes/Test/Camera_Test.unity`：相机验证。

测试场景的具体可用性以 Unity 中的实际状态为准；不要把目录存在当作本轮场景验收通过。

## 归位原则

- 沿用当前目录，不引入 `_Project/`、新的程序集划分或资源加载系统。
- 新建自制目录使用含义明确、无空格的名称，不再增加 `Other`、`Misc` 等杂物目录；不强制重命名已有资源包。
- 小模块不强制建立空的 Runtime/Data/Editor 模板。
- 移动 Unity 资产时保留对应 `.meta` 和 GUID；涉及按字符串路径加载的内容，先核对调用方。
- 不为了目录整齐改玩法、类型身份、序列化数据或包版本。

## 本轮代码清理与验证边界

- 删除四种无现用资产引用的旧 Impact 配置、`AttackerSpeedEffect` 和 `ImpactSystem` 中的旧兼容分支、空转更新列表。
- 保留 `SpeedEffectConfig`、`HitVfxConfig`、固定 Tick 速度效果、两个朝向解算器、震屏、`ClearAllEffects()` 和 `HasActiveEffects()`。
- 删除 ActorMotor 的三个无读取字段，并只从 `Prefabs/Actor/Actor.prefab` 删除对应三项旧序列化值。
- ActorTagGlowIndicator 的私有销毁辅助方法改名，避免与 Unity API 同名。
- 当前资产中的 8 条 SpeedEffectConfig、8 条 HitVfxConfig 保持不变。旧格式历史资产若重新导入，应先迁移，不再由本轮删除的兼容入口处理。

已完成 Unity 2022.3.62f3 编译器的 Runtime、Editor/测试源码编译，以及去掉 UNITY_EDITOR 定义的 Runtime 源编译，均为 0 error、0 warning；后者不等同于完整 Player Build。通过独立 Unity Mono 进程执行了现有测试中 20 个纯逻辑用例，覆盖速度时长换算、动画倍率与反馈启用规则；这不等同于完整 Unity Test Runner 通过。

当前项目已由 Unity 编辑器打开，未为自动验收关闭该实例或启动第二个实例抢占项目。仍需在 Unity 内运行完整相关测试，并人工确认：角色移动、命中特效、音效、震屏、顿帧及恢复；目录移动后的材质和场景引用也需确认。本记录不将未执行的场景或原生对象测试写作通过。

## 尚未收口：旧归档删除的引用

提交前将当前 GUID 定义与整理前的 HEAD 对照，发现旧归档资产虽然已从工作区删除，11 个现存文件中仍有 35 处指向这些已删除 GUID 的引用。这是旧资产归档删除的遗留问题，不是本轮 Impact 兼容类型删除造成的；目录移动保留 GUID 不能替代删除资产的引用检查。

| 分类 | 数量 | 位置及影响 |
| --- | --- | --- |
| 行为图的旧 Action 引用 | 7 | `Data/Graph/Jaeger/Jaeger_Combat.asset` 有 3 项，`Data/Graph/Enemy_Test_1.asset` 有 4 项。图中的 PlayActionTask/条件仍使用这些对象槽；不能仅当作无效文本删掉。Jaeger_Combat 仍被正式场景引用。 |
| 旧 Action List 引用 | 15 | `Prefabs/Actor/Characters/Player.prefab` 的 ActionStateManager 仍引用旧 Sword 列表；SampleScene 和多个 Test 场景也保留旧 Boxing/Enemy 列表覆盖。各测试场景覆盖是否仍有效需在 Unity 中核对。 |
| 旧 Timeline 引用 | 13 | 正式场景、SampleScene 和多个 Test 场景中的 `m_PlayableAsset` Prefab 覆盖残留，需与原组件和 Prefab 状态一起核对，不代表当前 Action 仍支持旧播放后端。 |

本轮未修改这些图、角色配置或场景，也未擅自恢复旧资产。项目负责人已决定后续自行清理这些引用；本轮将其作为已知问题保存到 Demo 起点，不把它们标记为已修复或发布验收通过。后续按实际保留范围重配当前动作与列表，或明确停用旧内容；不要为了让 GUID 扫描通过而清空引用或把旧攻击替换成不相关的新动作。

## Demo 分支约定

- `main`：CombatSample 的通用能力与基础版本。
- `Release`：Demo 开发线，承载演示内容、场景、流程、表现、调参与打包；不是已经功能冻结的发布候选分支。
- `demo-start`：本轮目录、代码及文档整理后的开发起点标签，包含上文已知资产引用问题，不代表可发布包或验收通过。
- Demo 专属内容在 `Release` 开发；通用系统修复保持独立提交，需要时从 `main` 合入。不要默认把整条 Demo 开发线反向合回 `main`。
- 可交付版本另用 `demo-v0.1.0` 等版本标签标记，不移动 `demo-start` 来指向后续成果。

本轮只准备开发分支与起点，不预先决定 Demo 的演示范围、目标平台或入口场景，也不创建占位场景、变更 Build Settings 或引入新的分支体系。
