# CombatSample Action V1 编辑器当前架构

> 状态：当前实现说明
>
> 代码基线：`FrameWork` 工作区，2026-09-14 验证
>
> 范围：Action V1 的 Timeline、Details、Preview 与共享编辑会话
>
> 权威：本文记录当前代码事实；旧冻结报告和实施计划仅作历史参考。

## 1. 结论

Action V1 编辑器现在采用一条可追踪的主链路：`ActionAsset` 保存真实数据，具体编辑操作负责校验和写入，`ActionV1EditorContext` 发布一份已整理好的会话状态，三个窗口只读取并显示这份状态。窗口刷新不再反向修正共享选择或时间。

```text
用户输入
  ↓
Commands / Timeline Operation
  ├─ 检查目标、写入许可和规则
  ├─ 记录 Undo
  └─ 修改 ActionAsset
          ↓
ActionV1EditorContext.ApplyAssetChange
  ├─ 生成新的 Document
  ├─ 校正选择
  ├─ 限制连续时间并派生当前帧
  └─ 发布一条包含最终状态的变更
          ↓
Timeline / Details / Preview 更新显示或求值
```

时间、选择、播放和相机不经过资产重建链路：

```text
播放或跳帧 → Context 时间 → Timeline 游标 + Preview 求值
选择变化     → Context 选择 → Timeline 高亮 + Details + Preview Overlay
相机操作     → PreviewWindow → Renderer.Draw
```

## 2. 职责与边界

| 部分 | 当前职责 | 明确边界 |
|---|---|---|
| `ActionAsset` | 保存 Action Timeline、动画段和 Gameplay 数据 | 不保存窗口状态、预览实例或编辑缓存 |
| Commands / Timeline Operation | 校验操作、记录 Undo、修改资产并提交实际变化 | 不直接刷新窗口，不发布第二套事件 |
| `ActionV1EditorContext` | 保存当前 Action、唯一 Document、选择、连续时间、角色覆盖和变更版本 | 不绘制 UI，不实现重叠或裁剪规则 |
| `ActionV1EditorDocument` | 将资产整理为只读集合、身份索引、校验索引和重叠区 | 不写资产，不改变会话状态 |
| Timeline | 组合视图、接收输入、发起命令；作为播放控制入口 | 不持有第二份 Document，不在刷新中校正 Context |
| Details | 显示选择，普通属性通过原生绑定写入，时间草稿显式 Apply | 不自行 Build Document，不反射访问运行时私有字段 |
| PreviewWindow | 从 Context 组装明确输入，处理相机和窗口生命周期 | 不选择 Action，不控制播放，不模拟 Target/Direction |
| Preview Renderer | 管理 PreviewRenderUtility、角色、Graph、Mesh、Material；分别求值和绘制 | 不读取全局 Context，不执行 gameplay |
| SpatialEvaluator | 确定性累积水平 RootMotion | 不处理 SelfRotation，不成为 gameplay replay |
| EditorPlayback | 推进共享连续时间并发布播放状态 | Timeline 是用户控制入口；其他模块只能因生命周期停止 |

## 3. 代码位置

| 文件 | 内容 |
|---|---|
| `ActionV1EditorCore.cs` | 共享会话、变更信息、Unity 外部事件适配、SerializedProperty 定位 |
| `ActionV1EditorDocument.cs` | Document、校验索引、重叠索引 |
| `ActionV1EditorOperations.cs` | 交互锁、时间操作输入、快照、规则与最终提交 |
| `ActionV1EditorCommands.cs` | 创建、删除、粘贴、普通结构编辑及剪贴板 |
| `ActionV1EditorPresentation.cs` | 共享主题、视口、几何和窗口控件 |
| `ActionV1EditorPlayback.cs` | 编辑器播放时钟 |
| `ActionV1TimelineWindow.cs` | Timeline UI 和输入分发 |
| `ActionV1DetailsWindow.cs` | Details UI、绑定与时间草稿 |
| `ActionV1PreviewWindow.cs` | Preview 输入、相机、Renderer 和资源生命周期 |
| `ActionV1PreviewSpatialEvaluator.cs` | RootMotion 空间求值缓存 |

这些拆分是代码职责的整理，没有增加 SessionManager、全局事件总线或通用命令对象框架。

## 4. 共享会话和通知

### 4.1 Document 生命周期

- Context 持有当前 Action 的唯一已发布 Document。
- 资产内容成功修改后完整生成新 Document，再替换旧引用并增加 `DocumentVersion`。
- 时间、选择、播放和相机变化不重建 Document。
- Timeline 与 Details 只读取 Context 的 Document。
- Commands 若校验非当前资产，可创建临时 Document；它不能替换当前会话状态。
- Document、校验索引和重叠索引以只读集合发布。条目保留源对象引用，只用于定位和显示；窗口不得通过该引用直接写入。

### 4.2 一次资产变化的顺序

`ApplyAssetChange` 按以下顺序完成全部共享状态，再发布通知：

1. 确认变化目标仍属于当前会话。
2. Structure、Timing、Content 或 Presentation 变化时生成并替换 Document；仅 PreviewResources 变化不重建 Document。
3. 按 EditorId 保留有效选择；删除失效项；必要时回到 Action 选择。
4. 将连续时间限制到新时长，保留合法小数与 Duration 终点。
5. 从连续时间派生 `CurrentFrame`。
6. 发布一条 `ActionV1EditorChange`。

新增、粘贴和删除可把替换选择随同提交，观察者不会见到“数据已变但选择尚未更新”的中间状态。

### 4.3 变更信息

每条通知携带：目标 Action、变化标记、Document 版本和来源。当前来源包括会话、命令、绑定、Undo/Redo、项目资源变化、ObjectChange 和播放。

主要变化标记的含义：

| 标记 | 含义 |
|---|---|
| Context | Action 切换或会话重建 |
| Structure / Timing | 结构、时间范围或布局变化 |
| Content / Validation / Presentation | 配置、校验或显示信息变化 |
| Selection | 共享选择变化 |
| Frame / PreviewPosition | 离散帧或连续预览位置变化 |
| PreviewCharacter / PreviewResources | 角色选择或预览依赖变化 |
| Playback | 播放状态变化 |

窗口处理同一条通知中的全部相关标记。窗口刷新不会调用 Context 的状态校正入口。

### 4.4 Unity 外部变化

`ActionV1EditorEventAdapter` 集中接入 Undo/Redo、`projectChanged` 和 `ObjectChangeEvents`。同一轮回调经一个 `delayCall` 合并，处理前核对 Action 与会话代次；Action 已切换的旧回调会被丢弃。

Context 保存一份按用途拆开的观察状态。Unity 报告外部变化时用它分类；Command 提交前也比较一次，以合并尚未来得及发布的真实变化：

- 条目身份、顺序和类型归为 Structure。
- 起点、时长和动画 SourceWindow 归为 Timing。
- 名称归为 Presentation；静音同时归为 Content 与 Presentation。
- Gameplay Config、Action 配置、AnimationAsset、Clip、Rig 烘焙设置和完整 RootMotion 采样数据归为 Content。
- 当前解析出的 Preview Prefab 及其资源依赖归为 PreviewResources。

因此，名称变化只刷新显示，不使 Preview 求值缓存失效或触发重绘；RootMotion 配置与任一采样变化会使视觉数据失效；Rig 烘焙设置变化会刷新校验但保留 Preview 实例；Preview Prefab 变化只重建 Preview 资源，不重建 Document。已由 Details 绑定写入但尚未发布的变化会合并进紧随其后的 Command 或外部回调；如果绑定跟踪回调更晚到达，观察状态会识别它已经处理，不再重复发布。

这里没有持续 update 轮询。Undo/Redo 和相关结构/时间变化会停止播放；无关资产变化不改变当前选择、时间或 Preview 实例。

切换 Preview Character 也通过同一个提交入口合并待处理变化，不能直接覆盖整份观察基线。只有角色变化时不重建 Document；同时存在名称、时间或内容修改时，先更新 Document 和共享状态，再把这些变化与角色变化一并发布。后续重复绑定或外部回调不再发布同一份数据。

外部刷新直接进入 `ApplyAssetChange`，由它完成一次 Capture、比较、状态更新和发布，不在 `RefreshExternal` 中重复扫描。没有实际数据变化的 Undo 回调不会停止播放。

## 5. 正式写入入口

所有编辑遵守同一约定：

```text
目标仍有效
  → 当前允许写入
  → 操作合法
  → Undo.RecordObject
  → 修改 ActionAsset
  → Context.ApplyAssetChange
```

被拒绝或结果无变化的操作不会创建 Undo、标脏或发布数据变化。普通属性继续使用 Unity 原生保存方式，不自动保存资产。

### 5.1 时间操作

Timeline 拖动和 Details 时间 Apply 共用“快照 → 候选 → 最终提交”规则：

- 快照记录会话 Action、轨道顺序、所有相关动画段和 GameplayItem 的身份与时间源值。
- `ActionV1TimelineOperationInput` 明确记录移动/裁剪增量或绝对时间草稿。
- 候选结果只用于 UI 反馈。
- 最终提交先确认源数据仍与快照一致，再用同一个操作输入重新求值并一次性写入。
- 候选时长与资产时长复用 AnimationSegment 的同一内部计算，避免浮点中间舍入导致一帧偏差；空 GameplayItem 槽位保留在快照结构比较中，但不占用时间区间。
- 目标删除、轨道重排、相关时间或动画源变化会使旧操作过期；普通名称变化不会。
- Timeline 开始拖动时按“取得交互锁 → 发布已完成的绑定变化 → 重新读取 Document → 按 EditorId 解析目标 → 捕获快照 → 捕获指针”的顺序启动，快照不会落后于刚完成的 Details 写入。

正式边界规则仍是：同轨 Clip 和 Point 不得重叠；半开区间相邻合法；跨轨允许重叠。创建、移动、裁剪和粘贴复用同一组重叠与动画时长规则。

### 5.2 Details

- 时间与动画源使用草稿、候选反馈和显式 Apply。
- 普通属性继续使用 `SerializedProperty` / `PropertyField`。
- 交互锁变化会立即禁用或恢复编辑区域。
- 原生绑定按字段语义报告 Presentation 或 Content，而不是一律报告为结构变化；同批标记由 Context 合并和去重。
- 依赖普通属性的条件区块只在新 Document 已发布后刷新，避免控件读取旧派生状态。
- 普通绑定修改保留控件；其他内容通知必要时重建页面，但保留 Action、主选择和源快照仍有效的时间草稿。真正过期的未提交草稿会提示；Action 或主选择变化会丢弃草稿。
- AnimationAsset 改名走 Presentation，并更新 Timeline 内容标签；删除空条目的最终选择随同数据提交发布。当前 Action 被销毁时，由会话统一清空和停播。
- CancelRule 增删重排和缺失 Config 补全通过具体 Commands 完成；窗口不使用反射访问 `_config` 或 `_cancelRules`。

## 6. Preview 求值与绘制

PreviewWindow 从 Context 读取 Action、连续时间、CurrentFrame、选择、角色覆盖和相关版本，组装 `ActionV1PreviewInput`。Renderer 和 SpatialEvaluator 只接收明确参数，不读取 `ActionV1EditorContext.Shared`。

```text
Context change
  ↓
PreviewWindow 生成 PreviewInput
  ├─ Evaluate：Pose、RootMotion 位置、当前 HitBox
  └─ Draw：相机、地面、角色、轨迹与 Overlay
```

更新规则：

| 变化 | 实际工作 |
|---|---|
| 连续时间 | 更新 Pose、当前位置和当前 HitBox |
| 动画或 RootMotion 数据 | 清除视觉求值缓存，在当前时间重新求值 |
| 选择 | 更新选中 Overlay，不重新累积 RootMotion |
| 相机或窗口大小 | 更新投影和纹理后重绘，不重新采样动画 |
| 角色或 Prefab 依赖 | 重建角色实例、Graph 和相关缓存 |
| 名称等纯 Presentation | 不使求值缓存失效，不请求 Preview 重绘 |
| 无变化 | 不创建资源、不求值、不请求持续重绘 |

Pose 使用共享的 `ActionAnimationSampleResolver`。RootMotion 使用相同的 SourceWindow 与 TranslationDomain 规则；SpatialEvaluator 的直接跳转、倒退和顺序播放产生相同位置。

Preview 的边界保持不变：不求值 SelfRotation，不模拟 Target/Direction，不运行 ActorMotor 或 GameplayItem，不提供独立 Action 和播放控制。PreviewRenderUtility、PlayableGraph、自建 Mesh 与 Material 都由 Renderer 单独拥有，清理入口可重复调用。

## 7. 生命周期

- 程序集重载前停止编辑器播放并取消事件订阅。
- Action 切换使上一会话代次的延迟回调失效。
- 进入 Play Mode 时停止编辑器播放，窗口取消交互，Preview 释放原生资源。
- Preview 静止时没有额外 update 或持续 Repaint。
- Camera、RenderTexture 仍由 PreviewRenderUtility 管理；角色、Graph、临时 Mesh 和 Material 由 Renderer 创建和释放。

## 8. 验证记录

### 8.1 自动验证

| 项目 | 结果 |
|---|---|
| `Assembly-CSharp-Editor.csproj` 编译 | 通过，0 错误；51 条为现有包/项目警告 |
| `ActionV1PreviewArchitectureTests` EditMode 契约测试 | 43/43 通过（2026-09-15） |
| 会话原子通知、Document 不必要重建 | 已由契约测试覆盖 |
| 同批绑定通知合并、旧 Action 延迟回调丢弃 | 已由契约测试覆盖 |
| 外部名称、静音、RootMotion 数据与 Preview Prefab 的分类 | 已由契约测试覆盖 |
| Binding 在 Command 前后到达均不会漏报或重复发布 | 已由契约测试覆盖 |
| 未发布的 RootMotion 修改与只声明 Presentation 的改名命令合并 | 已由契约测试覆盖 |
| 角色切换合并已排队/未通知的修改；纯角色切换不重建 Document | 已由契约测试覆盖 |
| 无变化 Undo 不停播；有变化 Undo 一次发布数据与播放状态 | 已由契约测试覆盖 |
| Rig 烘焙设置、RootMotion 中间采样及数组长度异常 | 已由契约测试覆盖 |
| Timeline 在绑定发布后基于当前 Document 捕获快照 | 已由契约测试覆盖 |
| 无关资产变化不修改当前会话 | 已由契约测试覆盖 |
| 时间操作过期与同轨重叠规则 | 已由契约测试覆盖 |
| 浮点边界处候选与实际时长一致、相邻区间拒绝/允许 | 已由契约测试覆盖 |
| 空 GameplayItem 槽位保持及变动后的提交检查 | 已由契约测试覆盖 |
| 空项删除单次通知、跨资产选择隔离、当前 Action 销毁清空 | 已由契约测试覆盖 |
| AnimationAsset 改名的 Presentation 分类、草稿保留/失效规则 | 已由契约测试覆盖；实际控件显示仍需人工验收 |
| Preview RootMotion 顺序无关和 Domain 规则 | 已由契约测试覆盖 |

测试使用内存临时对象，没有修改用户的 TestAction、生产 Prefab、材质或场景。

### 8.2 人工验收

以下项目不能由 batchmode 编译或纯契约测试替代，本次交付记录为未执行：

| 项目 | 状态 |
|---|---|
| Timeline 交互锁期间 Details 原生控件禁用及恢复 | 未执行 |
| Details 刚完成普通属性编辑后立即在 Timeline 拖动，目标和快照均使用新值 | 未执行 |
| Details 修改名称后立即切换 Preview Character，三个窗口显示一致且不重复刷新 | 未执行 |
| 普通属性编辑的焦点、草稿与即时显示 | 未执行 |
| 三窗口在 Undo/Redo、相关/无关资源重导入后的同步 | 未执行 |
| Preview 相机操作时不重算 RootMotion、窗口缩放后 Overlay 对齐 | 未执行 |
| 连续播放 10 分钟及反复开关窗口、切换角色、进出 Play Mode | 未执行 |
| 资源数量与 Console 的长期稳定性检查 | 未执行 |

完成这些人工项后，才可以把编辑器生命周期验收整体标记为通过。Runtime 实际行为验证仍是下一阶段，不属于本文的完成结论。
