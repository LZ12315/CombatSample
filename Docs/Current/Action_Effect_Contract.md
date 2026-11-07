# Action 区间特效、曲线震屏与隔离粒子预览

## 生命周期与阶段合同

`EffectItem` / `ScreenShakeItem` 都是 `RangeGameplayItem`，区间为 `[StartFrame, EndFrameExclusive)`。

```text
Enter → 捕获配置、登记区间资源
全部 Actor 完成 World 后、Hit 前 → 创建一次实例 / 原生 Impulse event
区间内 → 更新绑定 / 读取 Action 曲线，不重复创建或发射
Exit / Interrupt / Abort / Disable / Driver 释放 → 立即停止并释放本区间资源
```

- 保留既有 Range 调度：最后有效帧的 Finish 清理，不移动 Action / World / Hit / Finish 顺序。
- 首次 World 前退出会取消创建；Prefab Awake 中发生角色取消，也会释放刚创建的对象。
- 静音 Item / Lane 不生效。Loop 重新进入才创建新实例 / event。
- 多区间独立持有；停止一份不会取消其他区间、角色或命中 / 弹反的 Impulse。
- 单个区间在创建／空间更新时抛异常：记录错误、释放失败区间，不逐 Tick 重试，继续处理本次 World 的其他区间。释放异常也在表现边界记录，不阻断其他区间退出或 Action 完成；不因单纯表现错误将 CombatSimulationDriver 置为故障。
- VFX 已 Instantiate、但初始化／绑定未完成时，由 ActionEffectPlayer 自行隐藏并销毁实例，再把错误交给 Buffer。不能等一个从未成功返回的 handle 来清理资源。
- **没有粒子尾迹、二次震动或最少可见帧兜底。**短区间可以完全落在两次渲染之间；Details 会警告，但不会偷偷延长。
- 不依赖命中、目标、ActorCombater 或零伤害假命中。Combat 既有已提交 Hit、弹反反馈路由不变。

## EffectItem

Gameplay Lane 右键 → **Presentation → Effect**。

### 位置与缩放

| 配置 | 语义 |
| --- | --- |
| Prefab | 运行时实例化一次；保留 Prefab 自身的 Play On Awake / Loop / 脚本行为 |
| Anchor | ActorRoot / HumanoidBone / TransformPath |
| Humanoid Bone | 需要 Animator 和有效 Humanoid Avatar，不回退到角色根 |
| Path from Actor Root | 从 Actor 根开始的完整相对路径，可指向 Generic 骨架 / 武器挂点 |
| Local Position | 挂点空间位置偏移，包含挂点的旋转与缩放 |
| Follow Position / Scale | 开：持续跟随挂点位置与缩放；关：保留生成时的世界位置与缩放 |
| Scale Multiplier | Prefab 原始根缩放 × 倍率 × 挂点缩放 |

缺失挂点会明确警告并跳过，不每 Tick 重试。没有独立 Lifetime；区间定义最晚释放边界。Prefab 若自带提前 Destroy 的脚本，运行时不会反复补生成，应选择适合该区间的 Prefab。

### 朝向独立于位置绑定

| Orientation | 语义 |
| --- | --- |
| AnchorRelative | 挂点旋转 + Rotation Offset |
| ScreenOriented | 共享 Damage VFX 的屏幕发射轴约定，支持固定 / 随机屏幕角 |
| WorldDirectional | ActorForward / AnchorForward / WorldUp / CameraForward / Custom 世界向量，支持固定 / 随机 Roll |

`Rotation Offset` 在所选朝向之后叠加，单位为度。

`Update Policy` 显式指定：

- **MatchPositionBinding**：Follow 开启时连续更新朝向，否则只在生成时计算。默认值保留旧 AnchorRelative 的留置 / 跟随行为。
- **OnEntry**：只计算一次；即使位置继续跟随，旋转也不会被父级偷偷带走。
- **Continuous**：持续计算；可以只更新朝向而保持生成位置不动。

随机角度每次进入只抽样一次，不随连续朝向更新抖动。屏幕朝向 / CameraForward 缺少相机时跳过，不回退到 identity。连续朝向在 Cinemachine 更新后的 LateUpdate 读取相机；后续丢失相机会停止该实例。

ScreenOriented 使用与 Damage 相同的 stretched-billboard / 发射方向约定：特效主轴沿屏幕平面的指定方向，**不是任意 Prefab 的通用面片 Billboard**。世界向 Roll 绕所选主轴，不改变该主轴。

### 共用播放能力，但不共用上下文和寿命

- `VfxPlaybackUtility` 为 Action 和 Damage 共用粒子初始化与遮挡设置。
- 粒子统一设为 `Hierarchy` Scaling Mode，使整体缩放真正作用到 Local-scaling 粒子。
- `Particle Speed Multiplier` 显式覆盖粒子 simulationSpeed；不自动跟随 Action 速度 / 暂停，也不修改任意 Trail、Animator 或脚本时钟。
- Occlusion：DefaultDepth / EnvironmentOnly，使用既有 HitConfirm VFX layer/render feature。缺少 layer 时警告并使用普通深度。
- 不改粒子 Simulation Space、自动播放或循环；不会强行把 world-space 粒子改成 local-space。
- 退出先 `SetActive(false)`，再 Unity `Destroy`，不等帧末才隐藏，不保留粒子尾迹。
- Damage 继续使用原命中上下文、生成锚点、Lifetime / speed 的销毁规则；弹反仍在原始 HitPoint，未改为挂点特效。

## ScreenShakeItem：原生 Cinemachine Impulse

Gameplay Lane 右键 → **Presentation → Screen Shake**。

已移除手动输出 Camera Transform 的路径、全局正弦 mixer 与 `CameraUpdatedEvent` 回写。

### 曲线与方向

| 配置 | 语义 |
| --- | --- |
| Enabled | 关闭只抑制本区间震屏 |
| Position Amplitude | 位置曲线倍率，Unity 单位，不是角度 |
| Signal Mode = Directional | Vertical / Horizontal / Forward / 归一化 Custom 向量 × 有正负的 Motion 曲线 |
| Signal Mode = IndependentAxes | X / Y / Z 独立位置曲线，替代 Directional，不暗中相加 |
| Rotation Signal | 可选 Pitch / Yaw / Roll 曲线，独立 Rotation Amplitude，单位为度 |
| Max Distance | 原生线性衰减到零的距离，从来源角色根到接收虚拟相机，默认 20 |

曲线横轴是 **Action 归一化区间进度 0..1**，纵轴是真实有正负的位移 / 角度信号，不是围绕隐藏正弦的强度包络。没有 Frequency / Hz 参数。

当前与既有帧调度一致：`t = Clamp01((ContinuousPosition - StartFrame) / (DurationFrames - 1))`，一个有效帧的区间只采样 `t=0`。最后有效帧在 Finish 释放；并不保证每个逻辑采样都会渲染。慢速的不足一帧进度同样更新曲线，而不是只依靠整数 Tick。

- Action 暂停：保持当前值；可能是非零相机偏移，直到恢复或退出。
- Action 慢速：曲线跟随 Action 慢速，不跟随独立世界秒数。
- 默认波形首尾为零；非零端点只警告，不擅自修改用户曲线。
- 一个区间只有一个原生 event，由自定义 `ISignalSource6D` 读取 Action 时钟，HoldForever 直到所有者释放。不会每帧重新发 Impulse。
- 释放先让自身 signal 归零、释放时钟闭包，再 `Cancel(forceNoDecay: true)`；检查 event 的 signal 身份，避免陈旧池引用误操作其他 event。
- 对 Cinemachine 2.10 零时长不回收的边界保留极小的回收时间标记；signal 已立即归零，不是额外可见帧或尾震。
- 无延迟传播、无 secondary reaction。不调用 `ImpulseManager.Clear()`。

### 专用接收通道与相机混合

- Action 专用 **channel mask 2**；既有命中 / 弹反继续使用原 channel 1 / Source / Listener。
- `ActorCameraControl.Awake` 为其持有的 FreeLook、SoftLock、HardLock 安装专用 `ActionScreenShakeListener`，仅运行时添加，不修改相机 Prefab / Scene。
- 接收策略固定：增益 1、3D 距离、camera space、Finalize 阶段、无 secondary reaction。不会把共享命中 Listener 的方向策略一起改掉。
- FreeLook 在内部 orbit 混合后的根虚拟相机处理一次，不给每个子 orbit 再叠加一次。
- 其他相机系统不会被全局搜索并改装；需要显式接入同一专用 Listener。若已有共享 Listener 占用 channel 2，报告错误，不静默改 mask 或增益。
- **方向由每个虚拟相机的原生 camera space 解释，然后 Cinemachine 混合**，不是最终混合相机轴上的后处理。过渡时的方向 / 幅度因此需要实际画面校准，不能承诺旧 `0.3` 的手感一模一样。
- 原生位置相加、旋转组合与距离衰减留给 ImpulseManager / Listener；Action 只提供 signal、来源位置和生命周期。

## 编辑器诊断与隔离 Preview

Details 提供条件字段、可编辑 Motion / XYZ / PitchYawRoll 曲线、单位说明，以及实时诊断：缺少 Prefab、路径 / Humanoid 不匹配、无 autoplay 粒子、无效向量 / 曲线、非零震屏端点、极短区间。

Preview：

- 只在 PreviewRenderUtility 的隔离场景中实例化支持的 ParticleSystem Prefab，不运行 Action runtime / ImpactSystem，不发任何真实 Impulse。
- **先检查 Prefab 原组件**：脚本、Animator、Camera、Audio、Collider 等不在白名单的组件会使预览明确跳过，避免先 Instantiate 再禁脚本时已经执行 Awake。
- 支持常见 ParticleSystem、粒子 Renderer 和静态渲染组件；不宣称任意脚本 Prefab 可确定性回放。
- 固定粒子种子，可前后拖动 / 重采样。随机朝向使用局部抽样并恢复 Unity Random 状态，不影响游戏随机源。
- 留置位置从区间 Start 的角色姿态求出；跟随位置使用当前姿态。屏幕朝向使用**预览相机**，改变视角时重建生成相机上下文。
- 粒子年龄采用 nominal 1x Action 帧换算秒数 × 显式 simulationSpeed。运行时粒子并不跟随 Action pause，因此这不是任意暂停 / 慢放历史的真实回放。
- 粒子在当前 emitter pose 下重采样，不重演整条移动路径的 world-space 历史发射轨迹。
- 显示位置 / 朝向坐标轴与挂点连线；震屏只画信号方向 / 旋转提示，绝不移动真实或预览 Camera。
- 离开区间、切换内容 / 资源、关闭窗口、切换 Play Mode 或释放 Renderer 会销毁预览实例。

## 旧数据

- 保留旧 Point `_frame` → Start，一帧 Duration 的迁移；不批量修改 Action 资产。
- 保留 Effect Prefab、位置、旋转、Scale、Follow 等原数据；旧播放倍率缺失时为 1，不用默认值覆盖明确配置的非法零值。
- 保留 ScreenShake 的嵌套 enabled / intensity、已存在的方向 / Max Distance。嵌套配置改为 Action 专用类型，不再借用命中反馈配置。
- 缺少新曲线时使用首尾归零的默认波形；旧 Frequency 不再驱动播放，也不声称能等价转换手感。
- 旧 Effect Lifetime 不参与运行；最早把 EffectItem 混作 shake 的实验数据不属于自动迁移范围，需按原帧号 / 强度重建 ScreenShakeItem。

## 职责边界

- Scheduler 只负责时间与 Enter / Exit / Abort；`ActionRuntimeContext` 提供当前连续 Action 位置，不把相机业务塞入调度器。
- ActionEffectBuffer 负责登记、World 后实现与区间资源清理。
- ActionEffectPlayer / ActionEffectFollower 负责 VFX 空间和播放适配；Follower 是实例自身的 LateUpdate 绑定，不是 Actor 新增的业务组件。
- VfxPlaybackUtility 与已有 facing resolvers 共享纯播放 / 朝向能力，不合并 Damage 与 Action 的上下文。
- ActionScreenShakeOutput 只负责自己的原生 event / signal；专用 Listener 负责 Cinemachine 接收策略。
- Actor 只保留原有整角色模拟与生命周期协调，不新增相机 / 粒子细节。

## 验证与剩余验收

验证结果需区分：默认小型合同测试、隔离原生集成测试、主场景视觉验收。测试通过不等价于手感与美术验收完成。

原功能接入验证工件保存在 `.git/action-effect-validation/`。主线整理新增创建／空间更新失败隔离、退出释放失败不阻断 Action 完成的 3 项合同回归；当前 ActionEffectContractTests 22 / 22、ActionVfxContractTests 17 / 17 通过。整体批次与最新结果见 [主线迁移记录](Main_Migration.md)，工件位于 `.git/migration-validation/combat-review-*`。均使用当前源文件复制到隔离 Unity 2022.3.62f3 工程，不修改主工程场景 / Prefab / ProjectSettings / 包。

这些合同通过 fake presentation 验证资源所有权和异常边界，不在默认 suite 中实例化真实粒子或读取仓库 Prefab。真实 Prefab 初始化失败后的销毁、脚本 Awake 副作用及 Unity 延迟 Destroy 仍需运行环境检查。

仍需主工程实际操作：

1. Details 曲线编辑、折叠 / 条件字段、时间编辑 / 拖边 / 静音 / 复制、Undo / Redo、Prefab 引用保存重载。
2. 用代表性的持续 / 爆发粒子，检查世界 / 跟随位置、三种朝向及更新策略，Generic / Humanoid / 武器挂点、缩放、遮挡和运行时退出。
3. 实际 Preview 纹理合成与材质 / URP render feature 效果，不以无图形设备下的粒子计数代替画面质量检查。
4. 主场景 Brain 的 FreeLook → Soft / Hard Lock 过渡、旋转信号、距离衰减与幅度重调；确认其他相机没有占用保留通道。
5. 真实命中 / 弹反与多个 Action 区间叠加，分别中断、Disable、销毁与暂停 / 恢复后无残留；不撤销已提交 Hit。
