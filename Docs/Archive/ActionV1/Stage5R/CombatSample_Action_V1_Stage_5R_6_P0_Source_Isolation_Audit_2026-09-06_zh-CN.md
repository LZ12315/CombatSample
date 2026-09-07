# Stage 5R.6 P0 — Preview 源码复用与角色隔离审查

> **2026-09-08 归档说明：本文只保留源码审查证据；其 SceneView / Animancer 后续路线已暂停，不是当前执行计划。**

> 日期：2026-09-06
>
> 状态：P0 源码审查、公开 API 编译验证及角色构建策略定案完成；P1 未实施。
>
> 运行时隔离、Rig 保真度、资源清理和性能必须在 P1 的真实 Unity 实现中验证；本记录不表示这些检查已经通过。

上位计划：[SceneView / Animancer Preview 重构计划](CombatSample_Action_V1_Stage_5R_6_SceneView_Animancer_Preview_Plan_2026-09-06_zh-CN.md)。用户本次只要求先做第一步，未在此轮开始替换 Preview 窗口。

## 1. 结论

采用自己的 SceneView + 独立 Preview Scene + 独立 AnimancerGraph。直接使用现有包的公开 Graph API 和 DummyAnimancerComponent；不直接实例化 AnimancerPreviewObject，不继承整个 TransitionPreviewWindow，也不复制其全局 Selection、设置存储和 RepaintEverything 行为。

角色构建确定采用白名单：只创建新的 Transform 层级及 MeshFilter、MeshRenderer、SkinnedMeshRenderer、采样所需 Animator。完整 Instantiate 后禁用脚本不满足原隔离合同，本方案不采用。骨骼、Avatar、Mesh 和材质读取自源角色；源 GameObject、Actor 和源 Animancer Graph 不被写入或启动。

这样直接复用的是成熟的视口与动画系统，项目自行维护的部分限定为角色适配、生命周期、Action 语义和诊断。白名单还原有成本，但范围明确；不承诺支持任意角色组件。

## 2. 本地版本与公开 API 编译证据

- Unity：ProjectVersion.txt 为 2022.3.62f3。
- Animancer：本地 package.json 为 8.0.2；Editor asmdef 为 autoReferenced、Editor-only，项目已有对应 ProjectReference。
- 检查以本地源码与本地 Unity 编译程序集为依据，不把官网新版 API 当作本项目的实现。
- 创建了位于 Assets 之外的临时纯编译探针；使用 .NET SDK 10.0.303 的 C# 编译器，以 Unity 生成工程的真实 HintPath 和 Library/ScriptAssemblies 中的两个 Animancer DLL 为引用。未把探针导入 Unity，也未执行其方法。
- 探针编译通过：SceneView 的 OnSceneGUI / SupportsStageHandling、customScene / customParentForDraggedObjects，NewPreviewScene / ClosePreviewScene，Graph 构造 / CreateOutput / PauseGraph / Destroy、DummyAnimancerComponent、Layers[0].Play(clip)、state.Time / Speed、Graph.Evaluate(0) 和 PlayableGraph.Evaluate(0)。
- 初次探针使用 double 赋给 Time 失败；已按本地合同修正为 float 后通过。本地 TimeD 才是 double 属性，P1 不能混用。
- 现有 `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`：0 warning / 0 error。第一次沙箱调用因 Windows SDK 路径访问权限失败，正常提权重试后通过；不是代码编译错误。
- 没有修改 Unity 生成的工程文件、包源码或现有 Preview 实现。

该检查只证明公开 API 与引用可编译，不证明 Graph 采样姿势、脚本隔离或性能。临时探针及 DLL 在检查结束后清理，不留下长期 harness。

## 3. 复用范围与发现

| 源码位置（相对 Animancer 包） | 查到的具体行为 | 决策 |
| --- | --- | --- |
| Editor/Transition Previews/TransitionPreviewWindow.cs | 继承 SceneView，但有静态实例、全局 Selection 重选与设置回写 | 借鉴原生 SceneView 接入；不继承整窗 |
| Editor/Transition Previews/TransitionPreviewWindow.Scene.cs | 创建 customScene；事件注册、实例释放和场景关闭分开 | 借鉴结构，由自己的 Session 统一收尾；只处理本窗口回调 |
| Editor/Previews/AnimancerPreviewObject.cs:219 | 在禁用父节点下 Instantiate 整个角色，随后处理 Behaviour | 不直接复用角色构建 |
| 同文件 DisableUnnecessaryComponents | 明确保留 ExecuteAlways / ExecuteInEditMode | 与禁止执行自定义脚本的要求冲突，不采用 |
| 同文件 OriginalObject setter | 设置对象后调用 TransitionPreviewSettings.AddModel | 不通过该 setter 管理用户角色 |
| Editor/Transition Previews/TransitionPreviewSettings.cs:AddModel | Persistent model 会更新 Models 并 AnimancerSettings.SetDirty | 与 session-only 配置不符，不采用 |
| AnimancerPreviewObject.Dispose | 仅注销 sceneOpening / playModeStateChanged | 不能将 Dispose 视为已销毁 Graph / 实例；销毁顺序需自行明确 |
| Editor/Previews/DummyAnimancerComponent.cs | 公开 C# 包装器，不是 MonoBehaviour，不创建用户脚本组件 | 直接使用 |
| Runtime/Core/AnimancerGraph.cs:478 | CreateOutput 调用动画输出 API，并初始化共享事件 Invoker | 可使用，但共享服务与会话资源必须分清 |
| 同文件 Evaluate / Evaluate(float) | 求值后调用全局 AnimancerEvent.Invoker.InvokeAllAndClear | 预览不直接调用，避免替其他 Graph 消费事件队列 |
| 同文件 PlayableGraph | 公开底层 Graph 属性 | 当前帧采样使用 PlayableGraph.Evaluate(0)，P1 验证实际姿势更新 |
| Editor/Transition Previews/TransitionPreviewWindow.Animations.cs | 暂停 Graph、改时间、显式求值，也包含 Transition 混合及 MoveTime | 借鉴手动时间方式；不引入 Transition 或事件推进逻辑 |

源码可供项目内参考不意味着属于开源代码；遵循包内 License.txt。P0 未复制第三方代码到生产文件，不增加包依赖。

## 4. 角色构建合同

### 4.1 源对象和 Animator

1. PreviewCharacter 可为 Prefab 或 Scene 对象，只读取其层级与支持组件。
2. 源 Actor 优先为所选根的 Actor；没有时仅在子树中存在唯一 Actor 时自动采用。多个 Actor 不能用 GetComponentInChildren 的第一个作隐式决定。
3. 读取已序列化的 Actor.animancer.Animator 引用。本地 AnimancerComponent.Animator getter 只是返回引用，不需初始化 Graph。绝不读取源 Graph 或调用 Actor.Bind/Awake/Evaluate。
4. 明确绑定须属于选择的源子树；指向外部对象时诊断并要求用户选择覆盖该对象的根，不写入外部对象。
5. 无明确绑定时，唯一 Animator 可用；多 Animator 需会话级显式选择。无 Animator 仍可显示支持的 Renderer 和基准状态，但动画采样不可用。
6. ActorPath 相对已确定的 Actor 根；Path 相对采样 Animator 根；HumanBone 从所选 Animator 解析。缺少 Actor 根时不冒充 ActorPath 可成功解析。
7. 使用源 Transform → 预览 Transform 映射，不靠名字匹配骨骼。多 Animator 选择恢复也应重新验证身份/路径，不能持久化数组索引假定顺序不变。

### 4.2 白名单与基准

- 新建会话根并移入 Preview Scene；构建期间保持未激活，完成支持组件配置后再激活。此时不存在任何源 MonoBehaviour。
- Transform 保存 localPosition、localRotation、localScale 和 activeSelf；将场景根的父变换如何归一到预览空间写清，不能把源 localPosition 直接当作场景 worldPosition。
- MeshFilter.sharedMesh、Renderer.sharedMaterials、Animator.avatar 只读共享；不用 Renderer.materials 隐式生成未管理的副本。
- MeshRenderer 保存 enabled 和已支持的渲染设置；SkinnedMeshRenderer 显式复制源 localBounds、bones/rootBone、BlendShape 权重及必要设置。骨骼位于选定子树之外时报告，不静默映射为 null。
- Animator 不带 RuntimeAnimatorController，不复制源 AnimancerComponent；设置 fireEvents=false，自动根运动关闭，只有选中的 Animator 接收独立 Graph。
- 不创建 Actor、Motor、Collider、Rigidbody、Audio、ParticleSystem、Cloth、用户脚本或自定义 Renderer。RectTransform 等未支持的特殊层级不静默转成普通 Transform 并宣称保真。
- Clip 的绑定也须在支持范围内；不允许动画通过材质曲线修改共享材质。不支持的绑定显示诊断，或先实现明确的会话副本与恢复再支持。
- 基准恢复覆盖 Clip A → B 中 B 未写到的字段。Animator 原生绑定/内部状态与组件基准是不同问题，P1 通过 A/B 切换和往返 Scrub 验证，不承诺仅恢复 Transform 就足够。
- P1 临时验证对象需覆盖 Awake / OnEnable / OnDisable / OnDestroy / ExecuteAlways 计数，验证预览构建不触发这些源脚本。此测试尚未执行；生产白名单代码也尚未实现。

项目存在 ExecuteAlways 的 RootMotionOracleRecorder，它的 OnAnimatorMove 会写 Transform；Actor.OnEnable 会注册 CombatSimulationDriver。这些源码说明“保留编辑脚本”并不适合我们的隔离边界，并非声称当前预览已触发这些脚本。

## 5. 动画 Graph 与事件隔离

P1 使用新 Graph、无 TransitionLibrary、纯 Clip state。显式 source time、Speed=0、暂停自动推进，单次输入只执行一次零增量姿势求值。使用 Time（float）或 TimeD（double）直接定位，不用 MoveTime 模拟从旧时间经过事件。

`CreateOutput` 会播放 Graph，因此初始化后必须设置 Manual 并 PauseGraph；仅设置 state.Speed=0 不等于停掉整个 Graph。

独立 Graph、所选 Animator 和包装器在 Session 中保持强引用，构建完成后才发布 Ready。替换 Clip 时复用本 Session 的 state 缓存，不将外部 state、事件序列、Transitions 或 gameplay 回调接入预览。

**共享事件服务的边界：** AnimancerGraph.CreateOutput 会调用 Invoker.Initialize。AnimancerUtilities.InitializeSingleton 在 Edit Mode 可能创建 HideAndDontSave 的共享服务对象。因此不能声明 Animancer 完全没有全局状态，也不能把它当成 Session clone 销毁。

- 通过 Animator.fireEvents=false 与纯 Clip state 阻止预览事件；当前帧求值不调用会清空全局队列的 Graph.Evaluate 包装器。
- 使用公开 PlayableGraph.Evaluate(0)；这仍会执行 Graph 内部 Playables 更新，不是“跳过所有事件系统”的万能开关，因此 P1 要验证无事件入队和跨窗口干扰。
- 不修改全局 Invoker Enabled，不清理别人的事件队列，不销毁共享单例。
- 性能/资源检查分别计数会话资源和包级共享服务：允许一次性共享初始化，但反复开关不得出现共享对象数量持续增加；如发现包行为与此不符，先修复接入方式，不能擅自清理他人状态。

## 6. 生命周期定案

| 事件 | 行为 |
| --- | --- |
| 初次打开或角色变化 | 预览实例及依赖准备完成后创建 Graph，最后进入 Ready |
| 同一角色切 Animator | 停止时钟/手势、销毁旧 Graph、恢复基准、再连接新 Animator |
| Source/Avatar/Mesh 相关变化 | 事件标记失效，合并重建；不在 Repaint 查询依赖 |
| 切 Action/Clip | 失效相应动画状态；不无条件重建角色 |
| 普通 Frame / Preview Input | 只标记求值 dirty，合并一次采样 |
| Camera / Selection | 仅相应重绘/强调，不重建或扫描 Validator |
| Failed | 留存单一诊断；等待输入变化/Retry，禁止每个 GUI 事件重试 |
| 失焦/隐藏 | 释放手势；隐藏后停止自身播放工作，恢复时按需刷新 |
| 关闭/重载/切 Play Mode/异常 | 统一执行下述清理；返回 Edit Mode 不自动恢复播放 |

清理顺序：停止时钟及回调 → 使延迟请求失效 → 释放输入 → 销毁本 Session Graph → 清除窗口 customScene/customParent 引用 → 销毁预览实例与自有资源 → 关闭有效 Preview Scene → 清空引用。

每层清理使用 finally 继续释放剩余资源；方法可重复调用，不能因为前一对象已被 Unity 销毁就跳过后续资源。调用 SceneView 自身 OnEnable/OnDisable 等基类生命周期，不代替 Unity 管理其内部相机。自有灯光/地面只在 Preview Scene 中存在。

不订阅或改写其他 SceneView 的 Selection/相机；Scene GUI 过滤目标窗口。禁止直接把源 Prefab 拖进视口后绕开白名单：P1 应拦截/拒绝原生对象实例化，或将拖入转换为 PreviewCharacter 选择再走构建流程。

## 7. 旧实现基线与 P1 待测项

静态检查确认旧 DrawPreviewGUI 没有 Repaint 渲染门槛，Ensure 调用在绘制路径查询依赖 Hash，路径先按每区间生成再压缩。它们是待测热点，未取得 Profiler 数据，不能报告为已经证实的主因或已修复。

P0 未在 Unity 打开预览、未运行临时角色脚本、未取得帧耗时和内存数据。P1 才执行以下门槛：

- 单 Clip、Animator 位于子层级、多个 Animator、无 Animator、Humanoid/可用 Generic。
- Clip A/B 切换、首尾及往返 Scrub 的姿势一致性。
- 脚本隔离、源场景/资产 dirty 状态、共享资源不被写入。
- 关闭/重开/换角色 10 次、重导入、Domain Reload、Play Mode 转换、异常路径的资源与回调数量。
- 关闭、打开静止、Scrub、播放四组同机性能。空闲自身 Graph 求值/依赖查询为零；不存在持续内存增长和整编辑器异常卡顿。

API 编译检查已经通过，P1 可据此开始；但 P1 验收通过前不可接入 Action 动画映射、RootMotion、HitBox 或 Effect。

## 8. 参考源码指纹

以下为 P0 审查时的 SHA256，路径相对 `Packages/com.kybernetik.animancer/`。

```text
29E3CF26061ADD323E36E6EAAAA553DD9F4845E26B4AE025F09A5F98BBF4821C  Editor/Transition Previews/TransitionPreviewWindow.cs
E67BD93F88C57D5C1D6228720BFF1BD06486FC77C7AB82FFA529F4919413456C  Editor/Transition Previews/TransitionPreviewWindow.Scene.cs
F6AEB738E1F0506B05A7DDD09C16D664DAAAA24003241C62B4787645A875EF04  Editor/Transition Previews/TransitionPreviewWindow.Animations.cs
2BA875EC628C9C53CF2DE9631866213FDC7E503D9FACAD1A48CDC004EAF40888  Editor/Previews/AnimancerPreviewObject.cs
25C1AFD9D08D606E28E2CF3FFD76ED7B46EF23DB161D082624F7904B0D444C7D  Editor/Previews/DummyAnimancerComponent.cs
B00351350B73116D37E974EEBE5AB69137F60ACFFC8FF409C1EA222E33D01573  Runtime/Core/AnimancerGraph.cs
A48E9A28F68758717E4BB2445BD084F2A90E55D0DDFAA0F1A5E4695051C9CBF8  Runtime/Core/AnimancerUtilities.cs
20C195939531E7B9509088C5A73613456EFED936D53CDBB0CDFBECD66C38EBDD  Runtime/Core/Events/AnimancerEvent.Invoker.cs
3018F81AB0A51E136A361EA92FC11701D069CFCBD19D298E40DDCC16D82E5EE5  Editor/Transition Previews/TransitionPreviewSettings.cs
```

临时编译探针引用的已生成程序集 SHA256：

```text
DD010A07361FD684EB751116699C898418B64D3BDB3528BFA6B2FEA24426B577  Library/ScriptAssemblies/Kybernetik.Animancer.dll
5CFF5FD334A5EFC112464C913C54FC5D652655643A450C9C225D5A7DEAA64737  Library/ScriptAssemblies/Kybernetik.Animancer.Editor.dll
```
