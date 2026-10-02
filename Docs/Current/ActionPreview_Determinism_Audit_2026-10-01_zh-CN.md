# ActionPreview 确定性排查与修复

日期：2026-10-01。验证环境：Unity 2022.3.62f3。

## 结论

代码存在历史状态输入、手动导航与播放时钟竞争、连续采样时间显示不准确三个问题，已修复。目标契约是：给定同一个 Action、预览角色、内容版本和精确 PreviewPosition，骨骼姿态与根位移不随访问顺序变化。

实际 Clip 的批处理采样已通过。最初反映的明显视觉错位尚未在原 Action 窗口中完整复现，因此这份结果不能替代该窗口的人工验收。

## 发现与修复

### 1. 同 Clip 跳帧沿用上一帧输入

旧实现只在切换 Clip 时恢复 Transform 基准。同 Clip 内求值时，上一帧已经应用的世界位置以及未被动画覆盖的 Transform 通道仍留在实例中。

新增 `ActionPreviewPoseSampler`：

1. 每次恢复角色根节点与子节点的初始 local position / rotation / scale。
2. 在固定参考空间内采样绝对 SourceTime。
3. Renderer 在采样后独立应用空间累积结果，再生成 HitBox。

保留同 Clip 的 Manual Graph 复用，逐帧播放不会每帧重建 Graph。

旧实现仅调用一次 `SetTime`。现在连续设置两次相同时间，同步 current / previous time；这与仓库中 Animancer `AnimancerState.RawTime` / `SoloAnimation.SetTime` 的绝对时间跳转方式一致，避免跨越旧时间到新时间的 RootMotion 区间。

### 2. 内容失效没有失效原生 Graph

旧 `InvalidateData` 清除动画记录与空间缓存，但保留同一个 Clip 对象对应的 Playable。Clip 同对象重新导入或内容变化时，下一次求值可能继续使用旧绑定。

现在内容失效同时销毁 Graph，后续求值从原始姿态重新绑定。普通播放位置变更只重采样，不触发资源重建。

### 3. 手动导航与连续播放竞争

拖动、左右逐帧、首末帧导航均调用 `SetFrame`，此前这些调用不会暂停 `ActionEditorPlayback`；后续 Editor update 可以立即推进手动指定的时间。

现在 `SetFrame` 暂停时钟后定位到整数帧，并在同一次通知中发布 Playback 与时间变更。同帧点击也会暂停播放。

### 4. 显示相同 Frame 不代表采样时间相同

旧 Timeline 的时间读数与播放头使用 `CurrentFrame = floor(PreviewPosition)`，Preview 却采样连续 `PreviewPosition`。例如 30.1 与 30.9 显示同样的 30，动作实际处于不同时间。

现在读数显示实际 PreviewPosition（最多三位小数），Tooltip 显示完整精度与所属 Frame；播放头按连续位置定位。整数拖动和逐帧仍对应精确整数采样。

## 已完成验证

- 用现有 Unity 编译响应文件及新源码编译完整 Editor / 测试程序集；输出覆盖到临时目录，编译通过。
- 在隔离的最小 Unity 工程内加载该程序集，调用 12 个针对性 NUnit 契约用例，全部通过。没有执行完整默认 Test Runner suite。
- 新增两个纯内存模型的 EditMode 姿态契约测试：访问顺序与外部根变换独立；Clip 切换、空姿态和同对象 Clip 内容更新正确恢复。
- 新增三个手动导航用例：同帧、连续时间取整、后退一帧，验证时钟暂停且观察者只收到一次完整状态。
- 已有 SourceTime / Hold、连续时间、空间累积、HitBox 位移、TranslationDomain 所有权契约通过。

真实资源对比使用仓库模型 `Avatar_Kiana_C2_Model.FBX`、Battle/Clip 中 7 个 Attack Clip 和 AirAttack_End。每个实例包含 99 个 Transform。执行逐帧与循环回跳、200 次固定随机种子 seek、跨 Clip、空姿态、失效重建以及模拟外部世界位移 / 转向，再回到固定目标时间：

- 合计 4,497 次目标姿态比较。
- 最大 local position / rotation / scale 差异均为 0。
- 初始旧实现探针测到的 local position 差异约为 `1.6e-7` 至 `5.1e-7` Unity 单位；这证实输入历史影响，但该量级本身不能解释明显的肉眼错位。

临时探针和日志位于 `/tmp/opencode/action-preview-probe/`，不作为项目默认测试或资源。

## Unity 窗口复查

1. 对最初出现异常的 Action，固定 Frame（例如 30），从首帧、末帧、正常播放和反向拖动返回；对比骨盆、脚、手与 HitBox。
2. 开始播放后拖动，或点击左右逐帧，确认播放立即暂停并停在整数时间；循环多次后重复同一比较。
3. 在播放暂停于小数时间时确认时间读数、小数播放头与 Tooltip 一致；整数导航后读数回到整数。
4. 复查跨动画段、首段前空白、段间 Hold、Duration 终点，以及 Clip 重导入后同帧更新。
5. 检查关闭 / 重开 Details、脚本重载、进入 / 退出 Play Mode 的预览资源释放，以及恢复 Transform 后的播放流畅度。

Preview 仍按既有契约只呈现动画、RootMotion 平移与 HitBox；运行时 SelfRotation、碰撞、Velocity / Impulse 和特效随机性需要游戏运行环境。与运行时整体朝向 / 运动的差异应按这条边界判断。

## 后续修正：旧窗口脚本身份残留

Unity 重载时出现 `'ActionPreviewPanel' is missing the class attribute 'ExtensionOfNativeClass'!`。确认此前合并窗口的提交将 `ActionPreviewWindow.cs` / `.meta` 重命名为 `ActionPreviewPanel.cs` / `.meta`，保留了旧 EditorWindow 的 GUID `043077454134471b801068dfbd10fdd3`，但类型已变为普通可序列化类。旧窗口实例恢复时会沿旧 GUID 加载普通面板，造成原生窗口类型不匹配。

为 `ActionPreviewPanel.cs.meta` 分配独立的新 GUID。面板仍作为 `ActionDetailsWindow._previewPanel` 的内联可序列化字段保存；当前 Assets 中没有场景、Prefab 或资产引用旧面板脚本 GUID。旧独立 Preview 窗口状态已不作为可迁移输入。

Unity 刷新后如仍保留旧 Preview 页签，应关闭该页签；若旧布局持续尝试恢复已移除的窗口，可切换到 Default 布局，再从 Tools/CombatSample/Action Details 打开当前窗口。不要为普通面板添加原生类型属性或重新继承 EditorWindow。
