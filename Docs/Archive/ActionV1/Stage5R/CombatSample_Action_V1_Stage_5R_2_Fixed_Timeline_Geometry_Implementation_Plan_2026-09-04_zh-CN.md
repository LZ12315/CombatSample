# CombatSample Action V1 — Stage 5R.2 Fixed Timeline Geometry & Invalid Placeholders 实施记录

> 水平导航勘误（2026-09-05）：本阶段的水平 ScrollView/offset 实现已由 Stage 5R.4-Z 的 viewport-local Time Viewport 与独立 Time Range Navigator 取代；纵向同步、非法占位和安全显示合同继续有效。

> 状态：Accepted；Unity 人工验收通过
> 日期：2026-09-04
> 上位权威：`CombatSample_Action_V1_Stage_5_Editor_Redesign_Draft_2026-09-01_zh-CN.md`（Frozen）
> 前置切片：Stage 5R.0 Interactive Shell、Stage 5R.1 Identity & Compact Validation

## 1. Summary

Stage 5R.2 将四象限 Timeline 外壳固化为统一、安全的视图基础。Frame、Pixel、Lane、可见区、点击宽度和极端坐标现在由一个 Editor-only Geometry 模型处理；Ruler、Grid、Playhead、Entry、Overlap Marker 及视图定位命令不再各自计算坐标。

Timeline 仍处于 `VIEW MODE · AUTHORING COMMANDS LOCKED`。除 Stage 5R.1 已允许的显式 ID Repair 外，本阶段不写入 `ActionAsset`，也不开放任何正式 Authoring 操作。

## 2. Implemented Changes

### 2.1 Unified Geometry

- 新增 `ActionV1TimelineGeometry`，统一提供 Frame / Pixel 转换、Lane Y、Content 尺寸、可见 Frame 范围、Scroll 钳制和 Entry 显示矩形。
- 保留 `Horizon = max(60, DurationFrames + 30)` 与非负 Frame 展示合同。
- Point 保留一帧语义并使用最小点击宽度；Range 使用 `[Start, EndExclusive)` 语义宽度。
- 非有限 Zoom、整数溢出及超过 UI Toolkit 安全布局范围的坐标使用饱和计算；极端数据固定到显示边界并显示 `DISPLAY LIMIT`，原始 Frame 值只进入标签、tooltip 和诊断，不写回资产。

### 2.2 Four-quadrant Scroll and State

- Content ScrollView 仍是唯一双向滚动源；Header 只同步 Y，Ruler 只同步 X。
- 增加同步 guard、Document rebuild scroll token 与延迟恢复 generation，避免旧 Scroll 覆盖 Fit、Frame Selection、Issue Locate、Pan 或 Zoom 产生的新视图位置。
- Rebuild 前保存实际 Scroll，新 Geometry 生效后钳制恢复；用户主动开始滚动时取消过期恢复。
- Header Width、Zoom、Scroll 与 Issues Drawer 展开状态使用 `EditorWindow` 序列化字段恢复，不写入资产或 `EditorPrefs`。
- Header Width 继续限制在 180–320 px，只更新布局。

### 2.3 Visible Ruler and Grid

- Ruler 与 Grid 共用 major / minor tick 计算。
- 每次绘制只遍历当前可见 Frame 范围及边缘 padding；不按完整 Horizon 线性绘制。
- 显示 Frame 0、Duration end、Duration 后 authoring 区域和当前 Playhead。
- 鼠标锚点 Zoom、Fit All、Frame Selection 和 Issue Locate 全部通过统一 Geometry 定位。
- Frame、Selection、Scroll、Zoom、Hover 和真实 viewport Geometry 变化只更新显示，不重新运行 Validator。

### 2.4 Empty and Invalid Presentation

- Read Document 增加 `Normal`、`NullEntry`、`NegativeStart`、`InvalidDuration`、`MissingSource`、`InvalidTiming`、`GeometryOverflow` 纯展示状态。
- NoAction、缺失 Timeline 数据与空 Timeline 使用不同 Empty State；空 Timeline 仍保留固定 Animation Lane。
- null Segment / Lane / Item 显示带 `AuthoringPath` 的 quarantine placeholder。
- 负 StartFrame 固定在 Lane 左边缘并显示原始负值；零/负 Duration 与无法推导时长的 Animation 使用错误最小点击块。
- 缺失 AnimationAsset 或 Clip 显示 `Missing Animation Asset`；极端坐标显示 overflow sentinel。
- Placeholder 不参与 Duration、Validation 或 Runtime，也不生成临时稳定 Selection ID。

### 2.5 Boundary Preservation

- 保留 5R.1 Identity Banner、单 Undo Repair、分类 Issues Drawer、AuthoringPath 定位、32/36 px Lane、Selection、Marquee 和 Overlap Picker。
- Gameplay overlap 仍为合法同 Lane 重叠并使用中性 Picker；Animation overlap 仍显示 Validator error。
- Timeline、Details、Preview 中除 Timeline 的 `RepairEditorIds` 外，没有接入资产写命令。
- 未修改 Timeline 序列化数据、GameplayItem、Runtime、ActionPlayer、Scheduler、ActorAnimation、Root Motion 或 Legacy 路由。

## 3. Automated Verification

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`：成功，0 error；51 个 warning 来自既有 Unity packages、旧插件与旧项目代码，本切片没有新增编译错误。
- 静态写入口审查：Timeline / Details / Preview 中仅存在 Stage 5R.1 的 `RepairEditorIds`；没有 Add、Delete、Move、Resize、Trim、Rename、Mute、Config、Paste 或 Duplicate 调用。
- Geometry、Ruler 与 Grid 的循环均以可见范围为上界；极端 Horizon 不触发全范围刻度遍历。
- Unity Test Runner 未作为本阶段验收门槛。

## 4. Unity Manual Acceptance Gate

由项目负责人在真实 Unity 中观察：

- 2560×1440、1920×1080 和窄 Dock 下四象限布局、Header / Ruler / Content 同步。
- 0、1、数十条 Lane 的垂直滚动，以及长 Timeline 的水平滚动对齐。
- Zoom 锚点、Fit All、Frame Selection、Issue Locate 后视图位置不回跳。
- Domain Reload 后 Scroll、Zoom、Header Width 与 Issues Drawer 状态恢复。
- NoAction、空 Timeline、null entry、负 Frame、零/负 Duration、Missing AnimationAsset / Clip、非法 SourceRange / PlayRate 与极端 Frame 的占位表现。
- Selection、Marquee、Overlap Picker 与 ID Repair / Undo / Redo 无退化。
- Unity Console 0 compile error、0 USS parse error、0 UI Toolkit exception。
- 除显式 Repair 外，观察前后 ActionAsset 内容及 dirty 状态不变。

## 5. Exit Boundary

项目负责人已于 2026-09-04 确认 5R.2 没有发现问题，并允许进入 5R.3。创建、删除、拖拽、字段编辑与 Clipboard 仍留给后续切片。
