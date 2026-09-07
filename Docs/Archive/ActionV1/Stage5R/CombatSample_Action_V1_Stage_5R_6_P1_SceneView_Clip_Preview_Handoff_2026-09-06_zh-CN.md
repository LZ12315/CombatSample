# Stage 5R.6 P1 — SceneView 基础层与单 Clip 预览 Handoff

> **2026-09-08 归档说明：P1 实现已撤下。它未通过 Unity 完整验收，并出现角色变形；本文仅作为历史证据。**

> 日期：2026-09-06
> 状态：历史开发检查曾通过；实现已撤下，Unity 功能、隔离及性能未获接受。
> 阶段边界：本文不再授权进入 P2 Action Timeline 动画时间映射。

## 1. 本次交付

`ActionV1PreviewWindow` 已由普通 `EditorWindow + PreviewRenderUtility` 自制视口替换为原生 `SceneView`：

- 使用本窗口 `customScene` 承载独立 Preview Scene；相机、网格、Orbit、Pan、Zoom 和渲染由 SceneView 管理。
- UI Toolkit 浮层提供 PreviewCharacter、Animator、AnimationClip、秒数、Scrub、First、Play/Pause、Last、Frame Camera、Retry 和折叠诊断。
- 窗口明确显示 `Animation Foundation · Action Evaluation Pending`。P1 不需要 ActionAsset，也不读取或修改 Shared CurrentFrame、Preview Loop、Target、Direction 或 Selection。
- 向视口拖入 GameObject 时拦截 SceneView 默认实例化，改为设置 Shared PreviewCharacter，再进入白名单构建路径。
- 旧 Action evaluator、RootMotion、SelfRotation、HitBox、自制相机和 `PreviewRenderUtility` 已退出本窗口运行路径；没有保留自动回退后端。

## 2. 隔离与采样实现

Preview Session 只新建 Transform 层级、MeshFilter、MeshRenderer、SkinnedMeshRenderer 和 Animator。Skinned bones 与 rootBone 会重映射；Animator 不复制 RuntimeAnimatorController 或源 AnimancerComponent。

源角色的 Actor、Motor、Collider、Rigidbody、Audio、ParticleSystem、Cloth、ExecuteAlways 和其他自定义脚本均不会被实例化。Mesh、Material、Avatar 只以 shared 引用读取。角色根位置与旋转在预览空间归零，保留源根局部缩放及子层级局部变换。

Session 保存 Transform、activeSelf、Renderer enabled、SkinnedMesh localBounds 和 BlendShape 基准。每次采样先恢复基准；求值后再次恢复预览角色根与所选 Animator 根，保证 P1 原地预览。

Animator 解析顺序为：根 Actor 明确 Animancer.Animator、唯一子 Actor 明确绑定、唯一 Animator、多个候选时显式选择。明确绑定指向所选子树外会阻塞，不回退到其他 Animator。

动画使用本地 Animancer 8.0.2：

- 每个 Session 只持有一个独立 `AnimancerGraph` 和 `DummyAnimancerComponent`；
- 使用纯 Clip state，Manual update、PauseGraph、Speed 0、Animator/Foot IK 关闭；
- `Animator.fireEvents=false`、`applyRootMotion=false`；
- 采样只调用公开的 `PlayableGraph.Evaluate(0)`，不调用会消费包级事件队列的 `AnimancerGraph.Evaluate()`；
- 切换 Clip 或显式 Animator 时销毁并重建 Graph；同一 Clip Scrub 复用现有 Graph/state；
- 不支持的材质、ObjectReference 或白名单外绑定会阻塞该 Clip并显示诊断。

## 3. 刷新、性能与资源收尾

- Build、Sample、Refresh 分别具有 `ActionV1.Preview.Build`、`ActionV1.Preview.Sample`、`ActionV1.Preview.Refresh` ProfilerMarker。
- 空闲且没有待处理请求时，窗口解除 `EditorApplication.update` 订阅。因此静止状态不会持续执行本窗口刷新、Graph 求值、依赖查询、Document 或 Validator。
- 播放由单一 Editor 时钟推进，采样和时间 UI 最多 60 次/秒；不补帧循环。到末尾停止，末尾再次播放从 0 开始。
- Scrub 自动暂停；非有限秒数被拒绝；相同角色版本、Clip 和秒数不会重复求值。
- projectChanged、hierarchyChanged 和 ObjectChangeEvents 只标记相关 Session 重建请求；Undo/Redo 对源对象造成的实际变化也由这些事件覆盖。Build 不发生在 OnSceneGUI/Repaint。
- 关闭、重载、切换角色或进入 Play Mode 时，先停止时钟并解除 SceneView customScene 引用，再销毁本 Session Graph、白名单实例并关闭 Preview Scene。不会销毁或禁用 Animancer 的共享 Invoker。

## 4. 开发侧证据

### 编译

```text
dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q
Build succeeded.
0 Warning(s)
0 Error(s)
```

Unity 生成工程已包含 `Assets/Scripts/ActionSystem/Editor/V1/ActionV1PreviewWindow.cs`。未修改 `.csproj`、`.sln`、Library、Temp 或包源码。

### 纯生产逻辑断言

临时程序通过反射直接调用本次编译产物中的 `ActionV1PreviewPlayback`，覆盖正常推进、Clip 末尾停止、负/NaN elapsed、零长度 Clip、负/越界/NaN 秒数钳制、相同秒数合并及初次采样。结果：`P1 production playback logic: 10 assertions passed.` 临时程序及其产物已清理，未留下长期 harness。

### 空白检查

本次 Preview 源文件没有行尾空白。仓库级 `git diff --check` 仍报告用户现有 Scene / Asset 的 YAML 行尾空白；这些与 P1 无关，未顺手修改。

## 5. Unity 待验收

以下项目不能由命令行编译替代：

1. 使用项目实际角色和 Clip 验证 SceneView 相机、Scrub、播放、First/Last、Frame Camera。
2. 验证子层 Animator、多 Animator 显式选择、无 Animator、缺资源及不支持 Clip binding 的诊断。
3. A/B Clip 切换和任意方向 Scrub 后返回同一秒数，姿势一致且无累计根位移。
4. 用带 Awake、OnEnable、OnDisable、OnDestroy、ExecuteAlways 和动画事件计数的临时角色确认源脚本从未执行。
5. 确认 Prefab、场景角色、Scene、ActionAsset、Mesh、Material、Avatar 的内容和原 dirty 状态不变。
6. 连续开关与换角色至少 10 次，并验证 Domain Reload、资源重导入、Play Mode 切换和失败 Retry 后无 Graph、实例、Scene 或回调累积。
7. 同一环境预热后分别观察约 30 秒：Preview 关闭、打开静止、持续 Scrub、连续播放。记录 Editor CPU、主线程、GC 和三个 ProfilerMarker；静止时本窗口自身调用应为零。
8. 检查 2560×1440、1920×1080 和窄 Dock；确认 Console 没有新增 compile error、UI Toolkit exception 或持续日志。

若出现卡顿、资源残留、脚本执行或姿势污染，P1 不能通过，只修正本阶段。用户明确接受后，下一步才是 P2 Action Timeline 动画时间映射。
