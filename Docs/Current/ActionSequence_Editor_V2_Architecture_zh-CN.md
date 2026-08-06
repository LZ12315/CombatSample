# ActionSequence 编辑器 V2 架构

> 状态：已确认的架构基线；Stage 0 至 Stage 7 运行时接入已实现。V2 是 ActionSequence 的正常编辑入口，Prototype 仅作为诊断退路保留。
> 版本：1.0  
> 日期：2026-08-03  
> 目标版本：Unity 2022.3.62f3  
> 范围：ActionSequence 编辑器，以及编辑器所需的数据基础  
> 运行时边界：Sequence backend 的 ActionAsset 通过 ActionPlayer 使用固定帧 ActionSequence runtime 播放；编辑器预览仍只驱动播放头
> English edition: [ActionSequence Editor V2 Architecture](ActionSequence_Editor_V2_Architecture.md)
> 迭代背景与完成度：[ActionSequence 大迭代复盘与当前架构说明](ActionSequence_Iteration_Retrospective_2026-08-07_zh-CN.md)

## 1. 核心决定

目前的 `ActionSequenceEditorWindow` 定位为原型。它已经验证了数据模型和基础交互，但不能再通过持续局部修补，逐渐变成正式编辑器的基础。

ActionSequence Editor V2 将作为一套独立的 UI Toolkit 编辑器开发，并明确划分架构边界：

```text
序列化领域数据
      ↓
序列化访问层 + 稳定身份解析
      ↓
命令层 ────────────── 校验层
      ↓                  ↓
编辑器状态 ───────── 变更通知
      ↓
UI Toolkit 视图 + Manipulator
      ↓
Unity Inspector 选择桥接
```

V2 是 ActionSequence 的正常编辑入口。原型只从诊断菜单保留，只接受编译或数据读取兼容修复。删除原型必须经过单独稳定观察期和明确确认。

## 2. 产品定位

ActionSequence 是面向战斗动作的、确定性的固定帧编辑工具。它不是通用的过场序列编辑器，也不负责重造 Unity Timeline runtime。

我们采用 Unity Timeline 已经被验证的编辑语言：

- Track 是一级编辑单位。
- Track 类型决定它可以创建哪些 Clip。
- Timeline 窗口负责结构和时间关系编辑。
- Inspector 负责编辑当前选中的 Sequence、Track 或 Clip。
- 选择、右键菜单、锁定、静音、Undo/Redo、缩放、平移和帧吸附必须保持一致。

我们保留自己的运行规则：

- 战斗关键逻辑不依赖 `PlayableGraph`。
- 不把 Timeline 的场景绑定模型作为运行时真相来源。
- Track 和 Clip 不改成 Timeline subasset。
- 运行调度仍然只使用整数帧。
- 保持现有 frame 0、`[startFrame, endFrame)`、取消与清理语义不变。

## 3. V2 目标与非目标

### 3.1 V2 必须完成的编辑闭环

只有当用户可以完成以下流程时，V2 才算完成：

1. 打开 Sequence backend 的 `ActionAsset` 或独立 `ActionSequenceAsset`。
2. 新增、重命名、排序、Mute、Lock、折叠和删除 Track。
3. 每个 Track 只能创建合法的 Clip 类型。
4. 选择 Sequence、Track 或 Clip，并在 Inspector 中编辑正确的配置。
5. 以整数帧吸附的方式移动和缩放 Clip。
6. 拖动播放头、以鼠标为中心缩放、平移并 Fit 已编辑内容。
7. 同时支持 Fixed 与 Auto Duration，而且不要求用户为了获得可视空间手动增加总帧数。
8. 每个持久化操作都能作为一个清楚的操作被 Undo/Redo。
9. 校验问题可见，但编辑器不会静默删除或修复用户数据。
10. 保存、重新载入、脚本重编译以及重开窗口后，选择身份和布局状态不会损坏。

### 3.2 V2 首版明确不做

- 真实驱动 Animancer、Force 或 HitBox 预览。
- 多选和框选。
- 跨 Track 拖动 Clip。
- 复制、粘贴和 Duplicate 工作流。
- Clip 混合、曲线、缓动或重叠冲突处理。
- 嵌套 Track 或 Group Track。
- Marker 或 Signal。
- 自定义 Track 高度。
- 场景对象运行时绑定。
- 基于 GraphView 实现。

这些功能都需要独立的设计决定，不能顺手塞进 V2 首版。

## 4. 编辑器使用语言

### 4.1 窗口布局

```text
┌───────────────────────────────────────────────────────────────────────┐
│ Target | Add Track | ◀ ■ ▶ | FPS | Duration | Frame | Zoom | Fit    │
├──────────────────────┬────────────────────────────────────────────────┤
│ Track 控制区         │ 0  1  2  3  4  5 ...        帧标尺            │
├──────────────────────┼────────────────────────────────────────────────┤
│ ▾ Animation  M L  +  │      [ Animancer Clip             ]           │
│ ▾ Motion     M L  +  │              [ Impulse ]                       │
│ ▾ HitBox     M L  +  │                    [ HitBox ]                   │
│ ▾ State      M L  +  │ [ Tags ]                                      │
├──────────────────────┴────────────────────────────────────────────────┤
│ 状态栏 / 校验摘要                                                     │
└───────────────────────────────────────────────────────────────────────┘
```

左侧 Track 控制列不进行横向滚动。标尺和所有 Lane 共享同一个横向帧坐标变换。Track Header 与 Lane 共享同一个纵向滚动位置。

### 4.2 选择语言

V2 首版只支持单选：

| 点击目标 | Timeline 中的结果 | Inspector 中的结果 |
| --- | --- | --- |
| Clip | 高亮 Clip | Clip 时间与类型专属 Config |
| Track Header 或空白 Lane | 高亮 Track | Track 设置 |
| 空白背景 | 清除 Track/Clip 选择 | Sequence 设置 |
| 其他 Asset | 更换目标或使窗口解除绑定 | 该 Asset 的普通 Inspector |

选择优先级固定为 `Clip > Track > Sequence`。

Track 和 Clip 是 managed reference，而不是独立的 `UnityEngine.Object`。因此在不更换数据模型的前提下，V2 无法让 Clip 成为真正的 Unity Object selection。V2 会继续把所属 Asset 设置为 `Selection.activeObject`，并由它的自定义 Inspector 把当前 managed reference 作为主要编辑对象。不会引入隐藏 proxy object 或 subasset。

### 4.3 Track 语言

- Add Track 只出现在 Toolbar 和 Timeline 背景右键菜单中。
- 可创建的 Track 类型来自对 `ActionSequenceTrackDefinition` 安全子类的反射枚举。
- 每条 Track 的 Add Clip 菜单只显示它的 `AllowedClipTypes`。
- Mute 影响运行时参与，并让 Lane 在视觉上弱化。
- Lock 阻止结构、时间和配置编辑，但解锁按钮本身始终可用。
- Collapse 只影响编辑器显示。
- 删除是明确命令；删除非空 Track 时必须显示 Clip 数量并确认。
- Track 只允许在同一个 Phase 分组内调整顺序。

编辑器按 `Phase` 分组显示 Track，然后按该 Phase 内的序列化顺序显示。新 Track 插入到对应 Phase 分组末尾。对于旧 Asset 中跨 Phase 顺序异常的数组，V2 会按实际执行顺序显示，但不会静默重写数组；用户可以使用显式 Repair 命令整理序列化顺序。

这样，视觉上的从上到下顺序会与运行顺序一致：

```text
Phase -> Phase 内 Track 顺序 -> Clip StartFrame -> Track 内 Clip 顺序
```

### 4.4 Clip 语言

- 单击负责选择；双击未来可以 Frame 该 Clip，但不会打开第二个编辑器。
- 拖动中间区域预览同 Track 内移动。
- 拖动左右 Handle 预览缩放。
- 所有时间操作吸附到整数帧。
- 有效区间始终是 `[startFrame, endFrame)`，并至少持续一帧。
- Escape 取消当前操作。
- 释放鼠标时才把整个操作作为一个 Undo 命令提交。
- 右键菜单只显示所属 Track 上合法的操作。
- 首版允许重叠；运行顺序仍然是确定的。V2 不擅自定义 Blend 或冲突处理语义。

锁定 Track 仍然可以被选中并解锁，但其 Clip 不能被选中或编辑。这与 Unity Timeline 的保护语言一致，也可以防止用户从 Inspector 绕过 Lock。

### 4.5 时间、Duration 与可视工作区语言

Sequence Duration 与编辑器工作区是两个不同概念：

- `FixedFrames` 是明确写入数据的硬时长。
- `AutoFromClips` 使用最大 Clip EndFrame 推导动作时长。
- `viewEndFrame` 只属于编辑器，可以长于两种 Duration。
- 在右边缘附近创建或拖动 Clip 时，可视工作区自动向右增长。
- Fit 只调整视图来容纳已有内容并留出边距，不会重写 Duration。
- 缩放时，鼠标下方对应的 Frame 保持不动。
- 标尺根据 Zoom 自动选择大刻度和小刻度间距。
- Zoom 足够近时每一帧都有小格线，但只在可读的位置显示数字。

新创建的 Sequence 应默认使用 `AutoFromClips`。现有 Asset 保留已经序列化的 Duration Mode。默认值的修改属于创建工厂调整，不是隐式迁移。

本阶段的播放控制只控制编辑器播放头，不驱动 Action runtime、动画、位移或 HitBox 判定。

## 5. 现有领域模型与运行时契约

V2 保留以下所有权关系：

```text
ActionAsset / ActionSequenceAsset
└── ActionSequenceData
    ├── frameRate
    ├── durationMode
    ├── durationFrames
    ├── tracks[] : SerializeReference
    │   └── clips[] : SerializeReference
    └── legacy clips[]
```

运行时真相来源仍然是 `tracks[].clips[]`。旧的 flat clips list 只用于迁移和排查。

编辑器绝不能静默进行以下操作：

- 用户删光 Track 后自动重建默认 Track。
- 删除非法或缺失类型的 managed reference。
- 自动把 Clip 移到另一条 Track。
- 仅仅因为绘制或校验就重新排序序列化数组。
- 仅仅因为打开 Inspector 或窗口就 Clamp 数据。

Normalize 只负责安全的标量不变量。Validate 负责报告结构问题。具有破坏性的 Repair 必须由用户明确执行。

## 6. 必要的数据基础：稳定身份

当前 Selection Bridge 使用数组 Index 和 `SerializedProperty.propertyPath` 标识对象。重排、插入、删除、Undo 以及部分 SerializeReference 操作都会改变它们，因此它们不能作为 V2 身份。

在实现 V2 交互前，为两类对象加入隐藏的序列化 ID：

```csharp
ActionSequenceTrackDefinition.editorId
ActionSequenceClipDefinition.editorId
```

当前 Clip 的 `Guid` 来自一个非序列化字段，Reload 后会重新生成，因此不能作为稳定身份。Stage 1 必须把它的存储替换为序列化 ID；如果已有代码依赖公开 Getter，则保留该 API。Track 也要使用同一种身份机制。

规则如下：

- ID 是不透明的 32 位 GUID 字符串。
- ID 只表示编辑身份，不参与运行时排序或行为。
- 创建 Track/Clip 的工厂命令负责生成 ID。
- 用 V2 打开旧 Asset 时，对缺少 ID 的对象执行一次非破坏性身份升级，并只把 Asset 标记 Dirty 一次。
- 重复 ID 属于校验错误。显式 Repair 只重新生成第一次出现之后的重复 ID。
- 移动、重排以及 Undo/Redo 都必须保留 ID。
- 未来的 Duplicate 命令必须创建新 ID。

升级过渡期内，本次会话可以使用 property path 作为 fallback。只有当 ID 已存在时，Selection 才被认为可以持久解析。

Selection 最终表示为：

```text
目标 Asset + Selection Kind + Track ID + 可选 Clip ID
```

Index 和 Property Path 只是一次解析的结果，不再作为被保存的身份。

## 7. 架构分层

### 7.1 Domain 层

负责可以被运行时使用的序列化定义：

- `ActionSequenceData`
- `ActionSequenceTrackDefinition`
- `ActionSequenceClipDefinition`
- 具体 Track 与 Clip
- Validation Issue 值类型

Domain 不依赖 UI Toolkit、Inspector 状态、Pointer Event 或窗口生命周期。

### 7.2 序列化访问层

`ActionSequenceSerializedDocument` 包装当前 Unity Asset，并负责：

- 持有 `SerializedObject`；
- 为两种 Asset 找到根 `ActionSequenceData` Property；
- 把 ID 解析为 `SerializedProperty`；
- 为 Track/Clip 提供安全的枚举快照；
- Target Revision 和变更检测；
- managed-reference 类型创建辅助；
- 除 Command 外不进行持久化修改。

View 只接收只读模型或已经解析的 Handle，不允许自行拼接 Property Path。

解析得到的 Property 只在单次操作内使用，之后立即丢弃。结构变更、Undo/Redo、Domain Reload 或 Target 更换之后，绝不能继续缓存旧 `SerializedProperty`。

### 7.3 Command 层

`ActionSequenceEditorCommands` 是 V2 窗口修改持久化数据的唯一入口：

- 新增、删除、重命名、重排 Track；
- 切换 Mute、Lock、Collapse；
- 新增、删除 Clip；
- 设置 Clip Timing；
- 修改 Duration 设置；
- 分配或修复稳定 ID；
- 执行显式校验修复。

每个 Command 都必须：

1. 根据最新序列化状态解析 ID。
2. 检查操作是否合法以及 Track 是否锁定。
3. 开始或加入一个有名称的 Undo Group。
4. 能用 `SerializedProperty` 的地方都通过它修改。
5. 只有 managed-reference 操作确实无法通过 Property 完成时才使用 `Undo.RecordObject`。
6. Apply Property，在需要时标记 Dirty，并只发布一个 ChangeSet。
7. 返回包含成功状态、受影响 ID、新 Selection 和校验消息的 Result。

连续拖动和缩放使用临时预览值。Pointer Release 时只提交一次，因此一次手势只生成一条 Undo。Escape 直接丢弃预览。

### 7.4 Editor State 层

`ActionSequenceEditorState` 每个窗口一份，并且不包含任何用户资产数据：

- Target Asset；
- 选中的 Track/Clip ID；
- 当前 Frame；
- 播放状态和编辑器时间戳；
- Pixels Per Frame；
- 横向与纵向 Scroll；
- 编辑器工作区 EndFrame；
- 当前交互预览；
- Hover 与右键菜单状态；
- Validation 摘要；
- 布局与视图偏好。

Target、Selection 和当前操作属于 Session State。Zoom、Splitter Width 等显示偏好使用 UI Toolkit ViewData 或带项目专属 Key 的 `EditorPrefs`。Track、Clip、Mute、Lock、Collapse 和 Duration 永远不能写入 `EditorPrefs`。

`EditorWindow` 会序列化一份最小的 Domain Reload Snapshot，其中包含 Target、Selected ID、CurrentFrame、Zoom 与 Scroll。`CreateGUI()` 根据 Snapshot 重建 `ActionSequenceEditorState`，并在恢复 Selection 前重新校验所有 ID。正在进行的 Pointer Manipulation 和 Playback 永远不恢复。

### 7.5 Selection Bridge

`ActionSequenceEditorSelection` 发布不可变 Selection Value 和 `selectionChanged` 事件，内容使用 Stable ID 和 Target Asset。被选中的 Asset 仍然是 `Selection.activeObject`。

Selection Bridge 之所以是全局的，只因为 Unity Inspector Selection 本身是全局的。Timeline 的 Zoom、Scroll、CurrentFrame 和 Drag State 必须按窗口保存。打开两个 V2 窗口时，最后发生交互的窗口负责 Inspector Context；另一个窗口保留自己的本地高亮，但在再次交互前不会抢占 Inspector。

### 7.6 Validation 层

`ActionSequenceValidator` 只读，并报告：

- 缺失或重复 ID；
- Null managed reference；
- 缺失的 managed-reference 类型；
- Track 不接受该 Clip 类型；
- Clip Phase 与 Track 不一致；
- 非法帧区间；
- Clip 超出 Fixed Duration；
- 尚未迁移的 Legacy Clip；
- 运行顺序与显示顺序异常。

Issue 包含 Severity、目标 ID、Message 和可选的显式 Repair Command ID。除了一次性的“补齐缺失 Editor ID”之外，Validation 绝不能在 Repaint、Binding、反序列化或打开窗口时自动修复数据。

## 8. UI Toolkit 技术策略

V2 只依赖 Unity 2022.3.62f3 已提供的 API。

### 8.1 UXML、USS 与 C# 的分工

- UXML 定义稳定的窗口外壳：Toolbar Host、Ruler Corner、Viewport Host、Overlay 和 Status Bar。
- USS 定义尺寸、颜色、状态 Class、Hover/Selection/Locked/Muted 样式和主题差异。
- C# 自定义 `VisualElement` 实现动态 Track、Ruler、Grid、Clip 与交互逻辑。

动态 Timeline 几何不会放进 UI Builder；它由数据驱动，应该写成可复用的 C# Control。

### 8.2 绘制分工

密集且不可交互的图形使用 `generateVisualContent`：

- 标尺刻度和 Label 背景；
- 垂直帧格线；
- Phase 分隔线；
- Selection/Drag Guide；
- Playhead 背景线。

可交互对象使用保留式 `VisualElement`：

- Track Header 与控制按钮；
- Clip Block；
- Clip Label；
- Resize Handle；
- Playhead Handle；
- 菜单和 Tooltip。

不能为每个帧刻度或格线创建一个 VisualElement。Zoom、Scroll、尺寸、主题或 CurrentFrame 改变时，Canvas Control 通过 `MarkDirtyRepaint()` 重绘。

### 8.3 View 组合

```text
ActionSequenceEditorWindowV2
└── ActionSequenceEditorRoot
    ├── ActionSequenceToolbar
    ├── TimelineHeader
    │   ├── TrackHeaderCorner
    │   └── ActionSequenceRulerView
    ├── TimelineBody
    │   ├── ActionSequenceTrackHeaderView
    │   │   └── ActionSequenceTrackHeaderRow[]
    │   └── ActionSequenceViewport
    │       ├── ActionSequenceGridView
    │       ├── ActionSequenceTrackLaneView[]
    │       │   └── ActionSequenceClipView[]
    │       ├── ActionSequenceGuideOverlay
    │       └── ActionSequencePlayheadView
    ├── HorizontalScroller
    └── ActionSequenceStatusBar
```

Header Column 和 Viewport 使用带重入保护的纵向滚动同步。Ruler、Lane、Overlay 和横向 Scrollbar 共享同一个 `TimelineTransform`：

```text
screenX = headerWidth + frame * pixelsPerFrame - scrollX
frame   = round((screenX - headerWidth + scrollX) / pixelsPerFrame)
```

全编辑器只能有这一套坐标转换实现。

### 8.4 Track Row 与虚拟化

V2 首版使用由稳定 Track ID 驱动的可协调复用 Row。战斗动作通常是几十条 Track，而不是几千条。这种实现能确保 Header 与 Lane 成对对应，也避免同步两个独立虚拟列表带来的复杂性。

Refresh 不再清空并重建整个 Visual Tree，而是按 ID 进行 Reconcile：

- 复用已有 Row 和 Clip View；
- 只增删发生变化的 View；
- 结构改变后重新 Binding；
- Timing/Zoom 改变时只更新 Geometry；
- Canvas Layer 只在输入改变时 Repaint。

只有真实性能分析证明 Track 数量成为问题后，才引入单一 Composite Row 或协同 Pooling 的虚拟化方案。不能提前为了“看起来先进”增加复杂度。

### 8.5 为什么不用 GraphView

GraphView 解决的是 Node/Edge 图编辑。Timeline 的核心是共享的一维时间坐标、同步 Row、区间操作和密集格线绘制。GraphView 会引入大量图语义，却不会解决这些核心问题，因此 UI Toolkit 自定义 Control 才是正确底层。

## 9. 交互架构

交互逻辑放在独立 UI Toolkit Manipulator 中：

- `TimelinePanManipulator`
- `TimelineZoomManipulator`
- `PlayheadScrubManipulator`
- `ClipMoveManipulator`
- `ClipResizeManipulator`
- `TrackReorderManipulator`

每种 Manipulator 使用相同生命周期：

```text
PointerDown -> 校验 -> Capture Pointer -> 创建临时预览
PointerMove -> 更新预览状态 -> 只更新受影响 Geometry
PointerUp   -> Release Pointer -> 执行一个 Command -> Reconcile
Escape      -> Release Pointer -> 丢弃预览 -> Repaint
CaptureLost -> 如果尚未 Commit，则确定性地 Cancel
```

Manipulator 绝不能直接修改序列化字段。

键盘操作使用 Unity Shortcut Management，并通过 Window/Context Guard 限定范围。首版快捷键：

- Space：播放/暂停编辑器播放头。
- S：停止并重置播放头。
- Delete/Backspace：删除选中的未锁定对象。
- F：Frame 当前对象；没有对象时 Frame All。
- L：锁定/解锁当前 Track。
- M：Mute/Unmute 当前 Track。
- Left/Right：播放头移动一帧。
- Shift + Left/Right：播放头移动一个当前大刻度单位。

TextField 和正在编辑的 Property 拥有优先权，快捷键不能吃掉文字输入。

## 10. Inspector 架构

V2 自定义 Inspector 使用 UI Toolkit `CreateInspectorGUI()` 和 `PropertyField` Binding。

Inspector 模式：

```text
选中 Clip
  Selected Clip
    Identity/Type（只读）
    Timing
    类型专属 Config
  Owning Sequence [紧凑]
  ActionAsset Settings [默认折叠]
  Debug Raw Data [默认折叠]

选中 Track
  Selected Track
    Identity/Type/Phase（只读）
    Name、Mute、Lock、Collapse
  Owning Sequence [紧凑]
  ActionAsset Settings [默认折叠]
  Debug Raw Data [默认折叠]

没有选中 Track/Clip
  Sequence Settings
  ActionAsset Settings
  Validation
  Debug Raw Data [默认折叠]
```

Selection 或序列化结构变化时，Inspector 都必须重新根据 ID 解析 Property，绝不能继续持有 List 变动前的 Property。

Bound Field 提供 Unity 标准序列化与 Undo 行为。`SerializedPropertyChangeEvent` 会立刻发布编辑器变更通知，因此 Timeline 无需等待鼠标移到窗口上才更新。窗口同时监听 SerializedObject 变化，作为 Undo/Redo 和外部修改的兜底。

Debug Raw Data 只作为排查入口。它的 Label 使用 managed-reference 类型名或 DisplayName，绝不能显示 managed-reference 数字 ID。

## 11. 变更与刷新模型

V2 使用明确的 Invalidation 类型：

```text
TargetChanged      -> 重建 Document 和完整 ViewModel
StructureChanged   -> 重解 Selection、协调 Track/Clip View、Validate
ContentChanged     -> 更新 Label/Style/Inspector、校验受影响对象
TimingChanged      -> 更新 Clip Geometry、Duration 和 Ruler Range
ViewportChanged    -> 更新 Transform 并重绘 Canvas
SelectionChanged   -> 只更新高亮和 Inspector
PlaybackChanged    -> 只更新 Playhead
```

不能再用一个通用 `Refresh()` 对每种事件重建全部内容。

变更来源包括：

- 成功执行的 Editor Command Result；
- UI Toolkit SerializedProperty Change Callback；
- `Undo.undoRedoPerformed`；
- Target 更换或销毁；
- Domain Reload 和 `CreateGUI()` 重建；
- Geometry 变化；
- 仅在预览播放期间使用 Editor Update。

这会彻底移除原型中“鼠标进入窗口后才刷新”的依赖。

## 12. 生命周期与失败处理

- Domain Reload 后 `CreateGUI()` 可能多次执行，构建过程必须幂等。
- 所有 Event Subscription 必须在 Attach/Detach 或 Enable/Disable 中成对处理。
- Target 被销毁或不兼容时，窗口回到 Empty State。
- SerializeReference 类型缺失时显示 Error Placeholder 和 Raw Diagnostic，不删除数据。
- Selection ID 无效时只清除 Selection，不改资产数据。
- Undo/Redo 后按 ID 重解 Selection；对象已不存在时，先回退到所属 Track，再回退到 Sequence。
- Pointer Capture 丢失不能留下只提交了一半的 Timing 修改。
- 多窗口之间不共享 Zoom、Scroll、CurrentFrame 或 Drag State。

## 13. 建议目录结构

V2 与原型隔离：

```text
Assets/Scripts/ActionSequence/Editor/V2/
├── ActionSequenceEditorWindowV2.cs
├── ActionSequenceEditorWindowV2.uxml
├── Styles/
│   └── ActionSequenceEditorV2.uss
├── Core/
│   ├── ActionSequenceEditorState.cs
│   ├── ActionSequenceEditorSelection.cs
│   ├── ActionSequenceEditorChangeSet.cs
│   ├── ActionSequenceEditorCommands.cs
│   ├── ActionSequenceSerializedDocument.cs
│   ├── ActionSequenceTimelineTransform.cs
│   └── ActionSequenceValidator.cs
├── Views/
│   ├── ActionSequenceEditorRoot.cs
│   ├── ActionSequenceToolbar.cs
│   ├── ActionSequenceRulerView.cs
│   ├── ActionSequenceGridView.cs
│   ├── ActionSequenceTrackHeaderView.cs
│   ├── ActionSequenceTrackHeaderRow.cs
│   ├── ActionSequenceTrackLaneView.cs
│   ├── ActionSequenceClipView.cs
│   ├── ActionSequencePlayheadView.cs
│   └── ActionSequenceStatusBar.cs
├── Manipulators/
│   ├── TimelinePanManipulator.cs
│   ├── TimelineZoomManipulator.cs
│   ├── PlayheadScrubManipulator.cs
│   ├── ClipMoveManipulator.cs
│   ├── ClipResizeManipulator.cs
│   └── TrackReorderManipulator.cs
└── Inspectors/
    ├── ActionAssetSequenceInspectorV2.cs
    └── ActionSequenceAssetInspectorV2.cs
```

当 Runtime 仍编译进预定义 `Assembly-CSharp` 时，不要单独给 Editor 增加 asmdef，因为 asmdef Assembly 无法依赖预定义 Assembly。Runtime/Editor Assembly 拆分应作为独立的全项目迁移处理。

## 14. 实施阶段

### Stage 0 — 冻结原型与建立基线

- 在代码注释和文档中把现有窗口标记为 Prototype。
- 增加独立 V2 菜单入口。
- 固定当前测试 Asset 与手动编辑场景。

状态：原型代码标记和 V2 预览入口已实现。

退出条件：原型仍可使用，V2 可以独立打开空壳窗口。

### Stage 1 — Identity、Document 与 Commands

- 增加稳定序列化 ID 和非破坏性升级。
- 实现 `ActionSequenceSerializedDocument`。
- 实现 Command Result、Undo Group、Lock 检查与 Validation。
- 为每个结构命令和 ID Migration 添加 EditMode 测试。

状态：Stable Identity、Serialized Document、Type Registry、只读 Validator、Command/Result/ChangeSet，以及非破坏性 Normalize 语义已实现。由于当前项目已被另一个 Unity 实例打开，batchmode 验证暂未完成。

退出条件：不创建 EditorWindow 也能测试所有数据修改。

### Stage 2 — UI Toolkit 外壳与绘制基础

- 增加 UXML/USS 外壳。
- 实现 Timeline Transform、Ruler、Grid、滚动同步、Zoom、Pan 和 Fit。
- 使用稳定 ID 只读绘制现有 Track/Clip。

状态：V2 窗口外壳、Timeline Transform、只读 Track/Clip 绘制、Zoom/Pan/Fit、滚动同步、状态摘要与聚焦 EditMode 测试已实现。完整 Unity 验证待执行。

退出条件：不同窗口尺寸和 Zoom 下都能正确显示已有 Asset，并且不进行整树重建。

### Stage 3 — Selection 与 Inspector

- 实现稳定 ID Selection Bridge。
- 实现 V2 UI Toolkit Inspector。
- 加入即时序列化变更通知。
- 实现 Add Track/Add Clip、右键菜单和 Lock/Mute/Collapse。

状态：稳定 ID Selection、V2 窗口本地选中高亮、Add Track/Add Clip/右键菜单编辑、Lock/Mute/Collapse、UI Toolkit Inspector 模式、通过 Command 修改 Timing/Track 字段，以及即时 Content Change 通知已实现。由于当前项目已被另一个 Unity 实例打开，batchmode 验证暂未完成。

退出条件：完整的非拖拽编辑闭环、Inspector 同步和 Undo/Redo 都可用。

### Stage 4 — Timing 交互

- 实现 Playhead Scrub。
- 实现 Clip Move/Resize 临时预览与单 Command Commit。
- 实现工作区自动扩展与鼠标中心缩放。
- 增加快捷键。

状态：编辑器播放头状态、Play/Pause/Stop、Ruler Scrub、同 Track Clip Move/Resize 预览、单次 `SetClipTiming` Commit、Frame 快捷键，以及聚焦 Timing 计算测试已实现。当前环境 batchmode licensing 不可用，因此完整 Unity EditMode 验证暂未完成。

退出条件：Pointer Capture、Escape Cancel、Frame Snap、Lock 保护和 Undo Group 通过手动与针对性测试。

### Stage 5 — Validation 与打磨

- 增加行内 Issue Badge、状态摘要和显式 Repair Command。
- 增加 Missing Type/Error Placeholder。
- 验证深浅主题、窄窗口、高 DPI 与 Domain Reload。
- 分析 Repaint 与结构 Reconcile 性能。

状态：基于 Validator 的展示层、行内 Issue Badge、Issues 面板、显式 Repair Command 路由，以及 Missing managed-reference 类型的保留显示已实现。当前 shell 无法启动 Unity Editor，因此完整 Unity EditMode 验证暂未完成。

退出条件：不存在静默数据修改，也不存在依赖 Hover 的刷新。

### Stage 6 — 替换原型

- 执行完整验收门槛。
- 把 Double Click/Open Helper 切换到 V2。
- 暂时把原型保留在诊断菜单中。
- 经过稳定期并获得明确确认后再删除原型。

状态：正常编辑入口切换已实现。Sequence backend 的 `ActionAsset` 和独立 `ActionSequenceAsset` 现在通过双击和 Inspector 打开 V2 编辑器。LegacyTimeline 路由仍进入 Unity Timeline。Prototype 保留在 `Tools/Combat/Diagnostics/Action Sequence Prototype` 下。

退出条件：V2 成为唯一正常编辑入口，LegacyTimeline 仍然打开 Unity Timeline。

### Stage 7 — Runtime 接入

- 将 `ActionPlayer` 拆成 backend-neutral 协调器，以及 Timeline / Sequence 两种播放会话。
- 让 Sequence backend 的 `ActionAsset` 通过 `ActionSequenceRuntime` 播放。
- 保持 LegacyTimeline 行为、ActionStateManager 仲裁、速度 modifier、CancelWindow、显式停止、Disable 清理、Loop 和 ActionPlayer public API 不变。
- 为非法 Sequence 数据增加只读运行时诊断，不修改用户 Asset。

状态：已实现 ActionPlayer 会话路由、Timeline 兼容边界、内嵌 SequenceData 播放、Begin 时立即执行 frame 0、Update 驱动固定帧推进、速度与暂停、确定性 Stop/Disable 清理、Loop，以及聚合的 Sequence runtime warning。

退出条件：Sequence Action 可以从现有 ActionStateManager 路径运行；编辑器播放头预览仍不会执行 runtime clip。

## 15. 验证策略

### 15.1 EditMode 测试

- 缺失/重复 ID 的升级和修复。
- Reorder 与 Undo/Redo 前后 ID 稳定。
- Track 创建类型过滤。
- 每种具体 Track 的 Add Clip 合法性。
- 所有修改路径都无法绕过 Locked Track。
- 非空 Track 删除命令行为。
- 同 Phase 重排与跨 Phase 拒绝。
- Clip Move/Resize 帧不变量。
- Fixed/Auto Duration 计算。
- Validation 不修改源数据。
- 现有确定性 Runtime 排序与 Muted Track 行为。
- ActionPlayer Sequence 接入、frame 0 执行、完成延迟收口、停止清理和运行时诊断。
- 多个 Runtime/Editor Document 实例不共享状态。

### 15.2 Editor 手动验证矩阵

- Sequence backend 的 `ActionAsset` 和独立 `ActionSequenceAsset`。
- Empty Sequence、单 Track、多个同类 Track、零 Track 和旧数据迁移 Asset。
- Track 新增/删除/排序/重命名/Mute/Lock/Collapse。
- 所有 Clip 类型的新增/选择/删除/移动/缩放。
- Inspector 编辑立即更新 Label 与 Geometry，无需 Hover 窗口。
- 每种结构和时间操作后的 Undo/Redo。
- 窗口打开时脚本重编译/Domain Reload。
- 保存、重开 Unity，检查序列化数据和 Selection Fallback。
- 最小/最大 Zoom 下的横向 Zoom/Pan/Fit。
- 纵向滚动时 Header 与 Lane 始终对齐。
- Fixed Duration 边界与 Auto Workspace 扩展。
- Missing Type 和非法 Track 数据仍然可见、可恢复。
- LegacyTimeline 双击仍然打开 Unity Timeline。

### 15.3 性能检查

使用有代表性的压力 Asset，不以虚构的数千对象为目标：

- 30 Tracks / 300 Clips。
- 连续 Pan/Zoom 不产生明显 Layout Spike。
- Clip Drag 只更新预览 Clip 和 Canvas Overlay。
- Inspector 输入不会重建所有 Row。
- Idle Window 不持续进行 Full Refresh；只有预览播放时使用 Editor Update。

## 16. 替换验收门槛

只有全部满足以下条件，Stage 6 切换才算通过：

- Unity 2022.3.62f3 编译无错误。
- Stage 1 的 Command/Identity EditMode 测试通过。
- 两种 Target Asset 都通过第 3.1 节完整编辑闭环。
- 所有持久化操作都支持 Undo/Redo。
- Track/Clip Selection 在 Reorder 后仍保持，并能在 Undo/Redo 后正确解析。
- Inspector 修改会立刻重绘 Timeline。
- Window、右键菜单、Shortcut 和 Inspector 都不能绕过 Lock。
- 删除至零 Track 后仍保持零 Track。
- Validation 和打开窗口绝不静默删除用户数据。
- LegacyTimeline 路由不变。
- 在团队明确同意移除前，原型始终可以恢复使用。

## 17. 未来功能进入 V2 的规则

每个新功能在实施前必须回答：

1. 它属于 Asset Domain Data，还是每窗口 Editor State？
2. 哪个 Command 负责它的持久化修改？
3. 它的 Stable Identity 是什么？
4. 它发布哪一种 Invalidation？
5. Undo/Redo 语义是什么？
6. Lock 如何影响它？
7. 选中它时 Inspector 显示什么？
8. 它是否有 Runtime 语义？如果有，是什么？
9. 哪个针对性测试能够证明它正确？

这些答案不完整，功能就还没有准备好进入 V2。

## 18. Unity 官方参考

本架构以 Unity 2022.3 官方资料为平台基线：

- [创建自定义 Editor Window](https://docs.unity3d.com/cn/2022.3/Manual/UIE-HowTo-CreateEditorWindow.html)
- [Editor UI 支持](https://docs.unity3d.com/cn/2022.3/Manual/UIE-support-for-editor-ui.html)
- [SerializedObject 数据绑定](https://docs.unity3d.com/cn/2022.3/Manual/UIE-Binding.html)
- [创建自定义 Inspector](https://docs.unity3d.com/cn/2022.3/Manual/UIE-HowTo-CreateCustomInspector.html)
- [生成 2D Visual Content](https://docs.unity3d.com/2022.3/Documentation/Manual/UIE-generate-2d-visual-content.html)
- [Undo API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Undo.html)
- [Shortcut Manager](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ShortcutManagement.ShortcutManager.html)
- [Unity 2022.3 的 Timeline Package 兼容信息](https://docs.unity3d.com/cn/2022.3/Manual/com.unity.timeline.html)
- [Timeline Inspector 的选择语言](https://docs.unity3d.com/Packages/com.unity.timeline@1.5/manual/insp_about.html)
- [Timeline Track Lock 语言](https://docs.unity3d.com/cn/2019.1/Manual/TimelineLockingTracks.html)
