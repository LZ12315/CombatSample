# CombatSample Action V1 — Stage 5R.4 Authoring Interaction 实施记录

> 状态：Implemented，待 Unity 人工验收
>
> 日期：2026-09-04
>
> 上位权威：`CombatSample_Action_V1_Stage_5_Editor_Redesign_Draft_2026-09-01_zh-CN.md`（Frozen）

> 2026-09-05 补充：Stage 5R.4-Z 已系统替换本阶段继承的水平滚动/缩放模型；创建、Move/Resize/Trim、Undo 与 Clipboard 合同保持不变，并已接入新的 Time Viewport Geometry。

> 2026-09-05 补充：Stage 5R.4-C 取代本记录中“Animation overlap 只拒绝新引入 overlap”的编辑操作规则。现在每个本次修改的 AnimationSegment 在提交后必须无 overlap；未涉及对象间已有错误仍由 Validator 报告而不阻塞操作。

## 1. Summary

Stage 5R.4 在 5R.0–5R.3 的固定行 Timeline 基础上开放正式 Authoring：创建 AnimationSegment、GameplayLane 和七种 GameplayItem，执行 Lane 结构命令、Item/Lane Mute、Timeline Move/Resize/Trim，以及 session-only Copy/Paste/Duplicate。

所有资产修改继续直接作用于 `ActionAsset.Timeline`，没有第二份 Timeline、临时 Asset 或 Compile/Save 步骤。IdentityBlocked 仍是只读安全模式；普通 Timing、资源或 Config 问题不会锁死编辑器。

## 2. Implemented Changes

### 2.1 Creation and Structure

- Corner `+ Lane` 创建唯一默认名称与新 EditorId，并选择新 Lane。
- Animation Header `+` 与 Animation Lane 右键入口通过 Unity Object Picker 创建完整 Clip Segment；取消 Picker 不创建内容。
- Gameplay Lane Header `+` 与 Lane Content 右键入口创建七种 V1 Item；Impulse 为 Point，其他类型默认为 1 Frame Range。
- Lane Header 提供 Mute、内联 Rename、Move Up/Down 和带非空确认的 Delete。
- Item Context Menu 提供 Mute、Copy、Duplicate 和 Delete。
- null Segment/Lane/Item 保留 quarantine 显示，并提供基于当前 AuthoringPath 的显式删除，不伪造 EditorId。

### 2.2 Interaction State and Timing

- Timeline 统一处理 Idle、Scrub、Pan、Marquee、Move、Resize、Trim 和 InlineRename 状态。
- Move/Resize/Trim 在 Pointer Down 保存原始快照，Pointer Move 只更新 Ghost，Pointer Up 才提交资产。
- Group Move 保持相对 Frame 与 Lane 距离；Selection 包含 Animation 时不允许垂直移动。
- Point 的数据/Duration/overlap 语义仍为 `[Frame, Frame + 1)`，但 Timeline 表现为严格居中锚定 `FrameToPixel(Frame)` 的固定 `10×10 px` Keyframe Diamond；使用独立 `14 px` 透明命中范围与 `8 px` 纯显示边缘留白，Frame 0 也能完整显示。可见 Marker、命中范围与时间语义分别计算，拖拽 Ghost 使用相同菱形表现。
- Range 左右 Resize 保持最短 1 Frame；Animation Trim 使用冻结的 60 Hz / PlayRate 换算。
- 负 Frame、目标 Lane 越界、整数溢出、非法 SourceRange 和新增 Animation overlap 整体拒绝。
- Escape、窗口失焦、Detach 或 Pointer Capture 丢失统一取消当前交互，不产生 Undo 或 dirty。

### 2.3 Commands, Undo and Clipboard

- 结构与 Timing 写入使用完整对象 Undo；一次成功命令只登记一次 Undo。
- 命令预检使用不运行 Validator 的只读 Document；正式提交后由窗口进行一次 Validation refresh。
- Frame/EndExclusive 中间计算改用 `long`，并拒绝越出可序列化 Frame 范围的操作。
- Animation overlap 检查只拒绝新引入的 overlap，允许移动或裁剪修复已有问题。
- Clipboard 按 Authoring order 深拷贝 managed data、AnimationCurve 与 Unity 引用，不写磁盘。
- Paste 保留相对 Timing；同资产优先 Lane ID，跨资产使用唯一同名 Lane；映射或 overlap 失败时整体拒绝。
- Paste/Duplicate 在写入前生成全部新 GUID；成功后一次 Undo 并选择全部新对象。
- Duplicate 使用独立临时快照，不覆盖用户已有 Clipboard。

### 2.4 Focus and Refresh

- 启用 Delete/Backspace、Ctrl/Cmd+C、Ctrl/Cmd+V 和 Ctrl/Cmd+D，并保留 5R.3 导航快捷键。
- 原生输入控件聚焦时 Timeline 快捷键继续被屏蔽。
- Selection、Ghost 和 Scroll 不运行 Validator；Structure/Timing/Content commit 才触发 Document 与 Validation 刷新。
- Timeline 状态由只读外壳更新为 Authoring Mode；IdentityBlocked Banner 与 Repair 合同不变。

## 3. Boundaries

- Details 仍是只读展示；typed/conditional Config 编辑属于 5R.5。
- Preview evaluator 不在本阶段扩展，属于 5R.6。
- 未修改 Action 数据结构、Runtime public API、ActionPlayer、Scheduler、ActorAnimation、Root Motion 或 Legacy 路由。
- 不自动保存资产，不静默 Repair，不提交人工验收创建的临时资产。

## 4. Automated Verification

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`：成功，0 warning / 0 error。
- Unity Test Runner 不作为本阶段验收门槛。
- Unity Refresh、USS parse 和真实 Pointer/Object Picker 行为仍需项目负责人在 Unity 中确认。

## 5. Unity Manual Acceptance Gate

### Creation

- 空 Timeline 创建 Lane、AnimationSegment 和全部七种 Item。
- Header `+` 使用 CurrentFrame；Content 右键使用 Pointer Frame。
- 取消 Animation Picker 不产生对象、Undo 或 dirty。
- 新对象 ID 唯一，默认不完整 Config 只产生局部 Needs setup。

### Structure and Timing

- Lane Rename/Mute/Reorder/Delete 与 Item Mute。
- Point Move、Range Move/Resize、Animation Move/Trim、Gameplay 跨 Lane 和 Group Move。
- 非法 Ghost 明确显示原因；Pointer Up 不写资产。
- Escape、失焦和 Pointer Capture 丢失后无残留状态。
- 每次完整写操作仅需一次 Undo；Redo 恢复全部结果。

### Clipboard and 5R.3 Regression

- 同/跨 Action Copy/Paste/Duplicate，检查 Config、曲线、引用、Timing、Lane 映射和新 ID。
- 映射失败与 Animation overlap 整体拒绝且资产不 dirty。
- Duplicate 不改变随后 Ctrl/Cmd+V 使用的旧 Clipboard。
- 使用新建内容复查 overlap Picker、Single/Multi/Marquee、Primary/Hover、F、Issue Locate、Pan、Zoom 和滚动同步。
- Unity Console 0 compile error、0 USS parse error、0 UI Toolkit exception。

## 6. Exit Boundary

Stage 5R.4 获得 Unity 人工接受前，只修正本阶段的创建、结构、Timing、Undo、Clipboard 及其对 5R.0–5R.3 的回归；不得进入 5R.5 Details typed Config authoring。
