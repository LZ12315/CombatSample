# CombatSample Action V1 — Stage 5R.4-C Timeline 交互一致性收口

> 状态：补完开发检查通过，待 Unity 人工验收
>
> 日期：2026-09-05
>
> 上位权威：`CombatSample_Action_V1_Stage_5_Editor_Redesign_Draft_2026-09-01_zh-CN.md`（Frozen）

## Summary

本切片不改变 Action V1 数据、Runtime 或三窗口布局。它收口 Stage 5R.4 与 5R.4-Z 中已发现的 Timeline 交互不一致：密集 Point 的可达性、视觉与命中优先级、Navigator 拖动映射、Pointer 手势隔离，以及 Ghost 与最终命令的判定来源。

## Implemented changes

- Point density group 改为 26 px 最大组跨度，不再以相邻 Point 链式吞并长序列。Primary Point 在密集组中恢复真实菱形和直接拖拽；聚合 marker 使用稳定中点，Selection 不会改变其位置。
- 聚合 marker 与 Primary 菱形冲突时让位；对应成员仍可从 Lane command menu 的 `Navigate dense Points` 到达。隐藏的聚合成员不参加普通 Pointer hit test，Marquee 仍按真实时间范围选择它们。
- Gameplay hit test 读取共享 presentation record：Point 使用菱形轮廓，Range 使用真实可见矩形；只有没有实际图形命中时才查询透明扩展区。多个实际图形命中时按真实 Hover / Primary / Selected / Authoring 绘制层级消歧，不再给予 Point 类型无条件优先级。
- Point 聚合只为真正的 Primary Point 暴露独立菱形；其他 Selected、Hover 和路径定位只改变反馈。密度 Marker 与 overlap Marker 按稳定次序避让 Primary 和已保留 Marker；Animation 与每个 Gameplay Header 均提供不依赖 `CanAuthor` 的密集/重叠内容导航。
- Navigator 在 Pointer Down 冻结 NavigationExtent、轨道宽度和 Visible Range。Thumb 与左右 Handle 使用生产 `ActionV1TimelineInteractionMath` 在冻结域内换算和钳制，释放后不扩域、不二次跳变；主视图平移仍可扩展下一次手势使用的导航域。
- Content ScrollView 明确隐藏原生 horizontal scroller；右侧原生 scroller 只用于 Lane 垂直滚动，底部 Time Range Navigator 是唯一横向时间控件。
- Move、Resize 和 Trim 在 Pointer Down 创建 `ActionV1TimelineOperationSnapshot`，Pointer Move 由同一快照求值并绘制 Ghost，Pointer Up 提交同一个候选结果。提交前核对目标身份、Timing、Lane 顺序、AnimationAsset / Clip / Clip length / SourceRange / PlayRate；外部变化会拒绝陈旧提交。零 Delta、Rejected 和取消不写 Undo/dirty。
- Move、Resize 和 Trim 期间隐藏参与操作的原始 Entry，只显示蓝色或拒绝态红色 Ghost；结束、取消和 Capture 丢失后恢复原 Entry，并重新应用 Point 聚合可见性，避免旧位置残留双影提示。
- 所有 Pointer 手势通过单一准入入口获得 owner/pointer/capture；第二个 Pointer Down、Timeline 命令、Repair 与菜单写操作不能覆盖活动手势。Header Resize 使用按下宽度和总 Delta，并允许自身引发的 viewport 变化继续持有 Capture；外部尺寸变化、Undo/Redo、失焦和 Capture 丢失仍会取消。

## Development verification evidence

- 编译基线：缺少 `Temp/obj/Assembly-CSharp-Editor/project.assets.json`，按批准计划执行 `dotnet restore Assembly-CSharp-Editor.csproj -v:q`，未编辑 Unity 生成的工程文件。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`：成功，`0 warning / 0 error`。生成工程明确包含 `ActionV1EditorCore.cs` 与 `ActionV1TimelineWindow.cs`。
- 生产逻辑断言：直接反射本次 build 的 `Assembly-CSharp-Editor.dll`，通过固定导航域 3 例、有界 Point 分组 1 例、可见图形/绘制层命中 2 例、Marker 避让 2 例。断言调用生产 `ActionV1TimelineInteractionMath`，没有复制算法或留下测试 harness。
- 修改文件空白检查通过；全工作树 `git diff --check` 仍报告用户已有 `New Sequence Action.asset` 与 `MiHoYo_Release.unity` 尾随空白，本阶段未修改这些资产或 Scene。
- 未修改 Runtime、Timeline 序列化结构、Validator、Details、Preview evaluator 或 Legacy 路由。
- Unity Test Runner 不是本阶段验收门槛。

## Manual acceptance gate

在不提交临时资产的前提下，检查：密集/同帧 Point 与 Range 的选择和直接拖动；Navigator Fit、缩放、边界与 Handle 的连续性；拖动中 F、Delete、Space、滚轮、Escape、失焦、窗口尺寸变化和 Undo/Redo；以及合法/非法 Move、Resize、Trim 与已有 Animation overlap 的一次性修复。

开发检查已通过；Unity 人工接受仍未声明。在用户明确接受前，仅修复 5R.4-C 范围内的问题，不进入 Stage 5R.5 Details leaf editing。

## Acceptance update · 2026-09-05

用户随后确认 5R.4-C 没有问题，并批准进入 Stage 5R.5。最后补充的拖动表现（操作期间隐藏原位置 Entry，只保留 Ghost）包含在该次接受中。本节取代上方“等待人工接受”的旧状态说明；历史验证记录保留。
