# CombatSample Action V1 — Stage 5R.0 Interactive Visual Shell Proof 实施计划

> 水平导航勘误（2026-09-05）：本阶段记录的 Ctrl/Cmd + Wheel 与原生水平 ScrollView 合同已由 Stage 5R.4-Z 的直接滚轮缩放、Time Viewport 和 Time Range Navigator 取代；其余 5R.0 验收结论不变。

> 状态：Complete；真实 Unity 窗口人工验收通过
> 日期：2026-09-02
> 上位权威：`CombatSample_Action_V1_Stage_5_Editor_Redesign_Draft_2026-09-01_zh-CN.md`（Frozen）
> 范围：真实 Unity UI Toolkit 可交互只读外壳；不提供正式 Authoring 写入

## 1. Summary

5R.0 的唯一目标，是在真实 Unity 2022.3 中证明新的 Stage 5 信息架构、视觉密度、滚动模型和基础交互能够成立。

它使用最终会保留的三个窗口、Shared Context、只读 Document 与 UI Toolkit 组件，不制作一次性 HTML、截图专用窗口或第二份 Timeline 数据。窗口可以读取真实 `ActionAsset.Timeline`，但不得通过 5R.0 UI 修改资产。

5R.0 通过后才允许进入 5R.1。未通过时只调整外壳，不继续堆叠 Add、Move、Undo、Clipboard 或 Preview Gameplay 功能。

## 2. 冻结合同

本计划必须遵守以下已确认决策：

- 默认只打开 `Action Timeline`；Details 与 Preview 按需打开并可自由 Dock。
- 一条 Animation Lane 为 36 px；每条 GameplayLane 固定 32 px，不因 overlap 增高。
- Gameplay overlap 合法，使用中性 Marker/Picker；Animation overlap 继续显示为非法。
- Identity issue 进入全 Timeline 只读安全模式，不使用数组索引猜测 Selection。
- GameplayLane 是无 TrackKind 的组织容器，可混放七种 Item。
- Issues Drawer 默认收起，普通问题不占据主工作区。
- 5R.0 不执行 Repair、Add、Delete、Move、Resize、Trim、Paste、Duplicate 或任何资产写入。
- 不修改 Runtime public API、Stage 0–4 Scheduler、Timeline 数据结构、Legacy 路由或正式资产。
- 不引入第三方 Timeline package，也不复制未授权/GPL 候选源码。

## 3. 现有实现审查

### 3.1 可保留基础

#### `ActionV1EditorCore.cs`

- `ActionV1EditorChangeFlags` 的 Context/Selection/Frame/Structure/Timing/Content/Validation/Preview 分类方向正确。
- `ActionV1SelectionKind`、`ActionV1SelectionValue` 与 Shared Context 的稳定 ID 选择方向正确。
- `ActionV1EditorContext` 当前只持有 Action、Frame、Selection、PreviewCharacter 和 Preview inputs，没有写入 ActionAsset。
- `ActionV1EditorDocument` 已是从 `ActionAsset.Timeline` 重建的 Editor 内存快照，没有持久化第二份数据。
- `ActionV1EditorCommands` 与 Clipboard 保留在源码中供后续 5R.4 审查，不在 5R.0 调用或顺手重写。

#### `ActionV1PreviewWindow.cs`

- `PreviewRenderUtility`、source clone、baseline capture/restore 和 `CleanupPreview` 可以作为后续 Preview 的基础。
- 现有采样目标是 clone，而不是源 Prefab/场景对象；5R.0 保留这一安全边界。
- 5R.0 不扩展 RootMotion/SelfRotation/HitBox evaluator，只重建窗口外壳、空状态和诊断层次。

#### `ActionAuthoringValidation.cs`

- `ActionAuthoringValidator` 继续是唯一数据合法性权威。
- `ActionAuthoringIdentity` 继续提供正式 Repair 实现，但 5R.0 UI 不调用 Repair。

### 3.2 必须替换的实现

#### `ActionV1TimelineWindow.cs`

当前文件将 Toolbar、Ruler、Header、双向 Scroll、Lane、Item、Issues、Drag、Marquee 和 Playback 集中在一个类中，并为大量状态变化整体 `Clear/Rebuild`。Header 与 Ruler 也处于同一个双向 ScrollView，直接造成截图中的上下文丢失和布局失控。

5R.0 将替换它的视觉树和基础导航事件：

- 四象限固定布局取代单一双向 ScrollView。
- 持久 Visual Tree 和局部 refresh 取代 Frame/Selection/Scroll 时整体 rebuild。
- 统一 USS class 取代大部分 inline style。
- 只读 selection/hover/marquee/overlap picker 取代现有直接进入写命令的 drag 路径。
- `BuildIssues` 的默认展开 HelpBox 列表被 compact health + capped drawer 取代。

#### `ActionV1DetailsWindow.cs`

当前 Details 无最大内容宽度，字段横跨窗口；Selection issue 以全量 HelpBox 展开；FocusOut 会安排整个窗口 Rebuild，导致 Foldout、Scroll 和输入上下文重置。

5R.0 使用只读 presentation field 验证分区和响应式布局，不绑定可写 SerializedProperty。5R.5 再恢复正式 leaf editing。

### 3.3 5R.0 暂时不可达但不删除

- `ActionV1AnimationPickerWindow`
- Timeline 的 Add Lane、Add Item、Context Menu 写入口
- Move/Resize/Trim drag commit
- Delete、Copy/Paste/Duplicate
- Inspector/Editor 中已有的 Repair 核心实现

这些代码不作为 5R.0 验收证据。后续切片会根据 Frozen 合同逐项审查，而不是默认沿用现状。

## 4. 目标文件结构

### 4.1 继续修改的现有文件

| 文件 | 5R.0 职责 |
| --- | --- |
| `ActionV1EditorCore.cs` | 扩充只读 Document、Readiness、Issue presentation index 与 overlap 输入；保持 Commands 不可达。 |
| `ActionV1TimelineWindow.cs` | 只保留窗口生命周期、菜单入口、window-local serialized state 和根视图组装。 |
| `ActionV1DetailsWindow.cs` | 改为响应式、只读 Details shell；只显示 Primary Selection 问题。 |
| `ActionV1PreviewWindow.cs` | 使用统一 Chrome/Theme，提供明确空状态与 compact diagnostics；保留 clone cleanup。 |

### 4.2 新增 Editor-only 文件

| 文件 | 职责 |
| --- | --- |
| `ActionV1EditorTheme.cs` | Unity 2022.3 可用的颜色、尺寸、图标和 class-name 常量；不保存状态。 |
| `ActionV1EditorStyles.uss` | Context、Transport、Banner、四象限 Timeline、Lane、Item、Badge、Drawer、Details、Preview 的稳定视觉规则。 |
| `ActionV1EditorChrome.cs` | 三窗口共享 Context Bar、Health、Empty State、Status 和 compact button builders。 |
| `ActionV1TimelineView.cs` | 固定 Ruler/Header/Content 架构、lane/item presentation、scroll sync 和局部 refresh。 |
| `ActionV1TimelineGeometry.cs` | 纯 Editor 计算：frame↔pixel、authoring horizon、tick step、visible interval、overlap sweep 与命中区域。 |
| `ActionV1OverlapPicker.cs` | 中性 overlap popup，只改变 Shared Selection，不修改资产。 |

若实施时某个新文件不足以形成独立职责，可合并，但不得重新把所有 Timeline UI、数据计算与事件处理堆回 Window 类。

## 5. Core Read Model

### 5.1 Readiness

新增 Editor-only readiness snapshot：

```text
NoAction
IdentityBlocked
EditableWithIssues
Ready
```

它只根据 CurrentAction 与 `ActionAuthoringValidator` 结果推导：

- Missing/Malformed/Duplicate EditorId 数量大于 0 => `IdentityBlocked`。
- 普通 Timing/Config/Reference 问题 => `EditableWithIssues`。
- 5R.0 无论 Readiness 为何都保持资产只读；Readiness 只影响表现和 Selection 安全。

### 5.2 Document Entry

现有 `ActionV1DocumentEntry` 扩充为可显示非法数据的只读条目：

```text
SelectionKind
EditorId
AuthoringPath
DisplayKey
LaneId / LaneIndex / ItemIndex
RawStart / RawEndExclusive
SafeDisplayStart / SafeDisplayWidth
Muted
IdentityState
Source
```

规则：

- `AuthoringPath` 例如 `AnimationSegments[0]`、`GameplayLanes[1].Items[3]`，只用于诊断和本次 Document 生命周期内的显示定位。
- `DisplayKey` 可为当前 Document 生成的非持久 key，允许没有 ID 的对象被绘制；它不得进入 Shared Selection、Undo、Copy 或资产数据。
- `ByEditorId` 只收录合法且唯一的 EditorId；不再让 duplicate ID 的第一个对象静默成为权威。
- Null entry、负 Frame、Duration <= 0 和 invalid animation duration 使用安全 placeholder geometry，但绝不回写资产。

### 5.3 Validation Presentation Index

`ActionAuthoringValidator` 继续决定问题是否存在。Editor 只建立 presentation index：

```text
GlobalIssues
IdentityIssues
IssuesByEditorId
IssuesByAuthoringPath
BlockingIdentityCount
```

必要时可以为 `ActionAuthoringValidationIssue` 增加 Editor-only `AuthoringPath` 定位信息，但不得复制或改变 Validator 的合法性判断。

### 5.4 Overlap Index

`ActionV1TimelineGeometry` 对每条 GameplayLane 的真实 `[Start, EndExclusive)` 做 sweep：

- Point 视为 `[Frame, Frame + 1)`。
- 输出连续 overlap region、涉及的 DisplayKey 列表和稳定 ItemIndex 顺序。
- overlap 不生成 Validation Issue。
- Animation 使用相同定位结构，但展示为已有 Validator error。

## 6. Timeline Visual Architecture

### 6.1 固定层次

窗口根结构固定为：

```text
Context Bar        28 px
Transport Bar      30 px
Identity Banner    conditional
Timeline Main      flex-grow
Issues Drawer      collapsed / capped at 30%
Status Bar         22 px
```

Context/Transport 在窄窗口中仍保持单行；次要文字隐藏为 tooltip，不换成第二行。

### 6.2 四象限 Timeline

```text
┌──────────────────────┬─────────────────────────────┐
│ fixed corner         │ clipped ruler viewport      │
├──────────────────────┼─────────────────────────────┤
│ clipped header view  │ scrollable content viewport │
└──────────────────────┴─────────────────────────────┘
```

- Corner 固定。
- Header 只跟随 content 的垂直 offset。
- Ruler 只跟随 content 的水平 offset。
- 只有 Content ScrollView 拥有真实双向滚动条；Header/Ruler 使用 clipped viewport 和 transform 同步，避免互相回写抖动。
- Header 默认 220 px，可在 180–320 px 间 resize；5R.0 resize 只改变 window-local state。
- Animation Lane 36 px，Gameplay Lane 32 px，Ruler 24 px。

### 6.3 Authoring Horizon 与 Grid

- Horizon 为 `max(60, DurationFrames + 30)`。
- 不显示负 Frame。
- Major/minor tick 根据 pixels-per-frame 选择，标签不得互相覆盖。
- 垂直 Frame grid 由 `generateVisualContent` 或等价的单一绘制层完成，不为每条线创建 VisualElement。
- Content block 使用可交互的绝对定位 VisualElement；Selection border 不改变布局尺寸。

### 6.4 Item Presentation

- Animation 使用蓝色 range block 和清晰左右 trim affordance，但 5R.0 handle 不提交修改。
- Point 使用语义为一 Frame 的最小可点击 marker。
- Range 使用真实 `[Start, EndExclusive)` 宽度。
- 七种 Item 使用受控的类型色与图标，不使用任意彩虹色；文字不足时截断并提供 tooltip。
- Muted 使用低饱和度与 glyph，仍可查看。
- 普通 issue 使用局部 badge；`Needs setup` 不展开 Drawer。
- Primary/hover block 临时置顶，不保存 z-order。

## 7. 5R.0 允许的交互

### 7.1 Context 与窗口

- Action ObjectField 可以更换 CurrentAction；这只修改 Shared Context。
- Timeline 按钮按需打开 Details/Preview；默认入口不自动打开辅助窗口。
- 三窗口同步 Action、CurrentFrame、Primary Selection 与 PreviewCharacter。

### 7.2 Navigation

- Ruler Pointer Capture scrub。
- First/Prev/Play-Pause/Next/Last 与 Preview Loop，只推进 Editor CurrentFrame。
- Middle Mouse Pan。
- Ctrl/Cmd + Wheel mouse-anchored Zoom。
- Fit All；`F` 在有内容选择时 Frame Selection，否则 Fit All。
- Content horizontal scroll 同步 Ruler；vertical scroll 同步 Header。

### 7.3 Selection

- Single、Ctrl/Cmd Toggle、Shift Add。
- 空白点击回到 Action Selection。
- Marquee 替换、Shift + Marquee 追加。
- IdentityBlocked 时，合法唯一 ID 的内容仍可用于只读导航；无 ID、malformed 或 duplicate 对象只通过 AuthoringPath/Issue/overlap 定位，不进入稳定 Selection。
- Text/Object field 获得焦点时 Timeline 快捷键不触发。

### 7.4 Overlap Picker

- 中性 marker 显示同时命中的 Item 数量。
- 点击后列出类型、Lane 与 `[Start, End)`。
- 选择合法 ID 条目只同步 Primary Selection、Details 和临时 z-order。
- 非法 identity 条目可高亮定位，但不伪造 Selection ID。
- Picker 顺序不表达 Priority 或 Runtime winner。

### 7.5 明确禁用

所有可能写资产的按钮、Context Menu、drag handle 与快捷键在 5R.0 不创建或显示为明确 disabled proof state。点击 disabled affordance 不调用 `ActionV1EditorCommands`。

Identity Banner 可以展示最终 Repair 按钮的位置和数量，但按钮在 5R.0 disabled，并标注 `Available after shell acceptance`。

## 8. Details Shell

- 根部为 Context Bar + ScrollView。
- 内容列最大宽度 720 px；宽窗口居中。
- 小于约 520 px 时切换窄布局，Label 与 Value 上下排列。
- Header 显示 `Primary Selection · N selected`。
- Action、Segment、Lane 与七种 Item 使用 Frozen 设计中的语义分区。
- 5R.0 使用只读值或 disabled native field 证明真实高度和换行，不绑定写入回调。
- 只显示 Primary Selection 的问题；全局问题留在 Timeline Drawer。
- IdentityBlocked 显示统一阻塞卡片，不构造数组索引选择。
- Frame、Selection 或普通 Hover 不重建整个 Details tree；Selection kind 变化才重建页面骨架。

## 9. Preview Shell

- 使用统一 Context/Character Bar、Viewport、Contextual Input Overlay 与 compact diagnostics。
- 没有 Action 时显示 `Choose an ActionAsset`；没有 Character 时显示 `Choose a Preview Character`，均为居中明确空状态。
- PreviewCharacter ObjectField 只修改 Shared Context；源对象仍不被修改。
- 若保留现有 clone viewport，必须继续只操作 `PreviewRenderUtility` 中的 clone，并在 Window disable 时释放。
- 5R.0 不以 RootMotion、SelfRotation 或 HitBox evaluator 完整性作为验收条件，也不借机修改 Stage 4 Runtime sampling。
- Diagnostics 默认一至三行摘要，详细内容使用可展开区域，不持续压缩 Viewport。

## 10. Refresh 与性能合同

- CreateGUI 只建立根 Visual Tree。
- Frame change 只更新 playhead、frame label、active/hold presentation 和 Preview dirty state。
- Selection change 只更新 block/header selection class、status、Details primary header。
- Scroll/Zoom 只更新 geometry/transform，不重新运行 Validator。
- Structure/Timing/Content/Validation change 才重建 Document 或对应 lane tree。
- Validator 每次明确 document refresh 最多运行一次，不在每次 repaint/geometry event 中重复遍历。
- GeometryChanged 使用去抖或仅处理真实宽度断点变化，避免递归 rebuild。

## 11. Implementation Order

### 5R.0-A — Read Model 与只读闸门

- 增加 Readiness、identity state、非法 entry placeholder、issue index 和 overlap index。
- Window 不再调用 Commands、Repair 或 SerializedProperty 写入口。
- 保持现有 Runtime、资产和 Commands 源码不变。

### 5R.0-B — Shared Theme 与 Chrome

- 建立 USS、theme constants、Context Bar、Transport、Banner、Drawer、Status、Empty State。
- Timeline/Details/Preview 使用相同字体层次、间距和状态色。

### 5R.0-C — Timeline 四象限与 Navigation

- 固定 Corner/Header/Ruler、真实 Content Scroll、scroll sync、grid、playhead、pan/zoom/fit/scrub。
- 在空 Timeline 与长 Timeline 上先验收 geometry。

### 5R.0-D — Lane/Item/Selection/Overlap

- 32/36 px lanes、正常/非法 placeholders、single/multi/marquee、hover z-order、neutral overlap picker。
- IdentityBlocked 下验证可见但不可误选。

### 5R.0-E — Details 与 Preview Shell

- 响应式只读 Details、Primary-only issues。
- Preview 空状态、Character bar、compact diagnostics 和现有 clone cleanup 边界。

### 5R.0-F — 真实 Unity 验收

- 编译、Console、三尺寸截图和交互矩阵。
- 只调整 5R.0 外壳直到项目负责人接受；不提前开启 Authoring。

## 12. Verification

### 12.1 静态与编译

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`
- `git diff --check`
- Unity Refresh 后 Console 0 compile error。
- 确认没有修改 `ActionTimelineData`、GameplayItem、Runtime、Package manifest、场景、Prefab 或用户 ActionAsset。
- Unity Test Runner 不作为 5R.0 验收门槛。

### 12.2 真实窗口尺寸

必须由 Unity 中的真实窗口验证：

| 尺寸 | 验收重点 |
| --- | --- |
| 2560×1440 | 内容列不过度拉宽；Timeline 不显空旷；辅助信息不横跨整屏。 |
| 1920×1080 | Timeline viewport 占据主要高度；Issues 收起时不压缩 Lane。 |
| 窄 Dock | Context/Transport 不换成多行；Details 字段上下布局；Viewport/Scroll 仍可用。 |

### 12.3 数据状态

- NoAction、空 Timeline、Ready、EditableWithIssues、IdentityBlocked。
- 缺失 AnimationAsset、非法 SourceRange/PlayRate、负 Frame、Range Duration <= 0。
- missing/malformed/duplicate ID 内容可见，但不发生数组索引误选。
- 同 Lane 2/3/4 个 Gameplay overlap 保持 32 px，Picker 能到达每个合法条目。
- 非法 Animation overlap 保持单行并显示 error picker。
- Issues Drawer 默认收起，展开不超过窗口 30% 且内部独立滚动。

### 12.4 交互

- Scrub、First/Prev/Play/Next/Last/Loop。
- Horizontal/Vertical scroll sync。
- Middle Pan、mouse-anchored Zoom、Fit、Frame Selection。
- Single/Ctrl/Cmd/Shift/Marquee 与空白选择。
- Details/Preview 按需打开并共享 Context。
- 所有写操作不可达；操作前后 ActionAsset dirty state 和序列化内容不变。

## 13. Exit Criteria

5R.0 只有同时满足以下条件才完成：

- Frozen 六层 Timeline 信息架构在真实 Unity 中成立。
- 32/36 px 固定 Lane、Ruler/Header scroll、Validation compact presentation 和 overlap picker 可操作。
- 三个窗口在三种尺寸中均没有截图所示的全屏铺平、主工作区被 Issues 占据或元素互相遮挡问题。
- IdentityBlocked 不允许不可靠 Selection，也不静默 Repair。
- 5R.0 UI 没有写入任何 ActionAsset。
- 编译与 `git diff --check` 通过，Unity Console 无 error。
- 项目负责人查看真实 Unity 结果并明确接受 5R.0。

未满足任一项时继续停留在 5R.0，不开始 5R.1。

## 14. Implementation Evidence（2026-09-02）

- 已建立 `NoAction / IdentityBlocked / EditableWithIssues / Ready` 只读状态、非法数据 placeholder、Validation presentation index 与 Gameplay/Animation overlap index。
- Timeline 已替换为固定 Corner/Header/Ruler 与独立双向 Content Scroll 的四象限外壳；Animation 36 px、GameplayLane 32 px。
- Timeline 支持只读 Scrub、Playback navigation、Pan、mouse-anchored Zoom、Fit、Frame Selection、稳定 ID 多选、Marquee 与 overlap picker。
- Details 已改为 Primary-only、720 px 内容列、窄窗口响应式只读页面；Preview 已使用统一 Chrome、空状态、紧凑诊断并保留 isolated clone cleanup。
- Timeline、Details、Preview 中不存在 `ActionV1EditorCommands`、Repair、SerializedProperty 写入、`EditorUtility.SetDirty` 或 `Undo.RecordObject` 调用。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`：成功，0 warning，0 error。
- 5R.0 修改文件范围内 `git diff --check`：通过。全仓检查仍会报告用户现有 `New Sequence Action.asset` 的尾随空格，本阶段未修改该资产。
- 项目负责人于 2026-09-02 完成真实 Unity 观察并确认 5R.0 基本无问题、允许继续；5R.0 据此关闭，下一阶段进入 5R.1 计划，不提前开放完整 Authoring。
