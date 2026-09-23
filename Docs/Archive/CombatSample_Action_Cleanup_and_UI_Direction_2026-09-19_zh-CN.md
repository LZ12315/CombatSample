> 归档说明（2026-09-24）：所述 Legacy 退役和界面职责已完成；正文中“当前代码依据”是 9 月 19 日快照。现行实现见 Docs/Current 的正式 Action 架构。

# Action 后续清理与界面调整方向

日期：2026-09-19。
状态：历史讨论记录。界面职责与 Legacy 退役均已实施；当前事实以《Action 编辑与播放正式架构》为准。

## 一、待办：彻底退役旧 Action 系统

用户明确指出：这是个人 Demo，不需要长期保留多套兼容系统，也不应每推进一点就扩大检查范围、拖慢开发。

后续目标是只保留正式 Action 编辑与播放系统，而不是将旧代码移入 Legacy 就视为完成清理。

- 项目负责人决定不迁移旧动作，而是按需要从零配置正式 Action。
- 旧 Sequence / Timeline 编辑器、播放器、专用数据、后端字段与分流已经删除，不保留空壳兼容 API。
- 取消旧 Action 创建入口；正式 Action 在当前 Project 目录直接创建一个 `.asset`，不自动创建同名文件夹。
- 验证集中为必要的编译、引用检查和用户主要玩法实测，不为改名、搬迁、删除扩大成全面测试工程。
- 历史实现由 Git 保存；旧磁盘资产不再是受支持的播放输入。

## 二、当前界面需求

用户明确要求：Action 资产本身的属性编辑移回 Unity Inspector；不再放在 Details。界面应清晰、整洁、减少嵌套，融入 Unity 编辑器，不另做风格化主题。

当前代码依据：

- `ActionDetailsWindow.DrawAction` 承载 Action 优先级、重入、触发、取消、标签和准入条件等编辑。
- `ActionAssetInspector` 的正式动作页面主要只有后端、摘要和打开按钮，尚未接管上述属性。
- `ActionEditorStyles.uss` 设置独立背景/文字配色、圆角状态徽标、卡片和嵌套容器；原生控件外又覆盖了较多自定义样式。

建议落实的职责边界：

| 界面 | 内容 |
|---|---|
| Inspector | Action 资产级配置与打开 Timeline 入口 |
| Details | Timeline 选中的动画段、Gameplay 轨道和条目属性 |
| Timeline | 动作时间编排、选择、播放与时间控制 |
| Preview | 当前 Timeline 的视觉预览 |

界面整理方向：

- Action 本体只保留 Inspector 一个属性编辑入口，不在 Details 复制一份。
- Inspector 完整核对现有资产级字段，包括循环与退出条件，不能仅照搬当前 Details 页面而漏掉已有数据。
- 使用 Unity 原生属性字段、工具栏、按钮、折叠组、列表和 HelpBox；保留已有 Drawer 支持。
- 减少“分组外壳 + 同名 Foldout + 属性自身 Foldout”的重复层级，统一标签对齐与间距。
- 去除不必要的自定义底色、文字色、圆角卡片、装饰徽标和重复摘要，适配 Unity 的深色及浅色主题。
- Timeline 的时间刻度、片段、选中态、静音和错误等必要视觉表达继续保留；自定义绘制仅服务于编排信息。
- 不因窗口外观调整改变 Preview 的角色照明、地板、背景与动画求值。
- 沿用 SerializedProperty、Undo 与现有会话通知，Inspector 可独立编辑资产；窗口刷新不产生第二套写入或数据状态。

本节保留当时的设计依据。Action 属性已归入 Inspector，Details 专注 Timeline 内容，Timeline 与 Details 的界面样式已经按后续讨论多轮调整。
