# Stage 5R.6 — SceneView / Animancer Preview 重构计划

> **2026-09-08 归档说明：本计划已暂停，P1 实现已撤下，不得自动继续 P2–P4。Preview 需要重新设计。**

> 日期：2026-09-06
>
> 状态：已归档并暂停；曾确认的技术方向不再是当前执行方案。
>
> 历史进度：P0 审查完成；P1 曾通过开发侧编译与纯逻辑检查，但未通过 Unity 功能、隔离和性能验收，随后从当前代码路径撤下。详见 [P1 Handoff](CombatSample_Action_V1_Stage_5R_6_P1_SceneView_Clip_Preview_Handoff_2026-09-06_zh-CN.md)。
>
> 当前边界：不得实施 P2–P4；5R.7 暂缓，Preview 等待重新设计。

## 1. 目标与文档权威

P0 结论与证据见 [源码复用与角色隔离审查](CombatSample_Action_V1_Stage_5R_6_P0_Source_Isolation_Audit_2026-09-06_zh-CN.md)。已确认公开 API 可编译；角色与 Graph 的 Unity 生命周期、隔离和性能仍由 P1 证明。

Action Preview 改用 Unity SceneView 承载独立 Preview Scene，独立 AnimancerGraph 负责动画姿势。项目 Editor evaluator 负责 Action 时间、baked RootMotion、SelfRotation、HitBox 和诊断。

本计划取代旧 5R.6 的普通 EditorWindow + IMGUIContainer + PreviewRenderUtility 自制视口实现方向，以及旧 handoff 的完成声明。保留 Stage 5 冻结设计的三个独立可停靠窗口、共享 Action/Frame/Selection、clone-only 和 current-frame evaluation 合同。Preview 视口允许采用 SceneView 原生界面；控件与诊断尽量使用原生 Overlay/UI Toolkit，不再要求整个视口是普通 UI Toolkit 窗口。

Timeline、Details 已接受的编辑流程不重新设计。Stage 0–4 Runtime、Timeline 序列化结构、Validator、Bake 数据合同、正式资产和 Legacy 路由不变。现有 Animancer 包作为已安装依赖使用，不升级或修改包源码。

直接驱动正式 Scene 实例暂不作为交付方案。独立场景内可以使用从 Prefab 或场景角色构建的预览实例；传入场景角色不意味着取得对源实例的写权限。

## 2. 源码依据与复用决策

参考版本：本仓库 `Packages/com.kybernetik.animancer/package.json` 声明的 8.0.2。实施记录保存实际参考文件版本或 Hash，不根据官网新版 API 假定本地能力。

| 本地源码 | 已核实事实 | 我们的处理 |
| --- | --- | --- |
| `Editor/Transition Previews/TransitionPreviewWindow.cs` | 继承 SceneView | 使用自己的 SceneView 窗口，沿用原生视口与相机 |
| `Editor/Transition Previews/TransitionPreviewWindow.Scene.cs` | NewPreviewScene、customScene、Scene GUI 与关闭释放 | 借鉴场景绑定与生命周期；事件只处理本窗口 |
| `Editor/Previews/AnimancerPreviewObject.cs` | 管理角色实例、Animator 与独立 Graph | 审查可复用部分，禁止未经适配直接采用其角色克隆策略 |
| `Editor/Previews/DummyAnimancerComponent.cs` | 公开 Editor-only IAnimancerComponent 包装器 | 优先直接使用，实施时通过实际编译确认 |
| `Editor/Transition Previews/TransitionPreviewWindow.Animations.cs` | 暂停 Graph、设置时间、显式 Evaluate | 复用 Graph API；实现我们的 Clip/time 适配，不复制 Transition 混合业务 |

不直接继承整个 TransitionPreviewWindow：其静态实例、Selection、SerializedProperty 和 Transition Inspector 流程不属于 Action Editor。

不使用反射、Unity private/internal API 或复制大块第三方实现来接入。若确有小段源码复制需要，记录出处、版本、范围及修改，并保留原有版权/许可信息；源码可见不意味着开源许可。

## 3. 最小职责划分

以下为职责边界，类名可以在实现时按项目风格确定；不新增通用编辑器框架。

| 层 | 输入/职责 | 禁止承担的职责 |
| --- | --- | --- |
| SceneView Window | 原生画面、工具栏、输入与重绘 | 资产依赖扫描、逐事件手动 camera.Render |
| Preview Session | 场景、实例、基准、Graph、释放 | Action/Gameplay 执行 |
| Animation Sampler | Animator、Clip、source time → 姿势 | 自行推进 Action Frame、累积根运动 |
| Prepared Action View | 复用 Document/Validation，准备排序区间与依赖状态 | 持久化第二份 Timeline |
| Action Evaluator | Frame、准备数据、预览输入 → 运动结果、路径与 Gizmo 数据 | 调用 Runtime、Motor、Receiver 或现场 Bake |
| Diagnostics/Input UI | 呈现问题、定位、预览 Target/Direction | 修改 ActionAsset 或影响运动对象选择顺序 |

AnimancerGraph 只绑定预览 Animator；不复用源 ActorAnimation 的 owner、Combat tick、Action Layer 或运行中 Graph。整个预览只有一个时间推进来源。

Session 采用明确状态：Empty、Building、Ready、Failed、Disposed。失败不在每次 Repaint 自动重试；只由更换输入、相关依赖变化或显式 Retry 重建。Dispose 可重复调用。

## 4. P0 — 源码审查与角色构建定案

### 4.1 角色隔离不可降低

Animancer 8.0.2 会在未激活父对象下 Instantiate，然后禁用部分 Behaviour，但保留 ExecuteAlways / ExecuteInEditMode。这不能直接满足我们的脚本隔离要求。

P0 必须验证以下内容并形成实现结论：

- 正确的源 Actor 根、采样 Animator、Avatar、骨骼路径及 Renderer 引用如何确定。
- 预览角色构建、启用、销毁期间不得执行源角色 Gameplay / 自定义编辑模式脚本，也不得向正式战斗系统注册对象。
- 默认安全路径保留支持组件的白名单构建：Transform、MeshFilter、MeshRenderer、SkinnedMeshRenderer 与 Animator，并明确基准和引用映射。这是原计划已接受的边界。
- 完整 Instantiate 只有在已证明不会执行不允许的脚本且保持上述合同的情况下才能替换白名单；不能用“创建后再禁用”作为安全证明。证明失败时继续白名单，不要求用户放宽隔离来推进。
- 多 Animator 优先使用源 Actor 的明确绑定；无明确绑定且存在多个候选时，提供会话内显式选择，或阻塞采样并说明原因，不静默选择第一个。
- 不复制 Animator Controller 或源 Animancer Graph；动画事件关闭。依赖自定义脚本、布料、粒子或特殊 Renderer 的外观不默认为已支持，应报告可定位的兼容性诊断。
- 共享 Mesh、Material、Avatar 仅只读使用；如需要修改材质参数，必须先创建会话副本并纳入清理。

记录旧预览问题清单与性能基线；旧 evaluator 不视为可信实现，逐项审查后才移植。

### 4.2 P0 输出

复用清单、角色构建选择、实际 Animator/Graph API 编译结论、生命周期说明。P0 与 P1 是同一批实施工作，不因普通类名或内部实现选择反复申请确认。

## 5. P1 — SceneView 基础层 + 单 Clip 动画

这是第一批代码的完整范围，也是第一道用户验收门槛。

### 5.1 可交付操作

- 现有 Action Preview 菜单及按需打开入口进入新的 SceneView 窗口，不增加长期的第二套 Preview。
- 保留 Shared Context 的 PreviewCharacter。提供明确的空态、构建失败、Animator 选择及重试状态。
- 建立独立 Preview Scene，绑定本窗口 customScene；相机导航、渲染与 Handles 交给 SceneView。
- 提供会话级 AnimationClip 字段、秒数 Scrub 和 Play/Pause，作为 P1 明确标记的单 Clip 验证入口。它不读取或写入 Timeline，不引入正式测试资产。
- Clip 更换暂停播放，时间归零；非循环播放到 Clip 末尾停止。采样输入钳制到合法秒数，拒绝非有限数值。
- Graph 暂停自动推进。播放由一个 Editor 时钟驱动，并合并到一次采样；静止时保持最后姿势。
- 支持项目实际 Humanoid 角色；以具备 Generic 测试角色时补充 Generic 检查。未验证的 Rig 类型明确记录。
- 恢复采样基准，保证 Clip A → B、任意方向 Scrub 不残留未被新动画覆盖的状态；采样根必须是实际选择的 Animator 对应根。
- 根 Transform 保持基准，禁止自动根位移，也不播放 Gameplay、音效、粒子或动画事件。
- 工具栏、诊断不能抢占 SceneView 的原生相机输入；相机变化只重绘。

P1 明确显示“Animation foundation · Action evaluation pending”。旧 RootMotion/HitBox 暂不可用，不伪装为支持。P2 接入 Shared CurrentFrame 后移除单 Clip 独立时钟/验证入口，不保留双时钟。

### 5.2 生命周期

- 角色或采样 Animator 更换：停止推进，解除原输出，销毁旧 Graph/实例，再创建新状态。
- OnDisable、关闭、程序集重载、进入 Play Mode 和异常路径统一释放 Graph、预览对象、场景及自有资源，注销回调。
- Play Mode 期间本次编辑模式预览停止；返回 Edit Mode 后按需重建，不自动恢复播放。
- 窗口失焦释放本窗口手势；隐藏后不持续执行预览播放工作。状态改变时重建或求值，Repaint 不负责初始化依赖。
- 原生 SceneView 状态与 Editor 会话字段保存相机/面板设置；不使用 EditorPrefs，不向项目 Asset 写入窗口状态。
- Scene GUI 回调严格过滤本窗口，不能移动其他 SceneView 相机、抢占全局 Selection 或清理别的窗口资源。

### 5.3 性能与资源门槛

首先记录同一 Unity、同一角色、同一窗口尺寸下：Preview 关闭、静止打开、持续 Scrub、连续播放。旧预览可复现时保留对照；不能为收集基线反复让编辑器进入不可用状态。

使用 Unity Editor Profiler，预热后每种状态观察约 30 秒，记录 Editor CPU、主线程耗时、GC 分配、采样/重绘次数与测试条件；避免 Deep Profile 干扰第一轮对比。代码可增加少量长期有效的 ProfilerMarker，不引入常驻测试 harness。

硬性检查：

- 静止且输入不变时：自身采样、Graph 求值、Document/Validator 调用为零。
- OnGUI / Repaint 不查询 AssetDatabase 依赖，不构建角色，不调用 camera.Render，不对全编辑器发起无条件 RepaintAll。
- 一次时钟更新或输入变更最多触发一次最终采样；重复通知合并。
- 连续开关/换角色至少 10 次，预览对象、场景、Graph 及回调数量无累积；内存预热后不持续增长。Unity 的延迟回收与自身泄漏分别判断。
- 保存前后源场景和资产状态保持原样；不把原本已 dirty 的场景错误地标记为 clean。
- 没有新增 Console 错误、UI 异常或持续日志。

耗时以实测与同机基线比较，不预写没有依据的毫秒承诺。若明显拖慢整个编辑器，定位热点并修复，P1 不得通过。若工具不能读取 Unity Profiler，由用户在 Unity 采集；如实标为待验，不以 dotnet 编译代替。

### 5.4 P1 停止点

开发侧交付编译、生命周期/脚本隔离检查及可获得的性能证据，用户检查窗口操作、动画采样和编辑器流畅度。通过后才能进入 P2；否则仅修正 P1。

## 6. P2 — Action 时间映射

- 移除 P1 独立测试播放时钟，接入现有 Shared Context 的整数 CurrentFrame、Play/Pause 和 Loop；Timeline 继续为时间权威。
- 单/多 AnimationSegment、60Hz、Trim、PlayRate、首段前基准姿势、Gap/Hold、末段 Hold 遵循 Stage 4 冻结公式。
- Timeline 变化准备只读采样记录，Frame 变化只解析 Clip/time 并求值；不能在播放中重跑 Validator/Bake status。
- 缺失资源、非法 SourceRange、非法 PlayRate 不进入采样。Animation overlap 按既有合同跳过动画姿势并诊断；保持可用的其他预览能力。
- 切换 Action、Clip 和 Animator 均清除旧姿势残留；任何顺序到达同一 Frame 得到同一结果。
- 多窗口共享已完成的 Document/Validation 结果。确需补齐共享缓存时只调整 Editor 协作层，不重建 Timeline/Details 编辑结构。
- 依赖事件只标记准备数据失效并合并处理，区分动画资源失效与角色重建；场景角色引用的 Mesh/Avatar 等也在依赖范围内。

完成时间映射开发检查与 Unity Scrub/播放验收后进入 P3。

## 7. P3 — Action 运动与 HitBox

按 RootMotion → SelfRotation → HitBox 分小提交接入，每项通过定向检查后再叠加下一项。所有结果从基准求值。

### 7.1 RootMotion

- 沿用 AnimationAsset.RootMotionData 及 Bake 状态，Missing/Stale/Invalid/非法完整 source window 跳过并诊断；不现场 Bake。
- 当前 Frame 覆盖至 Frame + 1 的运动区间；动画仍采样 Frame 对应的姿势时间。
- Graph 的自动根运动禁用或根 Transform 显式恢复，由 baked 数据单独负责预览位移。
- 以内容边界组织半开区间，重叠按 StartFrame、Lane order、Item order 选取第一个。A[0,10)、B[5,20) 时 B 只贡献 [10,20)，source offset 仍相对 B 原始开始时间。
- 完全覆盖、部分覆盖、同帧、相邻、间隔和中断后恢复贡献均有确定性例子；冲突诊断包含全部参与对象、重叠区间和采用对象。
- 实现前写清 trajectory delta 所在坐标系、被裁掉前缀后的位移换算、基准朝向与 SelfRotation 的关系，用带转弯轨迹验证；不能直接继承旧 evaluator 的基准旋转乘 delta 写法并假定正确。
- 路径最终最多 256 点，并提前分配全局采样预算；不能每段先生成大量点再压缩。区间位移求值按内容数量组织，不按完整 Action 时长逐帧模拟。

### 7.2 SelfRotation

- RootMotion source 复用 RootMotionYawUtility；PresetLocal、Target、ContextDirection 使用明确的静态预览语义。
- 必须先确定 PresetLocal 的参考朝向、区间进入朝向及重叠后贡献恢复语义；不得在每个碎片重新解释同一个预设方向。
- RootMotion yaw 的 RotateBySpeed 和大角度轨迹需定向验证：整段最短 Quaternion 插值不等价于逐帧限速，不能声称与 Runtime 完全一致。实现需要给出有依据的采样/累计策略和边界，超出可信范围明确诊断。
- Target 输入允许三维位置，仅朝向计算投影到水平；ContextDirection 只表示水平朝向。依赖移动目标历史的效果不纳入当前帧保证。
- 非有限数值、非法枚举、零方向与无效轨迹均跳过并定位。
- 根据当前参与求值的对象显隐预览 Handle；Handle 捕获期间相机不接管，失焦/切换/关闭有一致收尾。

### 7.3 HitBox

- 在动画、根位移和朝向完成后解析骨骼，绘制所有 active、非 muted HitBox。
- 骨骼解析符合 ActorPath、Animator Path、Humanoid 三种合同；无效引用跳过单个 Gizmo，包含 EditorId/AuthoringPath 诊断。
- 胶囊含端部弧线及侧线，球形退化正常；形状、旋转、缩放使用与现有 HitBox 合同一致的解释，不静默修复数据。
- Selection 只改变强调，不触发运动重新求值或改变对象选择顺序。
- 不执行真实碰撞、伤害或 HitBox effects。

## 8. P4 — 综合交互、诊断和交接

- Action/Character/Frame、按需 Target/Direction、Reset、Frame Camera、紧凑可定位诊断整合到正式 Preview。
- 问题分 Authoring 与 Preview 来源；合法唯一 ID 选择，损坏 ID 只按路径定位。普通 Selection 和 Camera 不触发 Validator。
- 诊断区默认收起，展开最多窗口 30%，内部滚动。差量更新行，切帧不整页 Clear/Rebuild。
- 验证 PreviewCharacter、Loop、Selection 不丢弃 Details 草稿，不引起 Timeline 全量刷新。
- 在 2560×1440、1920×1080 和窄 Dock 验证原生 SceneView 操作、工具栏和诊断布局。
- 检查重导入、Domain Reload、进入/退出 Play Mode、角色销毁、异常采样及窗口关闭后的清理与状态恢复。
- 合并后重测 P1 四组性能状态；定位动画、运动或诊断各自的新增耗时。
- 移除无调用的旧 PreviewRenderUtility 视口、自制相机、测试入口和被替代的 evaluator；不保留自动回退旧后端。

P4 完成并获用户接受后，5R.6 才可结束，后续为 5R.7 全编辑器综合验收。

## 9. Effect 扩展边界

本次为后续 Effect 保留可放置对象、绑定骨骼、显式求值与清理的能力，不提前建立通用 Effect runtime 或加入新 Item 类型。

粒子/VFX 外观预览、命中预览输入及视觉特效时间控制，分别另写需求。现有 HitVfxConfig 依赖接触点、目标、相机朝向和随机参数，单靠 CurrentFrame 无法确定结果。声音、震屏、HitStop、伤害及速度历史不属于本次 current-frame Preview 验收。

## 10. 验证证据与变更控制

每个阶段的 handoff 分开记录：

1. 编译：Unity 已生成工程确实包含所有新源文件，执行 `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`；不手工改 csproj/sln/生成目录。Unity 未刷新导致源文件未纳入时明确说明，不能报告假绿。
2. 开发逻辑：对时间映射、区间贡献、同帧重复求值与路径预算使用确定性检查，直接检查生产逻辑，不复制算法、不引入长期测试 harness。
3. Unity：角色脚本隔离、姿势、Handles、关闭清理、原生 SceneView 行为与 Console。观察由用户完成，尚未执行的检查写“待验证”。
4. 性能：同机条件、采样方式、实际观测和对比；编译不是性能证据。
5. 仓库：检查修改范围与未跟踪文件空白，保留无关 Scene/Asset 改动，不提交临时角色、场景或动画资产。

Unity Test Runner 不作为门槛。不能将“有源码参考”“使用官方 API”“编译通过”写成“已证明稳定”。每阶段未通过时停在该阶段修正，不向后堆功能。

文档本次建立计划并纠正状态；代码实施从 P0/P1 开始。以后每阶段追加实测 handoff，并在 Docs/README.md 更新准确状态。
