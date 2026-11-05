# CombatSample｜运行与工程打开说明

## 一、运行 Windows 游戏

1. 解压完整的游戏输出目录，保留 exe、同名 Data 目录、UnityPlayer.dll 及其他随包文件之间的相对位置；不要只复制 exe。
2. 在 Windows x64 电脑运行 CombatSample.exe（如构建时另取名称，以实际 exe 为准）。游玩不需要安装 Unity Editor。
3. 游戏直接进入 CombatTrainingArena，控制玩家与 AI 敌人交战，无须进入其他场景。
4. 首次运行先点击游戏窗口取得焦点。WASD／方向键移动，鼠标控制镜头，左键轻击，右键重击／分支输入，中键切换软锁。
5. Esc 仅释放鼠标，不暂停战斗；左键可重新捕获鼠标，并可能同时出招。Alt + F4 或关闭窗口退出。
6. 胜负后自动结算。当前场景配置为 5 秒倒计时，再显示“正在重新开始…”等待 2 秒，之后才实际重新加载。不要把这两秒当作已在后台加载；实际加载耗时另计。

推荐先体验地面左键连段，再在衔接窗口尝试右键分支，观察挑空、空中轻击衔接、敌人霸体金光及胜负重开。动作只在符合状态和窗口时响应，不是所有输入都能随时打断当前动作。

## 二、打开 Unity 工程

- 工程压缩包为当前磁盘上已保存文件的完整快照，包含未提交源码与资源，不是仅导出旧 Git 提交。
- 解压后，CombatSample/ 这一层同时包含 Assets、Packages、ProjectSettings 与 README.md；在 Unity Hub 添加这一层目录。
- 使用 Unity 2022.3.62f3（revision 96770f904ca7），不要直接升级 Editor 或 Package。推荐解压到较短目录，例如 D:/CombatSample，避免资源路径过长。
- 需要可用的 Unity Editor 授权和 Windows Build Support。首次打开需要联网恢复 UPM 依赖；GitHub 来源的 SerializeReference Extensions 还要求系统安装 Git，并可从 Unity 环境访问 GitHub。Packages/packages-lock.json 随包保留。
- Animancer 是 Packages/com.kybernetik.animancer 下的嵌入式包；其他 Assets 插件与源文件一并保留，无须从本机 Library 复制缓存。
- 等待 Package、编译与资源导入完成后，打开 Assets/Scenes/CombatTrainingArena.unity。它是本次唯一可玩／构建入口，其他场景仅作为开发资料保留。
- 若换机器后出现 Root Motion dependencies changed，请在对应 AnimationAsset 的 Inspector 执行 Rebuild，等待完成后重新进入 Play。不要通过删除依赖校验来规避。

## 三、工程内容索引

| 目录 | 内容 |
| --- | --- |
| Assets/Scripts/Action | 动作仲裁、运行时、条件、取消窗口及时间轴编辑器 |
| Assets/Scripts/Actor | 角色运动、动画／Locomotion 与固定步仿真 |
| Assets/Scripts/Combat | 伤害、HitBox、状态与胜负结算／重开 |
| Assets/Scripts/Graph | AI 行为图的项目任务与条件适配 |
| Assets/Scripts/Camera、UI、Impact | 镜头协调、血条／结算界面与命中反馈 |
| Assets/Data | Action、Animation、Locomotion 与 AI 图配置 |
| Assets/Scenes/CombatTrainingArena.unity | 最终训练场，已绑定双方及胜负流程 |
| Assets/Art、Audio、Prefabs | 模型、动画、材质、特效、音频和预制体 |
| Assets/Tests/Editor | 玩法／数据合同测试 |
| Docs/Submission | 本次提交文档的可编辑源文件与 PDF |
| Docs/Current | 详细实现说明、工作流和既有验证边界 |

## 四、重新构建

1. 停止 Play Mode，保存全部代码、场景与数据。
2. File > Build Settings 中选择 Windows / x86_64，并确认唯一启用场景为 CombatTrainingArena。
3. 保留 Graphics Settings 的 Always Included Shaders 中的 CombatSample/Actor Rim Glow；该 Shader 由运行时按名称查找，移除后可能出现 Editor 有霸体金光、Player 无金光。
4. Build 到独立输出目录，不要覆盖 Assets 或将输出混入源文件。
5. 从新输出的 exe 验证移动／镜头、地面和空中动作、AI、音效／特效、霸体金光、胜利重开、失败重开及关闭游戏。

源码中保留了更多输入和实验资源。当前生成的 PlayerInputControl.cs 另外绑定 J 为轻击，其余键鼠主绑定已按生成代码核对；生成代码与 .inputactions 当前不是逐项完全一致，重新生成前应先确认希望保留的绑定。Shift／Space 等存在输入绑定，并不因此保证最终动作表提供对应的独立动作。本次操作说明不将这些预留内容作为必玩功能。

## 五、快照与验收边界

工程包不包含 Library、Temp、Logs、obj、UserSettings、IDE 生成工程文件、游戏构建输出或 .git 历史。这些排除项不属于 Unity 源工程资产缺失。

压缩包内容以生成时的已保存文件为准，不包含 Editor 内尚未保存的修改，也不包含后续录制的视频和后续重新构建的游戏。若之后修改代码、资源或设置，请重新打工程包，使工程、游戏和视频对应同一版。

交付目录附有文件清单及 SHA-256 校验信息，用于核对归档完整性。文件完整性、源码编译、纯逻辑测试和真实 Player 验收是不同检查；本次工程打包不宣称已经完成干净机器导入或完整 Unity Test Runner。游戏实际表现由最终导出包试玩与流程视频共同展示。
