# Demo 开发就绪审查

审查日期：2026-10-18。基线：`Release` / `a520b4f7`，这是当前约定的 Demo 开发分支，不需要再创建开发分支。

状态：**本页保留审查时的问题快照，不是所有问题均已修复。** 审查阶段只新增记录及索引，未修改玩法/资产。随后经确认，已在 `FrameWork` 工作区实现第 1～3 项的代码修复、7 个 Kiana 普攻的按下触发迁移，并重建第 4 项缺失 Shader；场景、相机生命周期与 Shader 画面/Player 验收仍待做。第 7 项已决定不实现 Play 热更新，只明确重进 Play 的生效边界；第 8 项已按最小方案双向补齐 Combat hub；第 5 项已完成本轮 AI 请求生命周期代码修复，集成验收交由用户执行；第 6 项仍按决定暂不修。输入条件随后按确认方案实现为当前 State / 纯 Event Sequence，包含核心外方向识别、有限历史和运行时候选凭据；旧 InputCheck 套娃及短按/长按兼容逻辑不恢复。新版三组独立源码编译及 77 个选定纯逻辑用例通过，用户反馈输入与编辑器手工测试未发现问题，完整 Unity 原生集成/设备覆盖及 Shader/Player 验收仍未记录完成；本页下文源码位置仍是审查时的历史快照。具体行为、测试和人工清单见 [玩家输入与动作条件合同](Player_Input_Contract.md)。上一轮输入及编辑器源码提交为 `d3785413`，Manager Prefab 清理与文档提交为 `8595045f`；当时 7 个 ActionAsset 的修改按要求未纳入提交。本轮 AI 请求修复、战术绕行节点及未使用节点清理在 `AI` 分支归档，并将其他本地分支快进到同一提交；本次不提交资源资产，也不推送。具体未提交资产以当前 Git 工作区为准。

## 结论与边界

当前固定 Tick、ActionRuntime、Locomotion、运动贡献所有权和命中收集/结算边界已具备继续开发的基础。当前最值得补的是输入正确性、配置错误的隔离、AI 请求生命周期和实际启用的渲染引用，而不是增加程序集、资源加载框架或继续拆目录。

这里的“确认”指源码控制流或资产引用可直接确认，不代表已经在运行场景逐项复现。没有发现并验证必须立即停工的 P0；也不能据此保证不存在其他缺陷。

- **P1**：建议先处理；影响基础输入、全局仿真连续运行或已启用的表现功能。
- **P2**：在扩展角色、AI、群战或边做边调的工作流前处理。
- **能力缺口**：取决于 Demo 内容，不等同于现有功能必然出错。

本轮覆盖主调用链：场景构建入口、Actor/Driver、动作仲裁/播放/条件、Locomotion/运动/动画交接、HitBox/伤害/反馈、输入、NodeCanvas 适配、相机、HUD，以及编辑器主要数据/命令入口、测试与包配置。没有逐行审计全部第三方插件，也没有完成所有美术/特效资源及场景的人工验收。

## 确认的问题

### 1. P1：新建的空准入条件可以使整个战斗仿真进入故障停止

位置：

- `Assets/Scripts/Action/Editor/ActionAuthoringListGUI.cs`，`CreateList` 的新增回调：新增条件先置为 `null`。
- `Assets/Scripts/Action/Data/ActionAsset.cs:114,162`：普通/事件准入路径直接执行 `cond.Check`，没有处理空项；退出条件则已有空项检查。
- `Assets/Scripts/Action/Conditions/InputSequenceCondition.cs:48`：输入序列内部同样直接解引用检查项。
- `Assets/Scripts/Actor/CombatSimulationDriver.cs:125` 附近：异常会设置 `_simulationFaulted`，后续 FixedUpdate 不再推进。

触发路径：给当前角色动作增加一个 Entry Condition，但尚未选择类型；或者输入序列存在空检查项，且执行到该项。下一次动作条件检查可能抛出异常。影响不是“这个动作不能播放”，而是 Driver 停止全部角色仿真。

最小修复：把不完整条件视为配置无效、拒绝对应动作，并提供能定位动作/条件索引的提示；补配置校验。不能简单跳过所有空项而意外放宽准入。保留 Driver 对真正仿真故障的 fail-fast，不建议在 Driver 外层吞掉一切异常继续跑。

人工验收：未选类型的准入条件、空输入序列项均不能导致其他角色停止仿真；有效条件的 AND/override/invert 行为不变。

### 2. P1：短按可能丢失，长按时间随渲染帧率变化

位置：`Assets/Scripts/Input/PlayerInputController.cs:17-18,302-359`。

- 按键 Started 只置 active，短按在 Canceled 时生成。
- 按住时长通过 `Update` 的 `elapsedFrame++` 计数；40 帧在 30/60/144 FPS 下约为 1.33/0.67/0.28 秒，并不是战斗固定帧。
- 新按键如果在两次 `UpdateInputState` 之间完成按下和松开，`elapsedFrame == 0`，取消回调直接返回，不生成短按。
- 再次按下时不立即清零旧计数；同一帧内重复按/松也可能继承上一段的时长。

影响：低帧率、快速点击或设备事件被同一轮 Input System 更新处理时，攻击/跳跃/闪避可能出现输入遗漏；不同机器的蓄力门槛不同。

最小修复：用输入事件的时间与边沿记录开始/结束，不以渲染帧计时；如希望继续以“帧”配置阈值，应明确按 60 Hz 转换为时长，保留序列化字段兼容。是否仍在松开时触发短按，应与玩法预期明确，而不是顺手改成按下触发。

人工验收：30/60/144 FPS 下相同持续时间判定一致；同次输入更新内的 press/release 不丢；快速双击彼此独立。

### 3. P1：禁用输入和切换角色没有清理完整的输入所有权

位置：

- `Assets/Scripts/Input/PlayerInputController.cs:89-93,418-426`。
- `Assets/Scripts/Action/Conditions/InputSequenceCondition.cs:23-26`。
- `Assets/Scripts/Action/Conditions/InputStateCondition.cs:15-18`。

`OnDisable` 只清 Locomotion 并禁用 Action Asset，没有清历史/按钮计数，也没有撤销 `Instance`；输入条件只检查单例和受控角色，不检查控制器是否启用。禁用输入 Action 时的取消回调还可能把按住状态转成一次短按事件。

切换 `controlledActor` 时也仅释放旧角色移动意图、清相机缓存，未清按钮和输入历史。因此旧角色留下的未消费输入可以被新角色认领。

影响：以后用禁用组件实现菜单/演出禁输入时，动作条件仍可能消费旧输入；角色切换可能出现无新操作却出招。

最小修复：统一定义输入可用性；禁用/失焦/切换角色时明确清理 raw axes、状态、计时和历史，并避免清理触发的 canceled 被当作新攻击。不要只修移动意图。

人工验收：按住攻击时禁用并重新启用控制器；留有缓存时切换角色；旧输入不能驱动新动作。是否允许跨暂停保留输入应显式决定。

### 4. P1：当前启用的命中特效环境遮挡缺失 Shader 引用

位置：

- `Assets/Settings/Engine/URP-Balanced-Renderer.asset:50`。
- `Assets/Settings/Engine/URP-HighFidelity-Renderer.asset:104`。
- `Assets/Settings/Engine/URP-Performant-Renderer.asset:27`。

三处启用的 `HitConfirm Occluder Stencil` 均引用 Shader GUID `770da3146d7444eb92a935c7eb85d4b8`，但 Assets、embedded Packages 和当前 PackageCache 的 `.meta` 中均无此定义。项目 Graphics/Quality 设置实际指向这些 URP 配置，并非只存在于废弃示例场景的引用。

`HitVfxConfig` 默认使用 EnvironmentOnly；`ImpactSystem` 会把这类特效放到 DeferredVFX 层，Renderer 再依赖模板遮挡重绘。因此不能仅凭 C# 编译通过认为该表现链路正常。可能影响“穿角色但不穿环境”的视觉合同；具体是否漏遮挡、错误重绘或产生其他症状，需要 Frame Debugger 确认，不能仅靠文本扫描定论。

最小修复：确认是否保留此渲染策略。保留则找回/重建对应遮挡 Shader 并重新配置验证；不保留则显式统一配置和回退路径。不要只清空 GUID 让扫描归零，也不要未经确认直接改三套 Renderer。

人工验收：切换三档画质，分别检查角色遮挡和墙体遮挡下的命中特效；用 Frame Debugger 确认模板写入与重绘次序，再在 Player 中复验。

### 5. P2：AI 动作请求在任务停止后仍可能执行，旧回调也可能污染新一轮任务

位置：

- `Assets/Scripts/Graph/Action/PlayActionTask.cs:64-68,80-106`。
- `Assets/Scripts/Action/Runtime/ActionStateManager.cs:146-165` 及下一 Tick 的请求快照/裁决。

`RequestExternalAction` 只入队，没有返回可撤销句柄。`PlayActionTask.OnStop` 只把 `_active` 设为 false；这只能忽略回调，不能阻止已提交的请求被 ASM 选中。任务还没有 `OnPause` 的请求处置。

如果同一个任务在旧请求回调前重新执行，`_active` 又变成 true，旧请求的回调便可能结束新一轮任务。减速/暂停动作仲裁会扩大请求滞留窗口。

影响：行为树分支已中止/切换，但角色下一固定 Tick 仍开始旧动作；AI 状态和实际动作不一致。

最小修复：给请求提供可撤销身份，任务 stop/pause/timeout 时撤销尚未开始的请求；回调携带本轮执行序号，忽略过期轮次。撤销待决请求不应等同于强停已开始动作，二者语义要分开。

人工验收：请求后、下次裁决前中止任务；中止后立即重跑同一任务；动作冻结期间超时；旧请求均不能启动动作或结束新任务。

处理决定（随本轮 AI 分支提交归档）：已保留原 `RequestExternalAction` 接口，并新增 `RequestCancellableExternalAction`，返回单个请求句柄。`Cancel()` 只撤销尚未进入启动尝试的该请求，同步回调 false 且只完成一次；待决队列移除该请求，决策快照及已收集候选会忽略已撤销项，不阻塞其他合法候选。同一 Action 的其他合法请求或轮询候选不受影响。ASM 进入启动尝试后句柄不可再撤销，不强停已开始动作；动作仍走原有准入、优先级与取消规则。

`PlayActionTask` 在结束、暂停和 UntilStarted 超时时撤销待决请求；先使本轮回调失效，再处理可能同步触发的撤销回调。回调捕获执行轮次，旧轮次不能清除新轮次的等待状态或结束新任务。恢复时按原模式重新请求；UntilStarted 继续使用原有 `Time.time` 截止时间，行为树任务暂停不会额外延长超时。处理了 ASM 禁用时同步失败回调的赋值顺序，不保存已经失效的句柄。未改任务序列化字段、行为树资产或已开始动作的打断语义。

本轮验证：Runtime、去 `UNITY_EDITOR` 的 Runtime、Editor/全部测试源码独立编译均 **0 error / 0 warning**。在既有合同测试中新增 7 个用例，覆盖撤销只完成一次、快照中撤销不阻塞其他候选、开始后撤销不停止动作、任务中止/暂停后重启或恢复、启动生命周期中重跑任务的旧回调隔离及零超时撤销；仅完成源码编译，**未执行 Unity Test Runner、场景或 Player 测试**。此前 77 个输入合同用例不是本轮 AI 修复的测试结果。实际行为树分支切换、暂停/恢复和冻结仲裁期间超时仍需用户验证。

### 6. P2：HitBox 的 32 Collider 查询上限会静默遗漏命中

位置：`Assets/Scripts/Combat/HitBox/ActorHitBoxRuntime.cs:59,163-175`；`ActionHitBoxGeometry.QueryNonAlloc`。

Overlap 查询固定使用 32 长度数组，没有满缓冲检测、扩容重查或警告。这里限制的是 Collider 数，不是敌人数；复合受击盒、自身碰撞体和查询层内其他碰撞体都会占名额，后面的筛选和去重无法找回被截断的结果。

影响：较大范围攻击/多个复合角色可能漏打；后续稳定排序只能排序已收集的子集，不能保证没有遗漏。

最小修复：满缓冲时按需扩容重查，设合理保护上限并可诊断；不要只无提示地把 32 改成另一个常数。

人工验收：范围内超过 32 个符合查询层的 Collider，目标按 IDamageable 去重后都能按规则结算一次。

### 7. P2：Play 中修改 Action List 或事件标签不会可靠生效

位置：`Assets/Scripts/Action/Data/ActionAssetList.cs:19-40`；`ActionStateManager.cs:120-123,502-527`。

动作列表首次读取后缓存，仅在资产 OnEnable 清理；普通 Inspector 列表修改不负责失效。事件映射又只在 ASM Awake 时建立。因而增删动作、修改 TriggerMode/EventTriggerTag 后，轮询列表和事件映射可能继续使用旧配置。

影响：边做边调时容易误判为输入或受击逻辑错误；重新进入 Play 后结果又不同。若不支持运行期换列表，这仍是需要明确的编辑器工作流边界，不必为此开发复杂热更新系统。

最小修复：简单方案是明确提示修改后重进 Play；若保留即时调试能力，则补列表失效与事件映射的显式刷新机制，并使两者一致。

人工验收：Play 中增删动作、变更事件标签后的行为与所声明的即时/重进 Play 规则一致。

处理决定：**不实现 Play 中热更新，保持缓存逻辑简单稳定。** 已核对确认的生效边界：

| 修改内容 | 生效时机 |
| --- | --- |
| ActionAsset 的条件值、优先级、CancelRule、SelfTag、TriggerMode | 下一次仲裁（同一资产对象现读） |
| ActionAsset 的 Timeline / Gameplay 项 / HitBox 配置 | 需要该动作重新开始（开始时抓快照） |
| ActionAssetList 增删条目 | 重进 Play（缓存只在 OnEnable 清） |
| TriggerMode 改为 Event、EventTriggerTag（事件映射） | 重进 Play（仅 Awake 建表） |
| LocomotionSetAsset 的 Move/Start/Stop/Pivot、混合时长、Stop 曲线 | 重进 Play（绑定时已成值快照） |
| AnimationAsset 的 LocomotionData、脚标记、停止时间覆盖 | 重进 Play（已绑成值） |
| LocomotionAsset.MovementConfig | 重进 Play（Runtime 构造时复制） |

因此 Play 中边调边看仅对条件、优先级、取消规则成立；结构调整一律重进 Play。不为热重载增加资产监听、显式刷新入口或运行时分支；如以后确有需求，再作为一个独立、可验证的特性设计。

### 8. P2：Actor 唯独不自动补齐 Combat hub，漏绑时攻击静默无效

位置：`Assets/Scripts/Actor/Actor.cs:40-53`；`ActorCombater.Awake`；`ActorHitBoxRuntime.DetectHits`。

Actor Awake 会补 Motor、ActionPlayer、ASM、Animancer、Animation、Locomotion，却不补 `combater`；ActorCombater Awake 只保存自己的 Actor 引用，没有回填 Actor.combater。HitBox 检测在 Actor.combater 为空时直接返回，锁定等路径也依赖它。

影响：从零搭建角色、添加组件或复制一个漏绑的新 Prefab 时，即使同对象已有 ActorCombater，仍可能出现动作/动画正常但不产生伤害的静默失败。已有正确绑定的 Prefab 不受影响。

最小修复：与其他组件一致补齐同对象 Combat hub 的 fallback，同时给必需组件缺失提供可定位提示；保留显式指定的序列化引用。

人工验收：新 Actor 同对象存在 Combat hub 但字段未手动指定时可正常攻击/锁定；显式已有绑定保持不变。

处理决定：已按最小方案双向兜底——`Actor.Awake` 与其他组件一致补 `combater`，`ActorCombater.Awake` 在 `Actor.combater` 为空时回填自身。两者都不覆盖已显式设置的引用，不改序列化字段、Prefab 或选敌行为，也不新增运行时日志。Runtime 及移除 `UNITY_EDITOR` 的 Runtime 编译均 0 error / 0 warning；未执行原生场景测试与人工验收。

## 能力缺口：先由 Demo 范围决定

1. **重复游玩/重试闭环**：ActorCombater 仅 Awake 初始化生命，死亡直接 SetActive(false)，没有重置生命的公开合同；简单 SetActive(true) 不是复活。当前源码没有场景级胜负/重试流程。Demo 至少需要明确死亡后如何重新开始；若采用重新加载场景，不必先引入对象池/完整复活系统。
2. **多阵营/多敌人目标筛选**：`ActorCombater.IsValidTarget` 只过滤自身、激活、组件启用和距离，没有敌我/阵营/可攻击性筛选，也没有 IsDead 判断。当前死亡会停用对象，通常由 active 检查排除；但后续多敌人、友军或复活时需要补目标合同。HitBox 的 LayerMask 与自动索敌是两条路径，设置攻击层不等于修复索敌。
3. **暂停与鼠标/界面流程**：当前 Escape 只是释放光标，R 是 Editor 暂停调试；没有正式暂停菜单、退出/重试交互。若只是开发场景可暂缓，若要交付他人试玩则应补最小流程。

这些都可以做小型场景流程，不需要搭完整 Game Framework。目标平台和正式入口场景仍需决定；当前唯一启用构建场景为 `Assets/Scenes/MiHoYo_Release.unity`，不代表已选定为最终 Demo 入口。

## 已知旧引用与本轮广域扫描的区别

[Project_Structure.md](Project_Structure.md) 中的 **35 处/11 文件**，统计的是上次旧归档删除直接遗留的引用，不是整个仓库所有缺失引用的总数；该范围和负责人自行清理的决定不变。

本轮另对 Data/Prefabs/Scenes/Settings/Resources 下 **99 个已跟踪的 asset/prefab/unity 文件**做 GUID 定义对照，纳入 Assets、embedded Packages、当前 PackageCache 定义，得到 **77 处/17 文件**找不到定义的外部 GUID，其中包含上述旧引用以及其他历史遗留，例如 ActorRoot/David/Lucy 的旧 Timeline binding、图中其他旧对象引用和第 4 项 Shader。该结果不是“又新增 77 个运行时故障”，也不能认定额外项由上轮清理造成。

- 扫描范围内未发现找不到 GUID 定义的 `m_Script`；这不覆盖所有插件资产，也不证明 SerializeReference/图类型全部有效。- 文本引用可能是已失效的 Prefab override 或旧 binding；是否仍实际生效须在 Unity 中核对。
- GUID 扫描不能校验 fileID、模型子资产、材质画面、场景生命周期或构建裁剪。
- 不擅自恢复旧资产、删除引用、替换角色动作；先按 Demo 保留范围确认。

## 验证记录

本轮使用正在打开的项目现有依赖缓存和 Unity 2022.3.62f3 随附 C# 编译器，输出到系统临时目录，没有写 Library 或关闭/另启 Unity 编辑器：

| 检查 | 结果 | 不代表什么 |
| --- | --- | --- |
| Runtime 源码编译 | 0 error / 0 warning | 不代表运行场景无异常。 |
| Editor 与现有测试源码编译 | 0 error / 0 warning | 不代表完整 Test Runner 已执行。 |
| 移除 UNITY_EDITOR 定义的 Runtime 源码编译 | 0 error / 0 warning | 使用现有依赖缓存，不等同真实 Player 构建、链接、Shader 编译或裁剪验证。 |
| 选定现有纯逻辑用例，独立 Unity Mono 执行 | 36 passed / 0 failed | 非完整 NUnit/Unity Test Runner，不含需要 Unity 原生对象或场景的用例。 |
| GUID 定义对照 | 99 个文件，77 处待核对外部 GUID | 非 Unity dependency/import/fileID 检查。 |

36 个用例：ActionSpeedEffect 的时长换算、动画倍率和反馈规则 20 个；CombatHitBuffer 的排序、互杀/死亡过滤、异常及重入合同 7 个；Timeline 磁吸数学 7 个；Locomotion 速度反馈与倍率匹配 2 个。这些用例通过不覆盖上文新增发现的缺陷。

临时日志：`C:\Users\20052\AppData\Local\Temp\combat-project-audit-m5nmcdyx`，包含编译响应文件/日志、纯逻辑 runner/结果和 GUID 清单；它不是仓库工具或长期验收入口。

未执行：完整 Unity Test Runner、PlayMode、场景实机复现、Profiler、Frame Debugger、完整 Player Build。相机切锁/混合、上下坡与墙角运动、运动平台、跳跃/受击/顿帧恢复、命中遮挡、音效与三档画质均需人工验收。没有因为编辑器文件较大或代码使用 Instantiate 就认定性能存在已验证问题。

## 建议最小修复批次

1. **输入与配置安全**：第 1、2、3 项，优先保护基础操作和不完整配置下的仿真连续性。能抽成纯逻辑的时间/边沿合同补少量 EditMode 测试，其余做人工生命周期验收，不为私有字段名增加反射测试。
2. **AI 和新角色接入**：第 5、8 项；再按工作流决定第 7 项是否需要即时刷新。
3. **命中与表现**：确认并修复第 4 项，在扩展群战前补第 6 项。
4. **最小 Demo 闭环**：确认角色、敌人、入口、平台和一局流程，处理保留内容实际使用的旧引用，完成失败/重试及一次真实 Player 打包验收。

不建议本轮追加 asmdef、Addressables、全量插件升级、资源重分包、复杂通用调度器或新的分支体系。
