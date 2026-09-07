# CombatSample Action V1 — Stage 5R.3 Fixed-row Overlap, Selection, Focus & Navigation 实施记录

> 水平导航勘误（2026-09-05）：缩放、平移和 Reveal 的水平坐标合同已由 Stage 5R.4-Z 统一为 Time Viewport；Overlap、Selection、Focus 与 Navigation 语义不变。

> 状态：Implemented；待 Unity 人工验收
> 日期：2026-09-04
> 上位权威：`CombatSample_Action_V1_Stage_5_Editor_Redesign_Draft_2026-09-01_zh-CN.md`（Frozen）
> 前置条件：Stage 5R.2 已通过真实 Unity 人工验收

## 1. Summary

Stage 5R.3 完成只读 Timeline 的 overlap 可达性、稳定选择、焦点与导航合同。Gameplay 与 Animation 内容继续使用固定单行；被遮挡内容可以通过 Marker / Picker 到达，Selection 和 Primary 不再依赖 Document 重建。

除 Stage 5R.1 的显式 ID Repair 外，本阶段不写入 `ActionAsset`，也未开放 5R.4 的 Authoring commands。

## 2. Implemented Changes

### 2.1 Overlap Read Model

- Overlap sweep 改用 long Frame 边界，Point 使用 `[Frame, Frame + 1)`，Range 与 Segment 使用 `[Start, EndExclusive)`。
- null entry 与非正 Duration 不参加 sweep；连续并发区间合并为互不重叠的 region。
- 每个 region 记录峰值并发数和全部参与对象；参与对象按原始 ItemIndex / SegmentIndex 排序。
- 链式 overlap 的 Marker 显示峰值并发，Picker 仍可访问整个连续 region 的全部参与对象。
- Document 提供每条 Lane 与 Animation Lane 的唯一 overlap 参与对象数量。

### 2.2 Fixed-row Presentation and Picker

- Gameplay overlap 使用中性 `×N` Marker；Animation overlap 使用错误色 Marker，并在对应 Header 显示摘要。
- 负区间完全位于 Frame 0 前时不绘制 Marker；跨 Frame 0 的 region 从左边界显示。
- 超出 5R.2 安全 Geometry 的 Marker 固定在 overflow 边界，并保留原始范围 tooltip。
- Picker 显示类型、Lane、原始区间与 AuthoringPath，并明确标记 `Navigation only · no priority`。
- Picker、Issues Drawer 与普通内容选择共用定位通道；不稳定 ID 只做路径临时定位。

### 2.3 Stable Selection

- Shared Context 的每个选择现在同时保存 `SelectionKind + EditorId`，可跨 Domain Reload 恢复。
- 取消 Primary 后直接使用已保存的最后选择，不再调用 `ActionV1EditorDocument.Build` 或 Validator。
- Single、Ctrl/Cmd Toggle、Shift Add、Marquee、Shift Marquee 与 Ctrl/Cmd+A 遵循冻结合同。
- Shift Marquee 无命中时保持原 Selection 和 Primary；Ctrl/Cmd+A 只选择具有唯一合法 ID 的 Segment 与 GameplayItem。
- Marquee 对正常内容使用真实时间区间与 Lane 范围；非法 placeholder 使用安全显示矩形。
- Selection 变化只更新样式、Status 和 Details，不触发 Document / Validation refresh。

### 2.4 Layer, Focus and Navigation

- Timeline 内容拆分为 Grid、Entry、Overlap 与 Guide 层，Marker 不再被 Primary / Hover Entry 遮挡。
- Entry 层级固定为 Hover、Primary、其他 Selected、Authoring order；Hover 离开后恢复确定性顺序。
- 点击 Ruler、Header、Lane Content、Entry 或 Marker 会恢复 Timeline command focus；原生字段焦点通过祖先检查屏蔽快捷键。
- 启用 Ctrl/Cmd+A，并保留 Space、Left/Right、F、Scrub、Pan、Zoom 和 Transport 导航。
- Escape、窗口失焦、Detach 与 Pointer Capture 丢失会清理 Scrub、Pan、Marquee 或 Header resize，不产生 Undo。
- Issue / Picker Locate 同时 reveal 横向 Frame 与纵向 Lane，并优先于过期 Scroll restore。

## 3. Boundary Review

- Timeline、Details 与 Preview 中唯一可达的资产写入口仍是 `RepairEditorIds`。
- 未接入 Add、Delete、Move、Resize、Trim、Rename、Mute、Config、Copy、Paste 或 Duplicate。
- 未修改 Timeline 序列化结构、GameplayItem、Validator 合法性、Runtime、ActionPlayer、Scheduler、ActorAnimation、Preview evaluator 或 Legacy 路由。
- Presentation z-order、overlap region、focus 与 selection history 均为 Editor session state，不进入资产或 Runtime。

## 4. Automated Verification

- Unity 清理 Temp cache 后，第一次 `--no-restore` 正确报告缺少 `project.assets.json`；使用普通 `dotnet build` 重新生成临时依赖缓存。
- 随后执行 `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`：成功，0 warning / 0 error。
- Unity Test Runner 未作为本阶段验收门槛。

## 5. Unity Manual Acceptance Gate

由项目负责人在真实 Unity 中观察：

- 同 Lane 1、2、5 个 Item、链式 overlap、多个分离 region 与 triple overlap 的 Marker 数量。
- Gameplay 中性 Marker、Animation 错误 Marker、Header 摘要和 Picker 全对象可达性。
- Hover / Primary / 多选层级离开或切换后的恢复。
- Single、Ctrl/Cmd Toggle、Shift Add、Marquee、空命中 Shift Marquee 与 Ctrl/Cmd+A。
- IdentityBlocked 下合法唯一对象可选，不稳定对象仅路径定位。
- Timeline command focus、原生字段快捷键隔离、Escape 和窗口失焦清理。
- F、Issue Locate、Picker Locate、Scrub、Pan、Zoom 及 5R.2 滚动同步无退化。
- Unity Console 0 compile error、0 USS parse error、0 UI Toolkit exception。
- 除显式 ID Repair 外，操作前后资产内容与 dirty 状态不变。

## 6. Exit Boundary

本文件只记录实现完成。项目负责人明确接受前，只修正 5R.3 的 overlap、selection、focus 与 navigation；不得进入 5R.4，也不得开放任何正式 Authoring command。
