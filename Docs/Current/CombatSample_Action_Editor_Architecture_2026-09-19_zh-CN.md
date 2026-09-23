# Action 编辑与播放正式架构

状态：当前仓库实现说明。更新于 2026-09-24。旧 V1、ActionSequence 和 Unity Timeline Action 文档均为历史记录。

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
- Inspector：编辑 Action 的播放设置、触发、条件、取消规则与自身标签。
- Timeline：编排动画段与 Gameplay Item，控制编辑器时间和播放。
- Details：编辑 Timeline 当前主选择的内容。
- Preview：读取 Timeline 的 Action 与连续时间，只负责视觉求值和预览资源。

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
| ActionDetailsWindow | 轨道、动画段和 Item 属性 |
| ActionPreviewWindow | 视觉求值、相机和预览资源生命周期 |
| ActionRuntimePlaybackSession | 固定帧播放管线与 ActionRuntime 的衔接 |

窗口刷新不修改共享状态；Preview 不控制 Action 或播放。

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
        Windows/            Timeline、Details、Preview
        Preview/            空间求值
        Styles/             USS
        ActionAssetInspector.cs
        ActionAssetCreator.cs
        ActionAssetOpenHandler.cs

HitBox 的正式绑定使用 ActionHitBoxAnchor。旧 BoneReference、旧 Playable、Assets/Scripts/Legacy、AnimationConfig 与字符串动画键路径均已删除。Locomotion 动画呈现暂时留空，由独立路线重新设计；Action 动画继续直接使用 AnimationAsset。

## 5. 资产边界

`Assets/Create/ActionAsset/` 保留了按角色归类的旧 Action 资产及其 GUID，也包含新建的正式动作。目录移动没有转换内容：旧资产仍可能序列化 `_timelineAsset`、`_playbackBackend`、`_sequenceData` 和已删除的 managed-reference 类型；旧 Sequence 内容不能继续播放。项目负责人按需从当前格式重新配置动作，不把目录归位当作迁移完成。

新 AnimationAsset / AnimationRigAsset 位于 `Assets/Create/AnimationAsset/`；`Assets/Create/Test/` 保存 TestAction 与临时 Locomotion 配置。Action List、行为图分别归于 `Assets/Create/ActionList/`、`Assets/Create/Graph/`。这些资源的版本提交与系统代码基线分开，旧资源不作为兼容输入。

## 6. 验证

2026-09-24 静态复核与编译：

- 正式源码中旧后端、旧 Session 和旧编辑器类型引用扫描为零。
- Runtime 编译通过，0 error；警告来自项目既有废弃 API。
- Editor 与保留 EditMode 测试源码编译通过，0 error；本次 48 个警告来自既有项目和包源码。
- Unity 生成的 csproj 尚未由编辑器刷新；编译时通过临时 MSBuild target 忽略其中已删除的旧文件项，没有修改生成文件。
- 项目负责人已完成本阶段人工检查并接受结果；Unity Test Runner 不再作为当前阶段验收方式。检查范围与编译记录见[执行链收尾检查](CombatSample_Animation_Action_Actor_Closure_Audit_2026-09-23_zh-CN.md)。

Unity 重新生成工程文件后会清除旧 csproj 列表项。项目负责人按当前格式从零配置所需 Action；该资产工作独立于已收口的编辑与执行链。
