# CombatSample Action V1 — Stage 5 开源 Timeline 编辑器审查

> 状态：设计输入；不批准修改 Stage 5 实现
> 日期：2026-09-02
> 目标：判断是否存在可直接采用的开源 Unity Timeline/技能编辑器，并明确可复用与必须排除的边界

## 1. 结论

目前没有找到一套可以直接替换 Stage 5、同时满足 CombatSample 既有合同的开源编辑器。

推荐方案是：

1. 保持 `ActionAsset.Timeline` 为唯一数据权威，继续实现专用的 Action V1 Editor。
2. 不安装或 Fork 任一候选项目作为 Stage 5 基座。
3. 只从 MIT 候选中借鉴或移植少量、无业务模型依赖的 Editor-only 基础设施。
4. 若实际复制 MIT 源码，保留原版权和 MIT 许可文本，并在项目第三方声明中记录来源与固定提交。
5. Timeline 数据操作、稳定 ID、Validation、Undo transaction、Preview evaluator 仍由 CombatSample 自己实现。

这不是因为候选项目质量都很差，而是它们解决的问题与本项目冻结合同不同。整包采用会引入第二份 Timeline、自己的 Runtime/序列化模型、源场景对象采样、自动保存或不兼容的 Undo 语义，适配成本大于按当前设计重建 Stage 5。

## 2. 本次源码审查基线

候选源码只克隆到本机临时目录，没有加入 CombatSample：

`C:\Users\20052\AppData\Local\Temp\combat-sample-timeline-audit-20260901`

固定版本：

| 候选 | 固定提交 | 许可 | 声明的 Unity 版本 |
| --- | --- | --- | --- |
| SnapPose | `880ac5d40ed33ab4d37330e7e9f548ec90dba46b` | MIT | 2021.3+ |
| Honami Animation System | `54a0986c0facd5c11a181af94cb2d59523be4815` | MIT | 6000.0 |
| NBC.ActionEditor 2.0 | `7943620ffe5314ff268e368979e1b24012ce3369` | 未发现许可证 | 未在 package.json 声明 |
| Unity Timeline | 项目已安装 `com.unity.timeline@1.7.7` | Unity Companion License | 当前项目 Unity 2022.3 可用 |

审查没有向 CombatSample 导入第三方 package、源码、资产或 `.meta` 文件。

## 3. 必须满足的采用门槛

候选只有同时满足以下条件，才可能作为 Stage 5 基座：

- 直接编辑 `ActionAsset.Timeline`，不要求 `TimelineAsset`、JSON 文档或另一套 Track/Clip 数据成为权威。
- 不修改 Stage 0–4 Runtime、Timeline 序列化结构和七种 Gameplay Item 合同。
- 支持固定 60Hz、整数 Frame、`[StartFrame, EndExclusive)` 和自动 Duration。
- Selection 使用稳定 `EditorId`，并支持 identity-only safe mode 与显式 Repair。
- Gameplay overlap 合法；一条 GameplayLane 始终只占一行。
- 拖拽使用原始快照和总 Delta；提交时一个 Undo；Escape 取消为零 Undo。
- 不自动保存资产，不把窗口状态写入 `EditorPrefs` 或 Runtime 数据。
- Preview 只操作独立 Preview Scene 中的 clone，永不采样或修改源 Prefab/场景对象。
- 可在 Unity 2022.3 中维护，不依赖 Unity 6 API 或完整第三方 Runtime。
- 许可证允许当前项目复制、修改和分发。

没有候选完整通过这些门槛。

## 4. 候选审查

### 4.1 Unity Timeline 1.7.7

#### 可以利用

- 项目已经安装，不增加 package 风险。
- `ClipEditor`、`TrackEditor`、`MarkerEditor` 可用于定制 Unity `TimelineAsset` 中的类型展示。
- 官方的帧尺、缩放、拖拽、选择和 Undo 体验可以作为交互参照。

#### 不能作为 Stage 5 基座

- `TimelineEditorWindow.SetTimeline` 的公开入口只接收 `TimelineAsset` 或 `PlayableDirector`。
- 公开扩展点定制的是 `TimelineClip`、`TrackAsset` 和 Marker，不提供把任意数据模型挂到原生 Timeline 画布的适配层。
- 实际 `TimelineWindow`、窗口状态、树和拖拽实现大多为 internal；依赖这些实现会形成版本脆弱的反射或源码 Fork。
- 要使用其原生画布，必须建立并维护 `TimelineAsset`。这会成为第二份 Timeline 或要求改写 Stage 1 数据权威，直接违反冻结合同。

#### 决策

不采用为基座，不建立 `ActionTimelineData ↔ TimelineAsset` 镜像。只把其视觉密度、时间导航和用户习惯作为产品参照。

### 4.2 SnapPose

#### 可以利用

- `TimelineScrubber` 是体量较小的 UI Toolkit `VisualElement`，展示了帧刻度、playhead、Pointer Capture 和 UI Toolkit scheduler 的组合方式。
- `PoseSampler` 对 `AnimationMode` 的启动、批量采样、停止和异常清理顺序有参考价值。
- MIT 许可明确，且 package 声明 Unity 2021.3+。

#### 必须排除或重写

- Scrubber 的最后位置是 `0..totalFrames`，而 Action V1 是 `0..DurationFrames - 1`。
- Scrubber 把 clip 自身 frame rate 作为时间权威；Action V1 固定 60Hz。
- `PointerLeave` 使用固定 pointer id 释放 capture，且离开控件会结束 drag，不满足 Stage 5 的完整 capture/cancel 合同。
- Preview 直接对传入的 `GameObject` 调用 `AnimationMode.SampleAnimationClip`，会操作源场景对象，不满足 clone-only Preview。
- Controller 包含多角色播放、Pose 写回、资产创建、`AssetDatabase.SaveAssets`、会话资产和 `EditorPrefs`，远超 Stage 5 范围。
- 没有稳定 ID、Lane、多选、合法 overlap、一次拖拽一个 Undo 或 identity safe mode。

#### 决策

不安装 package，不复制 Controller/Session/Preview。允许把 Scrubber 的局部结构作为阅读参考；实际 ruler、playhead、transport 和 playback controller按 Action V1 合同重写。

### 4.3 Honami Animation System

#### 可以利用

- 三个候选中，Timeline 的视觉层次、固定 header/ruler、transport、选择框和 pointer capture 最接近本项目目标。
- `TimelineState` 把窗口交互状态与 Runtime 数据分开，这一方向与 Shared Context / Window-local State 一致。
- `HonamiPreviewStage` 使用 `PreviewRenderUtility`，并在 domain reload、Editor quit 和 Dispose 中统一清理 native preview scene、mesh 与 material；这部分资源生命周期设计值得借鉴。
- MIT 许可明确。

#### 必须排除或重写

- package 明确要求 Unity 6000.0；CombatSample 是 Unity 2022.3。
- Timeline 直接绑定 `HonamiTimeline`、`HonamiTimelineTrack`、`HonamiTimelineClip`、状态机节点、事件和 Honami Runtime Controller，无法作为纯画布独立引入。
- 它是完整 Animator/PlayableGraph 替代系统，不是中立的 Timeline UI library。
- Timeline 设置大量写入 `EditorPrefs`，与 Stage 5 “不写 EditorPrefs”冲突。
- Clip drag 在每次 `PointerMove` 中直接修改数据并调用 `Undo.RecordObject`；没有 Action V1 要求的 ghost、原始快照、整组合法性预检和 PointerUp 单次提交。
- Escape 只覆盖时间输入，不覆盖 Move/Resize/Trim 的无 Undo 取消。
- Timeline Preview 会解析当前 Selection/场景实例并进入 `AnimationMode`，不是独立 Preview Scene clone。
- Selection 使用对象引用和静态 copied lists，不是稳定 `EditorId` selection。
- 合法 overlap、单行 overlap picker、identity safe mode 和 ActionAuthoringValidator 映射均不存在。

#### 决策

不安装、不 Fork 整包。将它作为主要视觉/交互参考。若后续确实复制 `HonamiPreviewStage` 中的清理骨架或极小 UI helper，必须：

1. 单独移植，不引入 Honami Runtime；
2. 改写到 Unity 2022.3 API；
3. 加入原版权和 MIT 许可声明；
4. 以 Preview clone 和 Action V1 evaluator 替换其场景采样逻辑。

当前更推荐只借鉴设计并自行实现，因为可直接保留的代码很少。

### 4.4 NBC.ActionEditor 2.0

#### 有价值的设计参考

- 领域形状与技能编辑器接近：Group/Track/Clip、拖拽、拉伸、多选、磁吸和预览 time pointers。
- Group/Track 固定行的 IMGUI 布局，可以作为“一条 lane 一行”的行为参照。
- 多选拖拽会缓存起始状态，并在最终非法时尝试整体恢复，这个思路与 original snapshot 接近。

#### 不能采用

- 仓库根目录和 package.json 均未发现许可证。公开 GitHub 仓库不等于获得复制、修改和分发授权，因此不能复制其代码。
- README 明确说明 2.0 仍有不少 UI bug，维护者当前投入有限。
- 使用自建 IMGUI/View/Event 框架，与 Stage 5 已选 UI Toolkit 架构不一致。
- 数据权威是自己的 `Asset/Group/Track/Clip`，并从 `TextAsset` JSON 反序列化。
- `App.OnUpdate` 定时自动保存，最终直接 `File.WriteAllText`，违反不自动保存和 Unity SerializedObject/Undo 合同。
- 实际编辑路径几乎没有有效 Unity Undo；拖拽会在过程中直接改模型。
- 使用 `EditorPrefs`、自己的配置文件和打包的 FullSerializer。
- 预览是顺序触发 Enter/Update/Exit 的模拟器，不是 Stage 5 current-frame evaluator，也没有独立 Preview Scene clone。

#### 决策

仅允许观察产品交互，不复制代码、不引入 package。除非作者以后提供明确许可证和授权，否则不得作为实现来源。

### 4.5 SuperCLine/actioneditor

- 功能范围完整，包含 Action、Skill、AI、事件与运行框架。
- 许可为 GPL-3.0；若将其代码并入项目，需要遵守 GPL 对组合和分发的要求。
- 数据模型、Runtime 和 Editor 高度一体化，目标是完整技能框架，而不是适配现有 `ActionTimelineData`。
- 基于较早 Unity/IMGUI 工作流，迁入后的升级和维护成本高。

决策：不采用、不复制。可以从最终产品截图中学习信息分区，但不作为源码来源。

### 4.6 其他候选

| 候选 | 结论 |
| --- | --- |
| Animation Sequencer | MIT，但核心是 DOTween 驱动的顺序动画，不是多 Lane Action Authoring；不采用为基座。 |
| SkillEditorDemo | 以 Unity `TimelineAsset`/Prefab 为 Authoring 中间层再导出数据，形成第二份权威；未确认清晰许可证；不采用。 |
| Slate | 成熟但为商业资产，不是可直接引入的开源基座；适合作为体验参照。 |

## 5. 可复用清单

### 5.1 允许借鉴的设计模式

- SnapPose：UI Toolkit scheduled playback、简单 scrubber 的元素拆分。
- Honami：固定 header/ruler/content 分层、transport/timecode 的视觉密度、Pointer Capture 使用方式。
- Honami：`PreviewRenderUtility` 的集中 Dispose、domain reload 和 Editor quit 清理策略。
- NBC：多选 drag 开始时缓存所有对象的原始状态；非法结果整体恢复的思路。
- Unity Timeline：常见快捷键、时间尺密度、frame/seconds 展示、track header 与 content 的视觉关系。

这些是模式，不是对候选数据模型、Runtime 或整个窗口类的授权采用。

### 5.2 不应复用的实现

- 任何候选的 Track/Clip/Asset Runtime 数据模型。
- Unity `TimelineAsset` 镜像、导出或双向同步层。
- 依赖 internal Unity Timeline Window 类型的反射或源码 Fork。
- 直接对源场景对象使用 `AnimationMode` 的 Preview Controller。
- `EditorPrefs`、JSON 文档或临时资产作为 Shared Context/Clipboard/窗口状态存储。
- 拖拽期间直接写 `ActionAsset` 并连续注册 Undo 的实现。
- 自动 SaveAssets 或 `File.WriteAllText`。
- 未授权仓库或 GPL 仓库的源代码。

## 6. 对 Stage 5 设计的影响

开源审查不改变当前 redesign 的核心决策，反而确认了以下边界是必要的：

- Timeline、Details、Preview 继续是三个独立窗口，共享 Editor-only Context。
- 每条 GameplayLane 固定一行；合法 overlap 用 Marker/Picker 解决可达性。
- `ActionEditorDocument` 只是从 `ActionAsset.Timeline` 构建的只读绘制快照。
- Interaction 必须由明确状态机管理，drag 只更新 ghost，PointerUp 才通过 Command 提交。
- Identity issue 进入显式 safe mode，不能临时退回数组索引选择。
- Preview 使用独立 clone，并由 current-frame evaluator 从 baseline 重建结果。
- Validation 默认摘要化，Issues Drawer 不占据主要工作区。

外部候选没有提供这些合同的现成实现，因此不能用“先接入再修改”的方式降低风险。

## 7. 推荐实施策略

批准 Stage 5 redesign 后，按以下方式实施：

1. 先完成 Shared Context、Document、ChangeSet、Readiness 和 Identity Safe Mode，不绘制复杂 Item。
2. 完成固定 header/ruler/content scroll 架构和空状态，先用只读 block 验证布局。
3. 完成稳定 ID selection、overlap marker/picker 和 issue locator。
4. 建立 interaction state machine、ghost、original snapshot、单次 Command/Undo，再开放 Move/Resize/Trim。
5. 完成响应式 Details 和 `SerializedProperty` leaf editing。
6. 最后建立 clone-only Preview Scene，并把资源清理作为独立验收门槛。

如果后续决定复制 MIT 候选中的任何具体代码，应先提交一个很小的来源清单，列明：原文件、固定提交、复制范围、修改点、许可保留位置和为何不能更简单地自行实现。没有这份清单时，默认只借鉴概念，不复制源码。

## 8. Exit Criteria

本次开源审查在以下条件满足时完成：

- 已确认不存在符合全部采用门槛的直接替代品。
- 已固定最相关候选的版本与许可证状态。
- 已明确 Unity Timeline、SnapPose、Honami、NBC.ActionEditor 和 GPL 候选的排除理由。
- 已形成可复用模式与禁止复用边界。
- CombatSample 实现代码、Package manifest 和项目资产未因审查发生变化。
- 项目负责人确认继续采用“专用 Action V1 Editor + 极小 MIT 参考”的路线，或明确选择另一条路线。
