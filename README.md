# CombatSample

Unity 战斗与移动示例工程。当前开发分支为 **`HyperGraph`**，主工作场景为 `Assets/Scenes/HyperGraph.unity`。

## 在另一台电脑继续开发

1. 安装 Git、Unity Hub，并通过 Hub 安装 **Unity 2022.3.62f3**（revision `96770f904ca7`）。以 `ProjectSettings/ProjectVersion.txt` 为准，不要直接升级 Editor 或 Package。
2. 克隆当前开发分支；以下配置避免新电脑的 Git 全局设置自动改写文本换行：

   ```bash
   git clone -c core.autocrlf=false -c core.longpaths=true --branch HyperGraph https://github.com/LZ12315/CombatSample.git
   ```

3. 在 Unity Hub 中添加克隆得到的 `CombatSample` 目录，并用上述版本打开。首次启动需要联网解析 `Packages/manifest.json` 与 `Packages/packages-lock.json` 并重新导入全部资源。其中 `com.mackysoft.serializereference-extensions` 仍是 GitHub 依赖，因此还需要能访问 GitHub，并保证 Git 可从 Unity Editor 环境中调用。
4. 等待 Package Manager、编译和资源导入结束，再打开 `Assets/Scenes/HyperGraph.unity`。目前跨机器验证针对 Windows / StandaloneWindows64，不代表其他操作系统和构建目标已经验收。
5. 切换电脑前保存 Unity 场景／资源与代码，再提交、推送；新电脑开始工作前检查 `git status` 并拉取。不要把另一台电脑尚未提交的修改直接覆盖掉。

## 同步边界

- 版本控制包含项目源文件、`.meta`／GUID、制作中的动作和动画资源、场景、嵌入式插件、Packages 清单／锁文件、ProjectSettings、测试和开发文档。
- `Library/`、`Temp/`、`Logs/`、`obj/`、自动生成的工程文件及 `UserSettings/` 不上传；Unity 会重建缓存，个人窗口布局等本机设置不属于共享项目状态。
- Unity 授权、操作系统、IDE、网络访问、Git 登录和未保存的 Editor／编辑器缓冲区不能由 Git 克隆代替。
- 动画烘焙数据与导入环境相关：换机器后若控制台出现 `Root Motion dependencies changed`，需在对应 AnimationAsset 上执行 Rebuild，方向性 Start 才会生效。
- 这是**当前已保存开发状态的同步**，并不表示所有制作中功能和既有问题已经完成验收。

完整同步核对、验证结果与已知限制见[跨机器交接](Docs/Current/Project_Transfer.md)。架构、制作流程及各功能合同见[当前文档索引](Docs/Current/README.md)。
