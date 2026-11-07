# CombatSample

Unity 战斗与移动示例工程，包含数据驱动动作、角色运动、命中／弹反、AI 行为图、相机与配套编辑工具。

## 当前开发状态

- **目标主线：`main`**。HyperGraph 的系统与样例正在 **`main-migration`** 整合、清理和验证，完成后再合回主线。
- 当前迁移分支尚未合回 `main`，也未推送远端；从远端克隆不会自动得到本机的整理结果。
- **主要试玩与回归入口：`Assets/Scenes/CombatTrainingArena.unity`**。保留现有玩家、敌人及胜负重开，作为系统验证样例，而不是限定项目未来只能有这一场战斗。
- 本阶段已移除 Demo 专用的“每次滞空只能起手一次空中连段”限制；动作自身的输入、空中状态和取消窗口条件仍然有效。
- 整合进度、验证范围与待办见[主线迁移记录](Docs/Current/Main_Migration.md)。

## 打开与运行

1. 通过 Unity Hub 安装 **Unity 2022.3.62f3**（revision `96770f904ca7`），以 `ProjectSettings/ProjectVersion.txt` 为准，不直接升级 Editor 或 Package。
2. 克隆工程并检出要开发的分支。下面的命令只克隆远端已有内容；继续迁移工作前须确认对应提交已经同步。

   ```bash
   git clone -c core.autocrlf=false -c core.longpaths=true https://github.com/LZ12315/CombatSample.git
   ```

3. 在 Unity Hub 中添加包含 `Assets`、`Packages`、`ProjectSettings` 的项目根目录。首次打开需要联网恢复 Package；GitHub 来源的 SerializeReference Extensions 还要求 Unity 能调用 Git 并访问 GitHub。
4. 等待依赖解析、编译与导入完成，打开 `Assets/Scenes/CombatTrainingArena.unity`，进入 Play Mode。
5. WASD／方向键移动，鼠标控制镜头，左键轻击，右键重击／分支输入，中键切换软锁。Esc 释放鼠标但不暂停；重新点击可能同时出招。动作是否响应由当前动作、状态与窗口决定。
6. 胜负后结算并自动重开；当前训练场为 **5 秒结算＋2 秒过渡**，过渡结束后才实际加载。重开依赖训练场在 Build Settings 中启用。

动画烘焙数据与导入环境有关：若出现 `Root Motion dependencies changed`，在对应 AnimationAsset 上执行 Rebuild 后重新试玩，不关闭 freshness 校验来掩盖问题。

## 构建与验证

- 当前 Build Settings 保留训练场作为唯一启用场景，本阶段不改构建、渲染、输入或战斗参数。
- Windows Player 构建需要对应的 Windows Build Support，输出到源目录以外的独立目录。
- 保留 Graphics Settings 的 `CombatSample/Actor Rim Glow` Shader：它由运行时按名称查找，不能仅凭静态资源引用判断未使用。
- 编译、合同测试、实际试玩和 Player 构建是不同层次的检查；已通过与未完成项目以[迁移记录](Docs/Current/Main_Migration.md)为准。

## 项目入口

| 路径 | 职责 |
| --- | --- |
| `Assets/Scripts/Action` | 动作数据、准入／取消、运行时与编辑器 |
| `Assets/Scripts/Actor` | 角色、运动、Locomotion、动画与固定步调度 |
| `Assets/Scripts/Combat`、`Impact` | 命中／状态、反馈与胜负流程 |
| `Assets/Scripts/Graph`、`Camera`、`UI` | AI 适配、镜头协调与界面 |
| `Assets/Data` | 动作、动画、Locomotion 与行为图配置 |
| `Assets/Scenes/CombatTrainingArena.unity` | 主要战斗回归样例 |
| `Assets/Tests/Editor` | 小型玩法／数据合同测试 |
| `Docs/Current` | 当前系统合同、工作流与验证记录 |

完整资料见[当前文档索引](Docs/Current/README.md)；第三方资源与许可边界见[素材与第三方说明](Docs/Third_Party_Notice_zh-CN.md)。

## 同步边界

版本控制保留源码、资产与 `.meta`、嵌入式插件、Packages 清单／锁文件、ProjectSettings 和开发文档。`Library/`、`Temp/`、`Logs/`、`obj/`、IDE 生成文件及个人 `UserSettings/` 不上传。

切换电脑前保存 Unity 场景、资源及代码，检查 `git status` 后提交并推送；另一台电脑先保存自己的改动，再同步同一分支。Unity 授权、IDE、Git 登录、编辑器缓冲区和窗口布局不由 Git 迁移。历史跨机器验证与已知限制见[跨机器交接](Docs/Current/Project_Transfer.md)。
