# Action V1 编辑器细节审查记录

日期：2026-09-15。基线：`FrameWork` 当前工作区，包含已有未提交修改。

状态：第 1～6 节记录的是修正前的代码证据；后续已按用户要求实施修正，结果见文末。保留原始证据便于追踪，不将旧问题描述视作修正后的当前行为。

## 范围与结论

检查了 Commands / Clipboard、操作快照与时间计算、Context / 外部事件适配、Document、Timeline / Details 的刷新和交互入口，以及 Preview 输入、求值、绘制和清理路径。没有进入 Runtime 行为验证；读取 AnimationSegment 的数据计算，仅用于比较编辑候选和实际写入后的时长。

主结构仍然成立，但不能将目前所有问题归为外观或代码风格：发现一项已通过独立数值实验确认的候选时长偏差，以及若干从调用路径可确认的边界遗漏。它们可以在现有职责内局部修正，无须新增管理层或通用框架。

证据分级：

- **数值验证**：在独立 C# 代码中使用当前两条原始公式执行；未调用 Unity 窗口。
- **代码路径确认**：条件、状态修改和调用关系可以直接从当前代码推出；尚未在 Unity UI 中操作复现。
- **待人工复核**：只记录需要观察的触发场景，不将可能性当作已复现缺陷。

## 1. 优先修正：动画候选时长与资产实际时长不同

证据：数值验证 + 代码路径确认。

位置：

- `Assets/Scripts/ActionSystem/Editor/V1/ActionV1EditorOperations.cs:648`，`CalculateAnimationDuration`。
- `Assets/Scripts/ActionSystem/V1/ActionTimelineData.cs:87`，`CalculateDerivedDurationFrames`。
- `Assets/Scripts/ActionSystem/Editor/V1/ActionV1EditorCommands.cs:319`，`CommitTimelineOperation`。

资产计算先将秒数存入 `double`，再乘帧率；编辑候选先在 `float` 表达式中乘帧率，再转换参与后续计算。两者虽然都使用 Ceil 和 `1e-6`，中间舍入不同。

本轮运行的最小数值实验：

```csharp
float start = 0f, end = 32f / 60f, rate = 1f;
double seconds = (end - start) / rate;
double asset = Math.Ceiling(seconds * 60 - 1e-6d);
double candidate = Math.Ceiling((end - start) / rate * 60 - 1e-6d);
// end = 0.533333361, asset = 33, candidate = 32
```

影响：Details 修改源区间或 Timeline 裁剪时，候选可能以 32 帧通过检查；提交同一组 Source 数值后，真实资产变为 33 帧。若另一个动画从第 32 帧开始，就可能提交出一帧重叠。提交重新求值仍调用同一条候选公式，无法保护这个差异。

建议：使用一份正式时长计算，候选与资产必须共享同样的精度、取整和溢出规则。先以当前资产计算为基准消除分歧；是否调整整体数值规则，应另行明确，不能顺手改变 Runtime 语义。补一个“候选通过后，实际资产时长及相邻关系仍一致”的契约测试。

## 2. 含空 GameplayItem 的资产会误拒绝正常时间操作

证据：代码路径确认。

位置：`ActionV1EditorOperations.cs:264`、`:400`。

快照构建 `_laneItems` 时跳过 `Source == null` 的条目，提交检查却将其数量与包含空项的 `lane.Items.Count` 比较。两者从捕获时就不一致，并非用户在操作中修改了数据。

触发：某一 GameplayLane 包含一个空项，编辑其他正常条目的时间。只要候选本身合法，反馈可以显示允许，最终提交仍会报告轨道结构变化。检查遍历所有轨道，因此空项甚至会影响另一条轨道上的正常内容或动画段。

建议：快照保留用于结构比较的完整槽位，包括空项；占用区间计算可以继续只使用有效条目。不要通过全面放松结构检查来绕过错误。补“空项保持原样时正常编辑可提交；空项位置或结构变化后旧操作拒绝”的测试。

## 3. 删除空条目的入口有两处未收口

证据：代码路径确认；延迟菜单失效场景待 Unity 操作复核。

位置：

- `ActionV1EditorCommands.cs:314`：先 `Commit`，再 `Shared.SelectAction()`。
- `ActionV1TimelineWindow.cs:2023`：解析旧条目后检查 `entry?.Source != null`，随后访问 `entry.SelectionKind`。

第一处会将一次删除分成数据通知和选择通知。编辑非当前资产时，`Commit` 本来不会更新当前会话，但后面的 `SelectAction()` 仍会改变当前选择。

第二处无法区分“条目仍存在但内容为空”和“条目本身已无法解析”。后者也能通过该条件，随后空引用。入口来自空条目的按钮或上下文菜单；旧目标在回调执行前失效时需要拒绝。

建议：最终选择随删除提交一次性交给 Context；窗口显式检查 `entry == null`，同时保留命令对实际数组槽位的检查。无稳定 ID 的延迟操作还应绑定原 Action / 文档代次，不能仅凭相同路径和空 Source 重新定位另一个目标。

## 4. 当前 Action 被外部删除，没有完整的会话清空路径

证据：代码路径确认；项目窗口删除后的实际画面待人工复核。

位置：`ActionV1EditorCore.cs:769` 的 `RefreshExternal`，以及 `Document` getter 和 `SetAction` 的相等性早退。

`RefreshExternal` 在 `_currentAction == null` 时直接返回。当前 Unity 对象被销毁后，这个判断也会为真，因此不会重建 Document、整理选择或通知窗口。旧 Document 和选择仍可能保留，Preview 也收不到由本次删除产生的失效通知；它在下一次自身 Repaint 时才有机会检查到空 Action。

建议：区分“会话原本没有 Action”与“原目标刚被销毁”，由 Context 完成一次清空、停播和发布。不能简单调用现有 `SetAction(null)` 就认为已覆盖，因为 Unity 对象的空值相等比较同样可能使它提前返回。使用临时内存 Action 的销毁操作验证会话状态，不删除用户资产。

## 5. AnimationAsset 改名未完整接入显示刷新

证据：代码路径确认。

位置：

- `ActionV1EditorDocument.cs:29` 的 `DisplayName` 读取 `AnimationAsset.name`。
- `ActionV1EditorCore.cs:303` 的 `AddAnimationData` 只记录该 AnimationAsset 的对象身份，没有记录名称。
- `ActionV1TimelineWindow.cs:567` 的轻量显示刷新不更新内容条目的 Label；Label 在 `RefreshGeometry` 中更新。

当动画资产仅改名、Clip / Rig / RootMotion 都没变时，观察状态可以完全不变，于是没有 Presentation 通知。即使补上通知，现有轻量路径仍主要更新错误标记、Tooltip 和轨道标题，动画条目文字要等下一次几何刷新才更新。

建议：动画资源名称纳入 Presentation 比较；轻量显示刷新同步内容 Label。单纯改名不要升级为结构变化或 Preview 数据失效。分别验证通知分类和实际标签刷新。

## 6. Details 草稿保留取决于通知来源，而非数据是否使草稿失效

证据：代码路径确认；焦点体验待人工复核。

位置：`ActionV1DetailsWindow.cs:1139`、`:122`。

`Content` 若来自 Binding，仅刷新 Document；来自 Command、外部资源或其他来源，则完整重建页面。页面重建无条件丢弃草稿。

明确场景：在 Details 编辑一份尚未 Apply 的时间草稿，然后在 Timeline 静音该条目。`SetMuted` 发布 Content，当前 Action 和主选择可以均未变化，时间源也未变化，Details 仍会丢失草稿。直接在 Details 通过绑定改同一个静音属性，草稿却能保留。绑定变化被其他提交吸收时，也可能因发布来源不同走不同路径。

此外，结构或时间变化导致草稿被清除时，目前没有统一的“草稿已因外部变化失效”提示。

建议：区分“控件需要怎样刷新”和“时间草稿是否仍有效”。前者可以考虑绑定来源以保护焦点，后者应基于 Action、主选择与快照相关数据。不要为了保留草稿而忽略真正过期的操作。

## 7. 可顺手整理，但不是已确认的功能故障

### 外部事件尚未先过滤相关对象

`ActionV1EditorCore.cs:837` 收到 `ObjectChangeEventStream` 后未检查其中的对象，统一排入延迟扫描。后续观察状态比较能够避免无关变化重建 Document，但无关编辑活动仍会触发当前 Action、动画采样数组和依赖 hash 的扫描。

这是可确认的额外工作量，不代表已测出卡顿。本轮不建议为了它建立复杂依赖图；后续可以在事件入口做简单相关对象过滤，保留必要的项目资源回调兜底。

### 少量无调用或只写不读的残留

全仓库 C# 引用检索确认：

- `ActionV1SerializedLookup` 没有调用者。
- `ActionV1EditorInteractionGate.IsBlockedFor` 没有调用者。
- Gate 的 `_pointerId` 只被赋值和复位，未用于判断。
- 操作快照 Entry 的 `Muted` 被捕获但未用于求值或过期检查。

可删除不再承担职责的内部代码；不要为保留这些字段而追加新逻辑。静音不影响时间占用合法性，未使用的 Muted 快照本身不意味着需要增加静音导致操作过期的规则。

### Document 的只读性主要依赖约定

集合以 IReadOnly 接口暴露，但 Document / Entry 的不少派生字段仍是可写 internal 字段，DisplayName 也从源引用即时读取。本轮未找到窗口写入这些派生字段的证据，因此不把它当成当前数据故障。若整理，可收紧发布后的写入范围；不需要另加 DTO 层。

## 8. Preview 本轮检查边界

已经核对：Renderer / SpatialEvaluator 没有自行读取共享 Context；动画时间和相机绘制分开；纯相机 Repaint 不重新累积位移；自建 Mesh / Material、PlayableGraph 与 PreviewRenderUtility 有现存清理入口；渲染使用 finally 配对 EndPreview。

未在本轮确认新的 Preview 原生资源泄漏。以下仍是人工检查项，不能根据静态代码宣布通过：

- 旋转 / 平移中失焦、鼠标在窗口外释放、进入 Play Mode 后，拖动状态是否正确结束。当前 `_orbiting` / `_panning` 主要由 MouseUp 清除，缺少单独的失焦复位路径。
- 反复开关窗口、切换角色、脚本重载和进出 Play Mode 后的资源数量及 Console。
- 连续播放至少 10 分钟；暂停时的实际求值次数与重绘行为。

## 原审查阶段的验证与改动

- 已完成：上述代码路径阅读、全仓库相关符号引用检索、两条动画时长公式的独立 C# 数值实验。
- 未执行：本轮 Unity UI 操作、完整编辑提交流程复现、长时间生命周期验收。
- 未重新运行现有编译和 EditMode 套件：本轮没有修改实现。此前 36/36 的结果不能证明本次新发现的边界已经被覆盖。
- 本轮仅新增本审查记录；未修改编辑器 / Runtime 代码、测试、生产资产、场景或项目设置。

后续修正顺序建议：先统一时长计算，再处理空项快照与删除路径、目标销毁的会话清理，之后处理名称显示和草稿保留，最后清理死代码。此顺序是审查建议，不表示已开始实施。

## 修正结果（2026-09-15）

- 时长：AnimationSegment 保留原公开方法及数值规则，提取内部静态计算供编辑候选复用。没有修改资产序列化或 Runtime 时长语义。回归覆盖 32/60 秒源区间在第 32 / 33 帧邻接的拒绝与允许，以及候选和实际资产时长相等。
- 空项快照：保留 null 槽位参与结构比对，只在占用计算中跳过；结构不变可提交，槽位重排会拒绝。
- 空项删除：最终选择随 Commit 一次发布；非当前资产删除不触碰当前选择。窗口的删除回调捕获来源 Document，文档过期或目标无法解析时直接拒绝。
- Action 销毁：外部刷新将已销毁 Unity 对象正规化为真正的 null，并通过 SetAction 完成停播、清空和单次通知。
- 名称显示：AnimationAsset 名称加入 Presentation 比较，Timeline 轻量刷新更新内容 Label，不使 Preview 数据失效。
- Details：草稿是否可保留由原 Action、主选择和源快照决定；整页重建可以复用有效草稿。真正过期的未提交草稿显示提示，用户自身 Apply 不误报外部失效。
- 残留：删除无调用的 ActionV1SerializedLookup、IsBlockedFor，以及 Gate 的无用指针字段/参数和快照 Muted 字段。
- Preview：增加失焦、关闭和进入 Play Mode 时的拖动状态复位，未改变相机手势、视觉效果或求值逻辑。

第 7 节的全量外部事件扫描和 Document 字段访问范围属于可选结构/性能整理，本轮保留现状；没有证据表明存在窗口修改派生字段或已测出的性能故障。它们不标为“已优化”。

人工复核仍未执行：菜单失效后操作、窗口间静音时草稿控件实际保留、改名后标签即时刷新、项目窗口删除 Action 后三窗口表现，以及 Preview 失焦操作、10 分钟播放和资源数量检查。数据契约测试不能替代这些 UI / 生命周期检查。

修正验证结果：

- `dotnet build Assembly-CSharp-Editor.csproj --verbosity quiet`：通过，0 错误，51 条现有警告。
- 最终 Unity EditMode `ActionV1PreviewArchitectureTests`：**43/43 通过**，结果文件为 `C:\Users\20052\AppData\Local\Temp\ActionV1DetailFixFinal.xml`。
- 新增 7 个测试用例，使用内存临时对象，不依赖生产资源。首次运行的删除测试使用普通内联类的 null 槽位，改为支持 null 的 SerializeReference GameplayItem 后通过；没有删减单次通知及跨资产隔离断言。
- 受限环境第一次 Unity 启动因 LicensingClient 超时未运行测试，之后在桌面权限下完成最终测试；不将启动退出码计作测试通过。
- 差异与新增文件空白检查通过。生产场景、Prefab、材质、TestAction 和项目设置未作本轮修改。
