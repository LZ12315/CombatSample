# Locomotion 资产配置与动画制作工作流

本文说明当前 Inspector 的配置入口和运行含义；本轮补充统一标量参数、独立二维轴和 TurnLean。代码职责见[当前架构](CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md)，验证状态与 Kiana 配置快照见[提交审查](CombatSample_Locomotion_Commit_Review_2026-10-05_zh-CN.md)。

## 1. 配置 LocomotionAsset

ActorLocomotion 持有候选资产列表。MixerAsset 提供持续 Move；SetAsset 在 Move 上增加可选 Start、Stop、Pivot。地面／空中由 Entry Conditions 决定，资产类型不直接决定地空模式。

| Inspector 分区 | 配置与含义 |
| --- | --- |
| Mode Selection | Priority 与 Entry Conditions。条件全部通过的候选中取最高优先级；同分时保持当前合法候选，否则按列表顺序。空条件列表表示无条件通过。 |
| Tags | 当前资产激活期间取得 Self Tags，退出时配对释放。 |
| Movement Config | Max Speed、Acceleration、Deceleration、Direction Response、Rotate Speed、Turn Response Time。方向速度响应、朝向旋转和松键减速分别控制。 |
| Move | `Mode` 统一选择 1D／2D Directional／2D Cartesian；1D 选择一个来源，2D 独立选择 X／Y 来源。含义、单位与符号方向放在 tooltip，样本使用原生列表中的紧凑行。 |
| Transitions（Set） | 统一混合时长、Stop Playback Mode、状态决策阈值与 Start／Stop／Pivot 列表。 |

没有合格候选时结束当前模式。没有可选过渡时继续 Move；不能播放的条目被跳过。Locomotion Inspector 不检查素材覆盖或表现质量，Runtime 保证核心数值、插值和生命周期安全。

### Move 的编辑操作

- 全部使用原生控件：`Mode`、`Parameter`、`X Parameter`、`Y Parameter` 是标准下拉框；`Samples` 是原生数组，外框、元素标题、拖排、增删和多选都由 Unity 渲染。
- 每个样本只占一行：`Animation`、`Threshold`、`Sync` 横向排列。这是通过 `LocomotionMove1DSample`／`LocomotionMove2DSample` 的 `PropertyDrawer` 控制元素内容与行高实现的，不接管列表外框。
- 列表外框和元素呈现交给 Unity；不依赖特定版本是否显示 `Element N` 标题。样本行保留上下留白，窄面板时限制数值列占用，避免动画引用列出现负宽度。极窄面板的实际可用性仍需人工检查。
- 混合方式合并了维度与算法的入口，但不改变数据模型。切换算法不改 X/Y 来源；切回 1D 也保留未激活的 2D 配置。
- 混合方式或参数来源不一致时显示混合值；多选下样本走原生数组编辑。
- 字段名使用英文：`Mode`、`Parameter`、`X Parameter`、`Y Parameter`、`Samples`、`Turn Lean`、`Full Lean Acceleration`、`Minimum Speed`、`Smooth Seconds`。
- 不在 Inspector 里堆常驻说明文字：参数含义、单位与方向（例如 `Local Input Z` 负数为后）放在参数字段 tooltip 中。混合方式冲突／无效或活动 Lean 设置非法时才显示诊断提示。
- 选择 `Turn Lean` 作为任一轴参数时出现 `Turn Lean` 折叠区，其说明同样只在 tooltip 中。
- 写入走原生 `SerializedProperty`，自动获得 Undo；查看与展开不迁移、不保存资产。本轮没有二维拖点图或动画预览。

### Move 参数与样本

1D 与 2D 共用同一组标量来源；2D 将两个读取结果组成 Vector2，不根据算法改变参数含义。

| 参数源 | Threshold 的含义 |
| --- | --- |
| Horizontal Speed | 政策缩放后的水平模型速度大小，m/s。 |
| Vertical Speed | Motor 已准备的请求垂直速度，去除时间缩放，m/s；上升／下降可用正负阈值。 |
| Input Strength | 控制者提交的 0～1 输入强度。松键时为 0，不改读残余速度。 |
| Local Input X / Z | 同一份角色当前朝向快照下的局部输入分量：X 为右，Z 为前；通常 -1～1。 |
| Local Velocity X / Z | 同一份朝向快照下、政策缩放后的局部模型速度分量，m/s。 |
| Turn Lean | 由自主模型转弯响应得到的侧倾强度：-1 左倾，+1 右倾；不改变 Gameplay 运动。 |

角色局部 Z 可以放在混合空间的 Y 轴；轴来源独立选择，算法不限制参数取值正负。

- **Directional** 按方向与长度插值，典型 Strafe 配置为 X=LocalInputX、Y=LocalInputZ；速度驱动可换为 LocalVelocityX/Z。
- **Cartesian** 按二维坐标关系插值，典型 Lean 跑步配置为 X=TurnLean、Y=InputStrength；速度驱动可将 Y 换成 HorizontalSpeed。不同单位的轴比例会影响插值，需要按素材制作样本坐标。
- 例如 Cartesian 的 Idle=(0,0)、普通快跑=(0,1)、左倾快跑=(-1,1)、右倾快跑=(1,1)。这是示例，本轮没有修改任何角色资源。

参数源决定 Mixer 权重，Move 速度匹配另读取合格实际运动反馈，调整播放快慢。任一活动轴使用 VerticalSpeed，或者所有活动轴都是 TurnLean 等纯姿态参数时，禁用水平步速匹配；其他含水平运动参数的空间继续匹配，与 Directional／Cartesian 无关。

Idle 推断需要足以判断静止的参数：至少一个 HorizontalSpeed／InputStrength，或完整的 LocalInputX/Z、LocalVelocityX/Z 同源分量对（允许交换轴）。同时要求所有活动水平运动轴的阈值为零，忽略 TurnLean 轴。含 VerticalSpeed 或纯姿态空间不推断 Idle。单个局部分量、重复分量或输入／速度混合分量不能凭零值判断静止，因此按普通样本参与步速匹配；例如 LocalInputX=0 的向前跑不会被排除。没有独立 Idle 字段，需要自动识别 Idle 时应选择上述完整参数空间。原有 1D Speed／InputStrength 和 2D 完整向量空间的语义不变。

有限阈值不强制落在输入值范围内，单样本也可播放；重复阈值保留作者顺序中的第一条可用样本。Loop 来自 Clip，Sync 默认开启；Idle 建议退出同步。同步只对齐归一化时间，不自动修复素材落脚相位。播放倍率需要合格的循环轨迹，缺失时使用倍率 1，不代表必然出现滑步。

### TurnLean 设置与边界

- 满幅侧倾加速度（Full Lean Acceleration）：m/s²，必须大于 0，默认 20。值越大越不容易倾满，越小侧倾越明显。
- 低速抑制（Minimum Speed）：模型速度低于该值时目标侧倾为零，从此速度到两倍速度逐渐恢复完整影响，默认 0.5 m/s。
- 平滑时间（Smooth Seconds）：以 Motion 有效时间计算的指数响应时间，默认 0.1 秒。越大越柔和、越滞后，0 表示立即响应。

使用同一 Tick 积分前后的平面模型速度，提取垂直于旧移动方向的加速度，乘当前 LocomotionScale 后映射、限幅和平滑；直线加减速不产生左右侧倾。单 Tick 方向变化达到 120° 时不作为普通弧线转弯。它适用于向前自由跑素材，不保证同一组完整 Lean 循环可用于侧跑或倒跑。

无有效移动输入时侧倾立即归零，避免 InputStrength=0 时残余侧倾混回跑步姿态。Action、平移／旋转 owner、离地或 LocomotionScale≤0 会清理侧倾；恢复首 Tick 重新建立基线。正值 LocomotionScale 和有效时间变化不构成接管，继续按本 Tick 速度快照计算；地面上的重力／空中倍率变化也不重置侧倾。

零 Motion dt 的直接参数采样不推进状态；实际动画暂停由 ActorLocomotion 调用 SuspendAnimationFeedback，清理 Lean，恢复首 Tick 为零、之后重新平滑。这是保守的中断策略，不表示暂停帧会立即提交直身姿态，也不保留跨接管的旧侧倾。设置在 Runtime 创建时绑定，不支持运行时热改。

Lean 设置非法时只禁用侧倾来源（TurnLean=0），不丢弃有效 Move 样本，也不改写资产为默认值。Inspector 在折叠区外显示提示；每个 Runtime 创建绑定时警告一次，不在每 Tick、重置或重建动画图时重复报告。默认 20／0.5／0.1 保持不变；实际效果需要结合模型响应、平滑和素材观察，不能把限幅前后的目标值当成最终姿态。

### 旧配置兼容

原 1D 的三种参数保持原数值含义。旧 2D LocalInput 在读取时等价于 LocalInputX/Z，LocalVelocity 等价于 LocalVelocityX/Z，算法默认 Directional；不写回资源。Inspector 展示有效轴，只有用户主动修改某个轴才保存独立轴配置。多选轴编辑先提交已有修改，再分别保留每个目标的另一有效轴，最后刷新外层 SerializedObject，避免外层提交覆盖逐资产修改；相关写入合并到同一 Undo 组。查看 Inspector 不主动写入配置。修改算法、样本也不会擅自替换原参数来源。

手动验收：混选旧 LocalVelocity、旧 LocalInput 和新独立轴资产，分别只改 X／只改 Y，确认另一轴各自保持；一次 Undo／Redo 应恢复／重做整次编辑。另检查仅查看不标脏、旧资产首次载入、样本拖排以及窄面板显示。

### Start、Stop、Pivot 的方向

Directional Start／Stop 使用状态进入时相对角色朝向的局部 Vector2；TurnToMove Start／Pivot 使用显式有符号转角。它们不是 Motor 的旋转指令；TurnToMove Start 还使用该角度将素材的姿态转换到前向 RunLoop 的朝向基准。

| 过渡 | 选片配置 |
| --- | --- |
| Start — Directional | 目标输入方向 Target Local Direction。保持旧的仅选片语义，适用于已正确制作的方向性／Strafe 姿态。 |
| Start — TurnToMove | Signed Turn Angle Degrees：素材结束时相对入场前方的行进／面向角；左负右正，后左 -180、后右 +180。需要当前有效烘焙轨迹和非循环 Humanoid Clip。 |
| Stop | 本 Tick 积分前模型速度方向 Source Local Direction。 |
| Pivot | Signed Turn Angle Degrees：负数左转、正数右转；例如 Turn_L = -180、Turn_R = +180。 |

Start／Stop 按方向夹角选最接近的候选。TurnToMove Start 在方向同分时优先 Motor 当前转向侧，前向填写 0；角度不是 Root Motion 曲线的最终累计角，不要把姿势自带的侧身角度加进去。Inspector 按 Facing Mode 只显示当前模式使用的方向字段。旧 Start 缺少 Facing Mode 时仍为 Directional。

Katana Normal 的五条 Start 已配置为 TurnToMove：0、-90、+90、-180、+180。它们在同一个时钟采样 Clip 和烘焙 yaw，在基础层混合前修正姿态；不要开启 Animator Apply Root Motion，也不要给模型额外设置固定旋转。转为侧移、起步中输入改变超过 45°或旋转被接管时退出该 Start；暂停不补帧。资源烘焙过期时该条目警告并跳过，请重烘焙后重新创建 Runtime。详情与验证边界见[起步方向对齐](Locomotion_Start_Alignment.md)。

Pivot 只接受非零且在 [-180, 180] 内的有限转角，-180 与 +180 是不同动作，不做环绕角度等价匹配。

Pivot 要求速度达到最低速度，且移动反向夹角与角色当前朝向到 FacingDirection 的身体转角都达到同一个 `PivotMinimumAngleDegrees`（默认 120°，含边界）；不增加第二套角度参数。左右跟随本 Tick Motor 已请求的实际 yaw 增量，同侧再按身体转角大小匹配，完全同分保留列表顺序。没有身体转向、仅有小幅锁定跟随修正、没有有效 FacingDirection 或没有同侧 Clip 时返回 Move，不用反侧动画凑数。例如移动反向 175°、身体只需修正 5°，即使 Motor 正在转，也不播放 180° Pivot。进入 Pivot 后固定所选 Clip，直到完成或被原有打断规则中断。

旧 Pivot 的 Source／Target 两向量已移除；当前仓库非空的 Katana Pivot 已迁移为 -180／+180。外部旧资产需按素材实际转身方向填写新角度，不从两个端点猜测 180° 路径。移动、FacingDirection 和 Motor 的旋转积分规则不变。

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
