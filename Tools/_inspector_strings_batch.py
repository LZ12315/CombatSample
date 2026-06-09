# -*- coding: utf-8 -*-
"""One-off: Unity [Header]/[Tooltip] -> simple English. Run from repo root."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "Assets" / "Scripts"

# Longest first (substring safety)
PAIRS: list[tuple[str, str]] = [
    (
        '[SerializeReference, SubclassSelector, Tooltip("必须满足列表里【所有】条件，本动作才会被系统选中")]',
        '[SerializeReference, SubclassSelector, Tooltip("All conditions in this list must pass. Then this action can run.")]',
    ),
    (
        '[SerializeReference, SubclassSelector, Tooltip("必须满足列表里【所有】条件，模式才会被系统选中")]',
        '[SerializeReference, SubclassSelector, Tooltip("All conditions in this list must pass. Then this mode can run.")]',
    ),
    (
        '[Tooltip("每次命中且本 Profile 挂在目标的 Receiver 上时，在 Clip 的 effects 之后按顺序执行其中启用的项。不想播受击反馈则不要给 Receiver 指定 Profile。")]',
        '[Tooltip("On each hit, runs enabled items in order after clip effects. Leave Receiver empty to skip hit feedback.")]',
    ),
    (
        '[SerializeField, Tooltip("特殊的移动模式列表 (按Priority仲裁)")]',
        '[SerializeField, Tooltip("Extra move modes. Higher priority wins.")]',
    ),
    (
        '[SerializeField, Tooltip("动作播放期间持续拥有的身份标签 (例如：State.Action.Attack)")]',
        '[SerializeField, Tooltip("Tag on you while this action plays. Example: State.Action.Attack")]',
    ),
    (
        '[SerializeField, Tooltip("存放受击、闪避、死亡等随时可能抢占的动作")]',
        '[SerializeField, Tooltip("Hit, dodge, death, etc. These can cut in anytime.")]',
    ),
    (
        '[Tooltip("如果勾选，只要本条件为 True，直接无视其他所有条件强行通过")]',
        '[Tooltip("If on and this is True, skip all other checks and pass.")]',
    ),
    (
        '[SerializeField, Tooltip("仅推动这些 Layer 上的刚体（例如只勾 Enemy）；不勾则推不到")]',
        '[SerializeField, Tooltip("Only push rigidbodies on these layers. Example: Enemy only.")]',
    ),
    (
        '[SerializeField, Tooltip("冲量 = (moveLength/dt) × 该系数。走路轻碰应远小于跑步猛撞")]',
        '[SerializeField, Tooltip("Push = (moveLength/dt) × this. Walk bump should be much smaller than sprint hit.")]',
    ),
    (
        '[SerializeField, Tooltip("越大停得越快，约 3～10；可与 Rigidbody.Drag 二选一或小幅叠加")]',
        '[SerializeField, Tooltip("Higher = stop faster. Try 3–10. Can mix a little with Rigidbody drag.")]',
    ),
    (
        '[Tooltip("玩家根与敌人 Transform 的水平距离超过此值则本 Clip 不生效（不旋转，0=无限制）")]',
        '[Tooltip("If player–enemy flat distance is over this, clip does nothing. 0 = no limit.")]',
    ),
    (
        '[Tooltip("屏幕语义斜向：Preset 固定角度；Random 在 [0, angleRandomRange] 内随机。")]',
        '[Tooltip("Screen tilt: Preset uses one angle. Random picks in [0, angleRandomRange].")]',
    ),
    (
        '[Tooltip("DefaultDepth 使用普通深度测试；EnvironmentOnly 只受环境遮挡，不受角色层遮挡。")]',
        '[Tooltip("DefaultDepth: normal depth test. EnvironmentOnly: world blocks VFX, not characters.")]',
    ),
    (
        '[Tooltip("可以拖入 ClipTransition (普通攻击) 或 MixerTransition2D (八向闪避)")]',
        '[Tooltip("Drag a ClipTransition (attacks) or MixerTransition2D (8-way dodge).")]',
    ),
    (
        '[SerializeReference, SubclassSelector, Tooltip("Locomotion自身的准入条件组")]',
        '[SerializeReference, SubclassSelector, Tooltip("Checks before this locomotion can run.")]',
    ),
    ('[Header("智能偏移 (锁定模式)")]', '[Header("Lock-on Offset")]'),
    ('[Header("全局动作 (最高优先级)")]', '[Header("Global Actions")]'),
    ('[Header("结束时清理 - 正常播完")]', '[Header("On End (Finished)")]'),
    ('[Header("结束时清理 - 被中断")]', '[Header("On End (Cut)")]'),
    ('[Header("标签管理 (Tag Management)")]', '[Header("Tags")]'),
    ('[Header("粒子系统设置")]', '[Header("Particle System")]'),
    ('[Header("Pivot 旋转参数")]', '[Header("Pivot")]'),
    ('[Header("虚拟相机")]', '[Header("Cameras")]'),
    ('[Header("开始时清理")]', '[Header("On Start")]'),
    ('[Header("基础配置")]', '[Header("Base")]'),
    ('[Header("组件引用")]', '[Header("References")]'),
    ('[Header("清理策略")]', '[Header("Cleanup")]'),
    ('[Header("变换设置")]', '[Header("Transform")]'),
    ('[Header("播放控制")]', '[Header("Playback")]'),
    ('[Header("粒子设置")]', '[Header("Particles")]'),
    ('[Header("属性")]', '[Header("Properties")]'),
    ('[Header("配置")]', '[Header("Settings")]'),
    ('[Header("状态")]', '[Header("Runtime")]'),
    ('[Header("资产")]', '[Header("Core")]'),
    ('[Header("??")]', '[Header("Settings")]'),
    (
        '[Tooltip("固定的偏移角度 (肩部偏置)。Pivot 将保持这个角度，而不是回正到0。")]',
        '[Tooltip("Fixed shoulder offset angle. Pivot keeps this instead of snapping to 0.")]',
    ),
    (
        '[Tooltip("父级变换引用（使用ExposedReference以便在Timeline中绑定）")]',
        '[Tooltip("Parent transform. ExposedReference so you can bind it on the Timeline.")]',
    ),
    (
        '[Tooltip("在特效创建后是否持续更新其Transform")]',
        '[Tooltip("Keep updating this transform after spawn.")]',
    ),
    (
        '[Tooltip("本次命中要触发的打击效果列表。只添加这招真正需要的效果。")]',
        '[Tooltip("Impact effects for this hit. Only add what this move really needs.")]',
    ),
    (
        '[Tooltip("可选：受击 VFX 朝向参考点；不填则用本次攻击者的 CharacterController 中心")]',
        '[Tooltip("Optional: aim hit VFX at this. Empty = use attacker CharacterController center.")]',
    ),
    (
        '[SerializeField, Tooltip("水平速度低于此 (m/s) 时直接归零，避免无限微滑")]',
        '[SerializeField, Tooltip("If flat speed is below this (m/s), snap to 0. Stops tiny drift.")]',
    ),
    (
        '[SerializeField, Tooltip("单次回调最大速度增量 (m/s)，防止数值叠爆")]',
        '[SerializeField, Tooltip("Max speed add per callback (m/s). Stops runaway values.")]',
    ),
    (
        '[SerializeField, Tooltip("低于此「试图靠近速度」视为毛刷，不推（减少刚碰到就滑飞）")]',
        '[SerializeField, Tooltip("If approach speed is below this, skip push (less bump slide).")]',
    ),
    (
        '[SerializeField, Tooltip("仅在 trySpeed 超过最小阈值后，可选再加一小点底力（一般保持很小）")]',
        '[SerializeField, Tooltip("Optional tiny extra push after speed is over the min. Keep small.")]',
    ),
    (
        '[SerializeField, Tooltip("关联的Timeline")]',
        '[SerializeField, Tooltip("Main Timeline asset")]',
    ),
]

MORE = [
    ('[SerializeField, Tooltip("动作优先级")]', '[SerializeField, Tooltip("Action priority")]'),
    ('[SerializeField, Tooltip("动作是否循环播放")]', '[SerializeField, Tooltip("Loop this action")]'),
    (
        '[SerializeField, Tooltip("动作自然结束后的强制派生动作")]',
        '[SerializeField, Tooltip("Next action after this one ends")]',
    ),
    ('[Tooltip("动作开始时清理")]', '[Tooltip("Clear targets when action starts")]'),
    ('[Tooltip("动作结束时清理")]', '[Tooltip("Clear targets when action ends")]'),
    ('[Tooltip("如果勾选，则结果取反")]', '[Tooltip("If on, flip the result")]'),
    (
        '[Tooltip("命中震屏：向 Cinemachine 广播 Impulse。留空则在本物体上自动添加")]',
        '[Tooltip("Camera shake: send Impulse to Cinemachine. Empty = add on this object.")]',
    ),
    ('[Tooltip("关闭后保留此效果块，但本次命中不执行。")]', '[Tooltip("Off = skip this effect for this hit only.")]'),
    ('[Tooltip("命中停顿时长（秒）。")]', '[Tooltip("Hit stop time (seconds).")]'),
    (
        '[Tooltip("命中停顿期间攻击者 Timeline 播放速度。0 = 定格。")]',
        '[Tooltip("Attacker Timeline speed during hit stop. 0 = freeze.")]',
    ),
    ('[Tooltip("动作黏滞期间攻击者 Timeline 播放速度。")]', '[Tooltip("Attacker Timeline speed during stick.")]'),
    ('[Tooltip("动作黏滞持续时间（秒）。")]', '[Tooltip("Stick time (seconds).")]'),
    ('[Tooltip("传给 Cinemachine Impulse 的强度。")]', '[Tooltip("Impulse strength for Cinemachine.")]'),
    ('[Tooltip("随机选取一条播放；留空则不播音效")]', '[Tooltip("Pick one clip at random. Empty = no sound.")]'),
    ('[Tooltip("每次播放在 1 ± 此值 范围内随机 pitch")]', '[Tooltip("Random pitch around 1 ± this value.")]'),
    ('[Tooltip("屏幕语义命中特效预制体；留空则不生成。")]', '[Tooltip("Screen-space hit VFX prefab. Empty = none.")]'),
    ('[Tooltip("AngleMode=Preset 时绕视轴的角度（度）。")]', '[Tooltip("Angle (degrees) when AngleMode is Preset.")]'),
    ('[Tooltip("AngleMode=Random 时的随机角范围上界（度）。")]', '[Tooltip("Max random angle (degrees) when AngleMode is Random.")]'),
    ('[Tooltip("特效整体缩放。")]', '[Tooltip("Overall VFX scale.")]'),
    ('[Tooltip("特效生存时长（秒）。")]', '[Tooltip("VFX lifetime (seconds).")]'),
    ('[Tooltip("粒子仿真速度倍率。")]', '[Tooltip("Particle sim speed scale.")]'),
    ('[Tooltip("世界方向命中特效预制体；留空则不生成。")]', '[Tooltip("World hit VFX prefab. Empty = none.")]'),
    (
        '[Tooltip("主喷射轴在世界空间中的方向。")]',
        '[Tooltip("Main spray axis in world space.")]',
    ),
    ('[Tooltip("绕喷射轴随机或预设旋转。")]', '[Tooltip("Roll around spray axis: random or fixed.")]'),
    ('[Tooltip("RollMode=Preset 时使用的固定角度（度）。")]', '[Tooltip("Fixed roll (degrees) when RollMode is Preset.")]'),
    ('[Tooltip("RollMode=Random 时的随机角范围上界（度）。")]', '[Tooltip("Max random roll (degrees) when RollMode is Random.")]'),
    ('[Tooltip("要控制的粒子系统预制体")]', '[Tooltip("Particle system prefab to drive")]'),
    ('[Tooltip("相对于父级的本地位置")]', '[Tooltip("Local position under parent")]'),
    ('[Tooltip("相对于父级的本地旋转（欧拉角）")]', '[Tooltip("Local rotation (Euler) under parent")]'),
    ('[Tooltip("相对于父级的本地缩放")]', '[Tooltip("Local scale under parent")]'),
    ('[Tooltip("激活时自动播放")]', '[Tooltip("Play on activate")]'),
    ('[Tooltip("播放完成后销毁实例")]', '[Tooltip("Destroy instance when done")]'),
    ('[Tooltip("随机种子，确保播放一致性")]', '[Tooltip("Random seed for stable playback")]'),
    ('[Tooltip("Pivot 跟随敌人旋转的平滑速度")]', '[Tooltip("How fast pivot follows lock target")]'),
    ('[Tooltip("退出锁定回到自由模式时的回正速度")]', '[Tooltip("How fast pivot resets when leaving lock-on")]'),
    ('[Tooltip("偏移角度变化的平滑时间")]', '[Tooltip("Smooth time for offset angle changes")]'),
    ('[Tooltip("切换左右肩所需的最小输入阈值")]', '[Tooltip("Min input to swap left/right shoulder")]'),
    ('[SerializeField, Tooltip("基础移动速度")]', '[SerializeField, Tooltip("Base move speed")]'),
    ('[SerializeField, Tooltip("默认的移动模式 (如: 普通自由移动)")]', '[SerializeField, Tooltip("Default move mode (e.g. free walk)")]'),
    ('[Tooltip("Clip 开始时清理输入缓冲")]', '[Tooltip("Clear input buffer when clip starts")]'),
    ('[Tooltip("Clip 开始时清理标签")]', '[Tooltip("Clear tags when clip starts")]'),
    ('[Tooltip("Clip 正常播完时清理输入缓冲")]', '[Tooltip("Clear input buffer when clip ends normally")]'),
    ('[Tooltip("Clip 正常播完时清理标签")]', '[Tooltip("Clear tags when clip ends normally")]'),
    ('[Tooltip("Clip 被强制中断时清理输入缓冲")]', '[Tooltip("Clear input buffer when clip is cut")]'),
    ('[Tooltip("Clip 被强制中断时清理标签")]', '[Tooltip("Clear tags when clip is cut")]'),
    ('[Tooltip("想要在这个时间段内激活的标签")]', '[Tooltip("Tags to add during this clip")]'),
    ('[Tooltip("每个输入之间的最大间隔")]', '[Tooltip("Max time gap between inputs")]'),
    ('[Tooltip("该移动模式对应的 2D 混合树或动画过渡")]', '[Tooltip("2D blend tree or transition for this mode")]'),
    ('[Tooltip("切入该动画的平滑融合时间")]', '[Tooltip("Blend time into this motion")]'),
    ('[Tooltip("该姿态下的速度倍率")]', '[Tooltip("Speed scale in this pose")]'),
    ('[Tooltip("需要在黑板上检测的标签")]', '[Tooltip("Tag to read from the board")]'),
    ('[Tooltip("在时间轴该片段期间要发放的标签")]', '[Tooltip("Tags to add while this plays")]'),
    ('[Tooltip("命中盒挂载到的骨骼/节点。")]', '[Tooltip("Bone or node for this hit box")]'),
    ('[Tooltip("rotationMode=AngularSpeed 时有效；0=不做旋转覆盖")]', '[Tooltip("Used when rotation mode is AngularSpeed. 0 = no rotate override")]'),
    ('[Tooltip("需要匹配的按钮")]', '[Tooltip("Button to match")]'),
    ('[Tooltip("需要匹配的按钮状态")]', '[Tooltip("Button state to match")]'),
    ('[Tooltip("需要匹配的摇杆方向")]', '[Tooltip("Stick direction to match")]'),
    ('[Tooltip("需要匹配的摇杆力度")]', '[Tooltip("Stick strength to match")]'),
    ('[Tooltip("需要匹配的输入状态")]', '[Tooltip("Input state to match")]'),
    ('[Tooltip("需要匹配的按钮组合")]', '[Tooltip("Button flags to match")]'),
]

for m in MORE:
    if m not in PAIRS:
        PAIRS.append(m)

PAIRS.sort(key=lambda x: -len(x[0]))


def main():
    changed = []
    for path in sorted(ROOT.rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        orig = text
        for old, new in PAIRS:
            if old in text:
                text = text.replace(old, new)
        if text != orig:
            path.write_text(text, encoding="utf-8", newline="\n")
            changed.append(path)
    print("Updated", len(changed), "files")
    for p in changed:
        print(" ", p.relative_to(ROOT.parent.parent))


if __name__ == "__main__":
    main()
