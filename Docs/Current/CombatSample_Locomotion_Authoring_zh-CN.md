# Locomotion 资产配置与动画制作工作流

更新：2026-10-06。本文说明当前 Inspector 的配置入口和运行含义。代码职责见[当前架构](CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)，验证状态与 Kiana 配置快照见[提交审查](CombatSample_Locomotion_Commit_Review_2026-10-05_zh-CN.md)。

## 1. 配置 LocomotionAsset

ActorLocomotion 持有候选资产列表。MixerAsset 提供持续 Move；SetAsset 在 Move 上增加可选 Start、Stop、Pivot。地面／空中由 Entry Conditions 决定，资产类型不直接决定地空模式。

| Inspector 分区 | 配置与含义 |
| --- | --- |
| Mode Selection | Priority 与 Entry Conditions。条件全部通过的候选中取最高优先级；同分时保持当前合法候选，否则按列表顺序。空条件列表表示无条件通过。 |
| Tags | 当前资产激活期间取得 Self Tags，退出时配对释放。 |
| Movement Config | Max Speed、Acceleration、Deceleration、Direction Response、Rotate Speed、Turn Response Time。方向速度响应、朝向旋转和松键减速分别控制。 |
| Move | Blend Type、Parameter、Samples，分别表示混合维度、参数源和样本；字段平铺，样本继续使用原列表。 |
| Transitions（Set） | 统一混合时长、Stop Playback Mode、状态决策阈值与 Start／Stop／Pivot 列表。 |

没有合格候选时结束当前模式。没有可选过渡时继续 Move；不能播放的条目被跳过。Locomotion Inspector 不检查素材覆盖或表现质量，Runtime 保证核心数值、插值和生命周期安全。

### Move 参数与样本

| 参数源 | Threshold 的含义 |
| --- | --- |
| 1D Horizontal Speed | 政策缩放后的水平模型速度，通常以 m/s 配置。 |
| 1D Vertical Speed | Motor 已准备的请求垂直速度，去除时间缩放；上升／下降可用正负阈值。此模式不使用 Move 速度倍率匹配。 |
| 1D Input Strength | 控制者提交的 0～1 输入强度。松键时为 0。 |
| 2D Local Velocity | 相对角色当前朝向的局部水平模型速度，Vector2 的 x 为右、y 为前。 |
| 2D Local Input | 相对角色当前朝向的归一化输入方向 × 强度。 |

参数源决定 Mixer 权重。Move 速度匹配另外读取合格的实际运动反馈，调整动画播放快慢。使用 Input Strength 仍可进行速度匹配。

零阈值或零向量样本可以作为 Idle；没有独立 Idle 字段。有限阈值不强制落在输入值范围内，单样本也可播放；重复阈值保留作者顺序中的第一条可用样本。Loop 来自 Clip，Sync 由各样本配置，默认开启。Move 播放倍率需要合格的循环轨迹数据，缺失时保持中性倍率并继续基础播放。

### Start、Stop、Pivot 的方向

方向是状态进入时相对角色朝向的局部 Vector2，不是素材质量检查或输入强度阈值。

| 过渡 | 使用的方向 |
| --- | --- |
| Start | 目标输入方向 Target Local Direction。 |
| Stop | 本 Tick 积分前模型速度方向 Source Local Direction。 |
| Pivot | 旧运动方向 Source 与新输入方向 Target。 |

配置可以只覆盖部分方向。Runtime 按角度选最接近的候选，不因缺少某个方向的专用素材而禁止过渡。素材效果由作者预览与跑测调整。

## 2. 准备 AnimationAsset

1. 指定 Clip，在 Animation 卡片的 Source File 确认来源文件；完整路径见提示。
2. 在 Bake 卡片指定 Animation Rig Asset。Rig 设置用于烘焙的骨架；Humanoid 左右脚路径留空时读取 LeftFoot／RightFoot，Generic 使用相对 Animator 的路径。
3. 点击 Bake／Rebuild Animation Data。同一次采样生成根轨迹、自动停止点与脚接触候选。
4. 在预览中核对轨迹和标记；需要时开启相应 Override。
5. 在 LocomotionAsset 引用资源。希望 Stop 使用距离时钟时，显式选择 Distance；旧配置缺少模式字段时保持 Time。

AnimationAsset 的卡片顺序与用途：

| 卡片 | 用途 |
| --- | --- |
| Animation | Clip 与来源文件。 |
| Bake | Rig、烘焙状态与重建入口；正常状态显示简洁状态行。 |
| Root Motion | Root X／Y／Z 轨迹曲线，采用亮色显示。 |
| Stop Point | 自动或覆盖的 Stop Frame、对应 Stop Time，以及到该点的 Remaining Distance 曲线。Stop Time 与距离曲线随 Override 保持与 Frame 一致的显示状态。 |
| Foot Markers | 自动或覆盖的左右脚接触帧列表，可添加、修改与删除。 |

停止点修改入口是 Stop Frame，Stop Time 是对应秒数。剩余距离由根轨迹与停止点派生，修改停止帧后重新计算。

同 Clip 重烘焙保留手工覆盖；关闭 Override 恢复自动数据。更换 Clip 后旧数据和旧覆盖不参与新 Clip。预览复用 Unity 的 AnimationClip Editor，标记采用帧列表编辑；没有额外时间轴，也不联动原生预览的内部播放头。

## 3. 停止点、距离与脚相各自做什么

停止点定义动画刹车段的结束时刻。距离曲线记录从每个采样时刻到该点的剩余平面路径长度；停止点之后是普通时间播放的收尾段。因此停止点可以早于 Clip 末尾，不能直接等同于整段动画结束。

自动停止点取最早使所有后续水平根位置都保持在最终位置半径 2 cm 内的采样时刻。它估计轨迹稳定，不识别“作者希望开始收尾”的语义；作者可通过覆盖表达自己的刹车结束点。没有可用距离段时使用普通时间播放。

自动脚标记根据脚骨骼高度的显著下降与局部谷值生成候选；非循环动画的初始站立不算一次新落脚，持续下降的末尾也不生成未完成候选。它不测量真实地面接触，偏早、偏晚、漏标或多标都需要按预览修正。

Stop 的运行顺序为：

1. ActorAnimation 在提交新 Stop 前，读取基础层贡献最大的已求值动画脚相；贡献包含层与各级父节点权重。主要动画没有脚相时返回未知。
2. Runtime 先按方向挑候选，方向同分时比较当前脚相与候选实际距离入场帧的脚相，再按作者顺序。
3. Distance 模式用积分后的模型速度、绑定减速度和运动倍率预测剩余刹车距离，以曲线反查动画时间。碰撞提前停住不改写这个时钟。
4. Stop 时间只能前进。零距离采样停止点一次，之后恢复普通时间播放收尾；再次输入可以打断。

脚相只排序候选，不移动距离入场时间。只有一个 Stop 候选时始终使用它；两个候选也未必覆盖任意跑步姿势，不保证完全无缝。

## 4. 修改配置后何时生效

执行数据在 Runtime 创建时绑定；普通模式切换、同一 Runtime 重入和动画图重建都不会刷新配置。

调整 Clip、样本、运动参数、停止点或脚标记后，重新进入 Play Mode，或禁用再启用 ActorLocomotion，以释放旧 Runtime 并重新创建。没有运行时刷新按钮或变更监听。

## 5. 跑测与定位入口

先检查快速启停、连续转向、混合中松键、刹车中再次输入、地空交接与 Action 结束。检查单个异常时，记录当时的参数源、方向候选、停止点和脚标记，不把不同配置下的表现混为一次结果。

需要日志时，在 ActorLocomotion 组件菜单使用 `Debug/Trace Next Turn or Release (120 Ticks)`，再触发移动中的反向或松键。记录用于核对状态、速度、参数、动画时间和混合权重；最终腿部姿态仍需结合画面或预览判断。
