## 目标

整理当前已经确定的新动作系统方案。

本版本只固定已经达成一致的部分，暂时不展开 `Locomotion` 的最终控制权方案。

核心要求：

- 保持简单
- 不引入不必要的新实体
- 让玩家动作、受击动作、以后敌人 BT 注入动作都能走统一处理路线
- 让 Tag 成为真正可靠的运行时数据载体

---

## 总体思路

最终保留的主线是：

- `ActionAsset` 仍然是最小动作播放单元
- `ASM` 仍然是统一的动作候选收集与播放裁决中心
- 所有攻击、闪避、受击等 `ActionAsset` 都统一进入 ASM 轮询
- 敌人 BT 以后通过 ASM 的公开方法把动作加入本帧候选池
- `Tag` 系统成为动作切换、窗口、受击路由的重要状态基础

本方案不强行引入新的动作图系统，也不强行引入额外的高层意图层。

---

## Actor 的双 TagContainer

`Actor` 保存两个 `TagContainer`：

### 1. PersistentTags

用于存放长期状态。

示例：

- 武器状态
- 锁定状态
- 角色形态
- 长期霸体状态
- 其它不应在当前动作结束时自动消失的状态

### 2. TransientTags

用于存放临时状态。

示例：

- 连段窗口
- 闪避无敌窗口
- 攻击中某段状态
- 受击事件路由 Tag
- 当前动作期间的各种临时标记

### 固定规则

`TransientTags` 在当前 `Action` 结束时统一清空：

- 自然结束时清空
- 被打断时也清空

这条规则不提供可选项，不放到 `ActionAsset` 上配置。

它是动作系统的固定行为。

---

## Tag 写入与读取规则

### TagClip

在添加 Tag 的地方，例如 `TagClip`，需要显式配置：

- 写入到 `PersistentTags`
- 或写入到 `TransientTags`

### TagCondition

在检查 Tag 的地方，需要显式配置：

- 检查哪个容器
- 是否使用模糊匹配

建议保留两项配置：

- `targetContainer`
  - `Persistent`
  - `Transient`
- `matchMode`
  - `Exact`
  - `Fuzzy`

### 模糊匹配

如果当前 TagTree 已支持层级包含判断，则直接复用已有能力。

语义建议：

- `Exact`：必须完全匹配
- `Fuzzy`：父级可匹配子级

---

## 受击的核心设计

### 基本原则

攻击不直接让目标播放受击动作。

攻击只负责：

`把本次命中的结果写成目标身上的临时 Tag`

然后目标自己的 ASM 再通过统一轮询选择受击 Action。

---

## 受击 Tag 的写入规则

命中相关配置放在攻击判定一侧，也就是 `HitClip` 所在层。

### 固定规则

`HitClip` 配置的受击 Tag：

- 只能写入 `TransientTags`
- 不提供写入 `PersistentTags` 的选项

原因是：

这些 Tag 的语义是“本次命中事件”，不是长期状态。

---

## 受击数据流

建议固定为以下流程：

1. 攻击 Timeline 播放，HitBox 命中目标
2. `AttackHandler` 组装命中数据
3. 命中数据中包含本次命中要施加给目标的受击 Tag
4. 调用目标 `ActorCombater.TakeDamage(hitData)`
5. 由目标自己的 `ActorCombater` 决定是否接受此次命中，并将受击 Tag 写入目标的 `TransientTags`
6. 目标自己的 ASM 在统一轮询中发现对应受击 Action 条件成立
7. ASM 切入受击 Action

### 关键原则

不要让攻击者直接操作目标的 `TagContainer`。

最终是否写入、写入什么，应由受击者侧完成。

这样以后仍然可以自然接入：

- 霸体
- 无敌
- 死亡优先
- 格挡
- 弹反

---

## 受击 Tag 的语义分层

建议明确区分两类 Tag：

### 1. Event 类 Tag

这类 Tag 用于“命中路由”。

例如：

- `Event.Hit.Light.Front`
- `Event.Hit.Heavy.Front`
- `Event.Hit.Launch`
- `Event.Hit.Knockdown`

特点：

- 由攻击命中时写入
- 只进入 `TransientTags`
- 主要用于触发对应受击 Action

### 2. State 类 Tag

这类 Tag 用于“动作状态表达”。

例如：

- `State.HitStun`
- `State.Knockdown`
- `State.AirborneHit`

特点：

- 由受击 Action 自己通过 `SelfTag` 或 `TagClip` 添加
- 代表角色当前真正处于什么受击状态

### 这样拆开的目的

把“受击事件”和“受击状态”分离。

- `Event.*` 负责路由
- `State.*` 负责状态

这样更清晰，也更稳定。

---

## 受击 Action 的条件写法

受击 Action 不需要特殊系统。

它和其它 Action 一样，只通过普通 `TagCondition` 成立。

例如：

- `Hit_Heavy_Front`
  - 检查 `TransientTags`
  - 需要命中 `Event.Hit.Heavy.Front`

因此：

受击动作依然只是 ASM 中的普通候选动作。

---

## ASM 的定位

`ASM` 继续作为统一的动作候选收集与播放裁决中心。

它负责统一处理：

- 普通攻击动作
- 闪避动作
- 受击动作
- 以后由 BT 注入的动作

它每帧做的事情仍然是：

1. 收集所有当前满足条件的内部候选动作
2. 收集外部注入的候选动作
3. 统一排序
4. 选出最终要播放的那个动作

因此：

不同决策来源，统一处理出口。

---

## 外部候选注入

为未来敌人 BT 预留的方式是：

`BT 通过 ASM 的公开方法，把某个 ActionAsset 注册为本帧外部候选`

建议语义是：

- 注册候选
- 不直接播放

而不是让 BT 直接绕过 ASM。

### 关键原则

外部候选仍然必须经过 ASM 的统一裁决。

这样可以避免产生后门逻辑。

---

## Action 的统一路线

本方案中，以下 Action 都走同一条路：

- 攻击
- 闪避
- 受击
- 以后敌人 BT 指定的动作

它们唯一的区别，只在于：

- 条件来源不同
- 候选来源不同
- 优先级不同

但最终都进入 ASM 统一选择。

---

## 当前已经确定、不再扩展的部分

为了保持方案稳定，以下部分目前不继续展开：

- 不引入新的 ActionGraph
- 不引入新的 Intent 层
- 不引入新的 Channel 实体系统
- 暂不确定 `Locomotion` 与 ASM 的最终优雅控制权方案

`Locomotion` 部分后续再单独处理。

---

## 当前方案的优点

### 1. 简单

没有引入额外复杂的图系统或高层语义层。

### 2. 统一

玩家动作、受击动作、未来 AI 动作都能进入同一处理路径。

### 3. Tag 成为稳定的数据载体

窗口、事件、状态都能有明确归属。

### 4. 后续仍可演化

将来如果要做更正式的图系统、分层状态机或更强的 Brain 层，也可以在这个基础上演进，而不需要推翻底层播放体系。

---

## 当前方案一句话总结

`Actor` 持有长期与临时两个 TagContainer；命中时攻击只向目标的 `TransientTags` 写入受击事件 Tag；攻击、闪避、受击以及未来 BT 注入的动作都统一进入 ASM 轮询，由 ASM 在同一条路线中完成候选收集、排序与播放裁决。
