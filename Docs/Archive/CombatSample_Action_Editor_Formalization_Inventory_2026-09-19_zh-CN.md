# Action 编辑器正式化盘点与整理清单（2026-09-19）

> 本文是实施前盘点。正式化后的当前结构与入口以 [Action 编辑器正式架构](../Current/CombatSample_Action_Editor_Architecture_2026-09-19_zh-CN.md) 为准；下文“尚未实施”等表述仅描述盘点时点。

状态：代码与磁盘资源盘点完成；以下为建议，尚未实施代码删除、改名、移动或资产迁移。

## 1. 结论与范围

当前编辑器可以正式命名并收拢入口，不需要重新设计会话、Document、Commands 或 Preview。当前主要问题是新旧入口混杂、共享类型寄存在旧目录，以及旧动作尚未完成替换。

“编辑器正式化”与“彻底移除旧后端”并非相同工作量：

- 编辑器正式化：改名、整理目录、由正式 Inspector 接管 ActionAsset、移除原型入口；可以保持现有动作运行行为。
- 完全移除旧后端：还要处理旧 ActionAsset、Playable、动作列表、行为树与旧场景的引用。不能只把后端字段改成新值。

本次不做 Runtime 缺陷排查，不安排逐项玩法验收。

## 2. 当前磁盘资源事实

| 后端 | 全 Assets 中 ActionAsset 数 | 当前启用构建场景的静态引用闭包内 |
|---|---:|---:|
| LegacyTimeline = 0 | 34 | 0 |
| Sequence = 1 | 41 | 38 |
| V1 = 2 | 1 | 1 |
| 合计 | 76 | 39 |

唯一启用的构建场景为 `Assets/Scenes/MiHoYo_Release.unity`。新后端动作是 `Assets/Create/Test/TestAction.asset`，由 Jaeger_Combat 行为树引用。

统计通过 ActionAsset 脚本 GUID 识别资产，以 YAML 的后端字段分类（缺省按代码默认 LegacyTimeline）；静态引用闭包沿 GUID 跟随 .unity/.prefab/.asset/.playable/.controller/.overrideController。它包含间接、备用或未激活引用，**不等于实际一定播放的动作数**，也不包含尚未保存的编辑器修改或运行时字符串加载。

动作列表现状：

| 列表 | 直接引用动作数 | 后端 | 已发现的主要消费者 |
|---|---:|---|---|
| Jaeger | 9 | Sequence | MiHoYo_Release、Camera_Test |
| Kiana | 10 | Sequence | MiHoYo_Release、Camera_Test |
| Boxing | 9 | Legacy Timeline | SampleScene 及多个 Test 场景 |
| Enemy | 10 | Legacy Timeline | SampleScene 及多个 Test 场景 |
| Sword | 5 | Legacy Timeline | Assets/Prefabs/Actor/Characters/Player.prefab |

38 个 Sequence 动作还包含取消关系等间接引用。全项目剩余 3 个不在当前构建静态闭包内的 Sequence 动作是 Jaeger_DashAttack、Kiana_Idle、Kiana_NormalLoco；不在闭包内不代表允许删除。

未找到使用 ActionSequenceAsset 脚本的独立磁盘资产，也未找到挂载 ActionSequenceRunner 的序列化组件；它们仍有旧编辑器/Runtime API 支持代码，不能把“无资源实例”误写成“无代码依赖”。

## 3. 入口问题：优先解决

### 3.1 正式 ActionAsset Inspector 仍属于旧编辑器

`ActionSequence/Editor/ActionSequenceAssetInspectors.cs` 中的 `ActionAssetSequenceInspector` 对所有 ActionAsset 注册 CustomEditor。其 Builder 使用旧 Sequence 的选择、Commands 与文档体系；V1 只是其中一个附加入口。

建议：

- 建立正式 `ActionSystem/Editor/ActionAssetInspector.cs`，接管 ActionAsset。
- 正式动作提供清楚的打开编辑器入口与必要摘要，编辑行为沿用现有共享会话。
- 如保留旧后端，Inspector 根据后端明确显示旧资源，并提供 Legacy 入口；不把旧数据误送进正式编辑器。
- Sequence 专用 Inspector 与其 Builder 放回旧系统，仅保留旧资源需要的功能。
- 清理重复的 CustomEditor 注册，不能新旧两个 Inspector 同时接管 ActionAsset。

### 3.2 创建入口默认仍是 Legacy Timeline

`ActionAssetCreater.cs` 同时提供 ActionAsset、ActionAsset Sequence、ActionAsset V1；最普通的 ActionAsset 入口实际创建 LegacyTimeline 和配套 .playable。

建议正式的 `Create/Combat/Action` 创建当前新后端，名称使用 New Action。旧创建入口退出常用菜单；若决定继续维护旧资源，则放在明确的 Legacy 子菜单。

注意：改变创建入口与改 ActionAsset 字段默认值是两件事。旧资产缺少后端字段时必须继续按旧含义读，不能通过修改字段初始化值隐式迁移。

### 3.3 打开入口混合三种系统

`ActionAssetHelper.cs` 同时处理独立 SequenceAsset、三种 Action 后端及场景 PlayableDirector 绑定。

建议改名为 `ActionAssetOpenHandler`，正式动作直接打开正式 Timeline；仍保留的旧资源显式走 Legacy 打开逻辑。完全删除旧后端后再删除 Director 选择与绑定路径。

## 4. 改名清单

保留现有职责，不新增 SessionManager 或通用框架。

| 当前 | 建议正式名称 |
|---|---|
| ActionV1TimelineWindow | ActionTimelineWindow |
| ActionV1DetailsWindow | ActionDetailsWindow |
| ActionV1PreviewWindow | ActionPreviewWindow |
| ActionV1EditorContext | ActionEditorContext |
| ActionV1EditorCore.cs | ActionEditorContext.cs（同一脚本及 meta 承接现有序列化会话类型） |
| ActionV1EditorDocument / Commands / Operations / Playback / Presentation | 去掉 V1，其余职责名称保留 |
| ActionV1PreviewSpatialEvaluator | ActionPreviewSpatialEvaluator |
| ActionV1PreviewInput / Renderer / CameraUtility 等内部类型 | 去掉 V1 |
| ActionV1EditorStyles.uss | ActionEditorStyles.uss |
| V1ActionPlaybackSession | ActionRuntimePlaybackSession |
| ActionAssetCreater | ActionAssetCreator（修正拼写） |
| ActionAssetHelper | ActionAssetOpenHandler |

补充：

- ActionRuntime、ActionRuntimeScheduler、ActionTimelineData、AnimationAsset、AnimationRigAsset 的名字已经正式，不为统一外观强行重命名。
- 保留旧后端期间，可将枚举 V1 改名为 ActionRuntime，但保留数值 2；UsesV1 对应改为 UsesActionRuntime，更新所有调用。
- 所有正式菜单、窗口文字与新建资源名称去掉 V1；原菜单建议统一到 Tools/Combat/Action。
- 测试中的新系统名称与目录同步调整，不改测试契约来迎合重命名。
- 旧文档标题保持历史原样；当前入口文档更新到正式命名即可。

## 5. 目录归位清单

建议目标结构（目录不代表增加同等数量的管理对象）：

```text
Assets/Scripts/
  ActionSystem/
    Data/                 ActionAsset、ActionAssetList、ActionTimelineData、CancelRule/Window 等
    Conditions/           现有条件及 Editor Drawer
    Runtime/              ActionRuntime、条目 Runtime、采样解析、ActionInstance、ActionContext
      Playback/           播放接口、ActionRuntimePlaybackSession
    Editor/
      ActionAssetInspector.cs
      ActionAssetCreator.cs
      ActionAssetOpenHandler.cs
      Core/               Context、Document、Commands、Operations、Playback、Presentation、校验
      Windows/            Timeline、Details、Preview
      Preview/            SpatialEvaluator
      Styles/             USS
  Animation/              保留 AnimationAsset、Rig、轨迹数据、AnimationConfig
    Editor/               Inspector、单 Clip 烘焙及共享烘焙实现
  Combat/
    HitBox/               形状配置、几何计算、绑定引用
      Editor/             BoneReferenceDrawer
  Actor/
    Motion/               Motor 的运动配置与 Domain
  Legacy/                 仅在选择保留旧动作时存在
    ActionSequence/       原 Sequence 数据、Runtime、编辑器与专用播放 Session
    Timeline/             原 Playable/Track/Behaviour、编辑器与专用播放 Session
```

ActionPlayer、ActionStateManager、ActorAnimation、ActorMotor 和 CombatSimulationDriver 继续放在 Actor，避免为“所有 Action 字样都放一起”打散 Actor 集成边界。当前 Runtime 数据中已经正式命名的类型只移动文件，不同时换 namespace。

具体必须先提取的共享代码：

| 当前位置 | 建议归位 | 原因 |
|---|---|---|
| TimelinePlayable/HitBox/ActionHitBoxClip.cs 中 ActionHitBoxConfig、ActionHitBoxShape | Combat/HitBox/ActionHitBoxConfig.cs | 新旧系统共用的数据不能由旧 Playable 文件承载 |
| TimelinePlayable/HitBox/ActionHitBoxGeometry.cs | Combat/HitBox/ | 当前 Runtime 和 Preview 共用 |
| TimelinePlayable/BoneReference.cs、Editor/BoneReferenceDrawer.cs | Combat/HitBox/ 及其 Editor/ | 绑定已是当前 HitBox 的正式依赖 |
| TimelinePlayable/Impulse/ImpulseConfig.cs | Actor/Motion/ | 新版 ImpulseItem 仍使用 |
| TimelinePlayable/Velocity/VelocityConfig.cs | Actor/Motion/ | 新版 VelocityOverrideItem 仍使用 |
| TimelinePlayable/MotionDirectionMode.cs | Actor/Motion/ | 新版编辑及执行路径使用 |

不要整目录删除 TimelinePlayable。AnimancerParameterMode 也被 Sequence 使用；若只删除旧 Timeline 而保留 Sequence，必须先从 AnimancerClip.cs 提取到保留部分。

当前 Preview 使用 URP 内置 Unlit Shader，没有需要迁移的自建网格 Shader。USS 在 ActionV1EditorTheme.StylePath 中有硬编码路径，移动时必须同步。

## 6. 删除、保留、迁移分类

### A. 可以进入本轮删除清单的历史入口

- `ActionSequence/Editor/ActionSequenceEditorWindow.cs`：Prototype 窗口；在 Scripts/Tests 中未找到其他代码调用其类型。删除窗口及菜单前，仅核对旧布局是否打开该窗口，不保留空壳。
- `ActionSystem/Editor/BuildGameplayAuthorityCutoverMigrator.cs`：E3-G 一次性切换工具，无其他 Scripts/Tests 调用；内部仍硬编码已不在原路径的 AnimationConfig / LocomotionMode。建议撤下并删除这套旧切换菜单，不用它承担新后端迁移。
- 不要连带删除 ActionSequenceEditorSelection：它仍被 Sequence V2 编辑器和 Inspector 使用。
- 独立 ActionSequenceRunner/ActionSequenceAsset 支持可列为次级删除候选；只有明确不再提供独立 Sequence 资源时，才一起移除其打开、创建、Inspector 和 Runtime 重载。不是本轮必须追求的删除量。

### B. 目前仍有依赖，不能直接删除

- Sequence Runtime、条目、数据和 SequenceActionPlaybackSession：当前构建动作仍使用。
- Legacy Timeline 播放和 Playable 类型：34 个旧动作、旧场景/Prefab 仍有引用。
- ActionAsset 内旧后端字段、ActionPlayer 的旧后端路由/预检查、ActionInstance 的对应兼容逻辑：跟随所支持后端一起处理。
- AnimationConfig 及 Locomotion 的动画键/Transition 路径：ActorAnimation.SetLocomotionBase 仍在使用，不能把它当作废弃 Sequence 附属物删除。
- RootMotionBaker 等共用烘焙实现：当前 AnimationAssetBakeWorkflow 仍依赖烘焙体系。旧 AnimationConfig 的烘焙入口另行按依赖整理。
- Unity Timeline 包、Playable API、Animancer：不是自制旧 Timeline 文件的同义词，不能随项目旧编辑器一起删依赖包。

### C. 完全删除旧后端前必须处理的迁移

若目标是最终只剩当前 Action 系统：

1. 先明确旧动作/场景哪些要保留，哪些作为 demo 历史内容舍弃。
2. 保留动作优先原位迁移，保持 ActionAsset GUID，让动作列表、取消关系和行为树引用继续有效。
3. 将动画 Key 通过角色 AnimationConfig 解析为实际 Clip/AnimationAsset；不能只改后端字段。相同 Key 在不同角色下可能不是同一个 Clip。
4. Sequence RootMotion/RootRotation、SelfRotation、FacingSnap、MotionPolicy 等按真实含义转换到当前条目。FacingSnap 的来源回退、Mixer 参数、Transition 混合与 Clip 直接采样不保证一一等价，不能宣称全量自动无损转换。
5. 整动作 MotionConfig、时间区间/终点、循环、Tag、HitBox、Effects、准入和取消配置需有明确映射；本盘点不以“复制字段”代替迁移设计。
6. 已选择舍弃的旧动作，要连同引用它们的旧列表、图或场景一起处理，不能只删除脚本制造 Missing Script。
7. 清理新动作中残留的旧数据：TestAction 虽然后端为 2，仍序列化了旧 _sequenceData 的空轨道类型。删除旧 SerializeReference 类型前也要处理这些残留。
8. 最后移除旧后端、旧字段、旧 Session、旧编辑入口与专用测试。编译成功不代表 SerializeReference 引用已经迁完。

“归档到 Assets 下”只改变位置，代码仍会编译、资产仍被导入，不算真正移除。若决定保存历史而退出项目，应以版本历史或项目外归档为界。

## 7. 实施建议与需要确定的唯一大方向

建议将一次收尾分成连续提交，而不是每一步插入玩法验收：

1. 新版正式命名、目录归位，先提取共享类型。
2. 新建/打开/Inspector 收敛，删除 Prototype 和旧一次性工具。
3. 按确定的旧资源策略，保留明确隔离的 Legacy 兼容区，或迁移/舍弃旧资源后彻底删除旧系统。
4. 统一更新当前架构说明；一次集中编译、资源引用检查和入口检查后交给用户统一实测。

**尚需确定：是否在这轮同时迁移或舍弃旧动作，从而彻底去掉旧后端。**这决定资源改动规模，不影响上面正式命名与归位清单的成立。

以尽快稳定编辑器为优先，我建议本轮先完成正式入口和目录收口，旧动作兼容明确隔离；这不是说旧后端应永久保留。如果用户希望这轮就只剩一套，则按“保留当前 demo 所需动作、其余历史内容明确舍弃”的范围规划迁移，避免盲目迁移所有 75 个旧动作。

## 8. Unity 引用与最低限度检查

- 保留被移动脚本的 .meta / GUID；改名窗口及 ScriptableSingleton 时同时考虑序列化类型身份、窗口布局恢复，不能只替换菜单文字。
- 保留 SerializeReference 数据类型名称与程序集；如果确需改动，先做明确的类型迁移。仅目录移动不需要新增 namespace 或 asmdef。
- 保留序列化字段名、后端枚举数字；改名现有值不能顺便重新编号。
- 核对硬编码 USS 路径、Inspector 注册、资产双击路由及菜单。
- 旧资源迁移需检查 GUID、managed-reference 类型和 Missing Script；不新增截图、窗口几何或每阶段完整玩法测试。
- 本次为静态盘点，没有运行 Unity、编译或 Test Runner。只新增此文档，未修改代码/资产；工作区原有改动全部保留。

## 附录：76 个 ActionAsset 清单

“构建闭包内”含间接和未必执行的引用；“否”不代表无其他用途或允许删除。

| 资源路径 | 当前后端 | 构建闭包内 |
|---|---|---|
| `Assets/Create/ActionAssets/Boxing/Combat/Boxing_AirAttack_1/Boxing_AirAttack_1.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Boxing/Combat/Boxing_AirAttack_2/Boxing_AirAttack_2.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Boxing/Combat/Boxing_AirAttack_3a/Boxing_AirAttack_3a.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Boxing/Combat/Boxing_AirAttack_3b/Boxing_AirAttack_3b.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Boxing/Combat/Boxing_LightAttack_1/Boxing_LightAttack_1.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Boxing/Combat/Boxing_LightAttack_2/Boxing_LightAttack_2.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Boxing/Combat/Boxing_LightAttack_3/Boxing_LightAttack_3.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Boxing/Locomotion/Boxing_Idle/Boxing_Idle.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Boxing/Locomotion/Boxing_NormalLoco/Boxing_NormalLoco.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Boxing/Locomotion/Boxing_NormalLoco_Dir8/Boxing_NormalLoco_Dir8.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Boxing/Locomotion/Jump_Land/Jump_Land.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Boxing/Locomotion/Jump_Loop/Jump_Loop.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Boxing/Locomotion/Jump_Start/Jump_Start.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/E3G_BuildCutover/Hit_Air_Hit/Hit_Air_Hit_Sequence.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/E3G_BuildCutover/Hit_Air_Launch_Hard/Hit_Air_Launch_Hard_Sequence.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/E3G_BuildCutover/Hit_Air_Loop/Hit_Air_Loop_Sequence.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/E3G_BuildCutover/Hit_Air_ReLaunch/Hit_Air_ReLaunch_Sequence.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/E3G_BuildCutover/Hit_Air_Straining/Hit_Air_Straining_Sequence.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/E3G_BuildCutover/Jump_Loop/Jump_Loop_Sequence.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/E3G_BuildCutover/Jump_Start/Jump_Start_Sequence.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Hit_General/General_Hit_Hard/General_Hit_Hard.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Hit_General/General_Hit_Light/General_Hit_Light.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Hit_General/General_Hit_Medium/General_Hit_Medium.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Hit_General/Hit_Air_Grounded/Hit_Air_Grounded.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Hit_General/Hit_Air_Hit/Hit_Air_Hit.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Hit_General/Hit_Air_Launch_Hard/Hit_Air_Launch_Hard.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Hit_General/Hit_Air_Launch_Light/Hit_Air_Launch_Light.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Hit_General/Hit_Air_Loop/Hit_Air_Loop.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Hit_General/Hit_Air_ReLaunch/Hit_Air_ReLaunch.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Hit_General/Hit_Air_Straining/Hit_Air_Straining.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Hit_General/Hit_Air_Straining_loop/Hit_Air_Straining_Loop.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Hit_General/Hit_Bounded_Up/Hit_Bounded_Up.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Hit_General/Hit_GetUp/Hit_GetUp.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Jaeger/Battle/Jaeger_Attack_1/Jaeger_Attack_1.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Jaeger/Battle/Jaeger_Attack_2/Jaeger_Attack_2.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Jaeger/Battle/Jaeger_DashAttack/Jaeger_DashAttack.asset` | Sequence | 否 |
| `Assets/Create/ActionAssets/Jaeger/Battle/Jaeger_RetreatAttack/Jaeger_RetreatAttack.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Jaeger/Hit/Jaeger_Hit_Hard/Jaeger_Hit_Hard.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Jaeger/Hit/Jaeger_Hit_Light/Jaeger_Hit_Light.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Jaeger/Hit/Jaeger_Hit_Medium/Jaeger_Hit_Medium.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Jaeger/Locomotion/Jaeger_Idle/Jaeger_Idle.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Jaeger/Locomotion/Jaeger_Rest/Jaeger_Rest.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Jaeger/Locomotion/Jaeger_Run/Jaeger_Run.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Kiana/Battle/Air/Kiana_AirAttack_1/Kiana_AirAttack_1.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Air/Kiana_AirAttack_2/Kiana_AirAttack_2.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Air/Kiana_AirAttack_3A/Kiana_AirAttack_3A.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Air/Kiana_AirAttack_3B/Kiana_AirAttack_3B.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Air/Kiana_RiderKick_End/Kiana_RiderKick_End.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Air/Kiana_RiderKick_Loop/Kiana_RiderKick_Loop.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Air/Kiana_RiderKick_Start/Kiana_RiderKick_Start.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Kiana_Attack_1/Kiana_Attack_1.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Kiana_Attack_2/Kiana_Attack_2.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Kiana_Attack_3A/Kiana_Attack_3A.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Kiana_Attack_3B/Kiana_Attack_3B.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Kiana_Attack_4A/Kiana_Attack_4A.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Kiana_Attack_4B/Kiana_Attack_4B.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Kiana_Attack_5/Kiana_Attack_5.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Battle/Kiana_Shoryuken/Kiana_Shoryuken.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Hit/Kiana_Hit_Hard/Kiana_Hit_Hard.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Hit/Kiana_Hit_Light/Kiana_Hit_Light.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Locomotion/Kiana_Dodge/Kiana_Dodge.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Kiana/Locomotion/Kiana_Idle/Kiana_Idle.asset` | Sequence | 否 |
| `Assets/Create/ActionAssets/Kiana/Locomotion/Kiana_NormalLoco/Kiana_NormalLoco.asset` | Sequence | 否 |
| `Assets/Create/ActionAssets/Kiana/Locomotion/Kiana_Slide/Kiana_Slide.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Kiana/Locomotion/Kiana_Slide_Back/Kiana_Slide_Back.asset` | Sequence | 是 |
| `Assets/Create/ActionAssets/Sword/Combat/Sword_HeavyAttack_1/Sword_HeavyAttack_1.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Sword/Combat/Sword_HeavyAttack_2/Sword_HeavyAttack_2.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Sword/Combat/Sword_HeavyAttack_3/Sword_HeavyAttack_3.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Sword/Combat/Sword_HeavyAttack_4/Sword_HeavyAttack_4.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Sword/Combat/Sword_LightAttack_1/Sword_LightAttack_1.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Sword/Combat/Sword_LightAttack_2/Sword_LightAttack_2.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Sword/Combat/Sword_LightAttack_3/Sword_LightAttack_3.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Sword/Combat/Sword_LightAttack_4/Sword_LightAttack_4.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Sword/Locomotion/Sword_Idle/Sword_Idle.asset` | Legacy Timeline | 否 |
| `Assets/Create/ActionAssets/Sword/Locomotion/Sword_Loco8_Normal/Sword_Loco8_Normal.asset` | Legacy Timeline | 否 |
| `Assets/Create/Test/TestAction.asset` | V1 | 是 |
