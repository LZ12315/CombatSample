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

## 后续顺序

1. **系统主链路**：先处理上面的已复现失败，再按 Action → Motion / Locomotion → Combat / 表现 → Camera / Graph 收敛重复职责、旧接口和生命周期问题。不为历史资源无限保留兼容层，也不借机重写框架。
2. **样例／资源瘦身**：在系统职责清楚后，保留足以覆盖核心能力的代表性数据与美术；按实际依赖处理旧场景、草稿配置和未使用资源，不按目录名字盲删。
3. **最终回归**：保留小型稳定合同测试，完成代表性战斗、运动接管、命中／弹反、相机、中断／禁用清理与连续重开；检查 Player 构建与启动。
4. **合回 main**：复核最终代码、数据、文档与项目设置后合回。本阶段只在 main-migration 提交，不推送、不移动 main 或 HyperGraph。
