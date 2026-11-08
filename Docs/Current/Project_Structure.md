# 项目结构与归位约定

状态：`main-migration` 的当前开发目录。迁移进度与验证结果见 [主线迁移记录](Main_Migration.md)；旧目录快照见 [Archive/Project_Structure.md](../Archive/Project_Structure.md)。

## 仓库根目录

- `Assets/`：Unity 导入的代码和资产。
- `Packages/`：包清单、锁文件及仓库内的包。
- `ProjectSettings/`：Unity 项目设置。
- `Docs/`：当前合同、历史记录和未实施方案。
- `Tools/`：本地检查与维护工具。
- `Bin/`：Unity 导入边界之外的历史资源；本轮 Assets 收敛不清理这里。
- `AGENTS.md`：项目协作约束。

`Library/`、`Temp/`、`Logs/`、`obj/` 及 Unity/IDE 生成的工程文件不是源码或资产整理对象，不提交生成输出。

## Assets 目录职责

| 目录 | 内容 |
| --- | --- |
| `Art/` | 样例需要的模型、原始动画、材质、Shader、特效与贴图，保留现用资源的原路径和包内说明。 |
| `Audio/` | 样例直接引用的音效。 |
| `Data/` | Action、Animation、Locomotion 和行为图配置；不放原始美术或代码。 |
| `Prefabs/` | 训练场环境、反馈、相机和管理对象。角色的模型及烘焙参考也可能来自 Art 下的模型资源。 |
| `Resources/` | 必须通过 Resources 加载的标签、字体和插件资源。 |
| `Scenes/` | 主要试玩场景；旧测试场景不再作为当前系统的可运行入口。 |
| `Scripts/` | 按功能域组织的项目代码；Editor 代码放在所属域的 `Editor/`。 |
| `Settings/` | 引擎、包及输入等配置资产。 |
| `Tests/Editor/` | EditMode 合同测试。 |
| `Plugins/` | Assets 下的第三方插件；本轮未拆改插件及其自带示例。 |

### 当前保留的样例数据

- `Data/Action/Katana/`：玩家连段、空中连段、挑空／下落攻击与受击。
- `Data/Action/Giant/`：敌人攻击、地面／空中受击与起身。
- `Data/Animation/Katana/`、`Giant/`：56 份 AnimationAsset，以及两份 AnimationRigAsset。
- `Data/Locomotion/Katana/`、`Giant/`：共五份地面、空中及绕行配置。
- `Data/Graph/Giant/`：`FSM_Giant` 与 `BT_Giant_Combat`，保留显式 Actor 目标获取和黑板传递的例子。

Jaeger、Kiana、GreatSword 等旧数据及其入口已按依赖收敛，不再是必须补齐的当前玩法。需要恢复旧样例时从 `HyperGraph` 检查点按功能迁移，不能只拷回旧图或旧 Action List。

Animancer 的默认 Transition Preview 模型改为当前 Katana AnimationRig 使用的模型；不再通过这个编辑器设置保留带失效 Timeline／嵌套 FSM 的 David 角色样例。Action 编辑器与烘焙仍使用各自 AnimationRig 的模型设置。

### Resources 与按名称加载

- `Resources/TagTree_Settings.asset`、`Resources/Tags/ActionTag.tags`：标签设置及 `Resources.LoadAll<TextAsset>("Tags")` 的输入。
- `Resources/UI/BattleDeathFont.otf` 与同目录许可证：结算界面字体。
- 保留其他既有 Resources 与插件资源，不以“没有静态入边”作为删除理由。
- `CombatSample/Actor Rim Glow` 由 `Shader.Find` 按名称查找，仍保留 Shader 及 Graphics Settings 引用。
- 编辑器样式存在按路径加载，随 `Scripts/` 一并保留。

普通美术通过场景、Prefab 和数据资产直接引用。Resources 不是一般资源仓库；纳入构建不等于启动时一次性加载全部资源。

### 场景入口

**`Scenes/CombatTrainingArena.unity` 是项目当前唯一保留的第一方场景，也是唯一启用的 Build Settings 场景。**

保留玩家、Giant 敌人、训练场环境及胜负重开。原 `HyperGraph`、`MiHoYo_Release`、`SampleScene` 和八份旧测试场景已与无现用依赖的草稿数据共同裁剪；不是把旧场景改名成验收场景。插件自带场景及特效来源 Readme 不属于第一方入口。

实际试玩方法见 [训练场说明](Combat_Training_Arena.md)。代码、合同测试、场景运行和 Player 构建各有不同的验证边界，不能用目录或资产存在代替验收。

## 归位与后续开发原则

- 沿用当前目录，不引入 `_Project/`、新的程序集划分或资源加载框架。
- 新增系统能力不受当前两个角色的素材范围限制；使用稳定合同测试和最小代表性配置，不重新引入整套无用动画包。
- 不因文件夹名称、单次 GUID 搜索或没有场景引用就删除资源；先核对 Resources、按名加载、导入器、Prefab、图和设置。
- 移动 Unity 资产时保留 `.meta` 和 GUID；保留第三方许可证、版权、来源与使用说明，说明文件可能位于不再包含实际模型／动画的原资源包目录。
- 不为目录整齐修改玩法、类型身份、序列化接口或包版本。
- 删除只缩小当前工作树，不消除 Git 历史中的大型资源；本轮不重写历史，不推送远端。
