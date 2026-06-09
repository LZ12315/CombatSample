# Camera_Test / MiHoYo SoftLock 相机交接报告

> **Main cleanup note (2026-06-09):** 本文是 `Camera_Test` 分支历史交接记录。文中的 `Assets/Scenes/MiHoYo.unity` 是旧分支语境；当前 `main` 中可见的 MiHoYo 场景是 `Assets/Scenes/MiHoYo_Release.unity` 和 `Assets/Scenes/MiHoYo_Test.unity`。在用户确认新的标准验证场景前，请不要把本文的旧场景路径当作当前 main 的事实。

> 目标读者：本地 Codex / 后续继续修 CameraControl 的开发者。  
> 分支：`Camera_Test`  
> 场景：`Assets/Scenes/MiHoYo.unity`  
> 当前状态：SoftLock 新架构实验已多轮尝试，但最新画面仍明显错误；请不要把当前版本当作稳定实现。

---

## 1. 用户原始目标

用户想重做 SoftLock 相机，不是简单调参数。目标是接近鬼泣 / 绝区零一类动作游戏的软锁体验：

1. 相机提供稳定画面。
   - 玩家小幅移动时，相机不要立即追。
   - 相机要有阻尼和从容感。
   - 玩家绕敌人移动时，相机应逐渐重构，而不是永远停住。

2. 相机提供足够信息。
   - 玩家和敌人都应在画面中。
   - 玩家不能经常挡住敌人。
   - 需要一定斜视/侧向留白，让敌人被“漏出来”。
   - 但 SoftLock 仍应保持玩家主体，不应变成普通双目标观战镜头或 HardLock。

3. 架构要干净。
   - 不要再让 SoftLock 变成一个手写完整相机系统。
   - 尽量让 Cinemachine 做它擅长的 screen-space composition / dead zone / soft zone / damping。
   - 代码只负责战斗语义、观察目标、权重和构图意图。

---

## 2. 已经讨论出的设计原则

### 2.1 Follow 不应该是“真实机位支点”

用户明确认可：

> Follow 的应该是观察目标，而不是代码直接决定的真实站位。

也就是说，SoftLock 不应该长期维持：

```text
Follow = Runtime_LockAnchor
Runtime_LockAnchor = 代码每帧算出来的相机支点
```

这种做法的问题是：代码在间接决定相机真实位置。Cinemachine 虽然有屏幕语言，但它只能跟随一个被代码搬动的 anchor，无法真正决定“相机是否应该移动”。

### 2.2 代码负责构图意图，Cinemachine 负责构图执行

合理边界：

```text
代码负责：
  - 当前锁定敌人是谁
  - 玩家/敌人/Aim/Reveal 等观察代理
  - TargetGroup 权重和半径
  - 什么时候提高敌人权重
  - 什么时候启用侧向留白 RevealProxy

Cinemachine 负责：
  - 根据 Follow / LookAt 判断真实相机是否移动
  - screen-space dead zone / soft zone
  - damping
  - framing / dolly / FOV 的基础执行
```

### 2.3 不能直接把 Unity TargetGroup 丢给 Cinemachine 后完全不管

用户也明确指出：

> 代码的确应该控制好权重，因为我们还需要相机能够自动向某一侧偏移从而把敌人漏出来。

所以正确方向不是“完全交给 TargetGroup 默认权重”，而是：

```text
Cinemachine 做运动执行；
代码做观察目标策划和权重控制。
```

---

## 3. 当前 Camera_Test 的最新实现状态

当前分支已经被多轮实验修改。重要文件：

```text
Assets/Scripts/Camera/ActorCameraControl.LockCameraRigRuntime.cs
Assets/Scripts/Camera/ActorCameraControl.CameraRigRouter.cs
Assets/Scripts/Camera/ActorCameraControl.SoftLockComposer.cs
Assets/Prefabs/Camera/CM_SoftLock.prefab
```

### 3.1 Runtime 新增的代理

`LockCameraRigRuntime` 目前包含：

```csharp
public Transform anchor;
public Transform aimProxy;
public Transform playerViewProxy;
public Transform enemyViewProxy;
public Transform revealProxy;
public CinemachineTargetGroup targetGroup;
```

新增的 SoftLock 观察代理：

```text
Runtime_SoftLockPlayerViewProxy
Runtime_SoftLockEnemyViewProxy
Runtime_SoftLockRevealProxy
Runtime_LockAimProxy
Runtime_LockTargetGroup
```

### 3.2 Router 当前 SoftLock 绑定

当前 `CameraRigRouter.ApplyCameraBindingForRuntime()` 中，SoftLock 绑定为：

```csharp
Transform group = rt.targetGroup != null ? rt.targetGroup.transform : null;
vcam.Follow = group;
vcam.LookAt = group;
```

HardLock 仍走旧逻辑：

```csharp
vcam.LookAt = rt.targetGroup != null ? rt.targetGroup.transform : null;
vcam.Follow = rt.anchor;
```

### 3.3 SoftLockComposer 当前做的事

当前 `ActorCameraControl.SoftLockComposer.cs` 会：

1. 更新 PlayerViewProxy。
2. 更新 EnemyViewProxy。
3. 更新 AimProxy。
4. 更新 RevealProxy。
5. 根据屏幕边缘情况提高敌人权重。
6. 根据玩家/敌人在屏幕上是否重叠启用 RevealProxy 权重。
7. 刷新 TargetGroup：

```text
TargetGroup targets:
  0. PlayerViewProxy
  1. EnemyViewProxy
  2. AimProxy
  3. RevealProxy
```

8. 运行时确保 SoftLock vcam 使用：

```text
Body = CinemachineFramingTransposer
Aim  = CinemachineComposer
```

注意：最后一条是我最后一次修正，目的是避免 `FramingTransposer + GroupComposer` 双重 group framing。

### 3.4 Prefab 当前情况

`CM_SoftLock.prefab` 仍然在 YAML 中序列化着旧的：

```text
Body = Transposer
Aim  = GroupComposer
```

但运行时 `SoftLockComposer.ApplyCinemachineSettings()` 会尝试替换为：

```text
Body = FramingTransposer
Aim  = Composer
```

这意味着 Inspector 中看到的组件可能取决于 PlayMode 运行到哪一步，容易造成误判。

---

## 4. 已经观察到的失败现象

用户连续截图反馈：

1. 第一版 Cinemachine proxy 架构能看到地面，但跟随变着急，失去上一版从容感。
2. 后续改成 `Follow = TargetGroup` 并让 `FramingTransposer` 接管 Body 后，画面严重错误：
   - Game View 大面积天空。
   - 玩家和敌人贴屏幕边缘或底部。
   - 摄像机 Transform 出现很异常的高度/俯仰。
3. 把 SoftLock 的 `LookAt` 从 AimProxy 改成 TargetGroup 后，基本无改善。
4. 后续我把 Aim 从 `GroupComposer` 切成普通 `Composer`，但用户尚未提供该最新提交后的有效截图。即便如此，当前架构需要重新审查，不建议继续盲调参数。

---

## 5. 当前最重要的技术判断

### 5.1 FramingTransposer 不是第三人称站位的直接替代品

Cinemachine `FramingTransposer` 的 screen-space body 语言确实强，但它的核心是：

```text
把 Follow target 放在屏幕指定位置；
根据 screen-space dead zone / soft zone 移动相机；
如果 Follow 是 TargetGroup，可以做 group framing / dolly / zoom。
```

但它不是天然的“玩家后上方第三人称站位”组件。它不自动知道：

```text
相机应该在玩家背后；
相机应该保持某个肩后方视角；
相机应该相对玩家/敌人战斗方向维持后方结构。
```

所以当前直接 `Follow = TargetGroup` + `Body = FramingTransposer` 可能导致相机解算到奇怪位置，尤其在 perspective 3D 战斗场景中。

### 5.2 Body 和 Aim 的职责必须避免重复

曾经尝试过：

```text
Body = FramingTransposer
Aim  = GroupComposer
```

这很可能导致双重 group framing：

```text
FramingTransposer 对 Follow TargetGroup 做 group framing / dolly；
GroupComposer 对 LookAt TargetGroup 再做 group aim / FOV / distance framing。
```

这种组合可能把 pitch 和位置一起拉坏。最新代码已尝试改成：

```text
Body = FramingTransposer
Aim  = Composer
```

但这条路线仍未证明可行。

### 5.3 纯 Cinemachine Body 接管并不等于一定正确

我们之前的理论是：

```text
让 Cinemachine 决定是否移动。
```

这个原则仍然合理。但现在更清楚的是：

```text
Cinemachine 决定是否移动的前提，是 Body 组件本身适合当前相机类型。
```

如果选用的 Body 组件不具备动作游戏第三人称站位基础，screen-space 能力再强也可能生成错误画面。

---

## 6. 推荐给本地 Codex 的下一步策略

### 6.1 不要继续盲调当前 FramingTransposer 参数

当前问题不是：

```text
ScreenY 太高
DeadZone 太小
Damping 太低
```

而是可能存在 Body 模型选型错误。

如果继续调：

```text
m_ScreenY
m_GroupFramingSize
m_CameraDistance
m_YDamping
```

大概率只是在错误模型上找局部可用点，后续仍会不稳定。

### 6.2 先建立两个明确实验，而不是在一个实现里混改

建议本地 Codex 建两个互斥实验路径，分别验证。

---

## 7. 实验 A：回到第三人称稳定站位，但保留观察代理和权重

这是我认为更稳妥的下一步。

目标：

```text
不要再让 FramingTransposer 决定第三人称站位；
使用 Transposer 或 3rdPersonFollow 提供稳定后上方机位；
代码仍不直接算真实 Camera；
代码只提供一个稳定的 Follow target 和一个 TargetGroup / Aim target。
```

可能结构：

```text
Body = CinemachineTransposer 或 Cinemachine3rdPersonFollow
Aim  = Composer 或 GroupComposer

Follow = PlayerViewProxy 或 CameraFrameProxy
LookAt = AimProxy 或 SoftLockTargetGroup
```

这里需要接受一个事实：

```text
第三人称后上方站位必须有某个参考方向。
```

这个参考方向可以来自：

```text
当前相机 yaw 的惯性
玩家输入方向
玩家→敌人方向的低频/限速版本
FreeLook 当前方向
```

但不要让它每帧急追玩家→敌人方向。

实验重点：

```text
先修复基础构图：不要天空，不要贴边；
再处理“从容跟随”。
```

建议从当前最新代码中保留：

```text
PlayerViewProxy / EnemyViewProxy / AimProxy / RevealProxy
TargetGroup 权重逻辑
RevealProxy 侧向留白逻辑
```

但替换 Body 方案。

---

## 8. 实验 B：继续 FramingTransposer，但降低它的职责

如果想继续验证 Cinemachine screen-space Body，可以尝试：

```text
Body = FramingTransposer
Aim  = Composer
Follow = PlayerViewProxy，而不是 TargetGroup
LookAt = AimProxy，而不是 TargetGroup
```

然后 TargetGroup 不直接作为 Follow，而只作为：

```text
信息量参考
动态权重/FOV 参考
后续可能的辅助 framing 数据
```

这个实验的意义是：避免 TargetGroup group framing 让 Body 过度重构位置。

风险：

```text
敌人信息可能不足；
SoftLock 可能退化成接近 FreeLook；
仍可能缺少第三人称背后站位约束。
```

所以实验 B 只适合验证 FramingTransposer 是否能承担主 Body，不一定是最终方案。

---

## 9. 当前代码中需要特别注意的点

### 9.1 Runtime 动态替换 Cinemachine 组件可能不适合长期保留

当前代码会：

```csharp
if (vcam.GetCinemachineComponent<CinemachineTransposer>() != null)
    vcam.DestroyCinemachineComponent<CinemachineTransposer>();

vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
```

以及：

```csharp
if (vcam.GetCinemachineComponent<CinemachineGroupComposer>() != null)
    vcam.DestroyCinemachineComponent<CinemachineGroupComposer>();

vcam.AddCinemachineComponent<CinemachineComposer>();
```

这对快速实验可以，但长期会导致：

```text
Inspector 状态和 prefab 状态不一致；
PlayMode 首帧/切换帧难以预测；
场景 override 很难管理。
```

建议本地 Codex 后续把 SoftLock 的 vcam 组件配置固定在 prefab 或场景实例中，而不是每帧动态替换组件。

### 9.2 Current branch 是实验污染状态

`Camera_Test` 当前已经有多轮尝试提交。若要恢复到较正常但“跟随太急”的基线，应考虑回滚最近一组 TargetGroup/FramingTransposer 实验提交。

可从 git log 中查找以下 commit message：

```text
Add soft lock view proxy runtime targets
Bind soft lock camera to framing target group
Drive soft lock framing through Cinemachine target group
Tune soft lock prefab framing defaults
Use target group for soft lock follow and aim
Use Composer aim with soft lock framing transposer
```

这些提交是当前 TargetGroup/FramingTransposer 纯化实验链路。回滚它们可以回到之前的 `Follow = Runtime_LockAnchor / LookAt = Runtime_LockAimProxy` 的较稳定版本，再重新做实验 A。

---

## 10. 建议的 Codex 工作指令

可以直接给本地 Codex 这样的任务：

```text
我们在 Unity 项目 CombatSample 的 Camera_Test 分支上修 SoftLock 相机，场景是 MiHoYo。
当前最新实现尝试用 Follow=Runtime_LockTargetGroup + Body=FramingTransposer + Aim=Composer/GroupComposer，让 Cinemachine 接管屏幕构图，但运行结果是 Game View 大面积天空、玩家/敌人贴边，说明这个 Body 模型或绑定方式不适合当前第三人称动作相机。

请先不要继续调 ScreenY/DeadZone/Damping 参数。请做一次结构修复：
1. 保留 SoftLock 的 PlayerViewProxy / EnemyViewProxy / AimProxy / RevealProxy 和 TargetGroup 权重思想；
2. 停止在运行时反复 Destroy/Add Cinemachine Body/Aim 组件；
3. 优先实验一个稳定第三人称 Body：Transposer 或 Cinemachine3rdPersonFollow；
4. Follow 不要直接使用代码算出的最终相机位置，但可以使用玩家主体 Proxy 或一个低频 CameraFrameProxy；
5. LookAt 使用 AimProxy 或 SoftLockTargetGroup；
6. 保证基础构图先正确：玩家不贴边，地面可见，敌人可见，不要大面积天空；
7. 再处理跟随从容感：不要让 Follow target/yaw 每帧急追玩家→敌人方向；
8. RevealProxy 继续只作为构图提示点，不要直接移动相机。

验收标准：
- SoftLock 下初始构图不崩；
- 玩家小幅移动时相机不急追；
- 玩家绕敌人时相机会逐渐重构而不是永远停住；
- 玩家和敌人都可见；
- 玩家不会长期遮住敌人；
- SoftLock 不变成普通双目标观战镜头。
```

---

## 11. 简短结论

当前失败不是因为“Cinemachine 不该用”。

真正结论是：

```text
Cinemachine 的 screen-space 构图能力应该继续使用；
但 FramingTransposer + TargetGroup 直接作为第三人称 SoftLock Body，目前在这个项目里没有跑通；
下一步应该把“第三人称基础站位”和“观察目标构图权重”分开处理。
```

最可能正确的方向：

```text
稳定第三人称 Body 负责基础机位；
代码维护观察代理和权重；
Cinemachine Composer/GroupComposer/TargetGroup 负责屏幕构图和信息量；
RevealProxy 负责侧向留白提示。
```
