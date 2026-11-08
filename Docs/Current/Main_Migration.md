# HyperGraph → main 整合整理

## 目标与方式

以代码和系统的稳定、简洁、完整为目标，不要求完整保留 Demo 成品内容。

在 `main-migration` 中先合入完整 HyperGraph，再按模块清理；不再逐个功能搬运。验证和整理完成后才合回 `main`。删除代码时同时处理动作、行为图、场景等引用，不留下缺失类型或失效入口。

## 整合基线

- 起点：`main` 的 `898194eb`，包含预览蒙皮刷新和列表复制粘贴两项回迁。
- 合入：`HyperGraph` 的 `7de6a220`，即 Demo 交付检查点。
- 整合基线提交为 `ad4281b4`；该提交尚未裁剪 Demo、资源包、交付文档或项目设置，也未推送远端。后续清理记录见下文。
- 保留 `main` 剪贴板 v2 / JsonUtility 的未保存对象引用修复；列表界面采用 HyperGraph 的现行模型。
- 复制粘贴测试采用 Requirements / 嵌套取消条件，不恢复已删除的 ExitConditions；测试对象保留可编辑标记。
- 文档合并区分历史验证记录与当前版本，移除自动合并引入的重复、过期说明。

## 整合基线验证

- Unity 2022.3.62f3 Roslyn 独立编译整合后的 Runtime 157 个源码、Editor / Tests 81 个源码：均通过，无诊断。
- 将整合源码复制到隔离、无场景与角色资源的工程，通过 Unity 2022.3.62f3 原生 EditMode Test Runner 执行 `AuthoringListClipboardTests`：5 / 5 通过，包含嵌套取消条件及资源引用。
- 四个合并冲突已解决；剪贴板实现保留 main 修复，现行列表绘制与 HyperGraph 一致。
- 这些结果不是完整 Test Runner 套件、主场景战斗、干净导入或 Player Build 的验收。

验证日志和 XML 为本机 `.git/migration-validation/` 下的临时工件，不作为项目运行依赖。

## 第一阶段：开发入口与 Demo 耦合清理

已实施：

- 根 README 改为开发入口，明确 main / main-migration 的关系及未推送状态；跨机器、目录约定和训练场文档同步，不再按最终交付范围限制开发。
- 保留 `CombatTrainingArena` 为主要试玩／回归入口，保留角色、敌人、环境、胜负重开、Build Settings 和全部战斗参数。
- 删除 Demo 专用的每次滞空空连起手次数限制；两份场景只移除专用组件，AirCombo_1 只移除对应 Requirement 和 managed-reference 数据，原有输入与空中状态条件保留。
- 删除专用测试，但将其中两项通用 Action 启动通知合同迁入 `ActionRuntimePlaybackSessionTests`；没有删除 ActionPlayer 的公共启动事件或改变其实现。
- 删除提交说明、设计文档／PDF 与提交打包工具。第三方说明移到 `Docs/Third_Party_Notice_zh-CN.md`，保留依赖归属、分发边界和字体许可入口，未修改任何原始许可证。

### 验证与已发现问题

- Unity 2022.3.62f3 独立编译：Runtime 155 个源码、Editor / Tests 80 个源码均通过，无诊断。
- 清理前先运行隔离原生 EditMode 小型回归：21 项，19 通过、2 失败，连续两次有相同数量失败。
- 清理后运行相应回归：17 项，15 通过、2 失败。减少的 4 项是已删除次数限制的专用用例；迁入的两项启动通知合同和 4 项胜负合同均通过。
- `CombinedMotionItems_KeepOwnerPriorityAndReleaseOnAbort` 清理前后均固定失败：期望 Z 速度 3，实际 0。下一阶段优先核对测试前提及运动仲裁，尚未确定根因，不标记为已修复。
- 剪贴板 fixture 在本次批量运行中不稳定：一次在空列表 CanPaste 失败，一次在保留 Inspector 编辑的 Paste 失败，均为清理前已出现；清理后仍出现前一种失败。整合基线中该 fixture 单独运行 5 / 5 通过，并不代表组合回归可靠。下一阶段需要定位系统剪贴板环境／测试隔离或实现问题，不能仅凭旧通过记录放行。
- 静态检查通过：删除的类型与 GUID 在 Assets 中无剩余文本引用；两份场景除组件块及其挂载槽外逐对象一致；AirCombo_1 除指定条件外数据不变；修改文档的相对链接可解析，旧提交工具／文档链接已清除。
- 本地日志与结果：`.git/migration-validation/stage1-before*`、`stage1-after*`、`stage1-static-checks.json`，不纳入版本控制。
- 主场景实际加载、空中连段手感、连续重开及 Player 构建尚未人工验收。静态引用检查不代替这些检查。

## 第二阶段第一批：失败定位与合同回归稳定化

### 组合运动

- 根因是测试初始化前提不成立：普通 ActorMotor 的 Awake 在 EditMode 中未运行，Motor 为 null，BeginMotion / ComposeMotion 直接返回。零速度并非运动仲裁算出的结果。
- 隔离副本诊断确认了未绑定状态；强行向整组组件发送 Awake 又触发 Unity 原生 `ShouldRunBehaviour()` 断言，因此不把这种启动方式加入该合同测试。
- 改为直接检查 Action 会话生成的策略／运动 owner：水平速度覆盖仍为向前 3，停止后策略恢复、覆盖与 Root Motion owner 释放，并恢复备用运动。没有修改运行时仲裁、Action 默认策略或资源参数。
- 这验证的是运动域与会话的合同，不宣称 KCC 世界、接地或碰撞求解已通过。

### 剪贴板

- 临时诊断确认了系统文本传输层的不稳定：写入 504 字符后首次读取为空、随后再次读取恢复；不经过任何项目序列化的 200 次普通文本写读也有 1 次空读。尚未将其归因于特定外部程序或操作系统缺陷。
- 将完整快照构造与文本校验／应用从操作系统剪贴板读写中分离。编辑器入口仍使用系统剪贴板，v2 / JsonUtility、同会话限制、对象引用和 Undo 行为不变，没有全局测试开关或替代缓存。
- 合同测试用 fixture 自己的文本快照走相同的数据路径，不再读写全局系统剪贴板；保留原有图／资源引用、独立快照与 Undo 断言，并明确检查 null、空串和普通外部文本都被拒绝，不能回退到旧快照。
- 该改动消除了合同测试对系统剪贴板可用性的依赖，不承诺底层 OS 传输永不失败；真实菜单、系统剪贴板与跨窗口刷新仍须人工检查。

### 当前验证

- Runtime 155 个源码、Editor / Tests 80 个源码独立编译通过，无诊断。
- 隔离 Unity 2022.3.62f3 原生 EditMode 回归连续两次 **17 / 17 通过**：Action 播放 8、列表快照／粘贴 5、胜负合同 4。验证使用未插入诊断代码的当前源码。
- 没有删测试、忽略错误日志、增加重试或放宽预期值来获取通过；运动用例的验证层次已经按上述边界重新明确。
- 结果与日志：`.git/migration-validation/stage2-contracts*`、`stage2-diagnostic*`；仅为本机验证工件，不进入版本控制。
- 这不是完整 Test Runner、主场景试玩或 Player 构建验收；第一阶段的失败数字保留作历史对照，以本节作为这两项问题的最新状态。

## 第二阶段第二批：Action 决策与通知边界

### 实施

- ActionPlayer 的 Started / Finished / Interrupted 共用逐订阅者异常隔离：错误仍记录日志，后续观察者继续收到通知。正常完成与异常中断均在旧实例清理后通知；观察者已经启动的新动作不因旧通知的异常而被破坏。
- ASM 防止条件／生命周期／结果回调重入同一决策，候选收集阶段中止后不再启动旧候选或消费输入；finally 释放裁决快照，不清掉回调新提交的待决请求。
- 请求取消先摘下旧请求快照，再执行回调。回调中的新请求不被本次清理误处理，保留到下一决策；已停用的 ASM 仍按原合同同步报告失败。
- 移除 ASM 无实际行为的 ActionFinished 订阅和空处理器。没有改动取消窗口、动作优先级、事件准入规则或公共／序列化接口，也没有修改角色与动作资源。

### 测试整理与验证

- 新增 5 项合同回归，旧实现上 5 项均失败：两种终态通知的异常隔离、清理回调新请求保留、条件中止后不启动／不消费、回调决策不可重入。修复后全部通过。
- 扩大验证时发现 8 项既有失败。其中 4 项为 Action 测试构造错误，已修正：两项优先级测试未注册竞争动作；旧执行回调测试误订阅显式替换不会发送的 Interrupted，改用 Started 时刻；未来 Item 上下文测试把 Item 错放在帧零，改为帧一。保留原有玩法断言，不为旧测试恢复旧行为。
- 删除仅反射检查私有 LateUpdate 不存在的结构测试，不把私有方法名字作为合同。
- Unity 2022.3.62f3 独立编译 Runtime 155 个源码、Editor / Tests 80 个源码通过，无诊断。
- 隔离原生 EditMode 扩展回归 **89 项，85 通过、4 失败**：Context 4 / 4、播放 10 / 10、Scheduler 10 / 10、ASM 52 / 56、剪贴板 5 / 5、胜负 4 / 4。不是全套验收通过。
- 剩余 4 项均在此次生产代码修改前已经失败，保留到 Motion / Locomotion 批次定位，没有改预期或跳过：
  - `LocomotionControlSnapshot_LateSubmissionBelongsToNextTick`：下一 Tick 速度期望 -0.15，实际约 -0.1707。
  - `LocomotionControlRelease_BrakesWhileDisableResetsSharedVelocity`：组件禁用后测试观察到速度未归零。
  - `LocomotionFreeze_PreservesIntentUntilPositiveMotionTick`：恢复后的意图消费／清理不符合测试预期。
  - `MotorCompose_PolicyAndOwnerPriorityKeepRequestedSeparateFromActual`：速度覆盖期望 -3，实际 0；需要核对 KCC 初始化前提。
- 日志和 XML：`.git/migration-validation/action-review-*`，仅本机工件。真实首帧 Gameplay 回调、Actor / Driver 禁用、中断表现与重启仍需主场景人工验证；本批没有宣称所有重入和场景生命周期路径已收口。

## 第二阶段第三批：Motion / Locomotion 验证边界与水平合成

### 定位与整理

- 上批 4 项失败没有证明移动算法错误：晚到输入用例把时序合同绑定到旧的反向积分数值；两项禁用／冻结用例依赖普通 MonoBehaviour 在 EditMode 中自动执行 OnDisable；Motor 用例则未初始化 KCC 却检查 Requested / Actual。
- 晚到输入改为与同一组按正常边界提交的输入结果对照，明确检查下一 Tick 的方向与强度。没有把 -0.15 简单替换为 -0.1707，也没有修改 DirectionResponse、加减速模型或参数。
- 清理用例直接执行 OnDisable / Driver abort 共用的 CancelSimulation，保留冻结不重放、显式释放优先的检查，并检查速度、当前资产与控制输入均被清理。真实禁用／重启回调仍需运行环境验证，不通过反射或发送 Awake 伪造。
- 将 ActorMotor 内的水平来源选择归入它持有的 TranslationDomain，Motor 复用同一实现并记录返回的来源／冲量事实。CharacterUp 投影、地面切向修正、竖直合成和实际世界结果仍由 Motor / KCC 负责；没有新建一套仲裁器或引入新的运动来源。
- 水平合同直接覆盖：自主策略限制、Velocity ＞ RootMotion ＞自主＋冲量、被覆盖 RootMotion 不补偿、actor-local 到世界转换、半速仅缩放一次、冻结不采样动态 provider、provider 使用运动缩放后的 dt。没有用未求解的世界状态默认值充当通过结果。

### 验证

- Unity 2022.3.62f3 独立编译 Runtime 155 个源码、Editor / Tests 80 个源码通过，无诊断。
- 将上一批回归加上现有 LocomotionContractTests：整理前 **118 / 122 通过、4 失败**；整理后 **122 / 122 通过**，没有删掉或忽略这 4 个用例。
- 此范围包括 Action / Context / Scheduler / ASM、剪贴板、胜负及 Locomotion 的 33 项输入／积分／选择／冻结／生命周期合同，不是全部运动动画或完整工程测试。
- 结果与日志：`.git/migration-validation/motion-review-*`；仅本机工件。场景、动作／角色资源、项目设置和所有运动调参未改动。
- 实际 Actor / Driver 禁用及恢复、接地／斜坡／碰撞、Requested 与 Actual 世界反馈、RootMotion 贴墙与重开仍待代表性场景验收；未将纯运动域通过等同于 KCC／Player 验收。

## 第二阶段第四批：Combat 通知与区间表现异常边界

### 定位与实施

- ActorCombater 原先直接调用整组 HealthChanged / Died / ParrySucceeded。观察者异常会中断剩余通知、死亡禁用或结算结果返回，并可能沿 Hit 阶段让整场固定模拟进入故障。现逐订阅者捕获并记录异常，不以表现／观察者错误改变已确定的命中结果。
- 在扣血后、通知观察者前锁定本发命中的致命归属。原先血量回调中追加另一发致命伤害时，原非致命命中会再次发送 Died；现仅追加的致命命中拥有死亡通知，仍保留 Died 先于 GameObject 禁用的时序。
- ActionEffectBuffer 按区间隔离创建／空间更新错误，取消并释放失败区间，同次 World 的其他区间继续，不重试失败实例。释放错误统一在表现资源边界记录；正常 Exit / Interrupt / Abort 和 Actor Clear 不再因一份 presentation 的 Dispose 异常阻断其他区间或 Action 完成。
- ActionEffectPlayer 在已生成对象但初始化／绑定失败时自行隐藏销毁，再交由 Buffer 记录错误，避免丢失尚未返回的资源 handle。
- 审查确认：HitBox 仍按 IDamageable 尝试去重，PendingHit 仍稳定排序并保留已提交命中；弹反反馈仍仅由结果入口选择防守者 Parry Profile，不经通知重复播放。没有修改命中排序、伤害、弹反窗口、霸体／无敌、速度效果持续时长或相机 Impulse 参数。
- 只隔离观察者与区间表现错误，没有吞掉真正的 IDamageable / Gameplay 结算失败。CombatHitBuffer 的部分结算后清空剩余并抛出、结算中追加请求拒绝等既有合同保留。旧 AttackHandler 与命中单次反馈路径未裁剪；不凭本轮代码搜索认定旧资源可删除。

### 测试与验证

- 原选定 Combat / 表现回归 111 项中 109 通过、2 项在死亡测试 SetUp 因 SendMessage("Awake") 触发原生 ShouldRunBehaviour 断言。仅将该 fixture 的启动改为反射调用 ActorCombater 自身 Awake，不发送引擎生命周期消息，不启动整套 Actor / KCC；这是组件初始化的例外，不断言私有结构或为测试增加生产接口。
- 新增 7 项稳定合同：血量观察者错误两种伤害结果、死亡观察者错误、回调追加致命伤害的唯一死亡归属，以及表现创建／更新／退出释放错误。先加入的 6 项在旧实现均失败；补充的退出释放用例在初步实现上仍失败，完成资源边界修复后全通过。原 111 项也全部通过，没有删掉失败断言或忽略错误日志。
- Unity 2022.3.62f3 独立编译 Runtime 155 个源码、Editor / Tests 80 个源码通过，无诊断。
- 与上一批 Action / Motion / 剪贴板 / 胜负回归组合：**236 / 236 通过**。其中 Combat / 表现（包含胜负）118 / 118：死亡 6、弹反规则 17、命中队列 7、反馈路由 12、速度效果 33、Action 区间表现 22、VFX 合同 17、胜负 4。
- 日志与 XML：`.git/migration-validation/combat-review-*`，仅本机工件。该结果不是完整 Test Runner、主工程干净导入、场景或 Player 验收。
- 原生 VFX 初始化失败后的销毁、Prefab Awake 副作用、真实禁用／重启、弹反窗口／事件 Action、多个区间与命中／弹反 Impulse 叠加和连续重开仍待运行环境检查。没有为了这些场景检查把资源依赖或原生粒子测试塞入默认 suite。
- 公共／序列化接口、场景、动作／角色资源、项目设置及美术参数均未改动。

## 第二阶段第五批：Camera 绑定归还与 Graph 任务边界

### 定位与实施

- Soft / Hard Lock 模块原先销毁目标时不归还 Follow / LookAt，虚拟相机留下指向已销毁对象的绑定。现在构造时保存原绑定，销毁运行时对象前逐项归还仍由自身持有的引用；若外部已替换其中一项，不覆盖新绑定，另一项仍独立清理。重复销毁无副作用。
- Move Toward Target 的 Push Once 原先也检查连续模式的隐藏 Timeout；保存过 useTimeout / timeout=0 的节点切回 Push Once 会立即失败。现超时判断只在 Move Until Distance 生效，与既有字段说明一致。
- Move Toward Target / Set Locomotion Intent 检查 Actor 与 ActorLocomotion 的启用状态，失效时任务失败并释放连续意图，不再向拒收意图的组件持续报告 Running / Success。Move 拒绝已停用目标，与 Strafe 的既有处理一致；不修改移动强度、停止距离、计时模型或转向公式。
- 审查 PlayActionTask 与 WithActorTag：保留待决请求可撤销、停止不强停已开始动作、暂停完成结果不重放、标签实际获取引用成对释放等合同，没有增加旧自身／目标接口回退。
- 旧配置核对：Enemy_Test_1 仍有 2 处 targetOverride；Jaeger 两图虽无旧字段，但 target 为空、没有获取步骤，Combat 图动作引用也为空，仍是未完成草稿。它们仍被旧场景／Enemy.prefab／嵌套 FSM 引用，留待样例依赖梳理决定补线或整体裁剪，不孤立删除、不声称迁移已完成。节点合同文档已纠正原来的过期描述。
- Deprecated 目录不能作为删除依据：FilteredCinemachineInputProvider 仍被 CM_FreeLook.prefab 引用，本批保留。没有改动相机优先级、混合／退出快照、构图、输入过滤、Action 震屏通道或序列化调参，也没有变更场景／Prefab／图资产。

### 验证与边界

- 原 NodeCanvas 目标／标签／绕行合同 **34 / 34 通过**；新增 6 项任务边界和 4 项相机资源所有权回归，当前测试在旧生产实现上 **34 / 44 通过、10 项均失败**。修复后均通过，没有删用例或放宽预期。
- 相机 4 项只创建虚拟相机、TargetGroup 与 Transform，直接检查绑定与销毁；不依赖仓库资源、Brain、渲染或机位几何。这是 Unity 引用所有权无法用纯数值合同替代的最小对象层测试，不调用控制器私有生命周期。
- Unity 2022.3.62f3 独立编译 Runtime 155 个源码、Editor / Tests 81 个源码通过，无诊断；新增源码仅一份相机生命周期测试及其 .meta。
- 与上一批选定回归组合：**280 / 280 通过，无跳过**。其中 NodeCanvasActorTargetTests 20、WithActorTagTests 10、CombatStrafeMovementTests 10、CameraRigLifecycleTests 4；其余 236 项为上一批 Action / Motion / Combat / 表现等回归。
- 日志与 XML：`.git/migration-validation/camera-graph-review-*`，仅本机工件。初版相机断言曾触发 NUnit 把 Transform 当集合比较／格式化的问题，最终按引用身份输出布尔断言；旧实现的最终对照以 regressions-old 为准，不把格式化错误当生产缺陷证据。
- 真实 Brain 混合与快速解锁／重锁、控制器／Actor 停用接管、原生 Impulse 叠加、完整 GraphOwner 暂停／恢复／停止、图 Inspector 保存重载和代表性敌人 AI 仍需主工程场景验收。没有把最小绑定测试当作全部相机生命周期或镜头手感验收。

## 第三阶段：样例／资源依赖收敛

### 保留与裁剪

- 以 `CombatTrainingArena.unity` 为第一方入口，递归核对场景、Prefab、Action／Animation／Locomotion、图、导入器 `.meta` 与 Shader Graph 引用；额外保留 Scripts、Tests、Plugins、Resources、Settings、ProjectSettings 引用、编辑器按路径加载资源及第三方来源／许可说明。
- 保留 Katana 玩家与 Giant 敌人，35 份 Action／Action List 数据、56 份 AnimationAsset 与两份 AnimationRigAsset、五份 Locomotion，以及 Giant FSM／BT，覆盖连段、空中受击、绕行标签、显式 Actor 目标、命中反馈和胜负重开。
- 删除 `HyperGraph`、`MiHoYo_Release`、`SampleScene` 及八份旧测试场景；相应 Enemy_Test_1、Jaeger、GreatSword 等草稿图和旧角色／列表配置一起裁剪，不再将它们列为必须恢复的玩法。
- Animancer Transition Preview 的默认模型从带失效 Timeline／嵌套 FSM 的 David 样例改为 Katana AnimationRig 已使用的模型；由此关闭 David／ActorRoot 的旧入口依赖，没有只清空坏引用来保留一个伪完整角色。
- 裁剪 **5,765 个资产、共 12,809 个资产及相应 `.meta` 路径**，保留 1,387 个资产。资产内容从约 **8.1 GiB 收敛到 209 MiB**（不含目录 `.meta`、Packages、Git 历史和导入缓存）。这仅缩小当前工作树，不重写历史。
- 原始美术／数据的现用 GUID 和路径、训练场场景、战斗／动作／运动／相机参数、全部系统源码与测试、插件、Resources、Packages 和 ProjectSettings 保持不变。原许可证、版权、Credits、来源及使用说明保留，部分原包目录因此只剩说明文件。

### 独立工程验证与动画重建

- 先将候选保留集复制到无 Library 的独立工程，用 Unity 2022.3.62f3 恢复原 Package 并编译、导入；校验工具只在 `.git/migration-validation/` 中，不成为项目测试／运行依赖。
- Unity 原生 `AssetDatabase.GetDependencies` 得到训练场 308 项依赖，所有 Assets 路径均在保留集中；检查训练场、保留第一方 Prefab 和 Data，没有缺失脚本、缺失 managed-reference 类型或空 Action List。这不是对全部第三方示例或所有非空对象槽的完整正确性证明。
- 首次导入后 56 份 AnimationAsset 全部报告 Stale。使用现有 `AnimationAssetBakeWorkflow.TryBake` 逐项 Rebuild，**56 / 56 成功并通过原有采样验证**；保存、退出再启动 Unity 后，56 份全部 Ready。
- 重建结果已回写主工作树；逐文件比较确认 **仅 dependencyHash／trajectoryHash 改变**，没有位移／旋转样本、脚步数据、Clip、Rig 或烘焙参数变化。未关闭 freshness 校验，也没有直接篡改哈希来绕过 Rebuild。
- 静态扫描仍记录 17 个历史未解析 GUID：主要在完整保留的 KCC 示例、NodeCanvas 编辑器样式，以及一个第三方 Smoke 材质的旧附加对象中；它们不是这次裁剪导致的新悬空引用。未把“原生范围检查无缺失脚本”表述为“整个仓库零缺失 GUID”。

### 最终验收已开始，尚未放行（第三阶段历史状态；最新结果见第四阶段）

- 在带完整保留资产的独立工程运行全部原生 EditMode：**922 项，906 通过、16 失败，无跳过或 inconclusive**。不是此前选定 280 项的同一范围，不可继续只报 280 全绿。
- 16 项分布：ActionMotionItem 2、ActionMotionPolicy 2、ActionPreviewArchitecture 1、LocomotionAnimation 8、LocomotionMatching 3。日志包含 EditMode 强发 Awake 的原生断言、会话／预览失效分类、已销毁 Graph 的状态访问，以及未创建 Playable 的权重写入；最终原因仍须逐项定位，不能全部先归为测试问题，也不删除／忽略来刷绿。
- **Windows x64 Development Player 构建成功，0 error、4 warning**；输出约 146 MiB。警告包括 KCC 示例旧 Physics API 与无图形设备构建时的环境探针提示，不升级第三方包或改渲染参数来消警。
- 无图形设备和 D3D11 Player 分别连续运行 75 秒，均记录到真实敌人命中、玩家死亡及重载后再次受击。无图形设备产生 Shader 不支持提示，不作为画面验证；D3D11 确认使用 RTX 3060，没有同类 Shader 错误。
- **D3D11 运行确认一项真实清理缺陷**：死亡重载时出现两条 `PlayableGraph is invalid`，栈为 `ActorAnimation.OnDestroy → LocomotionAnimationUtility.Destroy → ManualMixerState.Destroy → Disconnect`。该路径与完整测试的部分 Graph 销毁失败相符，但不能据此归并全部 16 项。下一批优先修复状态／Graph 的释放边界，并复跑真实重开；本批不以 Player 仍能重开为由忽略异常。
- 两次自动冒烟均未模拟玩家操作，75 秒后由验证脚本终止进程；不计为输入、画面构图、连续多轮重开或正常退出验收。
- 工件：`asset-dependency-audit.json`、`final-import-audit.json`、`final-rebuild-results.txt`、`final-full-editmode-results.xml`、`final-build-summary.txt`、`final-player-*-smoke*`，均为本机 `.git/migration-validation/` 临时记录。
- **尚未合回 main、尚未推送。** 剩余是关闭这 16 项失败、完成有图形设备的运行与编辑器操作验收，并复核后合回；不再重新开展逐模块框架整理。

## 第四阶段：Graph 生命周期修复与完整回归闭合

### 生产修复

- 根因：Unity 在调用 PlayableGraph.Destroy 后，本帧内部分节点句柄仍可能返回 IsValid。原先只检查节点的逻辑继续认定旧会话可用，采样或清理时进入已销毁 Graph 的 Disconnect。
- ActorAnimation 的会话、脚相、保护快照、Action 层与采样遍历统一要求所属 PlayableGraph 和节点都有效；Runtime 的缓存复用／释放也使用同一有效性边界。图已销毁时不继续解绑原生连接，不捕获后忽略异常。
- RestoreUpdateMode 先检查 IsGraphInitialized，不在清理时通过 Animancer.Graph 的懒加载 getter 复活图。
- 尚未挂图便显式释放的缓存树通过 Animancer 官方 MarkAsUsed 标记已经处理，再执行原有 Destroy；不关闭全局 UnusedNode 警告，不修改第三方包。这样保留真正遗弃节点的诊断，避免正常缓存释放在 finalizer 线程误报警告。
- 新增旧图会话拒绝提交、重复释放不创建新图的合同。旧实现上明确失败；最初 186 项定向回归由 169 通过／17 失败改善到 173 通过／13 失败，修复同时关闭三项旧 Graph 失败与新增回归。

### 其余 13 项：修正验证前提，不改变玩法

- 五项强发整组 Awake 的用例改到实际共用的 Translation／Rotation 域验证，保留覆盖优先级、只释放自身 owner 及身体局部旋转断言；不伪造 KCC 初始化。Motor 到动画的实际反馈路由仍列为运行检查，不能将域测试当完整世界求解。
- 三项 Move 准备用例先用 SetGraph 创建最小 native Mixer，再计算其权重；不要求 Animator、场景或额外美术，也不改变生产先挂图再准备的路径。
- Control 公开视图只在锁定阶段存在，Motion 后已消费；原始动画快照的合同保持不变。策略用例使用有效 Tracking Runtime，避免空 Mixer 未被选中却对默认零值断言。
- 普通 EditMode enabled 切换不证明 OnDisable 已执行，改调用共用动画会话释放入口；真正 Disable／Enable 仍需运行验收。
- RootMotion 旧条目的 SourceEndTime 由 playRate 派生，所以更改倍率应报告 Timing；单纯重建同长度轨迹才只改变 Content。保留两种情况的区分断言，不改编辑器失效分类实现。
- 保护姿势与释放前已显示的 Start 姿势对照，不硬编码成 Idle；非循环片尾测试先零 dt 建立 native 时钟，再跨越片尾，明确断言源 Clip 与完成时间。

### 验证

- 带完整保留资产的独立 Unity 原生 EditMode **连续两次 923 / 923 通过，无跳过或 inconclusive**：`final-lifecycle-contracts-results.xml`、`final-lifecycle-repeat-results.xml`。比前批多一项生命周期回归，没有删掉失败用例。
- 一次中间运行遭遇 Animancer finalizer 告警与 NUnit 输出 StringBuilder 竞争，XML 只有部分用例且无完整起止时间；该结果明确弃用，不能以其中的 Passed 或部分计数放行。显式释放缓存树的所有权处理完善后，以上两次均生成完整 923 项结果；未用重试或忽略错误替代生产／测试修复。
- 重新启动检查场景／数据仍无缺失脚本、缺失 managed-reference 类型或空 Action List，56 份动画仍全部 Ready；没有再改烘焙、场景、资源、参数、Packages 或 ProjectSettings。
- 更新后的 Windows x64 Development Player 构建成功，BuildReport 为 **0 error、0 warning**。原 KCC 示例旧 API 未改，增量构建无警告不表示第三方源码已升级。
- D3D11 Player 自动连续运行 **180 秒，39 次真实命中、三次玩家死亡并成功重载后再次受击，0 条异常／断言／错误或烘焙过期提示**；未再出现此前 Graph 清理异常。统计见本机 `lifecycle-final-summary.json`。180 秒后由验证脚本结束进程，只验证自动敌人攻击／死亡重开路径，不冒充玩家输入、画面构图、正常退出或完整交互验收。
- 工件均位于本机 `.git/migration-validation/`，包括 `final-failures-before*`、`final-failures-graph-fix*`、`final-lifecycle-*`。未将临时校验脚本、构建或生成目录纳入项目。

## 后续顺序

1. **系统主链路收尾**：已完成 Action、Motion、Combat / 表现和 Camera / Graph 的本轮定向代码审查；不是整体验收完成。剩余真实接管、清理与生命周期纳入后续场景回归；资源梳理若发现现用链路缺口，再定向修复，不继续为外观重构框架。
2. **样例／资源瘦身**：本轮已按训练场与动态入口依赖完成；除最终验收发现具体缺口外，不继续扩展资产清理范围。
3. **最终交互验收**：16 项失败及已复现的重开 Graph 异常已关闭。剩余玩家移动／运动接管、贴墙、主动命中／弹反、镜头快速切换与独立禁用恢复，以及预览蒙皮、系统剪贴板／菜单、图保存重载；自动战斗重开不替代这些实际操作。
4. **合回 main**：复核最终代码、数据、文档与项目设置后合回。本阶段只在 main-migration 提交，不推送、不移动 main 或 HyperGraph。
