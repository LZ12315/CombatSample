# Action 编辑与播放正式架构

状态：当前仓库实现说明。更新于 2026-10-16。旧 V1、ActionSequence 和 Unity Timeline Action 文档均为历史记录。

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

旧的整招级 ActionMotionConfig 不恢复。ActionAsset 新增独立的 ActionMotionPolicy，表达整招默认的运动限制；RootMotion、Velocity、Impulse 与 SelfRotation 仍由正式 Timeline Item 提供运动来源。MotionPolicyItem 则覆盖本动作对应阶段的策略，不改变运动来源优先级。

### Action 运动控制

- Inspector 的 `Motion Control`：Movement 为 Allow（Move Speed 百分比）、Brake（停止接受移动意图、按当前 Locomotion 减速）、Locked（立即压制自主平移）；Turning 为 Normal、Limited（正值最大转速，°/s）、Locked。Advanced 保留 Air Movement 和 Gravity 百分比，非中性值在折叠标题中可见。
- Action 默认锁定自主移动和转向（Movement=Locked、Turning=Locked），各倍率字段保持 100%。新建策略及缺失策略时的初始化采用该默认值；已有显式保存的 Movement／Turning 值保持不变，不批量改写资源。旧 MotionPolicyItem 的 useLocomotionScale／useAirLocomotionScale／useGravityScale 及数值保留只读解释；旧移动倍率 0 表示锁定。显式编辑移动模式才启用新模式字段。
- Details 中的 MotionPolicyItem 使用相同术语，但每项增加 `Inherit Action (实际默认值)`。全部沿用的窗口是合法的无副作用配置，不阻止播放。只覆盖明确配置的项；窗口使用 `[StartFrame, EndFrameExclusive)`，结束后按当前剩余窗口／动作默认重算，不需要恢复条目。
- 每次 ActionRuntime.Begin 捕获默认策略、所有未静音有效窗口的时间与数值，并提前验证策略配置；不预建其他 Gameplay Item Runtime，也不推广为预扫所有 Item 的 Context 需求。编辑作者数据不改变当前执行。
- 合成分两层：窗口先替代本动作对应项的默认值；同项活动窗口中移动／空中倍率取较小值，Brake 合并为禁止自主驱动，Locked 以零平移倍率优先；转向锁定优先，其余取更小的正值转速上限；重力倍率相乘。不同字段互不替代。编辑器提示同项窗口重叠，轨道顺序不提供优先级。
- `ActionMotionPolicySession` 向 Motor 注册一个整招策略 owner，而不是让默认值和窗口各挂独立 owner；所以默认锁定可被恢复窗口放开。Motor 再与独立外部限制合成，动作窗口不能解除外部限制。原有水平、垂直与旋转来源优先级不变。
- 策略在打开动作帧、执行其 Gameplay Item 之前更新；慢放的分数帧和暂停保持当前策略，跨到新动作帧才切换窗口。末帧 World 完成后统一释放。Complete／Interrupt／Abort／重播／循环／Dispose 路径配对清理，旧会话不能释放新会话的 owner。
- Brake 从当前有效自主速度正常减速，放宽倍率不能令刹停突然加速；完全压制平移时清理自主速度缓存，解锁重新加速。不清除冲量／弹道、不继承或补偿被覆盖的 RootMotion 位移。
- 自主转向从 tick 起始身体朝向计算；Locked 不积累隐藏转向，Limited 不超过 Locomotion 自身的速度限制。Motor 只约束 Locomotion 的备用旋转提案，Root／Scripted Rotation 不被该限制缩放。
- 原始 Control Intent 继续供动作选向／取消／缓冲读取；移动执行与基础动画只读取允许的自主驱动。新增策略事实进入 MotionStateSnapshot 与反馈资格比较。ActorMotor 的 Runtime Debug 显示 Action 移动倍率、最终移动／转向限制及现有运动 owner。
- 限制自主移动不等于冻结世界；Gravity=0 不清除已有下落速度，悬停由 Vertical Velocity 接管表达；暂停 Action 不自动冻结 Motor 的时间。

本轮验证入口为 `ActionMotionPolicyTests`。编译检查和不依赖 Unity 原生调用的纯数据测试不能替代 Unity 中的完整 EditMode／UI／播放验收；重点人工检查窗口继承提示、Undo、重叠诊断、Action 禁用／HitStop、RootMotion／SelfRotation 交接和旧资源显示。

## 2. 正式入口

- 创建：菜单 `Assets/Create/CombatSample/Action/Action`。这是 Unity 菜单路径，不是磁盘目录；在当前选中的 Project 文件夹直接创建 New Action.asset。
- 打开：双击任意 ActionAsset 或在 Inspector 点击 Open Action Timeline。
- 工具：Tools/CombatSample/Action Timeline。
- 属性与预览：Tools/CombatSample/Action Details，或从 Timeline 的条目打开。
- Inspector：编辑 Action 的播放设置、运动控制默认值、触发、条件、取消规则与自身标签。
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
| ActionPreviewPoseSampler | 固定参考空间内的绝对时间骨骼采样、Playable 生命周期及姿态基准恢复 |
| EffectListGUI / HitFeedbackProfileInspector | 共用 Effect 列表绘制；各入口自行提交修改，Details 负责 Action 变更通知 |
| ActionRuntimePlaybackSession | 固定帧播放管线与 ActionRuntime 的衔接 |

Action Inspector 卡片依次为 `Timeline & Playback`、`Trigger & Start Context`、`Tags`、`Motion Control`、`Rules`。Timeline 入口、只读 Duration 与播放设置合并为一张卡片，内部留 4px 分隔；Tags 紧跟 Trigger。Requirements 与 Cancel Rules 放在同一张 `Rules` 卡片中，先 Requirements、后 Cancel Rules。Requirements 沿用 `_entryConditions` 序列化槽与完整 `ActionCondition` 类型集，不自动搬动已有输入条件；空列表表示没有额外限制。独立 Exit Conditions 配置与主动退出检查已移除，结束走自然完成、合法取消、返回 Locomotion 或外部生命周期停止。Requirements、每条 CancelRule 的 Conditions、ActionList 每项的 Conditions 共用 `ActionAuthoringListGUI` 的布局与 Rect 绘制入口，统一列表边距、折叠位置、增删、类型选择和排序。Condition 元素以类型选择器作为标题，展开后直接绘制字段或已有具体类型 Drawer，不再叠加 SubclassSelector 的外层缩进。`CancelRuleDrawer` 负责目标类型对应的有效字段；`CancelWindowDrawer` 将 Window 画成紧凑区间：宽 Inspector 同行显示 `[anchor] [frame] – [anchor] [frame]`，视图宽度小于 480 编辑器逻辑像素时改为 From / To 两行，Window 标签不单独占一行。锚点菜单与帧数框限制为紧凑宽度，仅 Custom 锚点（或多选混合值）显示帧输入；隐藏数值位置仍保留，切换目标类型或锚点不清除原配置。Inspector 仍负责提交 SerializedObject 修改、Undo 与 Action 编辑上下文通知。

卡片内容使用 12px 内边距，窄 Details 页左右收为 8px。ActionList Inspector 直接显示 Actions 列表，不再叠加 Available Actions 卡片及重复标题。共用列表标题仅显示 Requirements、Cancel Rules、Conditions 或 Actions 名称，不附加数量，也不绘制可编辑 Array Size 数字框；标题使用原生 Foldout 间距，不额外套用列表条目的拖动柄/类型选择缩进。空列表初次显示时折叠，用户展开后保留状态，可使用原生底部增删按钮。Inspector 展开的条件/取消列表在标题之后额外留 4px、底部按钮之后额外留 6px，元素上下各留 4px；Requirements 与 Cancel Rules 间额外留 6px。保留原 ReorderableList 边框、拖动柄、选中背景及增删交互；条件内容不再额外套用类型选择器的横向缩进，CancelRule 的属性与 Conditions 区域统一内缩 8px，ActionListEntry 的 Conditions 区域保持内缩 16px（Conditions 的标题、列表框及原生底部按钮整体内缩，右边界不变），与父条目标题区分层级；顶层 Requirements / Cancel Rules 不增加此内缩。条件列表的空状态通过 ReorderableList 的 drawNoneElementCallback 显示 `No additional conditions`，不替换列表边框或增删/排序逻辑。规则和入口字段按固定内缩补偿标签列宽。原生资源字段、具体 Condition Drawer、Input Steps、SerializeReference 类型选择与 SerializedProperty / Undo 路径不替换。实际窄窗布局、嵌套列表增删/排序与 Undo 由人工确认，不新增 UI 几何测试。

Requirements、Cancel Rules、CancelRule / ActionListEntry 的 Conditions，以及 Details / HitFeedbackProfile 的 Effects，可在列表标题上右键使用原生样式的 `Copy` / `Paste`。Paste 整组替换而非追加；Requirements 与各处 Conditions 互通，Cancel Rules、Effects 各自只接受同类列表。折叠状态下也可操作；不新增常驻按钮、不接管文本框或单个元素的复制菜单。剪贴板使用系统剪贴板，范围为同一 Unity Editor 会话（含脚本重载），其他应用复制会正常覆盖它，不支持关闭 Editor 后恢复。数据由临时、不可保存的 ScriptableObject 整组承载，经 Unity Editor 序列化生成快照，保留实际类型、嵌套配置、隐藏序列化字段、曲线、空引用、组内共享/循环关系及资源引用；各次粘贴与源数据独立。提交前构建完整替换，成功后单次 Undo / Redo，并通知 Action 编辑上下文及刷新嵌套列表高度缓存。多对象编辑、缺失管理引用类型、只读/Timeline 交互锁或不兼容剪贴板时相应菜单禁用；菜单打开后宿主内容改变则取消回调，避免旧数组路径写入其他条目。默认测试只检查临时对象的数据契约，不创建项目资源；菜单命中、折叠列表、跨 Inspector / Details 刷新以及真实资源保存重载需在 Unity 内人工验收。

ActionList 的 `_entries` 保存 `ActionListEntry`（Action 引用、Allow From Neutral、Conditions）。列表条件只用于没有当前 Action 时的 Poll 起手，空条件无额外限制；关闭 Allow From Neutral 仅禁止起手，不移除 Event 注册或 Any / AnyWithTag 的取消目标发现，External 也不检查该入口条件。注册动作按首次出现顺序去重；同一 Action 可以有多个 Neutral 入口，按原有仲裁顺序选择首个通过的候选。每条 CancelRule 的 Conditions 在普通取消展开与 External 取消校验中都求值，再与目标 Requirements 合并；输入凭据只在实际开始成功后统一原子消费，跨层重复引用同一事件只消费一次。Event 维持原先仅跳过 Requirements 中玩家输入条件的行为。Locomotion 返回规则维持移动意图门槛，附加 Conditions 通过后才作为低于合法 Action 的返回候选，只有返回被执行时才消费其输入。

Animation Segment 的 `Use Full Clip` 与 `Revert` 位于同一行：前者占剩余宽度，后者固定为较窄的 64px。点击 `Use Full Clip` 将 Source 范围设为整个 Clip 并立即通过原有校验／Undo 命令提交，不再提供独立 Apply 按钮。其他手动字段仍使用 Enter 提交草稿，Esc 或 Revert 丢弃未提交草稿；已提交的 Full Clip 修改使用 Undo 撤销。

Timeline 顶栏的播放位置读数固定宽度（普通布局 96px、紧凑布局 80px），数字位数变化不挤动后续控件；超长数值截断显示，完整值仍可由原有 tooltip 查看。每次打开 Timeline 默认开启 Snap，窗口内仍可手动关闭或用 Ctrl/Cmd 临时反转。切换 Action 或在无条目选择时打开编辑器，默认选择时间最早且身份可选的 Animation Segment；已有明确条目选择保留，不移动播放头、不写资产，无可选动画段时保留空条目选择。

窗口刷新不修改共享状态；Preview 不控制 Action 或播放。

Details 在刷新属性页之前转发预览通知；播放头更新只重绘预览，不重建属性控件。分隔条调整不写资产、不产生 Undo，也不丢弃未提交的 Timing 草稿。预览相机和设置折叠状态由 Details 序列化保存；关闭窗口或脚本重载时释放资源，进入 Play Mode 时释放预览对象，失焦或面板卸载时结束相机拖动。旧独立 Preview 窗口的布局和相机状态不迁移。

Preview 的整数 Frame N 表示 Runtime 第 N 帧位移提交后的 Hit 阶段画面：Pose 在 N 的时间采样，角色和生效 HitBox 共享帧后世界位置。小数 PreviewPosition 仅在相邻帧后位置之间做视觉插值；Duration 终点显示最终位移，HitBox 已结束。Preview 不复现碰撞后的实际世界运动或完整 gameplay replay。

Pose 每次求值先恢复角色初始 Transform 基准，再用 Manual Graph 采样绝对 SourceTime，最后应用独立累积的世界位移。时间跳转同步 Playable 的 current / previous time，避免把之前访问的位置当作 RootMotion 时间区间；内容失效同时释放旧 Graph，下一次求值重新绑定 Clip。整数帧拖动、逐帧和首末帧导航暂停连续播放，并一次发布暂停状态与目标时间。Timeline 的时间读数和播放头显示实际 PreviewPosition，小数时间不再伪装成精确整数帧。

Timeline 条目实际开始拖动时暂停播放，并把 PreviewPosition 就近定位到整数帧，保留 Duration 终点。移动手势在 0 帧处夹紧，多选以最早条目为边界且保留相对时间；这项边界限制独立于 Snap 开关。磁吸使用未取整的鼠标位移计算屏幕距离，10px 捕获、16px 释放；目标为播放线、0 帧和可见条目边界，同距离优先播放线。Point 仅提供事件帧，Range / Animation 提供 start 与 end-exclusive；多选移动使用被抓取条目及整体外边界。吸附辅助线、播放线或目标边界高亮和操作提示显示实际命中目标。Ctrl/Cmd 在拖动中临时反转 Snap；非法重叠、轨道或动画 Source 范围仍由原操作快照校验。

编辑写入保留同轨不重叠、快照过期、Undo、编辑锁与身份修复。旧 Issues 分类和全量配置校验已删除。运行时不预扫未来 Item 的 Context 需求，也不因烘焙轨迹 stale 而拒绝执行；ActionRuntime.Begin 仍检查动画快照并在失败时 Abort。

## 4. 目录职责

    Assets/Scripts/Action/
      Data/                 ActionAsset、ActionTimelineData、Cancel 等资产数据
      Conditions/           共用 Action Requirements、Neutral 入口与取消规则条件
      Runtime/              ActionStateManager、ActionPlayer、ActionRuntime、Item Runtime、采样与执行上下文
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

HitBox 的正式绑定使用 ActionHitBoxAnchor，检测实现位于 `Assets/Scripts/Combat/HitBox/ActorHitBoxRuntime.cs`，由 ActorSimulationRuntime 持有并在固定阶段调用。普通取消候选和 External 取消请求共用 `CancelRule.MatchesTarget`；来源过滤、窗口、准入和仲裁仍由 ActionStateManager 负责。命中允许反馈后同步交给 `ImpactSystem.HandleConfirmedHit`，Receiver/Profile 与空间参考准备由 Impact 负责。

图任务与适配代码位于 `Assets/Scripts/Graph/`，保留 NodeCanvas 的类型名和命名空间。HitFeedbackProfile、HitFeedbackReceiver 位于 Impact。旧 BoneReference、旧 Playable、Assets/Scripts/Legacy、AnimationConfig 与字符串动画键路径均已删除。Action 动画继续直接使用 AnimationAsset；Locomotion 由 ActorLocomotion、Locomotion Runtime 与 ActorAnimation 协作，见[现行 Locomotion 架构](CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)。目录与职责整理范围及验证见[Scripts 整理记录](CombatSample_Scripts_Organization_2026-10-08_zh-CN.md)。

## 5. 资产边界

当前 Jaeger、Kiana、Katana 三份 ActionList 已从 `_globalActions` 引用列表显式迁移为 `_entries`，保留引用和顺序，不自动迁移 Action 内的条件。Kiana_Jump 原来因空 Poll EntryConditions 不会起手，迁移后关闭其 Allow From Neutral 以保留这个行为；需要时应补入口触发条件再开启。历史外部 ActionList 重新导入前需要相同结构迁移，不能直接沿用旧引用数组。

`Assets/Data/Action/` 按 Jaeger、Kiana 归类 Action 资产与各自的 Action List。Kiana 普攻位于 `Assets/Data/Action/Kiana/Attack/`，受击与移动动作分别位于该角色的 `Hit/`、`Move/`；Jaeger 受击动作位于 `Assets/Data/Action/Jaeger/Hit/`。

AnimationAsset 和 AnimationRigAsset 位于 `Assets/Data/Animation/`；Locomotion 配置位于 `Assets/Data/Locomotion/`；行为图位于 `Assets/Data/Graph/`。当前不再存在 `Assets/Create/`、`Action/Kiana_New/`、`Assets/Data/Test/` 或旧的资产 Archive 目录。当前项目目录约定见[项目结构](Project_Structure.md)。

旧归档从工作区删除后，部分行为图、角色 Prefab 和场景仍保留旧 Action、Action List 或 Timeline 引用，当前资产整理尚未整体验收；具体范围见[项目结构中的未收口引用](Project_Structure.md#尚未收口旧归档删除的引用)。不把目录归位或编译通过当作这些图和场景已可正常运行。

Impact 只保留正式配置路径：速度效果使用 `SpeedEffectConfig`，命中特效使用 `HitVfxConfig`。旧 HitStop/HitStick、屏幕向/世界向独立配置及其兼容执行入口已删除；两个朝向解算器仍由 `HitVfxConfig` 使用。历史资产不是兼容输入，若以后重新导入，应先迁移到当前数据类型；移动目录本身不会转换旧资产内容。

## 6. 验证

Action MotionPolicy 本轮：

- Runtime、Editor（含测试代码）及去除 UNITY_EDITOR 符号的 Runtime 均通过 Roslyn 编译；后者仅检查非编辑器条件路径，不是完整 Player Build。
- 使用 Unity 自带 Mono 实际执行 `ActionMotionPolicyTests` 中两个无 Unity 原生调用的数据／合成用例，均通过：独立限制与 owner 释放、配置校验与旧字段解释。没有把这两个用例当作完整 Unity Test Runner 执行。
- 已补充临时对象 EditMode 契约，覆盖窗口边界、快照、重叠、慢放暂停、终止与循环清理、Runner／ActorLocomotion 接线及真实 Motor 来源仲裁；完整套件尚待 Unity 内执行。
- 人工仍需确认 Inspector／Details 的模式、继承提示、Advanced 摘要、Undo、只查看不标脏、重叠诊断，以及实际动作的刹停、锁定恢复、禁用／HitStop 和来源交接。未改角色资源或场景。

2026-10-08 Scripts 整理的编译、纯逻辑测试和 Unity 原生验证边界见[本轮记录](CombatSample_Scripts_Organization_2026-10-08_zh-CN.md)。以下结果保留其原验证日期，不作为本轮完整验收。

2026-10-01 Timeline 磁吸与 0 帧边界：

- Unity 2022.3.62f3 Roslyn 编译完整 Editor / 测试程序集通过；隔离 Unity 批处理执行 20 个针对性契约用例通过。
- 新增纯 EditMode 用例覆盖实际像素捕获距离、保持 / 释放、播放线同距优先、失效目标、0 帧单条目与多选限制、Point 的真实吸附边缘、非法重叠和播放暂停归整（包含 Duration 终点）。
- 修改后的 USS 在 Unity 中导入成功，无样式解析告警。鼠标手感、辅助线与高亮、自动平移、Ctrl/Cmd 和松手落点仍需窗口人工复查，见 [Timeline 吸附修复记录](ActionTimeline_Snapping_Handoff_2026-10-01_zh-CN.md)。

2026-10-01 Preview 确定性排查：

- Unity 2022.3.62f3 Roslyn 编译完整 Editor / 测试程序集通过，输出位于临时目录。
- 隔离 Unity 批处理工程执行 12 个针对性契约用例通过；覆盖姿态恢复、Clip 切换、空姿态、同 Clip 内容变更、手动导航暂停、连续时间、空间累积和 HitBox 位移。
- 使用实际 Kiana Humanoid 模型及 8 个 Clip，对每个实例的 99 个 Transform 做 4,497 次同时间姿态比较；逐帧、随机 seek、循环回跳、跨 Clip、空姿态和失效重建后的位置、旋转和缩放差异均为零。
- Timeline 新的小数读数、连续播放头以及原始异常 Action 的完整窗口渲染仍需 Unity 界面复查。详见 [Preview 排查记录](ActionPreview_Determinism_Audit_2026-10-01_zh-CN.md)。

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
