# Action 编辑与播放正式架构

状态：当前仓库实现说明。更新于 2026-10-01。旧 V1、ActionSequence 和 Unity Timeline Action 文档均为历史记录。

## 1. 唯一路径

Action 已不再选择播放后端。每个 ActionAsset 都保存一份 ActionTimelineData，运行时统一通过：

    ActionStateManager
        ↓ 准入与仲裁
    ActionPlayer
        ↓ 创建唯一 Session
    ActionRuntimePlaybackSession
        ↓
    ActionRuntime / ActionRuntimeScheduler
        ↓
    Animation、Motion、World、Hit、Finish 固定阶段

已删除 ActionPlaybackBackend、旧 TimelineAsset 字段、ActionSequenceData、Sequence/Timeline Session、旧编辑器及其专用测试。Action SelfTags 由 ActionRuntime 单独持有；ActionInstance 只保存本次执行的 Action、Actor、Context 与公开进度。

整招级 ActionMotionConfig 已删除。RootMotion、Velocity、Impulse、MotionPolicy 与 SelfRotation 由正式 Timeline Item 表达。

## 2. 正式入口

- 创建：Assets/Create/CombatSample/Action/Action。在当前 Project 目录直接创建 New Action.asset。
- 打开：双击任意 ActionAsset 或在 Inspector 点击 Open Action Timeline。
- 工具：Tools/CombatSample/Action Timeline。
- 属性与预览：Tools/CombatSample/Action Details，或从 Timeline 的条目打开。
- Inspector：编辑 Action 的播放设置、触发、条件、取消规则与自身标签。
- Timeline：编排动画段与 Gameplay Item，控制编辑器时间和播放。
- Details：左侧编辑 Timeline 当前主选择的内容，右侧 Preview 读取同一 Action 与连续时间，只负责视觉求值和预览资源。

编辑器只有 Timeline 和 Details 两个窗口，均可独立停靠。Details 默认按 40% / 60% 左右分栏，可拖动中间分隔条；比例按当前项目保存在本机，窗口缩放时按比例调整，并保留左右最小宽度 300px / 320px。左侧独立滚动，窄布局根据左面板宽度判断；右侧保留 Preview Settings 和相机操作，不再提供独立 Preview 窗口。

不存在 Legacy 创建菜单、旧编辑器路由、PlayableDirector 绑定或后端切换入口。

## 3. 编辑数据流

    Inspector / Timeline / Details 输入
            ↓
    具体 Commands 与时间操作规则
            ↓
    Undo + 修改 ActionAsset
            ↓
    ActionEditorContext 重建唯一 Document
            ↓
    统一选择与连续时间
            ↓
    Timeline / Details 刷新，Preview 按明确输入求值

| 部分 | 职责 |
|---|---|
| ActionAsset / ActionTimelineData | 保存动作数据 |
| ActionEditorContext | 当前 Action、唯一 Document、选择、连续时间和变更通知 |
| ActionEditorDocument | 只读条目索引、派生布局、EditorId 唯一性及异常时间的安全显示；不扫描全部 Gameplay 配置或烘焙依赖 |
| ActionEditorCommands / ActionEditorOperations | Undo、写入、重叠和时间规则 |
| ActionAssetInspector | Action 资产级属性 |
| ActionTimelineWindow | 时间编排、选择和编辑器播放控制 |
| ActionDetailsWindow | 轨道、动画段和 Item 属性，以及内嵌预览面板的通知和生命周期管理 |
| ActionPreviewPanel / ActionPreviewRenderer | 内嵌预览界面、相机操作、视觉求值和预览资源 |
| EffectListGUI / HitFeedbackProfileInspector | 共用 Effect 列表绘制；各入口自行提交修改，Details 负责 Action 变更通知 |
| ActionRuntimePlaybackSession | 固定帧播放管线与 ActionRuntime 的衔接 |

窗口刷新不修改共享状态；Preview 不控制 Action 或播放。

Details 在刷新属性页之前转发预览通知；播放头更新只重绘预览，不重建属性控件。分隔条调整不写资产、不产生 Undo，也不丢弃未提交的 Timing 草稿。预览相机和设置折叠状态由 Details 序列化保存；关闭窗口或脚本重载时释放资源，进入 Play Mode 时释放预览对象，失焦或面板卸载时结束相机拖动。旧独立 Preview 窗口的布局和相机状态不迁移。

Preview 的整数 Frame N 表示 Runtime 第 N 帧位移提交后的 Hit 阶段画面：Pose 在 N 的时间采样，角色和生效 HitBox 共享帧后世界位置。小数 PreviewPosition 仅在相邻帧后位置之间做视觉插值；Duration 终点显示最终位移，HitBox 已结束。Preview 不复现碰撞后的实际世界运动或完整 gameplay replay。

编辑写入保留同轨不重叠、快照过期、Undo、编辑锁与身份修复。旧 Issues 分类和全量配置校验已删除。运行时不预扫未来 Item 的 Context 需求，也不因烘焙轨迹 stale 而拒绝执行；ActionRuntime.Begin 仍检查动画快照并在失败时 Abort。

## 4. 目录职责

    Assets/Scripts/ActionSystem/
      Data/                 ActionAsset、ActionTimelineData、Cancel 等资产数据
      Conditions/           Action 准入与退出条件
      Runtime/              ActionRuntime、Item Runtime、采样与执行上下文
        Playback/           播放接口与 ActionRuntimePlaybackSession
      Editor/
        Core/               Context、Document、Commands、Operations、Playback、Presentation
        Windows/            Timeline、Details
        Preview/            内嵌预览面板、渲染器与空间求值
        Styles/             USS
        ActionAssetInspector.cs
        ActionAssetCreator.cs
        ActionAssetOpenHandler.cs
    Assets/Scripts/Impact/Editor/
      EffectListGUI.cs           Details 与 Profile Inspector 共用的 Effect 列表绘制
      HitFeedbackProfileInspector.cs

HitBox 的正式绑定使用 ActionHitBoxAnchor。旧 BoneReference、旧 Playable、Assets/Scripts/Legacy、AnimationConfig 与字符串动画键路径均已删除。Locomotion 动画呈现暂时留空，由独立路线重新设计；Action 动画继续直接使用 AnimationAsset。

## 5. 资产边界

`Assets/Create/Action/` 按角色归类 Action 资产，并保留原有 GUID；Kiana 的新正式动作位于 `Action/Kiana_New/Combat/`。目录移动没有转换旧资产内容：旧资产仍可能序列化 `_timelineAsset`、`_playbackBackend`、`_sequenceData` 和已删除的 managed-reference 类型；旧 Sequence 内容不能继续播放。项目负责人按需从当前格式重新配置动作，不把目录归位当作迁移完成。

AnimationAsset 和 AnimationRigAsset 位于 `Assets/Create/Animation/`；Locomotion 配置位于 `Assets/Create/Locomotion/`，`Assets/Create/Test/` 保留测试资产。当前 Action List 随角色放在 `Assets/Create/Action/`，旧列表位于 `Assets/Create/Archive/ActionList/`；行为图位于 `Assets/Create/Graph/`。旧资源不作为兼容输入。

## 6. 验证

2026-10-01 Effect 列表绘制复用：

- Action Details 与 HitFeedbackProfile Inspector 使用同一个列表绘制类；Details 仍负责通知 Action 编辑上下文。
- Runtime、Editor 和现有测试源码通过 Unity 2022.3 自带 Roslyn 编译；未运行 Unity 界面测试，需在 Inspector 人工检查增删、排序、多态类型切换及 Undo。

2026-09-28 Details / Preview 窗口合并：

- 使用 Unity 2022.3 自带 Roslyn 编译当前 Runtime 120 个源码及 Editor / 测试 38 个源码，输出仅写入临时目录：0 error，Runtime 有 30 个既有警告，Editor / 测试无警告。
- 预览输入、渲染器、相机工具及空间求值逻辑保持原实现；已有预览失效策略测试只迁移面板类型引用。正式源码和当前文档中独立 Preview 窗口引用已清除。
- 本轮未执行 EditMode 测试，项目负责人选择自行测试。待 Unity 内确认：停靠与分隔条拖动、缩放时限宽及比例恢复、左侧窄布局和草稿 / 焦点保持、右侧动画与 HitBox 和相机操作，以及脚本重载和 Play Mode 切换时的资源释放。

2026-09-24 静态复核与编译：

- 正式源码中旧后端、旧 Session 和旧编辑器类型引用扫描为零。
- Runtime 编译通过，0 error；警告来自项目既有废弃 API。
- Editor 与保留 EditMode 测试源码编译通过，0 error；本次 48 个警告来自既有项目和包源码。
- Unity 生成的 csproj 尚未由编辑器刷新；编译时通过临时 MSBuild target 忽略其中已删除的旧文件项，没有修改生成文件。
- 项目负责人已完成本阶段人工检查并接受结果；Unity Test Runner 不再作为当前阶段验收方式。检查范围与编译记录见[执行链收尾检查](CombatSample_Animation_Action_Actor_Closure_Audit_2026-09-23_zh-CN.md)。

Unity 重新生成工程文件后会清除旧 csproj 列表项。项目负责人按当前格式从零配置所需 Action；该资产工作独立于已收口的编辑与执行链。
