# Scripts 组织与职责整理（2026-10-08）

## 范围与决定

本轮按功能职责整理 `Assets/Scripts`，并完成第二层的三项有限调整：命中反馈入口收拢、HitBox 检测实现独立、取消目标匹配规则统一。用户确认人工测试通过后，授权在 `main` 使用中文提交信息整理提交并推送。

本轮不改变类型名、命名空间、程序集、序列化字段或枚举值；不引入事件总线、通用调度器或新的角色组件。现有默认运行时／Editor 编译边界、NodeCanvas 任务命名空间、第三方插件和生成输入代码均保留。

开始执行时位于 `main`，HEAD 为 `051d61b6`。工作区已有动画、Input meta 修改以及 Test 资产删除，共 7 项；这些内容与删除状态按执行前哈希核对保留。

## 提交组织

1. `5a9e02e3 整理脚本目录并统一 Action 与 Graph 分类`：目录改名、脚本归位、对应 meta 及样式路径；动作取消与命中处理逻辑仍保持整理前版本。
2. `整理命中处理职责并统一动作取消规则`：三项职责调整、相关契约测试、当前文档路径和本轮人工验收记录。本记录随该提交保存，提交哈希以 Git 日志为准。

提交范围为本轮 Scripts、相关测试和文档；执行前已有的 7 项资产修改／删除保留在工作区，不纳入以上提交。

## 第一层：目录约定与文件归位

| 功能域 | 职责 |
| --- | --- |
| Actor | 角色实体、运动与固定模拟协调；保留 Locomotion、Motion 子系统 |
| Action | 动作数据、条件、选择、播放、执行与制作工具；原 ActionSystem |
| Combat | 命中检测与结算、伤害、受击规则、战斗目标 |
| Impact | 反馈配置与接收、反馈上下文准备、VFX、音效、震屏与速度反馈 |
| Animation | 动画数据与制作、Root Motion 及烘焙工具 |
| Camera / Input / UI | 各自的相机、输入、界面功能 |
| Graph | 项目图任务、条件与适配；原 NodeCanvas，不是插件本体 |
| Event / Utils | 保留当前小型基础工具，不新增公共大容器 |

明确子系统才细分目录；小模块不强制添加 Data/Runtime 等空模板。Editor 代码留在所属域的 Editor 目录；Deprecated 仅标记兼容归属，不因此删除或停用。

| 原位置（相对 Assets/Scripts） | 新位置 |
| --- | --- |
| ActionSystem/ 及其目录 meta | Action/，内部层次保留 |
| NodeCanvas/ 及其目录 meta | Graph/，内部层次和类型身份保留 |
| Actor/ActionPlayer.cs | Action/Runtime/ActionPlayer.cs |
| Actor/ActionStateManager.cs | Action/Runtime/ActionStateManager.cs |
| Combat/HitFeedbackProfile.cs | Impact/HitFeedbackProfile.cs |
| Combat/HitFeedbackReceiver.cs | Impact/HitFeedbackReceiver.cs |
| Camera/Utility/ActorCameraControl.*.cs | Camera/ActorCameraControl.*.cs，与主文件放在一起 |
| Actor/ActorTagGlowIndicator.cs | Actor/Presentation/ActorTagGlowIndicator.cs |
| Actor/CharacterControllerRigidbodyPush.cs | Actor/Deprecated/CharacterControllerRigidbodyPush.cs |

64 个已有 C# 文件移动，已有文件及目录 meta 保留原字节内容和 GUID。Camera/Utility 的文件移完后删除空目录及其目录 meta；新增 Actor/Presentation、Actor/Deprecated 的目录 meta。ActionEditorTheme 的 USS 加载路径更新为 `Assets/Scripts/Action/Editor/Styles/ActionEditorStyles.uss`。

## 第二层：三项职责调整

### 1. 命中反馈入口

`CombatHitBuffer` 继续负责稳定排序、调用 IDamageable.TakeDamage 和检查 ImpactAllowed；每条允许反馈的命中同步调用 `ImpactSystem.HandleConfirmedHit`。

Impact 入口接手 ImpactData 创建、目标 Receiver/Profile 查询、启用反馈检查和空间参考准备，再调用现有 ApplyImpact。没有反馈时仍在检查管理器之前返回；缺少配置或管理器的行为、攻击配置与目标 Profile 的处理顺序均保持。

ApplyImpact、EnsureExists 的公开 API 与 ImpactData 的原有字段、构造方式保留。固定 Tick 速度效果的更新、生效延迟、token 释放和异常清理不变。

### 2. HitBox 检测实现

`HitBoxHandle` 和 `ActorHitBoxRuntime` 提取到 `Combat/HitBox/ActorHitBoxRuntime.cs`，新增对应脚本 meta。两个类型名、可见性和实现保持；ActorSimulationRuntime 仍创建并持有它，在同一固定阶段检测命中，并在同一清理路径释放。

提取实现与整理基线 `051d61b6` 中原文件对应代码逐段一致；没有新增 MonoBehaviour，也没有改变命中对象去重、代表 Collider 选择或命中排序。

### 3. 取消目标匹配

`CancelRule.MatchesTarget` 提供 SpecificAction、AnyWithTag、Any 的共用目标判断。ActionStateManager 的普通候选展开和 External 校验调用同一判断。

该判断不接管取消窗口、准入条件、候选顺序、优先级或请求回调。Any 排除 Event；SpecificAction 的目标匹配本身不排除 Event，保留原普通指定目标路径的行为；External 来源仍先拒绝 Event，AnyWithTag 的普通展开仍过滤 Event。

## 验证结果与边界

- 使用 Unity 2022.3.62f3 自带 Roslyn、现有响应文件的编译参数和项目当前源码编译；输出全部位于 `/tmp/opencode/combat-scripts-*`。没有编辑 Library、Temp、csproj 或 sln。
- 基线：Runtime 143 个源码，0 error、12 个既有 warning；Editor/测试 51 个源码，0 error、0 warning。
- 整理后：Runtime 144 个源码（多出独立 HitBox 文件），Editor/测试 51 个源码；编译均为 0 error，warning 数量与基线相同。另做去除 UNITY_EDITOR 定义的 Runtime 源编译，通过；这不等于实际 Player Build。
- 对基线与整理后编译的 Runtime 程序集分别提取项目类型身份、公开成员、序列化字段和枚举值清单（排除编译器生成基础设施），3,358 条记录逐项一致。
- 对新编译程序集，在独立 Unity Mono 进程调用可脱离引擎的现有及新增 NUnit 用例：36 个用例通过、0 失败。覆盖命中排序、互相击杀、已死亡目标跳过、异常与重入清理、速度时长换算、动画倍率、反馈启用规则、Timeline 磁吸和 ActionContext 数值合同。
- 目标 Profile 的原测试改为验证 Impact 的反馈识别。新增反馈启用、无反馈入口及 Specific/Any/Tag 匹配合同。新增启用效果的两个纯逻辑用例已执行；其余新增用例依赖 Unity 原生环境，仅完成编译。
- 静态检查：64 个 C# 文件归位；除明确的逻辑修改文件外，原脚本与样式内容逐字节保持，包括 Camera partial 的原 BOM/换行。已有脚本 meta 内容保留，新增脚本与目录 meta 齐全，Assets 中 4,076 个 GUID 无重复；7 项预存用户变更保持。本轮脚本、测试与文档差异格式检查通过。用户动画文件已有尾随空格，不在本轮格式修改范围内。
- Unity Editor 启动被 `libxml2.so.2` 缺失阻断，没有运行完整 Test Runner，没有把原生对象、物理、窗口或场景检查记作通过。

详细编译与纯逻辑日志为本机临时产物，不是交付依赖；自动测试状态以上述实际执行结果为准。

## 用户验收反馈（2026-10-08）

用户反馈“我测试好了，没问题”。据此确认本轮 Scripts 目录整理与三项职责调整的人工验收通过，本轮范围收口。

该反馈未附 Test Runner 结果报告，因此不将上述未执行的原生自动测试改写为套件通过。

### 本轮原定复查范围

1. 重新导入脚本，检查 Console、Missing Script 和 Action 编辑器样式。
2. Timeline、Details、Preview 的选择联动、拖动、参数修改、Undo/Redo 与保存。
3. 普通命中、霸体、无敌、死亡的反馈许可，火花位置和朝向、音效、震屏、顿帧及恢复。
4. HitBox 激活／失活、多个 Collider 的去重、动作中断与角色禁用后的清理。
5. 在 Unity 中运行动作仲裁与取消目标匹配、反馈 Profile、速度效果、HitBox 相关合同；新增取消匹配用例也需在此执行。
6. 相机及角色 Tag 发光组件的原有引用与行为。

## 后续独立事项（本次未做）

### 输入历史与消费

目标：从 PlayerInputController 独立输入历史记录、过期与消费规则，保留现有调用入口。输入时钟、长短按手感、角色切换清理、成功启动后才消费的行为必须单独明确；不能在职责整理中顺带修改。

### Details 条目配置绘制

目标：独立 Gameplay Item 参数界面的绘制职责，窗口继续拥有选择、草稿、预览、绑定会话与刷新。绘制器沿用现有资产写入和变更通知机制，不创建第二套 Undo 或编辑状态；失效回调、选择切换和绑定会话检查需单独验证。

### 保留的其他候选

ImpactData 重复字段整理、旧效果管理回路、ActorCombater 的目标算法、相机兼容入口及 Timeline 深度拆分均未实施。Enums、命名空间和 asmdef 本次保持现状；未来新增枚举优先使用功能明确的独立类型，不顺手扩大公共 Enums。上述记录不自动成为活动任务。
