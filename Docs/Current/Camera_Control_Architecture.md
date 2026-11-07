# CameraControl：职责与整理边界

## 本轮范围

本轮只整理结构、旧入口和诊断，不调整 SoftLock 手感，不修改场景、相机 Prefab、Package 或序列化参数。

SoftLock 和 HardLock 都保留，各自拥有运行时状态。后续软锁机位、角色遮挡和构图体验应另做变更，不与本轮合并验收。

## 当前职责

代码位于 `Assets/Scripts/Camera/`：

| 文件 | 职责 |
| --- | --- |
| `ActorCameraControl.cs` | 保留全部序列化引用和调参字段；读取 Combat 状态；调度当前、待机和进入相机的更新；处理优先级、进入初始化及 FreeLook 衔接；管理模块生命周期；保留移动输入的相机水平坐标转换。同文件内保留少量共享的目标创建／销毁工具。 |
| `SoftLockCameraRig.cs` | 解析双方 CameraTarget，更新 Follow 与双方 framing 代理点，维护软锁 TargetGroup 和相机绑定。 |
| `HardLockCameraRig.cs` | 计算交战方向、锚点、高度、距离及平滑；维护硬锁 TargetGroup、Cinemachine 参数与绑定。 |
| `CameraDiagnostics.cs` | 按开关和切换窗口读取实际相机、绑定和目标组状态，输出位置、旋转、FOV 等信息；检查震屏监听器。 |

软锁、硬锁和诊断模块是普通 C# 类，不是新 MonoBehaviour，不持有整个 ActorCameraControl。切换逻辑直接作为主控制器私有方法，分配工具作为同文件内的无状态辅助类型，不再各占一个文件。
软硬锁参数分别通过值类型 Settings 传入，主组件中字段名称、类型和默认值保持不变，运行时 Inspector 调参仍在每次刷新时传入。

## 必须保留的时序

1. `Awake` 创建模块和各自的 TargetGroup；硬锁创建 Anchor，软锁在第一次实际刷新时创建三个代理点，不再先创建无用 Anchor。
2. `Start` 保持控制器局部旋转归零、硬锁锚点初始化、首次状态应用和震屏监听器检查。
3. 每次 `LateUpdate` 先读取 Combat 状态并处理切换。
4. 有 CombatTarget 时，先刷新当前锁定方案，再依次刷新可以预更新的待机软锁、硬锁。
5. `Brain.IsLive` 包括混合参与者。不是当前状态、但仍参与混合的退出镜头，不刷新其运行时输入。
6. 进入另一锁定模式时，先刷新目标和 TargetGroup，硬锁立即设置锚点，然后使进入镜头的旧状态失效并执行一次无阻尼计算，最后应用优先级。
7. 退出到 FreeLook 时，只有 FreeLook 尚未参与输出／混合，才使用主相机位置和朝向准备它。
8. 销毁控制器时，各锁定模块销毁各自对象。

相机优先级保持当前 20、其他 10。保留原有待机更新配置，不修改 Prefab 的 Standby Update。

## 保留的构图差异

- SoftLock：玩家／敌人观察点分别解析 Actor.CameraTarget；Follow 的 XZ 取玩家观察点，高度取玩家高度与双方加权高度的较大值。权重和半径保持非负处理。不在代码中新增侧向机位、Screen X、FOV 或防遮挡规则。
- HardLock：继续使用控制器 Transform 与敌人根 Transform，不改成双方 CameraTarget。保留中心偏置、平均高度加偏移、位置／Yaw 平滑，以及距离公式 `min(base + combatDistance * factor, max)`。保留 Transposer 的后向偏移和 GroupComposer 的 DollyThenZoom 配置。
- SoftLock 的三个代理点保持世界空间、不挂到角色下；不刷新时不会跟随角色运动。硬锁的 Anchor 也保持世界空间，但其 TargetGroup 仍直接引用双方原始 Transform。这不是严格冻结整台镜头，Cinemachine 仍可能计算阻尼和目标组变化。

## 旧入口整理

整理前检查项目源码、测试、图资产、场景与 Prefab 序列化文本，以下写入／转发入口未发现外部调用，已移除：

- `SetCameraState(...)`
- `CinemachineState` setter
- `UpdatePivotBehavior()`
- `CalculateWorldDirection(...)`

锁定请求使用现有 `ActorCombater.TryEnterSoftLock()`、`TryEnterHardLock()`、`ClearLock()`／`ToggleSoftLock()`。
`CinemachineState` 只读查询、`TargetGroup` 查询和 `ToWorldMoveDirection(...)` 保留。

删除软锁未使用的 `instant`／`updateStickySide` 参数，不再维护仅供日志使用的 dbg 字段、兼容目标组缓存和相机状态字典。
软锁、硬锁和诊断文件重命名时连同原 `.meta` 移动，保留 GUID。切换与分配工具合并进主文件，移除其独立脚本及 `.meta` 前已确认没有场景／Prefab／数据资产引用；主 MonoBehaviour 文件、类型名和 GUID 不变。

## 验证边界

已完成：

- 整理前相机源码快照与当前工作区其他 Runtime 源码的独立编译：0 error / 0 warning。
- 整理后 Runtime 独立源码编译：0 error / 0 warning。
- 移除 `UNITY_EDITOR` 系列条件的 Runtime 独立源码编译：0 error / 0 warning；不是 Player Build。
- Editor 与全部当前 `Assets/Tests/Editor` 源码引用新 Runtime 的独立编译：0 error / 0 warning；未执行 Test Runner。
- 对比全部 20 个私有序列化字段声明／默认值与 4 个公开 Actor／相机字段，保持一致。
- 对比本轮开始时的 `HyperGraph.unity`、相机 Prefab 和保留的相机 `.meta` 内容，保持一致（软锁、硬锁、诊断的 `.meta` 仅路径移动；合并掉的两个工具脚本 `.meta` 随之删除）。

验证产物输出到系统临时目录，没有改写 Library／Temp／工程生成文件，也没有启动第二个 Unity 编辑器实例。

仍需在 Unity 中人工对照，不能把编译结果当作手感验收：

- [ ] 自由视角鼠标／摇杆输入、相机相对移动方向。
- [ ] 站立与移动时进入／退出软锁。
- [ ] 0.4 秒混合尚未结束时快速解锁／重新锁定。
- [ ] 目标切换、失效／停用及自动回退。
- [ ] 通过现有 Combat API 在临时调试中验证硬锁，以及软硬锁相互切换，不新增正式输入入口。
- [ ] 退出镜头的代理点保护、待机预更新及震屏。
- [ ] 停止 Play 后无遗留运行时目标，无新增 Console 错误或丢失引用。

手工验证入口为 `Assets/Scenes/HyperGraph.unity`。本轮未运行 Play Mode、Unity Test Runner 或 Player Build，也未关闭正在使用的编辑器。
