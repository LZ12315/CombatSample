# ActorMovement 重构实施计划

## 背景

将 ActorMovement 从"互斥模式切换"架构重构为"多源叠加 + 朝向覆盖"架构。
- **位移管线**：多源叠加模型（locomotion、impulse 等各自写入独立通道，Movement 求和执行）
- **朝向管线**：优先级覆盖模型（Default + Override 两层 + Lock/Snap 机制）
- **RootMotion**：不再是互斥模式，而是数据源（位移+旋转均缓存并公开）

## 涉及文件

| 文件 | 改动类型 |
|------|---------|
| `Assets/Scripts/Actor/ActorMovement.cs` | **核心重构** |
| `Assets/Scripts/Actor/ActorLocomotion.cs` | API 适配 |
| `Assets/Scripts/Magnetism/ActionMagnetismSession.cs` | API 适配 |

---

## 任务清单

- [x] 1. ActorMovement — 删除旧的 MovementMode 枚举与相关字段
   - 删除 `MovementMode` 枚举（`RootMotion`、`CodeDriven`、`MotionWarping`）
   - 删除 `_currentMode` 字段
   - 删除 `SetMovementMode()` 方法
   - 删除 `SetCodeVelocity()` 方法
   - 删除 `_codeDrivenVelocity` 字段

- [x] 2. ActorMovement — 新增 RootMotion 数据层
   - 新增 `RootMotionApplyMode` 枚举（`Managed`、`External`），定义在 ActorMovement 类内部
   - 新增私有字段 `_rootMotionApplyMode`，默认值 `Managed`
   - 新增公开只读属性 `RootMotionDelta`（`Vector3`），返回 `_cachedRootMotionDelta`
   - 新增私有字段 `_cachedRootMotionRotationDelta`（`Quaternion`）
   - 新增公开只读属性 `RootMotionRotationDelta`（`Quaternion`），返回 `_cachedRootMotionRotationDelta`
   - 新增公开方法 `SetRootMotionApplyMode(RootMotionApplyMode mode)`
   - 修改 `OnAnimatorMove()`：同时缓存 `animator.deltaPosition`（经死区处理）和 `animator.deltaRotation`

- [x] 3. ActorMovement — 新增位移速度通道
   - 新增私有字段 `_locomotionVelocity`（`Vector3`），默认 `Vector3.zero`
   - 新增私有字段 `_impulseVelocity`（`Vector3`），默认 `Vector3.zero`
   - 新增公开方法 `SetLocomotionVelocity(Vector3 velocity)` — Locomotion 每帧写入
   - 新增公开方法 `SetImpulseVelocity(Vector3 velocity)` — ImpulseClip 每帧写入

- [x] 4. ActorMovement — 新增重力控制
   - 新增私有字段 `_gravityScale`（`float`），默认 `1.0f`
   - 新增公开方法 `SetGravityScale(float scale)` — 外部控制重力缩放（1.0=正常, 0=无重力）
   - 修改 `PerformGravity()`：将 `Physics.gravity * Time.deltaTime` 乘以 `_gravityScale`

- [x] 5. ActorMovement — 重构朝向管线
   - 删除旧字段：`targetRotation`、`_rotationSpeedOverride`
   - 删除旧方法：`UpdateRotation()`、`SetRotationSpeedOverride()`、`SetRotationInstant()`、`ResetRotation()`
   - 新增私有字段：
     - `_defaultFacingDirection`（`Vector3`）— 默认层朝向目标
     - `_overrideFacingDirection`（`Vector3`）— 覆盖层朝向目标
     - `_hasDefaultFacing`（`bool`）— 默认层是否有有效写入
     - `_hasFacingOverride`（`bool`）— 覆盖层是否激活
     - `_facingLocked`（`bool`）— 朝向是否锁定
     - `_overrideAngularSpeed`（`float`）— 覆盖层自定义旋转速度，-1 表示用默认
     - `_targetRotation`（`Quaternion`）— 当前帧的目标旋转
   - 新增公开方法：
     - `SetFacing(Vector3 worldDirection)` — Locomotion 每帧写入默认层
     - `SetFacingOverride(Vector3 worldDirection, float angularSpeed = -1f)` — Clip 写入覆盖层
     - `ClearFacingOverride()` — Clip 结束时释放覆盖层
     - `SnapFacing(Vector3 worldDirection)` — 瞬间转向（受击、攻击起手）
     - `LockFacing()` — 锁定当前朝向不变
     - `UnlockFacing()` — 解除锁定

- [x] 6. ActorMovement — 重写 Update 执行流程
   - **朝向执行**（顺序）：
     1. 如果 `_facingLocked` → 不更新 `_targetRotation`
     2. 否则如果 `_hasFacingOverride` → `_targetRotation = LookRotation(_overrideFacingDirection)`
     3. 否则如果 `_hasDefaultFacing` → `_targetRotation = LookRotation(_defaultFacingDirection)`
     4. 确定旋转速度：覆盖层有自定义速度用覆盖的，否则用 Inspector 默认的 `rotateSpeed`
     5. `RotateTowards(current, _targetRotation, speed * dt)`
   - **位移合成**（叠加）：
     1. `finalMovement = Vector3.zero`
     2. 如果 `_rootMotionApplyMode == Managed` → `finalMovement += _cachedRootMotionDelta`
     3. `finalMovement += _locomotionVelocity * Time.deltaTime`
     4. `finalMovement += _impulseVelocity * Time.deltaTime`
     5. `PerformGravity()` → `finalMovement += gravityVelocity * Time.deltaTime`
     6. `CC.Move(finalMovement)`
   - **帧末清零**：
     1. `_cachedRootMotionDelta = Vector3.zero`
     2. `_cachedRootMotionRotationDelta = Quaternion.identity`
     3. `_locomotionVelocity = Vector3.zero`
     4. `_impulseVelocity = Vector3.zero`
     5. `_hasDefaultFacing = false`（每帧重置，要求 Locomotion 每帧写入）
     6. 注意：`_hasFacingOverride` 和 `_facingLocked` **不清零**（它们由 Clip 显式 Clear/Unlock）

- [x] 7. ActorLocomotion — 适配新 API
   - `StartLocomotion()` 中：
     - `SetMovementMode(CodeDriven)` → `SetRootMotionApplyMode(External)`
   - `StopLocomotion()` 中：
     - `SetCodeVelocity(Vector3.zero)` → 删除（帧末自动清零）
     - `SetMovementMode(RootMotion)` → `SetRootMotionApplyMode(Managed)`
   - `ApplyIntent()` 中：
     - 所有 `SetCodeVelocity(xxx)` → `SetLocomotionVelocity(xxx)`
     - 所有 `UpdateRotation(xxx)` → `SetFacing(xxx)`

- [x] 8. ActionMagnetismSession — 适配新朝向 API
   - `Begin()` 中：
     - `SetRotationSpeedOverride(-1f)` → 删除（不需要预清理，ClearFacingOverride 在 End 中做）
   - `Tick()` 中：
     - `SetRotationInstant(faceDir)` → `SnapFacing(faceDir)`
     - `SetRotationSpeedOverride(angularSpeed) + UpdateRotation(faceDir)` → `SetFacingOverride(faceDir, angularSpeed)`
   - `End()` 中：
     - `SetRotationSpeedOverride(-1f)` → `ClearFacingOverride()`

- [x] 9. 编译验证
   - 确保项目无编译错误
   - 确保所有旧 API 引用已被替换，无遗留

---

## 新旧 API 映射速查

### 位移管线

| 旧 API | 新 API | 调用方 |
|--------|--------|--------|
| `SetMovementMode(RootMotion)` | `SetRootMotionApplyMode(Managed)` | Locomotion.Stop |
| `SetMovementMode(CodeDriven)` | `SetRootMotionApplyMode(External)` | Locomotion.Start |
| `SetCodeVelocity(vel)` | `SetLocomotionVelocity(vel)` | Locomotion |
| _(不存在)_ | `SetImpulseVelocity(vel)` | ImpulseClip（未来） |
| _(不存在)_ | `SetGravityScale(scale)` | ImpulseClip（未来） |
| _(私有)_ `_cachedRootMotionDelta` | `RootMotionDelta`（公开只读） | 任何外部系统 |
| _(不存在)_ | `RootMotionRotationDelta`（公开只读） | 任何外部系统 |

### 朝向管线

| 旧 API | 新 API | 调用方 |
|--------|--------|--------|
| `UpdateRotation(dir)` | `SetFacing(dir)` | Locomotion |
| `SetRotationInstant(dir)` | `SnapFacing(dir)` | MagnetismSession |
| `SetRotationSpeedOverride(speed) + UpdateRotation(dir)` | `SetFacingOverride(dir, speed)` | MagnetismSession |
| `SetRotationSpeedOverride(-1f)` | `ClearFacingOverride()` | MagnetismSession |
| _(不存在)_ | `LockFacing()` | 受击 Action（未来） |
| _(不存在)_ | `UnlockFacing()` | 受击 Action（未来） |
