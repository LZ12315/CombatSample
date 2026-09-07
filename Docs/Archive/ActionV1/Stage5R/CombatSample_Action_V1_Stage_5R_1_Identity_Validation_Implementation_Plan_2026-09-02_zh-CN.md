# CombatSample Action V1 — Stage 5R.1 Identity & Compact Validation 实施计划

> 状态：Accepted；后续切片已获准实施
> 日期：2026-09-02
> 上位权威：`CombatSample_Action_V1_Stage_5_Editor_Redesign_Draft_2026-09-01_zh-CN.md`（Frozen）
> 前置条件：Stage 5R.0 已通过真实 Unity 人工验收

## 1. Summary

Stage 5R.1 将已经验收的只读外壳固化为正式 Stage 5 编辑器基础，并完成稳定 Identity 与紧凑 Validation 工作流。

本阶段唯一允许写入 `ActionAsset` 的操作是用户显式触发的 `Repair Editor IDs`。Add、Delete、Move、Resize、Trim、Rename、Mute、Config 编辑、Paste 与 Duplicate 仍保持不可达，直到后续对应切片。

## 2. Implementation Changes

### 2.1 结构化 Validation 定位

- 为 Editor-only `ActionAuthoringValidationIssue` 增加只读 `AuthoringPath`，例如：
  - `AnimationSegments[0]`
  - `GameplayLanes[1]`
  - `GameplayLanes[1].Items[3]`
- Validator 在遍历数据时直接填写路径；Editor 不再通过消息文本、数组猜测或 issue 顺序反推对象。
- `EditorId` 继续用于稳定选择；`AuthoringPath` 只用于当前 Document 的非法数据显示、Issue 定位和 Repair 前诊断，不进入持久化 Selection。
- Validation presentation 建立以下纯展示严重度：
  - `IdentityBlock`：missing、malformed、duplicate EditorId。
  - `Error`：null entry、非法 Timing/SourceRange/PlayRate、Animation overlap、缺失引用或无效 Root Motion 数据。
  - `NeedsSetup`：`InvalidConfig`；仍属于 Validator 无效状态，但作为 Item 局部待配置提示显示。
- `ActionAuthoringValidator` 仍是唯一合法性权威；展示严重度不得改变 `IsValid`、Runtime 合同或数据值。

### 2.2 Identity Safe Mode 与显式 Repair

- Timeline 只要存在任一 IdentityBlock，维持全 Timeline safe mode：
  - 所有普通资产写操作保持锁定。
  - 合法且唯一 ID 的内容仍可只读选择和导航。
  - 不稳定对象只允许通过 AuthoringPath 临时定位，不伪造 Selection ID。
- 激活 Identity Banner 中的 `Repair Editor IDs`：
  - 按钮只在 `IdentityBlocked` 时可用，并显示待修复数量。
  - 不弹出无意义确认框；点击本身就是明确修复意图。
  - 先为所有待修复目标生成互不重复的 32 字符 GUID，再以一个 Undo transaction 写入。
  - missing、malformed 和 duplicate 的冲突副本全部纳入同一次修复；已有合法唯一 ID 保持不变。
  - 除 EditorId 外，Timing、Config、对象引用、Lane/Item 顺序和 Muted 状态必须逐字段保持。
  - 成功后标记资产 dirty、Primary Selection 回到 Action、清空内容多选，并只发送一次 Structure/Validation ChangeSet。
- 若从 Inspector 修复的资产不是 Shared Context 的 CurrentAction，则只修复并刷新 Inspector，不切换当前编辑中的 Action；若修复的是 CurrentAction，Inspector 与 Timeline 均执行相同的 Selection reset 和 ChangeSet。
- Undo 一次恢复修复前全部 ID 与 `IdentityBlocked`；Redo 一次恢复相同修复结果，不重新生成另一组 GUID。
- Timeline Banner 与 `ActionAsset` Inspector 使用同一个 Repair command，避免 Undo、dirty 和刷新行为分叉；加载、Repaint、Domain Reload 仍绝不自动修复。

### 2.3 Compact Validation UX

- Identity Banner 显示 missing / malformed / duplicate 分类数量及 Repair 按钮，不展开完整 issue 列表。
- Issues Drawer 保持默认收起，Header 显示总数和严重度摘要；只有用户主动展开才显示列表。
- Issue row 使用稳定的严重度样式和简短主文案，完整信息进入 tooltip；点击后按结构化 AuthoringPath 定位。
- 多个 issue 指向同一对象时复用一个局部 Badge；Details 按 Primary Selection 展示该对象的完整问题。
- `NeedsSetup` 使用中性待配置 Badge：
  - 不触发 Identity Banner。
  - 不自动展开 Issues Drawer。
  - 手动展开 Drawer 时仍可查看和定位，不隐藏 Validator 结果。
- 普通 Error 不使整个 Timeline 只读；本阶段尚未开放的 Authoring 操作仍因切片边界保持 disabled，而不是因为普通 Error。

### 2.4 固化 5R.0 基础

- 保持已验收的四象限布局、32/36 px Lane、Theme/Chrome、Scroll/Pan/Zoom/Scrub、Selection 与 Overlap Picker 的视觉和行为。
- 将 proof-only 文案更新为正式阶段状态：明确区分 `Identity Safe Mode` 与“后续切片尚未开放的 Authoring commands”。
- Shared Context 继续只保存会话状态；不得向 ActionAsset、EditorPrefs 或新 compiled asset 写入窗口状态。
- Validation 只在 Document 明确刷新或 Repair/Undo/Redo 后运行一次；Frame、Selection、Scroll、Zoom 和 Repaint 不重新验证。

## 3. Public Contracts

- `ActionAuthoringValidationIssue.AuthoringPath` 是新增的 Editor-only 只读合同；现有 `Code`、`EditorId`、`Message` 和 `IsValid` 语义不变。
- 保留 `ActionAuthoringIdentity.RepairInvalidIds(ActionAsset)` 入口，但其实现必须满足单次 Undo、预生成 replacement IDs 和数据不变合同。
- 不修改 Runtime public API、`ActionTimelineData` 序列化结构、GameplayItem 类型、ActionPlayer、Scheduler、ActorAnimation 或 Legacy 路由。
- 不开放任何除显式 Identity Repair 以外的资产写入口，不引入第三方包或测试专用 Runtime 类型。

## 4. Verification

### 4.1 静态与编译

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`。
- Stage 5R.1 修改范围 `git diff --check`。
- Unity Refresh 后 Console 0 compile error、0 USS parse error、0 UI Toolkit exception。
- 静态确认 Timeline/Details/Preview 除 Repair command 外仍不调用 Commands、SerializedProperty 写入或 `EditorUtility.SetDirty`。
- Unity Test Runner 不作为验收门槛。

### 4.2 Identity Matrix

- 分别验证 missing、malformed、duplicate，以及三者同时存在。
- Repair 前不稳定对象可见但不可进入稳定 Selection；合法唯一对象仍可只读选择。
- Repair 后所有 Segment、Lane、Item ID 均为唯一 32 字符 GUID。
- 对比 Repair 前后除 EditorId 外的序列化数据完全一致。
- 一次 Undo 恢复全部旧 ID，一次 Redo 恢复同一组新 ID。
- Inspector 与 Timeline 两个入口产生相同 Undo、dirty、Selection 和刷新结果。
- Save、重新导入、Domain Reload 后 ID 保持，不发生额外生成。

### 4.3 Validation Presentation

- IdentityBlock 只进入 Banner 和对应 issue/badge，不占满主工作区。
- InvalidConfig 显示 `Needs setup`，不自动展开 Drawer。
- Timing、Source、Reference、Root Motion 和 Animation overlap 显示 Error。
- 点击每个 issue 精确定位其 AuthoringPath；duplicate ID 不选择错误对象。
- Details 只显示 Primary Selection 的问题；多选不隐式批量编辑。
- Frame、Selection、Scroll、Zoom 操作不导致 Drawer/Foldout 状态重置或重复 Validation。

### 4.4 回归与资产边界

- 5R.0 的三种窗口尺寸、滚动同步、Zoom、Selection、Marquee 和 Overlap Picker 无退化。
- Repair 以外所有写入口仍不可达。
- 不提交人工验证使用的临时 ActionAsset、场景、Prefab 或 ProjectSettings 改动。
- Legacy Timeline、Sequence Editor 和双击路由保持原样。

## 5. Exit Criteria

Stage 5R.1 仅在以下条件全部满足后完成：

- 结构化 AuthoringPath 覆盖全部可定位到 Segment、Lane 或 Item 的 Validator issue；`MissingTimeline` 等真正全局问题明确留在 GlobalIssues。
- Identity Safe Mode 不误选、不静默修复；显式 Repair 满足单次 Undo 和数据不变合同。
- Compact Validation 不再产生全屏错误洪流，NeedsSetup 与 Error/IdentityBlock 的视觉含义清楚。
- 编译、scoped diff check 和 Unity Console 通过。
- 项目负责人完成真实 Unity Repair/Undo/Redo 与问题定位验收并明确接受。

验收前不进入 5R.2，也不恢复 Add、Delete、Move、Resize、Trim、字段编辑或 Clipboard。

## 6. Implementation Record — 2026-09-02

已实现：

- `ActionAuthoringValidationIssue` 增加结构化 `AuthoringPath`；AnimationSegment、GameplayLane、GameplayItem、null entry、Identity、Config、Root Motion 与 overlap 问题均由 Validator 遍历点直接填写路径。
- Editor presentation index 删除 issue 顺序/消息启发式绑定，直接按 `AuthoringPath` 和稳定 `EditorId` 建索引，并区分 `IdentityBlock`、`Error`、`NeedsSetup`。
- Timeline Identity Banner 显示 missing / malformed / duplicate 数量，并启用唯一资产写入口 `Repair IDs`。
- Timeline Issues Drawer 保持默认收起，Header 显示分类摘要；问题行显示精确路径，完整内容放入 tooltip；Item 使用单一局部 Badge，`NeedsSetup` 使用中性样式。
- Details 显示分类摘要与 Primary Selection 的路径化问题；所有字段仍为只读。
- ActionAsset Inspector 与 Timeline Banner 统一调用 `ActionV1EditorCommands.RepairEditorIds`。
- Repair 在 Undo 记录前预生成全部互异 GUID，只修改损坏/冲突 ID；成功后 dirty，当前 Action 的 Selection 原子重置为 Action，并发送一次合并的 Structure / Validation 刷新。
- 原有 `ActionAuthoringIdentity.RepairInvalidIds` 的 Undo / Redo 合同保留；补充了 Validator 路径合同断言。

自动验证证据：

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`：通过，0 warning / 0 error。
- Stage 5R.1 修改范围 `git diff --check`：通过；仅输出工作区既有 LF/CRLF 提示。
- 静态入口检查：Timeline / Details / Preview 中只有 Timeline 的 `RepairEditorIds` 可达；未接入 Add、Delete、Move、Resize、Trim、Rename、Mute、Config、Paste 或 Duplicate。

仍需项目负责人在 Unity 中验收：

- missing / malformed / duplicate 混合资产的 Banner 数量、路径定位与安全选择。
- Repair 后一次 Undo / Redo、相同新 ID 恢复、非 ID 数据不变和资产 dirty 状态。
- Timeline 与 Inspector 两个 Repair 入口一致；Console 0 compile error、0 USS parse error、0 UI Toolkit exception。
- `NeedsSetup`、普通 Error 与 IdentityBlock 的实际视觉密度和可辨识度。
