# CombatSample Action V1 — Stage 5R.4-Z Time Viewport & Zoom Navigation Correction 实施记录

> 状态：Implemented，待 Unity 人工验收
>
> 日期：2026-09-05
>
> 行为参考：[Time Viewport HTML 原型](VisualReferences/action-v1-time-viewport-proof.html)

> 2026-09-05 补充：Stage 5R.4-C 取代 Navigator 拖动时扩展导航域的行为。Navigator Pointer Capture 期间固定 NavigationExtent；主视图平移仍可按本记录的规则扩展导航域。

## 1. Summary

Stage 5R.4-Z 将 Timeline 横向导航从“可变像素画布 + ScrollView 水平偏移”替换为单一 Editor-only 可见时间范围。Timeline 宽度现在始终等于视口宽度，Ruler、Grid、Entry、Playhead、Ghost、Scrub、Marquee 和 Authoring 手势共享同一个 Frame/Pixel 变换。

本切片只纠正 Editor 视图与导航，没有修改 `ActionAsset` 数据结构、Validator、Runtime、Legacy 路由或 Stage 5R.4 的 Undo/Clipboard 合同。

## 2. Implemented Changes

- 新增 session-only `ActionV1TimeViewport`：保存 VisibleStart、VisibleSpan、WorkspaceEnd，并从视口宽度推导 PixelsPerFrame。
- Workspace 初始为 `max(120, Horizon × 2)`；Fit All 只显示 Horizon，不收缩 Workspace。
- Content ScrollView 只保留纵向滚动；Ruler 与 Content 使用 viewport-local 坐标，不再依赖水平 scroll offset。
- 鼠标滚轮在 Ruler/Content 上直接缩放；横向主导的 wheel/trackpad delta 平移；Middle Mouse 与 Alt+Left Drag 平移。
- 连续滚轮使用 220 ms / 1 px 的稳定焦点，缩放边界仍消费事件，避免回落到 ScrollView 产生跳动。
- 新增底部 Time Range Navigator，支持 Thumb 平移以及左右 Handle 缩放；Navigator 与主视图同步刷新。
- Grid/Ruler 只遍历可见 Frame，并使用自适应 1/2/5 刻度，极端 Timeline 不随完整 Horizon 线性绘制。
- Fit、Frame Selection、Issue/Picker Locate、Point Diamond、Range/Segment、Overlap 和 Ghost 已切换到统一 Viewport Geometry。
- Visible range、WorkspaceEnd、Vertical Scroll、Header Width 和 Issues 状态由 EditorWindow 会话序列化恢复，不写入资产或 EditorPrefs。
- Range 的语义主体与透明命中区已拆分：可见宽度严格对应 `[Start, End)`，不再使用 28 px 最小视觉宽度侵入相邻 Point；低缩放下不足 16 px 的 Range 只执行 Move，放大后才开放左右 Resize/Trim。
- Gameplay 点击使用语义距离消解重叠命中，透明 Range hit target 不会抢走更接近的 Point。
- Point 使用 viewport-only 密度簇：同 Lane 的 Point 在屏幕间距不足 26 px 时合并为堆叠菱形与数量 Badge；放大后自动恢复独立 Point。簇 Picker 仍按 ItemIndex 导航，不代表数据 overlap、优先级或 Runtime 顺序。
- Density Picker 选择或 Issue Locate 某个成员后，簇锚定该 Point 并显示 focused 样式；不生成临时 EditorId，也不写入资产。
- Time Range Navigator 不再用 `WorkspaceEnd = Horizon × 2` 直接计算 Thumb。独立 NavigationExtent 初始等于 Horizon，因此 Fit All 为满宽；放大后 Thumb 按 `VisibleSpan / NavigationExtent` 线性缩短，只有向右平移越过既有导航域时才按 `max(30 frames, VisibleSpan × 25%)` 的 30-frame 区块增长。单纯向外缩放使用当前 VisibleEnd 作为临时显示域，不把预留 Workspace 提前计入 Thumb。

## 3. Verification Evidence

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`：成功，0 warning / 0 error。
- Stage 5R.4-Z 修改文件未发现新增尾随空白；全仓 `git diff --check` 仍只报告用户已有的 `New Sequence Action.asset:144` 尾随空白，本切片未修改该行。
- Unity Test Runner 不作为本切片验收门槛。

## 4. Unity Manual Acceptance Gate

- Ruler/Content 直接滚轮缩放，无需 Ctrl/Cmd；连续缩放时鼠标下 Frame 不漂移。
- 横向触控板输入平移；到达缩放边界继续滚轮不会触发意外滚动。
- 缩小到完整 Workspace 时，Timeline、Grid 和 Ruler 始终铺满视口。
- Navigator Thumb 与左右 Handle 连续工作，没有延后一帧的跳变。
- Fit、F、Issue Locate、Picker Locate 以及 Zoom 后的 Scrub、Marquee、Move、Resize、Trim、Ghost 坐标准确。
- Point 保持固定 10×10 px 菱形并严格锚定整数 Frame。
- 缩小后语义上不重叠的 Point/Range 不出现由最小视觉宽度造成的假重叠；两者的透明命中区仍能分别到达正确对象。
- 同 Lane 密集 Point 缩小时显示为不互相覆盖的数量簇；点击簇可到达每个 Point，放大后自动拆分。
- Fit All 时 Navigator Thumb 为满宽；轻微放大只产生对应比例的线性缩短，不再因两倍 Workspace 立即变成约 50%。
- 多 Lane 垂直滚动仍与 Header 同步；Domain Reload 后视图状态恢复。
- 所有纯视图操作不改变 ActionAsset 内容或 dirty 状态。
- Unity Console 为 0 compile error、0 USS parse error、0 UI Toolkit exception。

## 5. Exit Boundary

获得真实 Unity 人工接受前，只修复 5R.4-Z 的时间视口、缩放、平移与 Navigator，不进入 Stage 5R.5 Details leaf editing。
