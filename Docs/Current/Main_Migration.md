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

## 后续顺序

1. **系统主链路**：下一批优先定位上述 4 项 Motion / Locomotion 失败，再继续运动接管与恢复、Combat / 表现、Camera / Graph 的重复职责、旧接口和生命周期审查。不为历史资源无限保留兼容层，也不借机重写框架。
2. **样例／资源瘦身**：在系统职责清楚后，保留足以覆盖核心能力的代表性数据与美术；按实际依赖处理旧场景、草稿配置和未使用资源，不按目录名字盲删。
3. **最终回归**：保留小型稳定合同测试，完成代表性战斗、运动接管、命中／弹反、相机、中断／禁用清理与连续重开；检查 Player 构建与启动。
4. **合回 main**：复核最终代码、数据、文档与项目设置后合回。本阶段只在 main-migration 提交，不推送、不移动 main 或 HyperGraph。
