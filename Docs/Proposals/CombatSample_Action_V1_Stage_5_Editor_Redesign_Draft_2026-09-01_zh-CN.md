# CombatSample Action V1 — Stage 5 Editor Redesign（Frozen）

> 状态：**Frozen；Stage 5 remediation 实施权威**
> 日期：2026-09-02
> 范围：只重新设计 Action V1 Authoring Editor；不迁移资产、不切换 Runtime、不修改 Stage 0–4 合同

## 2026-09-08 Preview 技术路线暂停

Timeline 与 Details 的冻结设计和已接受实现继续有效。两轮 Preview 实现均已从当前代码路径撤下，窗口只保留无运行逻辑的占位入口。

Preview 仍是产品目标，但本文不再指定其当前技术路线。SceneView / Animancer 方案、早期 PreviewRenderUtility 方案、审查和失败证据均已移入 [Stage 5R 历史记录](../Archive/ActionV1/Stage5R/)，不得据此自动继续 P2 或 5R.7。

以下 Preview 章节只描述目标能力；重新实施前必须形成新的、明确批准的技术计划。当前实现边界见 [Preview 前编辑器检查点](../Current/CombatSample_Action_V1_Editor_PrePreview_Checkpoint_2026-09-08_zh-CN.md)。

## 1. 目标

Stage 5 的目标不是“让所有命令都有一个入口”，而是交付一个可以稳定完成正式 ActionAsset Authoring 的工作流：

```text
Action Timeline（结构与时间）
        ↓ shared selection / current frame
Action Details（当前选择的属性）
        ↓ shared preview inputs
Action Preview（当前帧视觉结果）
```

三个窗口直接编辑同一份 `ActionAsset.Timeline`。不存在第二份 Timeline、Save/Compile roundtrip、隐藏自动修复或 Runtime side effect。

## 2. 现有实现为何不可验收

当前 Stage 5 实现存在结构性问题，不能通过增加边距或更换颜色解决：

1. 缺失或非法 EditorId 会禁用 Segment、Lane 和 Item，但 Repair 入口被放在完整错误列表末尾，形成没有解释的编辑锁死。
2. Timeline 与 Details 默认展开全部 Validation HelpBox，错误列表取代主要工作区。
3. Track Header、Ruler 和 Timeline Content 位于同一个双向 ScrollView，滚动后失去上下文。
4. 同 Lane 合法重叠的 GameplayItem 绘制在完全相同的位置，后绘制对象遮挡前一个对象。
5. Timeline 缺少帧网格、明确创建入口、空状态、稳定焦点、完整 Pan 和可靠拖拽反馈。
6. Details 是无宽度约束、无语义分组的 SerializedProperty 平铺，并在 FocusOut 后整体重建。
7. Preview 缺少明确的 Action/Frame 状态、结构化空状态和上下文相关的 Preview Input 呈现。
8. 三窗口虽然共享数据，却没有主工作区与辅助窗口的清晰关系。

因此 Stage 5 当前状态应视为 **Remediation Required / Not Accepted**。

## 3. 设计原则

### 3.1 Timeline 是主工作区

- Timeline 负责创建、结构、Timing、Selection 和导航。
- Details 不承担 Timeline 导航，只编辑 Primary Selection。
- Preview 不承担 Gameplay Authoring，只显示 CurrentFrame 视觉结果。

### 3.2 错误必须转化为下一步操作

- Identity 问题是编辑器定位安全问题，进入显式只读安全模式。
- 普通数据或 Config 问题不锁编辑；用户必须能够选择并修复它们。
- Validation 默认是摘要和 Badge，不得占据主要内容空间。

### 3.3 Authoring 数据与视觉布局分离

- Overlap Marker/Picker、Scroll、Zoom、Foldout、Issue Drawer 状态只属于 Editor 内存。
- 每条 GameplayLane 始终只占一个固定高度的 Timeline row，不因 Item overlap 自动扩高。
- Item 的 presentation z-order 不保存、不参与 Runtime、不表达 Priority 或 ExecutionOrder。
- 非法数据的展示可以使用安全占位尺寸，但不得静默回写或修正资产。

### 3.4 每次操作可预测且可撤销

- Drag 永远基于 Original Snapshot + Total Delta。
- 一次 manipulation 只有一个 Undo transaction。
- Escape 恢复原始状态并释放 Pointer Capture，不产生 Undo。
- 整组操作要么全部成功，要么全部拒绝。

### 3.5 布局密度基线

以下尺寸是 Unity UI Toolkit 的逻辑像素基线，用来约束信息层级；允许随 Editor DPI 缩放，但不允许由内容数量任意撑开：

| 区域 | 默认尺寸 | 自适应规则 |
| --- | ---: | --- |
| Timeline Context Bar | 28 px 高 | 单行；宽度不足时次要文字折叠为图标或 tooltip，不换成第二行 |
| Transport Bar | 30 px 高 | Transport 与 View 两组保持分隔 |
| Ruler | 24 px 高 | 固定在 Timeline viewport 顶部 |
| Track Header | 220 px 宽 | 用户可在 180–320 px 内拖动分隔线；不随 Item 名称自动变宽 |
| Animation Lane | 36 px 高 | 始终一行 |
| Gameplay Lane | 32 px 高 | 每条 Lane 始终一行；overlap 不扩高 |
| Item block | 22 px 高 | 在 Lane 内垂直居中；选中描边不得改变布局尺寸 |
| Status Bar | 22 px 高 | 固定在窗口底部 |
| Issues Drawer | 默认收起；展开最多占窗口 30% | 内部滚动，不继续压缩 Timeline |
| Details content column | 最大 720 px 宽 | 宽窗口居中；窄窗口改为 label/field 上下排列 |

Timeline 在正常 1080p 工作区中必须优先保留 Lane viewport；Context、Transport、Issues 和 Status 的组合不得把主工作区压缩成只剩一两行。

## 4. Window 与 Shared Context

### 4.1 Shared Context

Shared Context 只保存 Editor session 状态：

```text
CurrentAction
CurrentFrame
PrimarySelection
SelectedEditorIds
PreviewCharacter
PreviewTarget
PreviewDirection
PreviewLoop
```

规则：

- 不写入 ActionAsset、EditorPrefs 或 Runtime 数据。
- Domain Reload 后恢复；Unity Editor 完整重启后允许清空。
- Clipboard 为 session-only，Domain Reload 后允许清空。
- Action 切换时 CurrentFrame 回到 0，Selection 回到 Action。
- Selection 在 Document rebuild 后按 EditorId 验证；目标消失时回到 Action，不猜测替代对象。

### 4.2 Window-local 状态

下列状态不进入 Shared Context：

```text
Timeline Scroll / Zoom / expanded Issue Drawer
Details Foldouts / Scroll
Preview Camera Orbit / Distance
```

它们可以通过 EditorWindow 序列化跨 Domain Reload 恢复，但不得写入项目资产或 EditorPrefs。

### 4.3 默认入口

- `Open Action V1 Editor` 打开并聚焦 `Action Timeline`。
- 默认不自动打开 `Action Details` 或 `Action Preview`，Timeline 始终是主工作区。
- Timeline Context Bar 提供 `Details` 与 `Preview` 窗口入口。
- 双击 Timeline 内容可按需打开 Details 并定位；普通单击只同步 Selection，不抢走 Timeline 焦点。
- 三个窗口继续保留独立菜单入口并允许用户自由 Dock。
- 不调用 Unity 非公开 Docking API，不强制破坏用户已有 Layout。

### 4.4 ActionAsset Inspector

Inspector 的 V1 区域只提供：

```text
Open Action V1 Editor
60 FPS / Duration summary
Validation health summary
Repair Editor IDs（仅 Identity issue 存在时）
```

- Raw `_actionTimeline` 数组不作为正常 Authoring 入口。
- Inspector 不重复完整 Issues Drawer；点击 health summary 打开 Timeline Issues。
- 不显示 Legacy Timeline、Sequence 或 Playback Backend 为 V1 Details 内容。
- Inspector 修改不会自动 Save Asset。

## 5. Editor Readiness 状态模型

| 状态 | 条件 | Timeline | Details | Preview |
| --- | --- | --- | --- | --- |
| NoAction | CurrentAction 为空 | Action 选择空态 | Action 选择空态 | Action 选择空态 |
| IdentityBlocked | missing / malformed / duplicate EditorId > 0 | 内容可见但只读；只允许 Repair、查看 Issues 和导航 | 显示阻塞原因与 Repair | 可显示可安全解析的视觉内容，同时报告身份阻塞 |
| EditableWithIssues | Identity 完整，但存在普通 Validation Issue | 全部 Authoring 操作可用 | 可编辑当前问题字段 | 尽可能求值；无法求值部分显示诊断 |
| Ready | Validator 通过 | 全部 Authoring 操作可用 | 正常编辑 | 正常预览 |

### 5.1 Identity Safe Mode

IdentityBlocked 时顶部固定显示：

```text
Editing paused — 4 Editor ID issues prevent safe selection and dragging.
[Repair 4 IDs]
```

Repair 合同：

- 这是唯一允许的自动生成 ID 操作，并且必须由用户显式触发。
- 一次修复全部 missing / malformed / duplicate ID。
- 单个 Undo transaction。
- 不改变 Timing、Config、Lane、Mute 或对象顺序。
- 修复完成后重新 Validate，不自动保存资产。
- 显示短暂结果反馈：`4 Editor IDs repaired · Ctrl+Z to undo`。

IdentityBlocked 时不允许临时数组索引选择，避免 Selection 在 reorder/reload 后指向错误对象。

## 6. Action Timeline 信息架构

窗口由六个固定层次组成：

```text
1. Context Bar       Action / 60 FPS / Duration / Health / Details / Preview
2. Transport Bar     First / Prev / Play-Pause / Next / Last / Preview Loop
3. Blocking Banner   仅 IdentityBlocked 时出现
4. Ruler + Lanes     主工作区
5. Issues Drawer     默认收起
6. Status Bar        Frame / Selection / Interaction feedback
```

Fit、Zoom 等 View 操作不得混入冻结的 Transport 按钮组；它们放在 Ruler Corner 或 Transport Bar 右侧的独立 View Group。

### 6.1 固定区域与滚动

- 左上 Corner 固定。
- 左侧 Track Header 固定，不参与横向滚动。
- 顶部 Ruler 固定，不参与纵向滚动。
- Timeline Content 横向滚动时 Ruler 同步。
- Lane Content 纵向滚动时 Track Header 同步。
- Scroll 同步必须有 reentrancy guard，避免互相回写抖动。
- Rebuild 前读取真实 Scroll Offset；View command 计算出的新 offset 不得被旧值覆盖。

### 6.2 Authoring Horizon

- CurrentFrame 范围仍为 `[0, DurationFrames - 1]`。
- Timeline 可视 authoring horizon 至少显示 60 Frame，并在 Duration 后保留 30 Frame 空间。
- 用户可以在 Duration 之后右键创建内容；创建后自动 Duration 扩展。
- Timeline 不显示负 Frame。

### 6.3 Lane Header

Animation Header：

```text
Animation | Fixed Lane | [+]
```

Gameplay Header：

```text
Mute | Lane Name | overlap count（仅存在时） | [+] | [⋮]
```

- Header `+` 在 CurrentFrame 创建内容。
- Content 右键菜单在指针 Frame 创建内容。
- Lane 名称为空时显示 presentation fallback `Unnamed Lane`，但不回写数据。
- 单击 Header 选择 Lane。
- 双击名称进入 Inline Rename；Enter/FocusOut 提交，Escape 取消。
- `⋮` 提供 Rename、Move Up、Move Down、Delete。
- 删除非空 Lane 必须确认，并明确会删除其中 N 个 Item。

### 6.4 内容视觉

AnimationSegment：

- 使用蓝色矩形；标签显示 AnimationAsset 名称。
- 左右 Handle 始终有可识别的 Trim cursor/hover state。
- 无有效 AnimationAsset 或 Duration 时显示最小宽度的错误占位块，例如 `Missing Animation Asset`，不伪造有效 Duration。

PointGameplayItem：

- 语义区间仍为 `[Frame, Frame + 1)`。
- 使用最小可点击宽度的 Point marker；视觉宽度不得改变 Frame 语义。

RangeGameplayItem：

- 使用 `[StartFrame, EndExclusive)` 宽度。
- 左右 Handle 用于单 Item Resize。
- Muted 使用低饱和度与 mute glyph，但仍保持可选择。

通用：

- Selection 使用明亮边框，不只依赖颜色。
- Issue Badge 放在条目右上角。
- Hover tooltip 显示类型、Lane、Timing 和首个 Issue。
- 最小视觉宽度只影响点击和标签，不影响 Timing 计算。

## 7. 固定单行 Lane 与 Gameplay overlap

每个 GameplayLane 始终对应一条固定高度的 Timeline row。Gameplay overlap 合法，但不得通过增加子行或扩高 Lane 来展示。Overlap 是并行内容的可达性问题，不是 Validation error。

### 7.1 单行绘制

- Point 仍视为 `[Frame, Frame + 1)`，Range 使用 `[StartFrame, EndExclusive)`。
- 所有 Item 使用相同的垂直中心线绘制。
- 重叠 Item 可以在时间区域内覆盖彼此；Primary Selection 和当前 hover Item 临时置顶。
- Presentation z-order 只用于可见性，不保存，也不表达 Runtime order、Priority 或 winner。
- Lane 高度与 overlap 数量无关，所有 GameplayLane 保持一致行高。

### 7.2 Overlap Marker / Picker

Editor 对每条 Lane 的区间执行 sweep，找出 active Item 数量大于 1 的连续重叠区域：

1. 在重叠区域上方显示中性、紧凑的堆叠 Marker，例如 `2` 或 `3`；不得使用错误红色或 warning 文案暗示 Gameplay overlap 非法。
2. Marker 不改变 Item Timing，也不占用新的 Timeline row。
3. 点击 Marker 打开 Picker，列出该区域涉及的全部 Item、类型和 `[Start, End)`。
4. 选择 Picker 条目会设置 Primary Selection、临时置顶目标，并同步 Details。
5. Picker 顺序使用稳定 authoring ItemIndex，仅用于查找；UI 明确说明它没有 Priority 含义。
6. Marquee 依据所有 Item 的真实 interval 命中，即使某个 Item 当前被另一个 Item 覆盖。
7. Lane Header 可显示中性的 `2 overlapping items` 摘要；无 overlap 时不显示额外文字。

普通点击仍可选择当前可见的 Item；完全被覆盖的 Item 必须始终可以通过 Overlap Picker、Issues Drawer 或 Details 导航到达。

### 7.3 非法 Animation overlap

Animation Lane 同样保持固定单行。若外部编辑或旧非法数据产生 overlap：

- 使用带错误颜色的 Overlap Marker/Picker 保证每个 Segment 都能被定位。
- Primary/hover Segment 临时置顶。
- Animation Lane 显示 overlap warning。
- Move / Trim / Paste 继续按 Validator 合同整体拒绝制造新 overlap。
- Picker 只解决定位问题，不使非法 overlap 合法化。

## 8. 非法 Authoring 数据的展示

| 问题 | Timeline 表现 | 允许操作 | 自动修改资产 |
| --- | --- | --- | --- |
| Missing/malformed/duplicate ID | Identity Safe Mode；对象仍可见 | Repair / 查看 Issues | 仅显式 Repair 生成 ID |
| Null Segment/Lane/Item | 对应分组显示 `Null entry #N` quarantine row | 显式 Delete，单次 Undo | 否 |
| StartFrame < 0 | Lane 左边缘显示 `← -N` off-canvas marker | 选择后在 Details 修正或 Delete | 否 |
| Range Duration <= 0 | 最小宽度错误占位块 | Details Timing / Delete | 否 |
| Missing AnimationAsset | `Missing Animation Asset` 占位块 | Details Source / Delete | 否 |
| Invalid SourceRange/PlayRate | Segment Issue Badge | Details Source / Trim（仅合法提交） | 否 |
| Animation overlap | 固定单行中的错误 Overlap Marker/Picker | Move/Trim/Delete | 否 |
| Invalid Item Config | Item Issue Badge | Details Config / Delete | 否 |
| Missing/stale RootMotionData | Item/Segment Issue Badge；Preview 跳过 RootMotion | Bake/Rebuild 仍属于 AnimationAsset Inspector；Details 可换引用 | 否 |
| Preview bone missing | Preview diagnostic；只跳过对应 Gizmo | 修改 BoneReference/角色 | 否 |

`ActionAuthoringValidator` 继续是唯一数据校验权威。Editor presentation 可以建立 Editor-only issue locator，但不得复制一套不同的合法性规则。

## 9. Selection 合同

### 9.1 单选与多选

- 单击：只选择目标，并设置为 Primary Selection。
- Ctrl/Cmd + 单击：Toggle；新增目标成为 Primary。
- Shift + 单击：Add，不做范围选择；新增目标成为 Primary。
- 空白单击：回到 Action Selection。
- Marquee：替换选择；Shift + Marquee：追加选择。
- `Ctrl/Cmd + A`：选择全部具有有效 EditorId 的 Segment 和 GameplayItem，不选择 Lane。

### 9.2 Details 与多选

- Details 永远编辑 Primary Selection。
- Header 明确显示 `Primary Selection · N selected`。
- 不提供隐式批量字段编辑。
- Group Move、Delete、Copy、Duplicate 等明确支持集合的操作仍作用于全部 Selection。

### 9.3 Focus

- 点击 Ruler、Lane Content 或 Item 后，Timeline command root 获得焦点。
- TextField、ObjectField、CurveField 等编辑控件拥有焦点时，Timeline 快捷键不触发。
- 窗口失焦时结束 hover，不自动提交正在进行的 drag；Pointer Capture lifecycle 负责完成或取消。

## 10. Interaction State Machine

同一窗口同时只允许一种状态：

```text
Idle
├─ Scrub
├─ Pan
├─ Marquee
├─ Move
├─ ResizeLeft / ResizeRight
├─ TrimLeft / TrimRight
└─ InlineRename
```

### 10.1 Scrub

- Ruler 左键 Pointer Down 进入 Scrub，并 Capture Pointer。
- Pointer Move 持续更新整数 CurrentFrame。
- Pointer Up 或 Escape 释放 Capture。
- Scrub 修改 Editor Context，不写 ActionAsset，不进入 Undo。

### 10.2 Pan / Zoom / Fit

- Middle Mouse Drag 平移 Timeline。
- Ctrl/Cmd + Wheel 以指针下的 Action Frame 为缩放锚点。
- Zoom 后锚点在 viewport 中的位置保持不变。
- `F`：有内容选择时 Frame Selection；否则 Fit All。
- Fit All 显示 `[0, DurationFrames)` 与小量右侧 padding。

### 10.3 Move

- Pointer Down 保存全部 selected content 的 StartFrame、LaneIndex 和必要 Timing 快照。
- Pointer Move 只计算总 `deltaFrame` / `deltaLane`，不增量累加。
- Preview ghost 显示新的位置，原对象保持可辨认。
- 任一对象进入负 Frame、目标 Lane 越界，或 Animation 产生 overlap 时，整组 ghost 变为 invalid 并在 Status Bar 给出原因。
- Pointer Up 只在合法且 delta 非零时创建一个 Undo transaction。
- 选择中包含 AnimationSegment 时，非零 vertical delta 整组拒绝。

### 10.4 Range Resize / Animation Trim

- 只允许单对象操作。多选状态下按下任意 Resize/Trim handle 时，Selection 立即收敛到该 handle 所属对象，并将其设为 Primary；随后只对该对象开始一次 Resize/Trim。这个选择变化不单独产生 Undo，实际 Timing 提交仍只有一个 Undo transaction。
- Resize 最短 1 Frame。
- Trim 实时显示 Start/End、SourceStart/SourceEnd、DerivedDuration。
- Animation 左 Trim 使用冻结公式：

  ```text
  newStartFrame = originalStartFrame + deltaFrames
  newSourceStart = originalSourceStart + deltaFrames / 60 * PlayRate
  ```

- Animation 右 Trim 使用冻结公式：

  ```text
  newSourceEnd = originalSourceEnd + deltaFrames / 60 * PlayRate
  ```

- Animation Move 只改变 StartFrame，不修改 SourceStart/SourceEnd/PlayRate。
- 非法 source range、Clip 边界、负 Frame 或 Animation overlap 时拒绝提交。
- V1 不支持 multi-resize。

### 10.5 Cancel

- Escape 恢复所有 preview transform/size。
- 释放当前 Pointer Capture。
- 清空 interaction snapshot。
- 不调用 Undo.RecordObject，不发送 Asset change notification。

## 11. 创建与结构操作

### 11.1 AnimationSegment

- Animation Header `+`：以 CurrentFrame 打开 AnimationAsset Picker。
- Content 右键：以 Pointer Frame 打开 Picker。
- 取消 Picker 不创建对象。
- 有效选择使用完整 Clip、PlayRate 1、SourceStart 0、SourceEnd Clip.length。
- 创建时生成新 32 位 GUID，提交一个 Undo 并选中新 Segment。
- 若会 overlap，创建前整体拒绝并说明冲突对象。

### 11.2 GameplayLane

- Corner `+ Lane` 创建唯一默认名称并生成 GUID。
- GameplayLane 是无 TrackKind 的纯组织容器；同一 Lane 可以混放全部七种 GameplayItem。
- Lane 名称和顺序用于 Authoring 组织；Editor 不把它们表现为 Runtime Priority、ExecutionOrder 或 winner。
- 新 Lane 被选中，Details 立即允许重命名。
- Reorder 通过 Header 菜单或明确的拖动 reorder handle；不得把普通 Timeline vertical move 与 Lane reorder 混为一个状态。
- Delete 非空 Lane 必须明确显示将同时删除的 Item 数量；确认后 Lane 与其中全部 Item 在一个 Undo transaction 中删除，不自动搬移 Item。

### 11.3 GameplayItem

- Lane Header `+` 使用 CurrentFrame。
- Lane Content 右键使用 Pointer Frame。
- 菜单列出七种 V1 Item，并按 Point / Motion / Collision / State 等纯展示分组；分组不写入数据，也不代表 Runtime order。
- Point 创建在目标 Frame；Range 创建为 1 Frame；Config 使用类型默认值。
- 新对象生成 32 字符 GUID、一个 Undo、成为 Primary Selection，并保持 Timeline 焦点。
- 默认 Config 不完整时允许立即创建，但只在该 Item 显示局部 `Needs setup` Badge，并由 Details 引导填写；不得因此自动展开全局 Issues Drawer。

## 12. Copy / Paste / Duplicate

### 12.1 Clipboard

- Editor session 内存深拷贝。
- 保留 Config、AnimationCurve 和 Unity Object 引用。
- 不创建临时 Asset，不写磁盘。
- Copy 本身不创建 Undo。

### 12.2 Paste

- 复制块最早 Frame 对齐 CurrentFrame。
- 保留相对 Timing。
- 所有副本生成新 GUID。
- Animation 固定进入 Animation Lane。
- 同 Action 优先按原 Lane ID。
- 跨 Action 只允许唯一同名 Lane 映射。
- Lane 缺失或同名歧义时整体拒绝，并列出无法映射的 Lane；不静默创建 Lane。
- Animation overlap 时整体拒绝。
- 成功 Paste 为一个 Undo transaction，并选中全部新对象，最后一个为 Primary。

### 12.3 Duplicate

- 保持原 Lane。
- 整块最早 Frame 放到原块最大 EndExclusive。
- 保留相对 Timing。
- Animation overlap 时整体拒绝。
- 成功 Duplicate 为一个 Undo transaction。

## 13. Action Details

### 13.1 响应式布局

- 内容列最大宽度约 720 px；宽窗口时居中，不让字段横跨整屏。
- 窄 Dock 宽度下 Label 与 Field 可上下重排。
- Scroll、Foldout 和光标位置不得因普通叶子字段修改而整体重置。
- 只在 Selection Kind 或结构发生变化时重建必要结构；普通 Content change 使用局部绑定刷新。

### 13.2 页面结构

Action：

```text
Summary（60 FPS / Duration）
Priority
Cancellation
Tags
Entry Conditions
```

AnimationSegment：

```text
Selection Header
Current Selection Issues
Timing（Start / Derived Duration / EndExclusive readonly）
Animation Source（Asset / SourceStart / SourceEnd / PlayRate）
Advanced（EditorId readonly）
```

GameplayLane：

```text
Name / Muted / Item Count
Advanced（EditorId readonly）
```

GameplayItem：

```text
Common（Muted / Point or Range Timing）
Typed Config sections
Advanced（EditorId readonly）
```

七种 Config 继续使用现有字段和条件显示，不新增数据：

- Impulse：Horizontal / Vertical 条件区。
- HitBox：Bone / Shape / Data / Effects。
- RootMotion：AnimationAsset / SourceStart / PlayRate。
- SelfRotation：Source / Mode 对应字段。
- VelocityOverride：Horizontal / Vertical 对应字段。
- MotionPolicy：三个可选 policy 与对应 value。
- Tag：TagReference / TargetContainer。

Config 叶子字段必须继续通过 `SerializedProperty` / Unity 原生控件编辑：

- ObjectReference、AnimationCurve、Tag、LayerMask、BoneReference 和 effects 保留原生选择、拖放与展开体验。
- Unity 原生 Undo 继续生效。
- 一次叶子提交统一发送 Content / Validation / Preview ChangeSet。
- 条件显隐只改变 presentation，不清空或改写隐藏字段。

### 13.3 Validation in Details

- 顶部只显示 Primary Selection 的问题。
- 不重复显示全部全局问题。
- 问题文字说明缺什么以及需要编辑哪个字段。
- IdentityBlocked 时 Details 显示统一阻塞卡片和 Repair，不显示无法定位的伪选择。

## 14. Validation Presentation

### 14.1 Timeline Health

Context Bar 显示：

```text
Ready
3 issues
10 issues · editing paused
```

### 14.2 Issues Drawer

- 默认收起，标题显示总数和阻塞数。
- 展开高度有上限，内部独立纵向 Scroll，不压缩 Timeline 到不可用。
- 按 Identity / Animation / Gameplay / Global 分组。
- 普通 issue row：Object label、Lane、message、Select。
- 点击 Select：设置稳定 Selection、滚动到内容、打开或刷新 Details。
- Identity issue：使用统一 Repair action，不尝试通过重复或缺失 ID 定位。
- null/unlocatable issue：显示 Authoring path，并提供显式 Delete（如可安全定位）。

### 14.3 Badge

- Segment、Lane、Item 显示与自身 EditorId 关联的问题数量。
- Lane Badge 可以聚合 Lane 自身问题，但不重复聚合所有子 Item，以免每条问题显示两次。
- Badge 点击展开 Issues Drawer 并过滤到该对象。

## 15. Action Preview

### 15.1 布局

```text
Context / Character Bar
Preview Viewport
Contextual Input Overlay
Compact Diagnostics
```

- Character ObjectField 必须带可见标签。
- Viewport 显示 CurrentAction、CurrentFrame 和 `Current-frame evaluation`。
- 无 Action 或无 Character 时显示居中空态和明确入口。
- Camera Frame、Reset Inputs 是 Preview 工具，不进入 Timeline Transport。

### 15.2 Evaluator

每次求值先恢复 baseline，再固定执行：

```text
Animation Pose
→ RootMotion Transform / Path
→ SelfRotation Facing
→ Active HitBox Gizmos
```

- 不调用 ActionRuntime。
- 不执行 Impulse、Tag、Velocity、Damage 或 MotionPolicy history。
- Muted Lane/Item 不参与 Preview。
- Animation 使用 Stage 4 相同的 Segment/gap/hold/trim/play-rate/60Hz 映射。
- Animation sample 后恢复 root transform，再应用 baked RootMotion。
- 缺失或 stale RootMotionData 时跳过对应变换并显示诊断，不现场 Bake。
- RootMotion 按 Item 的 `sourceStartTime + coveredFrames / 60 * playRate` 查询累计轨迹，并绘制当前位置与路径。

### 15.3 Preview Inputs

- 只有 active SelfRotation 需要 Target 时显示 Target Handle。
- 只有 active SelfRotation 需要 ContextDirection 时显示 Direction Handle。
- CombatTarget、ContextTarget、ContextInstigator 统一映射到 Preview Target Handle。
- ContextDirection 映射到 Preview Direction Handle。
- PresetLocal 与 RootMotion source 不显示无关 Handle。
- Handle 标记为 `Preview Input`，不伪装成 Authoring 数据。
- Reset Inputs 恢复默认前方位置/方向。
- 同 motion ownership 类别多个 active Item 时显示 warning，并按 StartFrame、Lane order、Item order选择第一个用于 Transform。
- 所有 active、非 muted HitBox 都绘制；缺骨骼只跳过对应 Gizmo。

### 15.4 Refresh 与清理

只在以下变化时重新求值：

```text
CurrentAction
CurrentFrame
Selection（用于选择强调与相关诊断）
Timeline structure/timing/content
PreviewCharacter
PreviewTarget/Direction
Preview playback state
```

关闭窗口时释放 Preview Scene、clone、render resources 和 sampling state。源 Prefab/scene object 永远不被修改。

PreviewCharacter 变化或 Domain Reload 后必须先销毁旧 clone，再在独立 Preview Scene 中安全重建；任何异常路径都不能留下对源场景对象的写入。

## 16. Undo / ChangeSet 合同

| 操作 | Undo 数量 | Asset Dirty | Selection 结果 |
| --- | ---: | --- | --- |
| Scrub / Pan / Zoom / Preview input | 0 | 否 | 保持 |
| Add Segment/Lane/Item | 1 | 是 | 新对象成为 Primary |
| Move / Resize / Trim drag | 1 | 是 | 保持操作选择 |
| Escape cancel | 0 | 否 | 恢复 drag 前选择 |
| Rename / Mute / leaf field edit | 每次明确提交 1 | 是 | 保持 |
| Delete selection | 1 | 是 | 回到 Action 或剩余明确对象 |
| Paste / Duplicate | 1 | 是 | 新对象集合 |
| Repair IDs | 1 | 是 | 回到 Action；重新建立可定位选择 |
| Copy | 0 | 否 | 保持 |

ChangeSet 至少区分：

```text
Context
Selection
Frame
Structure
Timing
Content
Validation
Preview
```

- Selection / Frame 不触发整个 Timeline tree rebuild。
- Drag 过程中不发送 Asset ChangeSet。
- Structure change 可以重建 Lane/entry tree。
- Timing change 更新布局、Duration、Ruler 和 Preview。
- Content leaf change 更新对应 Details/entry label/badge/Preview。
- Validation refresh 在 command commit 后执行一次，不在每个 repaint 重复遍历。

## 17. 快捷键

| 快捷键 | 行为 |
| --- | --- |
| Delete / Backspace | 删除选择；非空 Lane 需要确认 |
| Ctrl/Cmd + C | Copy |
| Ctrl/Cmd + V | Paste at CurrentFrame |
| Ctrl/Cmd + D | Duplicate |
| Ctrl/Cmd + A | 选择全部 Segment 与 Item |
| Space | Play / Pause |
| F | Frame Selection；无内容选择时 Fit All |
| Escape | Cancel 当前 interaction |
| Ctrl/Cmd + Wheel | Mouse-anchored Zoom |
| Middle Mouse Drag | Pan |

所有快捷键在文本或原生字段编辑焦点中禁用。

## 18. 人工验收矩阵

Unity Test Runner 不作为 Stage 5 验收门槛。验收以 Unity 编译、真实窗口交互和旧场景 smoke 为主。

### 18.1 截图资产恢复场景（第一门槛）

使用包含一个无效 AnimationSegment、一个空名 Lane、TagItem、MotionPolicyItem 和四个缺失 ID 的资产：

1. 打开 Timeline 后主工作区仍占据窗口大部分空间。
2. 顶部明确显示四个 Identity blocker；Issues 默认收起。
3. 所有内容可见但处于只读安全模式。
4. Repair 一次生成四个有效唯一 ID；数据字段与 Timing 不变。
5. Undo 一次恢复全部四个 ID；Redo 再次恢复修复结果。
6. 修复后每个对象都可独立选择。
7. TagItem 与 MotionPolicyItem 即使同帧重叠也分别可见、可点击。
8. Details 分别引导设置 AnimationAsset、TagReference 和 MotionPolicy。

### 18.2 基础 Authoring

- 空 Timeline 创建第一段 Animation、第一条 Lane 和七种 Item。
- Header `+` 使用 CurrentFrame；右键使用 Pointer Frame；取消 Picker 不创建内容。
- Lane Add/Rename/Reorder/Delete/Mute；Item Mute。
- 保存、重新导入和 Domain Reload 后数据与 ID 保持一致。
- 没有自动保存和静默 ID 修复。

### 18.3 Selection 与布局

- Single、Ctrl/Cmd Toggle、Shift Add、Marquee、Shift Marquee、Select All。
- 多选跨 Animation 与 Gameplay；Details Primary 关系明确。
- 同 Lane 1、2、5 个重叠 Item 始终保持相同行高；Marker 数量正确，Picker 可选择每一个 Item。
- 非法 Animation overlap 保持单行，也可通过错误 Picker 分别选择，并保持 Validation error。
- 水平滚动不移动 Header；垂直滚动不移动 Ruler。

### 18.4 Timing 操作

- Point Move、Range Move/Resize、Animation Move/Trim。
- Group Move 保持相对 Frame 与 Lane 距离。
- 负 Frame、Lane 越界和 Animation overlap 整组拒绝。
- Resize/Trim 实时反馈尺寸和 Timing。
- Escape 恢复原始显示，不产生 Undo。
- 每次完整拖拽只需一次 Undo。

### 18.5 Navigation

- Ruler scrub、Middle Mouse Pan、Fit All、Frame Selection。
- Ctrl/Cmd + Wheel 在不同 scroll offset 下保持鼠标锚点 Frame。
- Playback 到最后 Frame 停止；Loop 回 Frame 0。
- Speed 与 Runtime 不参与 Editor playback。

### 18.6 Clipboard

- 同 Action Copy/Paste/Duplicate 保留 Config、Curve、引用和相对 Timing。
- 跨 Action 唯一同名 Lane 映射成功。
- 缺 Lane、歧义 Lane、Animation overlap 整体拒绝且资产不脏。
- 所有副本获得不同 GUID；Undo 一次移除整个 Paste/Duplicate。

### 18.7 Details / Validation

- Action、Segment、Lane、七种 Item 页面分区清晰。
- 条件字段按 enum/toggle 显隐，Foldout 和 Scroll 不因叶子编辑重置。
- Timeline Badge、Details selection issue 和 Issues Drawer 数量一致。
- 普通 Config issue 不锁编辑；Identity issue 才进入 Safe Mode。
- Issue Select 能定位并选择目标。

### 18.8 Preview

- Prefab 与场景对象源不被修改。
- 单/多 Segment、首段前、段间、末段 Hold、Trim、PlayRate。
- RootMotion current transform/path、SelfRotation 四种输入、多个 HitBox。
- Muted 排除、owner conflict warning、缺骨骼与 stale trajectory 诊断。
- Action/Character 空态清楚；关闭窗口无 clone 或 Preview resource 残留。

### 18.9 回归与边界

- Unity Refresh 后 Console 0 compile error。
- 可用时执行 `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`。
- `git diff --check` 通过。
- Legacy Timeline、Sequence Editor、双击路由保持原样。
- `MiHoYo_Release` 运行无正式战斗行为差量和新增 Console 异常。
- 不提交手工测试资产、PreviewCharacter 或场景改动。

## 19. Runtime 与数据边界复核

本 redesign 不授权以下变化：

- 不增加或修改 Runtime public API。
- 不修改 Timeline 序列化结构、GameplayItem 类型或 Config 字段。
- 不修改 `ActionPlayer`、`ActionStateManager`、`ActorSimulationRuntime`、`CombatDriver`、`ActorAnimation` ownership 或 Stage 2–4 Scheduler。
- Editor playback 只推进 Editor CurrentFrame，不创建或调用 `ActionRuntime`。
- Lane/Item Mute 不影响 Duration 和 Authoring Validation；Muted 内容不参与 Preview。
- Gameplay overlap 继续合法，不新增 Validator overlap error。
- 不现场 Bake RootMotion，不从缺失数据回退到 runtime sampling。
- 不迁移、创建或切换正式 Action/Animation 资产。
- 不改变 Legacy/Sequence 双击路由，直到 Stage 7。

## 20. 需求追踪摘要

| 原 Stage 5 合同 | Draft 证据 | 状态 |
| --- | --- | --- |
| 三个独立 dockable window 与 Shared Context | §4 | Covered |
| 直接编辑 ActionAsset，无第二份 Timeline | §1、§3 | Covered |
| Stable ID selection 与显式 Repair | §5、§9 | Covered |
| 固定 Animation Lane 与 Gameplay Lane 管理 | §6、§11 | Covered |
| Ruler / Scroll / Pan / Fit / Scrub / Zoom | §6、§10 | Covered |
| 七种 Item 创建与默认 Timing | §11 | Covered |
| Single / Multi / Marquee | §9 | Covered |
| Point/Range/Group Move、Resize、Trim | §10 | Covered |
| Gameplay overlap 合法且可操作 | §7 | Covered |
| Copy/Paste/Duplicate 与跨 Action Lane 映射 | §12 | Covered |
| Details typed/conditional Config | §13 | Covered |
| Validation Badge / Drawer / identity-only Repair | §5、§8、§14 | Covered |
| Inspector V1 入口与 raw arrays 隐藏 | §4.4 | Covered |
| 独立 Preview Scene 与 current-frame evaluator | §15 | Covered |
| Animation/RootMotion/SelfRotation/HitBox Preview | §15 | Covered |
| Undo transaction、ChangeSet 与局部刷新 | §16 | Covered |
| 快捷键与 Pointer Capture cancel | §10、§17 | Covered |
| Domain Reload / cleanup / no auto-save | §4、§15、§18 | Covered |
| 不切 Runtime、不迁资产、不破坏 Legacy | §19 | Covered |

## 21. 开源 Timeline 审查结论

源码与许可审查见：

`Docs/Proposals/CombatSample_Action_V1_Stage_5_Open_Source_Timeline_Audit_2026-09-02_zh-CN.md`

结论：不存在同时满足 `ActionAsset.Timeline` 单一数据权威、Unity 2022.3、稳定 EditorId、单次 Undo、clone-only Preview 和当前许可证边界的可直接采用方案。Stage 5 不引入或 Fork 外部 Timeline package；默认只借鉴设计模式。若以后需要复制任何 MIT 源码，必须先记录固定提交、原文件、复制范围、修改点和许可保留位置。

## 22. 建议实施切片（冻结设计后）

```text
5R.0  Interactive visual shell proof（只读强制验收门槛）
5R.1  Shared visual language + readiness/identity safe mode + compact validation
5R.2  Fixed ruler/header scroll architecture + grid + empty/invalid placeholders
5R.3  Fixed-row overlap markers/pickers + selection/focus/navigation
5R.4  Interaction state machine + commands/Undo/clipboard
5R.5  Responsive Details + typed Config + issue localization
5R.6  Preview structure/diagnostics/contextual inputs
5R.7  Manual acceptance matrix + docs handoff
```

每个切片完成后先在 Unity 中验证可见结果，再进入下一切片。不得再次以“代码中存在入口”代替真实交互验收。

### 22.1 5R.0 Interactive Visual Shell Proof

5R.0 使用最终保留的 UI Toolkit 组件建立可交互、只读的真实 Unity 外壳，不制作一次性 HTML 或伪造最终交付的旁路窗口。它可以读取真实 `ActionAsset` 和只读 `ActionEditorDocument`，但不得写入资产。

必须包含：

- Timeline、Details、Preview 三个真实独立窗口及 Shared Context 同步。
- Context Bar、Transport、Identity Banner、固定 Header/Ruler、Grid、Playhead、32/36 px Lane、Status Bar 和默认收起的 Issues Drawer。
- 真实 Scroll/Pan、mouse-anchored Zoom、Fit、Scrub、Selection、Hover 和 Gameplay overlap Picker。
- 正常、空、非法 Timing/Config、IdentityBlocked 和重叠内容的只读显示。
- Details 响应式布局和 Preview 明确空状态；不承诺在本切片完成 Preview evaluator。
- 2560×1440、1920×1080 和窄 Dock 三种尺寸的真实 Unity 截图与人工操作证据。

明确禁止：

- Add、Delete、Move、Resize、Trim、Paste、Duplicate、Repair 或任何资产写入。
- 为通过演示而修改用户资产、创建正式测试资产、自动保存或静默修复 ID。
- 在 5R.0 视觉与基础交互未获项目负责人接受前进入 5R.1。

## 23. 已确认的设计决策

2026-09-02，项目负责人已在设计讨论中确认：

1. 三个窗口保持独立可 Dock；默认只打开 Timeline，Details/Preview 按需打开。
2. 一条 GameplayLane 始终只占一行；合法 overlap 使用中性 Editor-only Marker/Picker 访问被覆盖 Item，不进入 Validation。
3. Animation overlap 继续非法；在没有 CrossFade、权重或 Priority 合同前不隐式选择 winner。
4. 任一 missing、malformed 或 duplicate EditorId 使整个 Timeline 进入只读安全模式，直到显式 Repair。
5. GameplayItem 立即创建；不完整 Config 使用局部 `Needs setup`，不展开全局错误列表。
6. GameplayLane 是无类型的组织容器，可混放七种 Item，不引入 TrackKind。
7. 多选时 Details 只编辑 Primary Selection，不做隐式批量字段编辑。
8. 删除非空 Lane 时，确认后连同全部 Item 在一个 Undo transaction 中删除，不自动搬移。
9. 跨 Action Paste 只按唯一同名 Lane 映射；缺失或歧义时整体拒绝，不静默创建 Lane 或打开复杂映射流程。
10. Issues Drawer 默认收起；普通 issue 点击后定位内容，只有 Identity Repair 提供批量修复。
11. 正式 Authoring 前先交付 5R.0 可交互只读外壳，并以真实 Unity 视觉与操作验收作为强制门槛。

以上决策已不再开放为实现阶段的临时判断。项目负责人已于 2026-09-02 明确确认冻结；本文自此成为 Stage 5 remediation 的实现权威。实施必须从 5R.0 开始，且在 5R.0 获得人工接受前不得进入 5R.1。
