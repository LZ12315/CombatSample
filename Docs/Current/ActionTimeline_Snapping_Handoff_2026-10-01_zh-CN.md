# Timeline 吸附与 0 帧边界修复

日期：2026-10-01。

源码路径于 2026-10-08 随 Scripts 整理更新为 `Assets/Scripts/Action/`；本文测试结果仍是 2026-10-01 的记录。本轮验证见[Scripts 整理记录](CombatSample_Scripts_Organization_2026-10-08_zh-CN.md)。

## 修复原因

- 移动手势跳过 `ConstrainPointerDelta`，鼠标拖过 0 帧后直接进入非法候选状态，松手无法完成操作。
- 磁吸距离先把鼠标位移取整为帧后才计算，捕获手感随缩放变化。
- 播放线显示连续 PreviewPosition，但吸附使用 `CurrentFrame`；暂停在小数帧或 Duration 终点时，显示线与目标不一致。
- 原吸附缺少保持 / 释放区间与目标提示；Point 的移动边缘错误包含占位结束帧，多选仅使用整体外边缘。

## 当前交互契约

1. **0 帧限制**：移动手势拖过起点后停在 0 帧，Snap 关闭时也有效。多选以最早条目为界，所有条目保持相对时间。直接数值写入的负帧仍不合法。
2. **播放线对齐**：超过拖动阈值、实际开始移动 / 调整长度 / 裁剪时，暂停编辑器播放，并把播放位置就近归到整数帧。Duration 终点仍可作为吸附目标。
3. **像素磁吸**：使用未取整的鼠标位移，10px 内捕获；已命中的目标在 16px 范围内保持，拉开后释放。目标消失或变为非法时立即解除保持。
4. **目标与优先级**：播放线、0 帧与可见内容边界参与吸附。先选最近的合法目标；同距离优先播放线，其余按移动边缘顺序和目标帧确定。已保持的目标在释放前优先。
5. **条目边缘**：Point 只提供事件帧；Range / Animation 提供开始与 end-exclusive。移动多选可对齐被抓取条目的边缘或整体外边缘；调整长度 / 裁剪只使用被拖动的一侧。
6. **反馈**：黄色辅助线与播放线 / 目标边界高亮；操作提示显示 `Snap F…: Playhead` 或对应 Item 名称及 Start / End / Frame，Tooltip 保存完整提示。
7. **快捷键**：保留 Snap 开关与 Ctrl/Cmd 临时反转，按下 / 松开修饰键可在鼠标不移动时更新吸附。松手重新计算最终指针位置后提交。
8. **合法性**：操作快照仍校验同轨重叠、目标轨道与动画 Source 范围。0 帧限制不会绕过其他校验。

## 改动文件

- `Assets/Scripts/Action/Editor/Core/ActionEditorOperations.cs`：移动边界、未取整位移的磁吸、保持 / 释放、目标优先级、真实移动边缘。
- `Assets/Scripts/Action/Editor/Core/ActionEditorPlayback.cs`：暂停并定位就近整数帧。
- `Assets/Scripts/Action/Editor/Windows/ActionTimelineWindow.cs`：接入新规则、播放线目标、反馈和快捷键事件。
- `Assets/Scripts/Action/Editor/Styles/ActionEditorStyles.uss`：辅助线与目标高亮。
- `Assets/Tests/Editor/ActionTimelineSnappingTests.cs`：15 个确定性契约用例。
- 当前编辑器架构文档与本文。

## 验证

- Unity 2022.3.62f3 Roslyn 编译完整 Editor / 测试程序集，输出写入临时目录，编译通过。
- 在隔离最小 Unity 工程中加载编译程序集，调用新增 15 个 NUnit 用例和既有 5 个针对性回归用例，全部通过；未运行完整默认 Test Runner suite。
- 验证了动画段、Range、Point 的 0 帧限制，多选相对时间和主条目边缘，非法重叠不被放行，磁吸缩放与保持 / 释放，播放线归整及 Duration 终点。
- USS 在 Unity 中导入成功，无样式解析告警。
- 临时引擎探针与日志：`/tmp/opencode/action-preview-probe/timeline-validation.log`。

## Unity 窗口复查

1. 选择没有起点占用冲突的 Item，快速拖过 0 帧并松手；开启 / 关闭 Snap 都应落在 0 帧。对动画段、Point、多选各复查一次。
2. 手动定位播放线后，将 Item 开始 / 结束、Point、长度手柄、动画裁剪边缘拖向它；确认黄色反馈、显示帧号和实际落点一致。
3. 从播放暂停的小数位置开始拖动，确认播放线就近归整；在 Duration 终点重复吸附。
4. 缩放时间轴，在同轨邻接与跨轨对齐时小幅抖动再拉开；检查保持稳定且容易释放。
5. 在鼠标不动时按下 / 松开 Ctrl/Cmd，观察提示是否立即变化；释放鼠标后再次选中，确认提交时间与预览一致。
6. 拖到视口外触发自动平移、跨轨移动，以及 Escape / 失焦取消；确认目标高亮和辅助线正常清除。

鼠标操作与完整窗口渲染尚未自动化验收，以上手感与可见性检查需要在当前 Unity 会话完成。
