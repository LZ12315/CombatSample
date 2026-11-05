# CombatSample

Unity 战斗与移动示例工程。当前开发分支为 **`HyperGraph`**。最终游玩与打包的**唯一场景入口**为 `Assets/Scenes/CombatTrainingArena.unity`。

## 最终交付范围

- 内容开发已收敛，只交付 `CombatTrainingArena` 这一场战斗，不增加菜单、选关或其他可玩场景。
- 当前 Build Settings 仅包含并启用该场景；其他场景保留为工程资料，不属于本次游戏的构建入口。
- 玩家胜利和失败均显示结算、倒计时并自动重开当前场景。当前场景保存的时长为 **5 秒结算＋2 秒重开过渡**，过渡结束后才实际加载。
- 场景已显式绑定玩家与敌人的战斗组件。正式交付前仍需在导出的游戏中验证启动、输入、AI、镜头、音效／特效、胜负重开与关闭游戏；场景引用核对不代表 Player 验收通过。

提交所用的[游戏设计文档、运行说明与工程归档方法](Docs/Submission/README.md)见 `Docs/Submission/`。在项目根目录执行 `python Tools/Package_Submission.py` 可生成包含当前未提交源文件的完整工程包与校验清单。

## 在另一台电脑继续开发

1. 安装 Git、Unity Hub，并通过 Hub 安装 **Unity 2022.3.62f3**（revision `96770f904ca7`）。以 `ProjectSettings/ProjectVersion.txt` 为准，不要直接升级 Editor 或 Package。
2. 克隆当前开发分支；以下配置避免新电脑的 Git 全局设置自动改写文本换行：

   ```bash
   git clone -c core.autocrlf=false -c core.longpaths=true --branch HyperGraph https://github.com/LZ12315/CombatSample.git
   ```

3. 在 Unity Hub 中添加克隆得到的 `CombatSample` 目录，并用上述版本打开。首次启动需要联网解析 `Packages/manifest.json` 与 `Packages/packages-lock.json` 并重新导入全部资源。其中 `com.mackysoft.serializereference-extensions` 仍是 GitHub 依赖，因此还需要能访问 GitHub，并保证 Git 可从 Unity Editor 环境中调用。
4. 等待 Package Manager、编译和资源导入结束，再打开 `Assets/Scenes/CombatTrainingArena.unity`。目前跨机器验证针对 Windows / StandaloneWindows64，不代表最终场景的 Player Build 或其他操作系统和构建目标已经验收。
5. 切换电脑前保存 Unity 场景／资源与代码，再提交、推送；新电脑开始工作前检查 `git status` 并拉取。不要把另一台电脑尚未提交的修改直接覆盖掉。

## 同步边界

- 版本控制包含项目源文件、`.meta`／GUID、制作中的动作和动画资源、场景、嵌入式插件、Packages 清单／锁文件、ProjectSettings、测试和开发文档。
- `Library/`、`Temp/`、`Logs/`、`obj/`、自动生成的工程文件及 `UserSettings/` 不上传；Unity 会重建缓存，个人窗口布局等本机设置不属于共享项目状态。
- Unity 授权、操作系统、IDE、网络访问、Git 登录和未保存的 Editor／编辑器缓冲区不能由 Git 克隆代替。
- 动画烘焙数据与导入环境相关：换机器后若控制台出现 `Root Motion dependencies changed`，需在对应 AnimationAsset 上执行 Rebuild，方向性 Start 才会生效。
- 最终内容入口已确定，但保存／同步工程并不等于干净导入、Player 构建及所有既有问题已经完成验收。

完整同步核对、验证结果与已知限制见[跨机器交接](Docs/Current/Project_Transfer.md)。架构、制作流程及各功能合同见[当前文档索引](Docs/Current/README.md)。
