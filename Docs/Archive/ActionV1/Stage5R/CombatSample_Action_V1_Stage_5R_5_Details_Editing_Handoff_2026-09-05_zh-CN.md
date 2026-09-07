# CombatSample Action V1 — Stage 5R.5 Details 字段编辑 Handoff

> 状态：开发侧实现与编译检查通过，等待 Unity 人工验收
>
> 日期：2026-09-05
>
> 设计权威：`CombatSample_Action_V1_Stage_5_Editor_Redesign_Draft_2026-09-01_zh-CN.md`

## 前置状态

用户已接受 Stage 5R.4-C 的 Timeline 行为，包括最后补充的“拖动时隐藏原位置 Entry”表现。5R.5 未修改 Runtime、Timeline 序列化结构、Validator 规则、Preview evaluator 或 Legacy 路由。

## 已实现

- Action Details 从只读页面升级为 Primary Selection 属性编辑器；多选数量继续显示，但不执行隐式批量编辑。
- Action 页面只开放 V1 仍需沿用的 Priority、Cancel Rules、Self Tags、Trigger / Event Tag、Start Context、Reentry 和 Entry Conditions；Legacy Timeline、Sequence、Playback Backend、旧 MotionConfig 与 Runtime Loop 未暴露。
- Point、Range 与 AnimationSegment 使用 Editor-only 时间草稿。草稿提供 Apply / Revert、Enter / Escape；中间非法输入不写入资产。
- AnimationSegment 更换 `AnimationAsset` 时保留 SourceRange 与 PlayRate；`Use Full Clip` 只修改草稿 SourceRange，仍需 Apply。
- Details 草稿与 Timeline Ghost/提交共用 `ActionV1TimelineOperationSnapshot`、候选结果、Animation overlap 规则和 stale-source 检查。有效提交一次 Undo；NoChange、Rejected、Revert 不写 dirty/Undo。
- 七种 Gameplay Config 使用类型化、条件化字段；隐藏字段不在绑定或切换枚举时清空。Bone、Tag、LayerMask、Curve、资源引用以及 SerializeReference 列表继续由 Unity `SerializedProperty` / `PropertyField` 承载。
- 缺失 concrete Config 或 Impulse / HitBox / Velocity 必需嵌套配置时，提供显式 `Create Default Configuration`，单次 Undo，只补缺失对象。
- IdentityBlocked 时字段只读并提供显式 Repair；Details 写操作与 Timeline Pointer gesture 共享 Editor-only 准入锁。
- 普通叶子编辑不再整页 Clear/Rebuild；只更新共享 Document/Validation 与当前问题摘要。条件结构改变、Selection/Action 改变、Undo/Redo 才重建必要页面，并恢复 Scroll/Foldout 会话状态。
- 多个窗口构建同一 Action 文档时复用基于 Action 与 Animation 依赖 dirty signature 的 Validation 结果，避免同一变更重复运行 Validator。

## 修改文件

- `Assets/Scripts/ActionSystem/Editor/V1/ActionV1DetailsWindow.cs`
- `Assets/Scripts/ActionSystem/Editor/V1/ActionV1EditorCore.cs`
- `Assets/Scripts/ActionSystem/Editor/V1/ActionV1TimelineWindow.cs`
- `Assets/Scripts/ActionSystem/Editor/V1/ActionV1EditorStyles.uss`
- `Docs/README.md`

## 开发侧验证

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`：成功，0 warning / 0 error；生成工程明确包含 `ActionV1DetailsWindow.cs`。
- 直接反射本次 build 的生产程序集：60Hz Animation Duration 推导 4/4 通过；共享 Pointer gesture gate 5/5 通过。没有保留测试 harness。
- 对绝对 Point / Range 草稿进行程序集外完整构造时，PowerShell/.NET Framework 无法解析 Unity 的 `netstandard 2.1` 与 `UnityEngine.CoreModule` 依赖，因此没有将该尝试记为通过；对应完整行为留给 Unity 人工验收。
- 本阶段四个 Editor 源文件的尾随空白检查通过。全工作树 `git diff --check` 仍只报告用户已有临时 ActionAsset / Scene 尾随空白，本阶段没有修改这些资产或 Scene。
- Unity Test Runner 未作为验收门槛。

## Unity 人工验收

1. Action、Lane、Segment、Point/Range Item 页面字段显示正确；多选只修改 Primary。
2. Point/Range/Segment 草稿的 Apply、Revert、Enter、Escape、非法值、NoChange 与单步 Undo/Redo。
3. 更换 AnimationAsset 保留 Trim/PlayRate；Use Full Clip 后仍需 Apply；overlap、目标删除/重排及 Clip 依赖变化会拒绝陈旧提交。
4. 七种 Config 的条件显隐、隐藏值保留、缺失配置创建，以及 Tag/Bone/Curve/LayerMask/effects/conditions 的原生编辑体验。
5. 叶子输入不丢焦点、Scroll/Foldout；Timeline 的 Duration、Badge、Issues 与 Details 同步。
6. IdentityBlocked、活动 Timeline 手势、Domain Reload、窗口关闭及三种 Dock 宽度下无新增 Console、USS 或 UI Toolkit 异常。

人工验收通过前，不进入 Stage 5R.6 Preview。
