# ActionSequence 大迭代复盘与当前架构说明

> 状态：当前事实说明，不是未来实施计划
> 日期：2026-08-07
> 代码基线：`FrameWork` 分支，`9d568e97`（Stage 7 Runtime 集成测试提交）
> 覆盖范围：ActionSequence 数据模型、V2 编辑器、ActionPlayer Runtime 接入及兼容边界
> 当前架构细节：[ActionSequence 编辑器 V2 架构](ActionSequence_Editor_V2_Architecture_zh-CN.md)

## 1. 这份文档解决什么问题

这轮迭代跨越了数据模型、编辑器和运行时。单独看最后的代码，很容易知道“现在有什么”，却不知道：

- 为什么没有继续使用 Unity Timeline 作为战斗动作载体；
- 为什么早期 Prototype 没有继续修补，而是重新建设 V2；
- Stable ID、Serialized Document、Command 和 Validator 为什么是必要基础；
- V2 编辑器和 ActionPlayer Runtime 如何连接；
- 哪些目标已经完成，哪些仍然没有做；
- 下一步为什么不能只按编号机械地宣布 Stage 8。

本文按“问题 → 决策 → 实施 → 当前状态”的顺序复盘 Stage 0–7。它负责说明发生了什么以及为什么；V2 架构文档负责定义当前系统具体如何工作。

## 2. 一句话总结

Action 系统已经从“`ActionPlayer` 直接播放 Unity Timeline”演进为：

```text
ActionStateManager
        ↓
   ActionPlayer
     ↙      ↘
Timeline    Sequence
Session     Session
  ↓           ↓
Director   ActionSequenceRuntime
```

对上层而言，`ActionPlayer` 仍是唯一播放入口；对内容作者而言，ActionSequence 已经拥有正式的 UI Toolkit 固定帧编辑器；对旧资源而言，LegacyTimeline 仍然有效，不需要一次性迁移。

这轮工作的本质不是“做了一个自定义 Timeline 皮肤”，而是建立了一个不依赖 Timeline runtime 时序、可以逐步替换旧动作载体的完整基础设施。

## 3. 改造前的架构

改造前，战斗动作的主要链路是：

```text
ActionAsset
    └── TimelineAsset
            ↓
ActionStateManager
            ↓
ActionPlayer + PlayableDirector
            ↓
Timeline Track / Clip / PlayableBehaviour
```

这套方案的优势很明确：

- Unity Timeline 已经提供成熟的 Track、Clip、Ruler、Inspector 和拖动语言；
- 配置动画、HitBox、位移和 Tag 很直观；
- 团队不需要先建设编辑器就能制作动作。

问题同样明确：

- 战斗关键事件依赖 Timeline/PlayableGraph 的求值时序；
- 动作逻辑帧与 Unity 播放时间之间缺少单一、明确的固定帧事实；
- HitStop、取消窗口和跨帧推进很难完全建立在统一整数帧语义上；
- Timeline 的场景绑定与通用影视序列能力不是战斗动作真正需要的领域模型；
- 一旦运行时规则需要偏离 Timeline，编辑便利性和执行确定性会互相牵制。

因此我们没有否定 Timeline 的编辑经验，而是把它拆成两部分看待：

```text
保留：Timeline 的编辑语言
放弃：Timeline 作为 Sequence backend 的战斗运行时载体
```

## 4. 最初确定的产品语言

ActionSequence 的目标从一开始就不是通用 Sequencer，而是固定帧战斗动作编辑器。作者需要能直接回答四个问题：

1. 哪些行为会发生？
2. 它们发生在哪些整数帧？
3. 它们属于哪个执行通道？
4. Runtime 会以什么顺序执行？

由此确定了基本模型：

```text
ActionAsset
└── ActionSequenceData
    ├── Frame Rate / Duration
    └── Tracks[]
        └── Clips[]
```

核心规则包括：

- Track 是一级编辑单位，不是只用于着色的视觉行；
- 强类型 Track 决定允许创建哪些 Clip 类型；
- Clip 使用 `[startFrame, endFrame)` 半开区间；
- Frame 0 必须执行；
- Runtime 顺序固定为 `Phase → Track 顺序 → StartFrame → Track 内 Clip 顺序`；
- Mute 影响 Runtime，Lock 和 Collapse 只影响 Editor；
- Timeline 窗口负责结构和时间，Inspector 负责详细 Config；
- LegacyTimeline 与 Sequence 可以并存，迁移是渐进式的。

这些规则最初记录在 [ActionSequence Editor Design Spec](ActionSequence_Editor_Design_Spec.md) 中。该文档仍是产品语言来源，但其中部分早期“未来功能”已经在本轮后续阶段实现，不能再把它当作当前功能清单。

## 5. Prototype 带来的关键认识

早期 Prototype 证明了 `tracks → clips` 模型和固定帧 Runtime 可以工作，但也暴露出一个更大的问题：正式编辑器不能靠逐个补 UI Bug 建成。

当时的主要结构性问题包括：

- Track/Clip 选择依赖数组 Index 和 Property Path，重排或 Undo 后可能指向错误对象；
- `Normalize()` 同时承担初始化、迁移和修复，可能在加载或刷新时静默改变作者数据；
- Lock 只在部分按钮上生效，Inspector 或其他入口仍可能绕过；
- Timeline 窗口、Inspector 和数据修改各自直接操作序列化状态；
- 修改后刷新依赖 IMGUI 事件或鼠标进入窗口，失效边界不清楚；
- Track 类型反射、Clip 合法性、Undo 和 Dirty 缺少统一规则；
- 缩放、滚动、Ruler 和 Clip 几何没有共享的坐标模型；
- 新功能越多，局部修补之间的耦合越严重。

这些问题记录在 [ActionSequence Editor Implementation Audit](ActionSequence_Editor_Implementation_Audit_2026-08-02.md) 中。那份文档是 Prototype 的问题现场，不是当前缺陷列表；其中大多数 P0/P1 项已经由 V2 解决。

最终决定是：

- 冻结 Prototype；
- 使用 UI Toolkit 建设独立 V2；
- 先完成数据身份、序列化访问、命令和校验，再实现 Timeline UI；
- Prototype 只保留为诊断退路，不再承担产品演进。

## 6. Stage 0–7 的实施过程

Stage 是架构推进顺序，不与 Git 提交一一对应。部分 Stage 在同一个实现提交中完成，测试和校验又通过独立提交补齐。

### Stage 0：冻结原型并建立独立入口

这一阶段做了两件事：

- 把旧 `ActionSequenceEditorWindow` 明确标记为 Prototype；
- 建立可以与 Prototype 同时打开的 V2 窗口入口。

意义在于停止继续扩大旧窗口的职责，让新架构可以在不破坏旧工作流的情况下独立成长。

### Stage 1：Stable Identity、Document、Commands 与 Validator

这是 V2 最关键的基础阶段。

Track 和 Clip 获得隐藏、持久化的 `editorId`。选择不再把数组位置当作对象身份；插入、重排、Undo/Redo 和 Domain Reload 后，可以重新按 ID 解析同一个对象。

随后建立了四个边界：

```text
ActionSequenceSerializedDocument
    统一读取 SerializedObject，并按 ID 解析数据

ActionSequenceEditorCommands
    统一执行所有持久化修改

ActionSequenceEditorChangeSet
    明确说明 Structure / Content / Timing / Validation 哪一类发生变化

ActionSequenceValidator
    只读报告问题，不在校验时修改数据
```

这一步同时收窄了 `Normalize()`：

- 不再自动补默认 Track；
- 不再迁移 Legacy Clip；
- 不再删除 Null 或非法对象；
- 不再重排 Track；
- 不再 Clamp 作者的 Clip Timing。

需要改变结构的数据修复必须通过显式 Command，并产生清楚的一条 Undo。

### Stage 2：UI Toolkit 外壳与只读 Timeline

V2 使用 UXML、USS 和 C# `VisualElement` 建立正式窗口。

这一阶段完成：

- Toolbar、Track Header、Lane、Ruler、Grid、Status Bar；
- 两种 Sequence Target 的读取；
- Fixed/Auto Duration 的工作区计算；
- 横向 Zoom、Pan、Scroll 和 Fit；
- Header 与 Lane 的纵向滚动同步；
- 深浅主题和窄窗口布局基础；
- 使用 Stable ID 进行 Track/Clip View Reconcile。

Ruler 和 Grid 使用 `generateVisualContent` 绘制，不为每一帧创建 `VisualElement`。Timeline 全部几何只通过一个 `ActionSequenceTimelineTransform` 转换，避免 Ruler、Clip 和播放头各自维护坐标。

### Stage 3：Selection、Inspector 与非拖拽编辑闭环

V2 接入 Stable ID Selection Bridge，并让自定义 Inspector 根据当前选择显示三种模式：

- Sequence 设置；
- Track 设置；
- Clip Timing 和类型专属 Config。

Track 和 Clip 仍是 SerializeReference 普通对象，不是独立 `UnityEngine.Object`。Unity 选择的是所属 Asset，Inspector 再通过 Stable ID 找到被选中的 managed reference。

这一阶段同时完成：

- Add/Delete/Rename/Reorder Track；
- Add/Delete Clip；
- Mute、Lock、Collapse；
- 按 Track 类型过滤 Add Clip 菜单；
- 非空 Track 删除确认；
- Inspector 修改即时通知 Timeline；
- Undo/Redo 后重新解析 Selection。

至此，不使用拖动也可以完成完整 ActionSequence 配置。

### Stage 4：时间交互

这一阶段让 Timeline 从“可配置”变成“可以直接编辑时间关系”：

- Ruler 点击和拖动播放头；
- 编辑器时间游标 Play/Pause/Stop；
- 同 Track Clip Move；
- 左右 Resize；
- 整数帧吸附；
- Fixed Duration 边界限制；
- Auto Duration 工作区向右扩展；
- 鼠标中心缩放、Frame Clip/Track/All；
- Delete、Frame、Lock、Mute 和逐帧移动快捷键。

拖动期间只更新窗口内预览 Geometry。Pointer Up 时才调用一次 `SetClipTiming()`，因此一个完整手势只产生一条 Undo；Escape 或 Pointer Capture 丢失会取消预览，不修改 Asset。

### Stage 5：Validation 与产品化打磨

Validator 的结果被接入实际界面：

- Track/Clip 行内 Issue Badge；
- Status Bar 摘要；
- Issues 面板；
- 显式 Repair 命令；
- Missing managed-reference type 的错误占位；
- 非法或缺失数据继续可见，不在绘制时删除。

这一阶段的核心结果不是“多了几个警告图标”，而是确立了数据安全原则：打开窗口、刷新、选择和校验都不能静默改写用户资产。

### Stage 6：V2 成为正式编辑入口

完成稳定观察后，正常入口切换到 V2：

- 双击 Sequence backend `ActionAsset` 打开正式 Action Sequence Editor；
- 双击独立 `ActionSequenceAsset` 同样打开 V2；
- Inspector 只保留一个正式编辑器按钮；
- LegacyTimeline 继续进入 Unity Timeline；
- Prototype 移到 `Tools/Combat/Diagnostics/Action Sequence Prototype`。

内部类型名 `ActionSequenceEditorWindowV2` 暂时保留，以避免无价值的布局和类型迁移；用户界面不再显示 V2 或 Preview 文案。

### Stage 7：ActionPlayer Runtime 接入

Stage 7 改变了 Action 播放架构，但保留了上层 API。

`ActionPlayer` 从直接持有全部 Timeline 播放细节，变成 backend-neutral 播放协调器。内部新增统一的 `IActionPlaybackSession`，并分成：

- `TimelineActionPlaybackSession`：封装原 PlayableDirector 行为；
- `SequenceActionPlaybackSession`：持有独立 `ActionSequenceRuntime` 和 Context。

Sequence 播放语义包括：

- Begin 时立即执行逻辑 Frame 0；
- `Update()` 使用 `Time.deltaTime` 驱动固定帧累加器；
- 一次更新跨过多帧时，逐帧执行而不跳过；
- Pause 停止推进，但不退出活动 Clip；
- `PlaybackSpeed = BaseSpeed × ExternalSpeedModifiers`；
- 速度为 0 时逻辑帧冻结，因此现有 HitStop token 可以作用于 Sequence；
- 自然完成时活动 Clip 以 `completed=true` 退出；
- Stop、切招或 Disable 时以 `completed=false` 退出；
- Loop 保留同一个 `ActionInstance` 和启动 Context，但创建全新的 Clip Runtime；
- Runtime 初始化诊断只读，不修改 Asset；
- Session 异常会清理 Action，并最多发送一次 Interrupted。

LegacyTimeline 的播放、Pause、速度、Loop、停止与意外 Director Stop 语义被保留在 Timeline Session 中。`ActionStateManager`、CancelWindow、ActionSpeedEffect 和 `ActionPlayer` 的公共调用方式不需要区分后端。

## 7. 当前数据架构

当前作者数据链为：

```text
ActionAsset
├── PlaybackBackend
├── TimelineAsset                 # LegacyTimeline 使用
└── ActionSequenceData            # Sequence 使用
    ├── FrameRate
    ├── DurationMode
    ├── Fixed Duration
    ├── Tracks[] : SerializeReference
    │   ├── Track Editor ID
    │   ├── DisplayName / Mute / Lock / Collapse
    │   └── Clips[] : SerializeReference
    │       ├── Clip Editor ID
    │       ├── StartFrame / EndFrame
    │       └── 类型专属 Config
    └── Legacy Clips[]            # 只用于兼容、诊断和显式迁移
```

内置 Track/Clip 关系为：

| Track | Phase | 合法 Clip |
| --- | --- | --- |
| State Track | State | Tag Clip |
| Animation Track | Animation | Animancer Clip |
| Motion Track | Motion | Impulse Clip |
| HitBox Track | HitBox | HitBox Clip |
| Cleanup Track | Cleanup | 当前首版无内置 Clip |

Editor ID 只用于编辑器身份，不参与 Runtime 排序或行为。Runtime 仍以 Phase、序列化 Track 顺序、StartFrame 和 Track 内 Clip 顺序作为确定性执行依据。

## 8. 当前编辑器架构

当前编辑器的数据流是：

```text
ActionAsset / ActionSequenceAsset
              ↓
ActionSequenceSerializedDocument
              ↓
    ┌─────────┴──────────┐
Commands             Validator
    ↓                    ↓
ChangeSet / Result   Validation Issues
    └─────────┬──────────┘
              ↓
ActionSequenceEditorState
              ↓
UI Toolkit Timeline + Inspector
```

各层职责如下：

- Domain：保存运行时可用的 Sequence、Track 和 Clip 数据；
- Serialized Document：统一解析两种 Target，不让 View 缓存长期 `SerializedProperty`；
- Command：唯一的持久化修改入口，负责合法性、Lock、Undo、Apply 和 Dirty；
- Validator：只读报告 Missing ID、重复 ID、Missing Type、非法类型、Phase 和 Timing 等问题；
- Editor State：保存每个窗口的选择、播放头、Zoom、Scroll 和临时拖动预览；
- View：显示数据并发布用户意图，不直接写 Asset；
- Selection Bridge：把 Unity Asset Selection 与 managed-reference Stable ID 选择连接起来；
- Inspector：重新按 ID 解析当前 Track/Clip，并编辑对应配置。

刷新也不再只有一个“全部重建”入口：

| 变化类型 | 主要刷新范围 |
| --- | --- |
| Target | 重建 Document 和完整 ViewModel |
| Structure | Reconcile Track/Clip View，重新校验 Selection |
| Content | 更新 Label、Style 和 Inspector |
| Timing | 更新 Clip Geometry、Duration 和 Ruler |
| Viewport | 更新 Transform，重绘 Canvas |
| Selection | 只更新高亮和 Inspector Context |
| Playback | 只更新编辑器播放头 |

## 9. 当前运行时架构

`ActionStateManager` 仍负责动作候选与仲裁：

```text
Neutral / CancelRule / Event / External Request
                    ↓
            Entry Conditions
                    ↓
          Priority Layer / Value
                    ↓
              ActionPlayer
```

`ActionPlayer` 负责统一生命周期和公共状态：

- `CurrentAction`；
- `CurrentFrame`；
- `CurrentFrameRate`；
- `TotalFrames`；
- Base Speed 与外部速度 modifier；
- Finished/Interrupted 事件；
- Stop、Disable、Loop 和异常收口。

具体时间载体由 Session 负责。Sequence Session 创建的 `ActionSequenceRuntime` 只属于本次播放；Loop 和下一次 Begin 都会创建新的 Clip Runtime，不共享活动状态。

Sequence Runtime 初始化时：

1. 按 Track/Clip 序列化顺序读取数据；
2. 跳过 Muted Track；
3. 校验 Phase 和 AllowedClipTypes；
4. 为非法 Timing 计算临时安全区间，不回写定义；
5. 对 Fixed Duration 外内容跳过或临时截断并记录诊断；
6. 为 Legacy Clip 创建只读兼容投影；
7. 按确定性顺序建立 Runtime Record。

每个逻辑帧的顺序保持为：

```text
退出本帧结束的 Clip
        ↓
进入本帧开始的 Clip
        ↓
Tick 当前活动 Clip
```

## 10. 新旧架构对比

| 维度 | 改造前 | 当前 |
| --- | --- | --- |
| 主要动作载体 | Unity Timeline | LegacyTimeline 与 ActionSequence 双后端 |
| 上层播放入口 | ActionPlayer | 仍是 ActionPlayer，公共 API 不变 |
| 时间事实 | Director 时间换算帧 | Sequence 使用固定帧累加器 |
| 编辑模型 | Timeline Track/Clip | 强类型 Track + SerializeReference Clip |
| Track 合法性 | 由 Timeline Track 类型决定 | `AllowedClipTypes` + Command 双重约束 |
| 对象身份 | Timeline Unity Object/Subasset | 持久 Stable Editor ID |
| 选择 | Unity 原生 Timeline Selection | 所属 Asset + Stable ID Selection Bridge |
| 持久化修改 | 多个 UI 入口直接修改 | 统一 Command/Result/ChangeSet |
| Undo | 依赖各入口正确实现 | 每个成功命令一条明确 Undo |
| Lock | Timeline 自带 | Window、Shortcut、Inspector、Command 统一检查 |
| 校验 | 依赖 Timeline/自定义逻辑 | 只读 Validator + 显式 Repair |
| 刷新 | IMGUI/Repaint 与局部事件 | 明确的 Structure/Content/Timing 等失效类型 |
| Runtime 数据修复 | 曾依赖 Normalize | Runtime 只做临时安全投影，不改 Asset |
| HitStop | 影响 Director 速度 | 统一影响 Timeline/Sequence Session 速度 |
| 兼容策略 | 只有 Timeline | 旧 Timeline 可长期运行，Sequence 渐进使用 |

## 11. 当前已经具备的能力

### 11.1 内容制作

- 创建 Sequence ActionAsset 和独立 ActionSequenceAsset；
- 创建、删除、重命名和同 Phase 排序 Track；
- Mute、Lock 和 Collapse；
- 只创建当前 Track 允许的 Clip；
- 在 Timeline 选择并在 Inspector 编辑 Track/Clip；
- Move、左右 Resize 和整数帧吸附；
- Fixed/Auto Duration；
- Zoom、Pan、Scroll、Fit 和 Frame；
- 播放头 Scrub 与纯编辑器时间播放；
- 快捷键；
- Undo/Redo；
- Missing Type 和非法数据可见；
- 显式 Identity、Legacy 和 Phase Order Repair。

### 11.2 数据安全

- 删除到零 Track 后保持零 Track；
- 打开、选择、缩放、滚动和校验不会修改资产；
- Stable ID 在重排、保存和重导入后保持；
- Duplicate/Malformed ID 不会被错误解析成第一个对象；
- Validator 不清理 Missing managed reference；
- Runtime 初始化不改变 Track/Clip 数量、顺序或 Timing；
- Locked Track 的持久化操作由 Command 层最终拒绝。

### 11.3 运行时

- Sequence 可以从现有 ActionStateManager 路径进入 ActionPlayer；
- Animancer、Tag、Impulse 和 HitBox Clip 已有 Runtime 实现；
- Frame 0、半开区间和确定性排序语义已落地；
- Pause、速度 modifier、HitStop 冻结、Stop、切招、Loop 和 Disable 有统一生命周期；
- LegacyTimeline 保持兼容；
- 非法 Sequence 的合法部分可以继续执行，并输出聚合诊断。

## 12. 验证证据与边界

### 12.1 已有自动测试覆盖

当前仓库包含 84 个聚焦的 ActionSequence/ActionPlayer NUnit `[Test]` 用例，主要分布在：

- Stable Identity 与 Repair；
- Serialized Document 与 ID 解析；
- Command、Lock、Undo/Redo 与 Selection Suggestion；
- Validator 和 Validation Presentation；
- Timeline Transform、Editor State 和 Timing 手势计算；
- Track/Clip View Reconcile；
- 固定帧 Runtime 排序、Frame 0、半开区间、Mute、Legacy 和诊断；
- ActionPlayer Sequence Begin、完成延迟与 Stop 清理。

这些测试代码已经提交，但当前 shell 环境未能成功启动 Unity Editor/batchmode，因此不能把它们描述为“本次会话已全部运行通过”。Unity 2022.3.62f3 内的完整 EditMode 运行仍是正式发布前的验证项。

### 12.2 已完成的手动验证

本轮实际手动检查已经确认：

- V2 Timeline 可以打开、显示和编辑 Track/Clip；
- Selection 与 Inspector 可以工作，字段修改能够即时同步；
- Zoom、Pan、Fit、窄窗口布局和滚动同步可以正常使用；
- Clip Move/Resize、播放头与常用操作基本正常；
- V2 已成为正式入口，LegacyTimeline 路由仍然保留；
- 原有敌人 Timeline Action 在 Stage 7 重构后仍能播放；
- Sequence Action 已能通过正式 Runtime 路径播放。

### 12.3 仍需系统验证的风险

目前没有发现阻塞问题，但以下链路还没有形成可重复的完整验收记录：

- Animancer Clip 的真实动画采样、过渡和退出；
- Sequence HitBox 的命中、去重、效果触发与中断清理；
- HitStop/ActionSpeedEffect 对动作帧、动画和 ActorMotor 的同步冻结；
- Sequence → Timeline 与 Timeline → Sequence 的复杂 CancelWindow 切换；
- Loop 多轮后 Tag、Hit Target 和 Clip Runtime 是否完全不累积；
- 播放中 Disable Actor 后运动配置、重力、Tag 和活动 Clip 的完整恢复；
- 30 Track / 300 Clip 压力数据下的编辑器性能；
- Domain Reload、高 DPI、Missing Type 资产恢复等完整手动矩阵。

这些是风险清单，不代表已经确认存在缺陷。

## 13. 明确尚未完成的内容

以下功能没有包含在 Stage 0–7：

- 编辑器内真实驱动角色、Animancer、Force 或 HitBox 的预览；
- 多选、框选、复制、粘贴和 Duplicate；
- Clip 跨 Track 拖动；
- Track 自由拖拽排序和 Group Track；
- Clip Blend、Curve、Ease 或重叠冲突语义；
- Marker、Signal 和嵌套 Sequence；
- 自定义 Track 高度；
- Timeline → Sequence 自动批量迁移工具；
- Action 仲裁过程和活动 Clip 的运行时可视化；
- 将独立 ActionSequenceAsset 直接加入 ActionPlayer 公共 API；
- 删除 Prototype；
- 删除 LegacyTimeline backend。

这些项目不是遗漏的尾巴，而是需要结合真实生产需求重新排序的后续能力。

## 14. 下一阶段的三个合理方向

现有批准架构到 Stage 7 结束，目前没有已经批准的 Stage 8。下一步应由真实需求决定，而不是继续按编号自动扩张。

### 方向 A：真实动作垂直迁移

选择一个包含动画、位移、HitBox、HitStop、Tag 和 CancelWindow 的代表动作，从 Timeline 完整迁移到 Sequence。

它可以最直接回答：

- 当前 Clip 类型是否足够；
- 制作效率的真正瓶颈在哪里；
- Runtime 生命周期是否经得住完整战斗链；
- 哪些编辑器功能确实值得优先建设。

### 方向 B：运行时可观察性

建设 Action Runtime/仲裁调试器，显示：

- 当前 Action、Backend、Frame、Duration 和速度；
- Sequence 活动 Clip 与最近 Enter/Exit；
- 当前打开的 CancelWindow；
- 候选 Action、EntryCondition 结果、优先级和最终胜者；
- HitStop 速度 modifier 与运行时诊断。

这个方向适合解决“动作为什么没切换、为什么某个 Clip 没执行”的排查成本。

### 方向 C：生产编辑效率

根据真实配招过程补充：

- Duplicate；
- Copy/Paste；
- 多选；
- 跨兼容 Track 移动；
- Track 拖拽排序；
- 更强的搜索、筛选和批量 Timing 操作。

这个方向应由重复劳动证据驱动，避免再次凭想象堆积编辑器功能。

在做出下一阶段决定前，建议先完成至少一个真实动作的端到端制作与运行记录。它不是为了证明“Sequence 能播放”，而是为了找出下一阶段最有价值的问题。

## 15. 文档阅读关系

建议按以下顺序理解本轮工作：

1. 本文：了解发生了什么、为什么，以及当前完成度；
2. [ActionSequence 编辑器 V2 架构](ActionSequence_Editor_V2_Architecture_zh-CN.md)：查询当前详细架构和约束；
3. [ActionSequence Editor Design Spec](ActionSequence_Editor_Design_Spec.md)：理解最初产品语言；
4. [ActionSequence Editor Implementation Audit](ActionSequence_Editor_Implementation_Audit_2026-08-02.md)：了解 Prototype 为什么被替换；
5. [帧表迁移完整落地方案（历史草案）](../Proposals/帧表迁移完整落地方案_历史草案.md)：只作为早期思路背景，不作为当前实现依据。

## 16. 对后续工作的约束

任何进入 ActionSequence 的新功能仍应回答以下问题：

1. 它属于 Asset 数据还是窗口状态？
2. 哪个 Command 负责持久化？
3. Stable Identity 是什么？
4. 它发布哪一种 ChangeSet/Invalidation？
5. Undo/Redo 和 Lock 语义是什么？
6. Inspector 如何表达它？
7. Runtime 是否依赖它？
8. 哪个测试或垂直用例证明它正确？

这套约束是本轮迭代最重要的产物之一。它让 ActionSequence 后续可以继续成长，同时避免重新退回“改一步、补一步”的原型状态。
