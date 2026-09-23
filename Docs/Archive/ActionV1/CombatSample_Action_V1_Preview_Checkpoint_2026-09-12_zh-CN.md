# CombatSample Action V1 — Preview 收尾检查点

日期：2026-09-12。状态：本轮代码修正已编译；Unity 操作、画面和长时间生命周期验收尚未完成。

本文件记录当前源码与用户已确认的边界，不是不可修改的规格。遇到差异应明确区分当前代码、旧报告和新需求，不以兼容旧报告为理由增加抽象层。9 月 8 日的 Pre-Preview checkpoint 保留为历史快照。

## 已确认的产品边界

- Timeline 负责 CurrentAction 和播放操作；共享 Editor clock 保存连续 PreviewPosition，并派生整数 CurrentFrame。关闭 Timeline 停止播放。
- Details 编辑当前 Action 的选中内容，顶部只读显示 Action 并提供定位按钮；从菜单打开 Details 不再读取 Project Selection 改变 Action。Inspector 的打开入口继续进入 Timeline。
- Preview 是 Timeline 的视觉求值窗口，没有独立 Action、播放、进度条、Loop、Focus 按钮或 F 快捷键。Preview Settings 仅保留可选 Character Override。
- Preview 显示动画 Pose、已烘焙的水平 RootMotion 位移及轨迹、起点和 HitBox。没有 Target / Direction 输入，不求值 SelfRotation，不运行完整 gameplay replay。实际转向效果留到游戏中验证。
- AnimationAsset 保存动画与烘焙结果，引用可复用的 AnimationRigAsset。Rig 提供 BakeRigPrefab、DefaultPreviewPrefab 和 BakeSettings；两个 Prefab 可以不同。
- 当前预览角色必须是恰好一个 Animator、没有 MonoBehaviour 的 presentation prefab；不自动清洗任意 production prefab。Animator controller、animation events 和 Animator 自带 root motion 不参与 Preview 驱动。
- 保留已有相机操作、初次取景、蓝色背景、无网格灰色地板、照明与 2× 超采样。地板固定于初始根位置，不跟随角色，不加入碰撞或阴影。本轮不继续调整视觉风格。

## 本轮修正

| 位置 | 修正与行为 |
| --- | --- |
| ActionV1EditorCore | 文档/选择校验通过连续位置进行范围限制。打开或刷新 Details 不再将小数位置取整；终点允许停留在 Duration，整数 CurrentFrame 仍限制在最后一帧。动作缩短时才向新终点收缩。 |
| AnimationAssetBakeWindow | 切换目标资产时完整载入它自己的 Clip / Rig（包含空值），不再保留前一个资产的配置；用户编辑草稿时不自动回填。Apply 只保存当前目标资产。 |
| AnimationAssetBakeWorkflow | 窗口使用待应用的 Clip / Rig 生成并验证烘焙结果，成功后一次 Undo 记录提交配置与数据，只保存目标资产；失败时不改目标。保留原有公开 TryBake 入口及旧资产配置读取方式。 |
| ActionV1TimelineWindow / Details / Core | 改名使用 Presentation 通知，静音保留 Content 通知；轻量刷新同步轨道标题、静音样式和 Toggle，不进入停止播放、重建布局的路径。结构/时间编辑仍保留原有交互策略。 |
| ActionV1PreviewWindow | 轨迹数组容量不足时按倍数扩容，以实际点数绘制，不再随首次播放的每个路径长度重新分配数组。 |
| ActionV1PreviewWindow | 在已有外部资源变更通知中检查当前 Prefab 的依赖 hash，变化时标记实例失效，在下次绘制重建。普通 Timeline 内容改变只清除求值缓存；同一角色的依赖更新重建保留相机观察点、距离与角度，不增加 update 轮询。 |
| ActionV1PreviewArchitectureTests | 删除依赖旧 Jaeger 路径并要求 Bake/Preview Prefab 相同的测试；补充连续时间/终点/时长缩短，以及烘焙失败不写草稿配置的契约测试。 |

Prefab 更新检测依据已保存/导入的资产状态，使用公开的 [AssetDatabase.GetAssetDependencyHash](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.GetAssetDependencyHash.html)。不承诺未保存 Prefab Stage 草稿会实时更新 Preview。该路径的实际重导入、Undo/Redo 和镜头保持仍需 Unity 验收，不将源码推断记为画面复现。

## 本轮验证证据

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -p:BuildProjectReferences=false -v:quiet`：通过，0 warning / 0 error。现有工程包含本轮修改的测试文件。此命令验证编辑器 C# 编译，不代表执行了 Test Runner 或重新构建全部依赖程序集。
- 首次沙箱编译因本机 SDK 目录读取权限失败；经权限审核后同一命令通过。
- 未使用 Unity UI/连接器执行测试，未启动另一份同项目 Unity、关闭现有 Unity 或修改用户测试资产。本轮不报告新增测试、画面或生命周期检查已通过。
- 本轮代码修改局限于上述 Editor 文件和测试；没有主动修改 Runtime、TestAction、生产材质、Prefab、场景或项目渲染设置。工作区已有其他未提交变更，本记录不将其计入本轮成果。

## 待执行的 Unity 验收

使用临时 AnimationAsset / Action 副本完成资源写入检查，不改用户的 TestAction。

| 检查 | 预期 | 状态 |
| --- | --- | --- |
| 运行新增 EditMode 用例 | 小数位置、终点、时长缩短、失败烘焙不变更资产通过 | 未执行 |
| Bake 目标 A → B（B 含空字段），编辑/清空草稿 | 显示 B 的字段；草稿不从 A 或 B 自动回填 | 未执行 |
| 成功 Bake、单次 Undo/Redo；失败 Bake；保存重开 | 配置和数据共同恢复；失败无写入；其他脏资产不被顺带保存 | 未执行 |
| 开启 Details，修改名称/静音，包括暂停与播放状态 | 当前连续时间不跳回整数帧，名称与静音显示立即一致；静音影响当前 Preview 求值 | 未执行 |
| Timeline 选择 Action，菜单打开 Details / Preview | 两窗口跟随 Timeline；Details 的定位按钮不切换上下文 | 未执行 |
| 保存/重导入当前角色 Prefab 或依赖，再修改无关资产 | 相关变更重建角色并保留镜头；无关变更不重建角色；Undo/Redo 后画面正确 | 未执行 |
| Jaeger 动画、RootMotion、腾空、转向及 HitBox | Pose/位移/Overlay 对齐；地板与起点固定；SelfRotation 不影响 Preview | 未执行 |
| 连续播放至少 10 分钟，重复开关窗口/切换角色/进出 Play Mode | 无新增 Console 异常；PreviewScene、材质、Mesh、RenderTexture 数量不持续增长；静止无持续重绘 | 未执行 |
| 完整编辑链：烘焙 → Timeline 添加/拖动/Trim → Details 编辑 → Undo/Redo → 保存重开 | 数据、选择、时间及 Preview 一致；重叠规则仍是同轨不允许、跨轨允许 | 未执行 |

后续先完成 Preview 与完整编辑流验收，再单独检查编辑产物在 Runtime 中能否使用。本检查点不宣称 Runtime 验收完成。
