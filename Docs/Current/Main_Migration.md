# HyperGraph → main 整合整理

## 目标与方式

以代码和系统的稳定、简洁、完整为目标，不要求完整保留 Demo 成品内容。

在 `main-migration` 中先合入完整 HyperGraph，再按模块清理；不再逐个功能搬运。验证和整理完成后才合回 `main`。删除代码时同时处理动作、行为图、场景等引用，不留下缺失类型或失效入口。

## 整合基线

- 起点：`main` 的 `898194eb`，包含预览蒙皮刷新和列表复制粘贴两项回迁。
- 合入：`HyperGraph` 的 `7de6a220`，即 Demo 交付检查点。
- 当前阶段仅建立完整基线，没有裁剪 Demo、资源包、交付文档或项目设置，也没有推送远端。
- 保留 `main` 剪贴板 v2 / JsonUtility 的未保存对象引用修复；列表界面采用 HyperGraph 的现行模型。
- 复制粘贴测试采用 Requirements / 嵌套取消条件，不恢复已删除的 ExitConditions；测试对象保留可编辑标记。
- 文档合并区分历史验证记录与当前版本，移除自动合并引入的重复、过期说明。

## 本次验证

- Unity 2022.3.62f3 Roslyn 独立编译整合后的 Runtime 157 个源码、Editor / Tests 81 个源码：均通过，无诊断。
- 将整合源码复制到隔离、无场景与角色资源的工程，通过 Unity 2022.3.62f3 原生 EditMode Test Runner 执行 `AuthoringListClipboardTests`：5 / 5 通过，包含嵌套取消条件及资源引用。
- 四个合并冲突已解决；剪贴板实现保留 main 修复，现行列表绘制与 HyperGraph 一致。
- 这些结果不是完整 Test Runner 套件、主场景战斗、干净导入或 Player Build 的验收。

验证日志和 XML 为本机 `.git/migration-validation/` 下的临时工件，不作为项目运行依赖。

## 后续按模块推进

1. 清理明确的 Demo 专用规则与提交打包内容，核对其代码／序列化引用闭包。
2. 选择保留的代表性战斗测试入口，再处理场景、角色配置和未使用美术资源。
3. 整理 Action / Motion / Locomotion / Combat / Camera / Graph 的重复职责、旧接口和生命周期问题；不为历史资源无限保留兼容层。
4. 配套测试随稳定合同保留，删去只保护临时结构的测试；完成小型回归、代表性战斗和重启／停用清理检查。
5. 复核最终差异和项目设置后合回 main。上述清理与验收尚未完成。
