# CombatSample Action V1 — Stage 5 Handoff

> **Superseded for editor UX acceptance.** 本文记录第一次 Stage 5 实现，不代表当前编辑器已验收。后续实现以 Frozen Stage 5 Editor Redesign 与 Stage 5R.0 Interactive Shell 文档为权威；Stage 6 不得开始。

> 状态：**实现完成；Unity Editor 交互验收待项目工作站确认**
> 日期：2026-09-01
> 边界：仅新 V1 Authoring Editor；不迁移资产、不切换 Runtime

## 已交付

- 新增相互独立、可停靠的 `Action Timeline`、`Action Details`、`Action Preview`，共享 session-only CurrentAction、CurrentFrame、稳定 ID Selection、PreviewCharacter 和预览输入。
- Editor 直接修改 `ActionAsset.Timeline`；只读 Document 仅用于渲染与定位，不持久化、不建立 Compile/Save roundtrip。
- Timeline 支持固定 Animation Lane、Gameplay Lane 创建/重命名/重排/删除/Mute、七种 Item 创建、Single/Multi/Marquee、Point/Range/Group Move、跨 Lane Move、Range Resize、Animation Trim、整数 Frame scrub/zoom/playback，以及单事务 Undo。
- session clipboard 支持跨 ActionAsset 深拷贝，保留 Config 与 Unity 资源引用；Paste/Duplicate 生成新 EditorId，并按 Lane ID/唯一同名 Lane 映射。
- Details 提供 Action rules、Segment、Lane、Point/Range timing 与七种类型化 Config；Config 条件字段按 source/mode/toggle 即时显示。
- Timeline/Details 复用 `ActionAuthoringValidator` 展示 Issue；missing/malformed/duplicate ID 只能通过显式 Repair 命令修复。缺失或歧义 ID 的条目在修复前不可交互。
- PreviewCharacter 显式选择 Prefab 或场景对象，并只克隆到 `PreviewRenderUtility` 的隔离 Preview Scene。current-frame evaluator 固定执行 Animation Pose → baked RootMotion/path → SelfRotation → active HitBox gizmo；不调用 `ActionRuntime`，不执行 Damage/Impulse/Tag/Velocity history。
- Preview Target/Direction handles 为依赖 runtime context 的 SelfRotation 提供明确的预览输入；同 motion owner 的 active overlap 显示警告并确定性选择第一个 authored item。
- `ActionAsset` Inspector 的 V1 区域现在只提供 Open、Duration、Validation 和显式 ID Repair，不再把 raw Timeline 数组作为正常 Authoring 入口。

## 保持不变

- 未修改 `ActionPlayer`、`ActionStateManager`、`ActorSimulationRuntime`、`CombatSimulationDriver` 或正式 Legacy/Sequence 分派。
- 未改变 Action V1 Runtime API、Timeline 序列化结构、Scheduler、Gameplay receivers、ActorAnimation 或 ActorMotor ownership。
- 未迁移或创建正式 Action/Animation 资产；`ActionAsset` 双击仍按现有 Legacy/Sequence 路由，直到 Stage 7。
- 新 V1 Editor 不依赖旧 ActionSequence Document、Commands、Track 或 Clip 类型；Stage 7 删除 Legacy Editor 时不需要保留这些依赖。

## 验证证据

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Unity 2022 Editor 源编译 | Passed (focused) | 四个新增 Editor 源使用 Unity 2022.3 Roslyn、当前 Unity/Runtime/Editor 引用独立编译，0 error。 |
| 运行中 Unity 完整编译 | Pending final refresh | 运行中 Editor 的现有日志曾完成 Tundra build/domain reload；最后一轮 Stage 5 源修改晚于该日志，需项目工作站触发 Refresh 后确认。 |
| `dotnet build --no-restore` | Infrastructure blocked | Unity 生成的 `Temp/obj/Assembly-CSharp-Editor/project.assets.json` 缺失；没有 Restore 或改写生成目录。 |
| Static boundary | Passed | 新目录不引用 ActionSequence、ActionRuntime、ActionPlayer 或正式 Combat loop；`git diff --check` 对 Stage 5 源通过。 |
| Unity Test Runner | Not used | 按项目负责人要求，不作为 Stage 5 验收门槛。 |
| Windows UI automation | Unavailable | Computer Use native pipe 两次连接失败，未对 Unity UI 执行输入；真实交互矩阵保留为人工检查。 |

## 项目工作站人工验收

1. Unity Refresh 后确认 Console 没有新增 compile error；从 `Tools/Combat/Action V1` 打开三个窗口，并从 ActionAsset Inspector 打开 Timeline。
2. 对一个临时 ActionAsset 执行 Lane 与全部七种 Item 的创建、选择、Marquee、Move/Resize/Trim、跨 Lane Group Move、Mute、删除确认和 Undo/Redo。
3. 验证 Copy/Paste/Duplicate，同资产与两个具有同名 Lane 的 ActionAsset 之间都保留 Config/引用、Timing，并生成不同 EditorId。
4. 保存并 Domain Reload；确认 CurrentAction、CurrentFrame、Selection 和 PreviewCharacter 恢复，Clipboard 允许清空，资产数据不丢失且 ID 不被静默重建。
5. Preview 分别验证首段前 base pose、段间/末段 hold、Trim/PlayRate、baked RootMotion path、四种 SelfRotation 输入、HitBox bone gizmo、Muted 排除以及 overlap/缺失骨骼诊断。
6. 运行 `MiHoYo_Release`，确认旧 Action、Root Motion、HitStop、取消/切换无行为差量和新 Console 异常。

## Stage 6 前置条件

上述人工交互矩阵通过并由项目负责人接受后，Stage 5 Exit Criteria 才关闭。下一阶段只设计 Stage 6 Asset Migration：先 Dry Run/report，再就地写入同 GUID ActionAsset；正式 Runtime cutover 仍属于 Stage 7。
