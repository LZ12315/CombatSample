# KCC 接入复盘总结

## 一句话总结

这几轮我们做的事情，可以简单理解为：

一开始 KCC 已经接进项目了，但它更像是“把原来的 CharacterController 移动逻辑换了一个执行器”。经过这几轮调整后，我们把它推进成了“由 KCC 负责最终物理结果，动作系统只负责表达运动意图”。

换句话说：

- 以前更像是：动作系统算好速度，然后让 KCC 帮忙移动
- 现在更像是：动作系统提出想怎么动，KCC 实际算出角色最后动成什么样，再把结果告诉动作系统

这对动作游戏很重要。因为角色撞墙、上坡、下台阶、被地面吸附、RootMotion 被挡住，这些都不是动作系统自己能准确判断的，最终应该以 KCC 的求解结果为准。

## 第一阶段：先审查当前 KCC 接入

我们先看了你已经完成的 KCC 接入。

当时的判断是：整体方向是对的，不是乱接。

项目里已经有比较清楚的分层：

- `ActorMovement` 负责动作意图，比如移动输入、RootMotion、Impulse、VelocityClip、跳跃状态
- `ActorMotor` 负责和 KCC 对接，实现 `ICharacterController`
- `MotionChannels` 负责把不同来源的速度组合起来，比如水平冲量、垂直冲量、重力、Velocity owner

这个结构是好的，因为动作系统没有直接塞进 KCC 插件里，KCC 也没有反过来污染动作逻辑。

但我们也发现它还停在“第一阶段适配”：

旧的 CharacterController 时代，通常是业务层自己算移动，然后调用 `Move`。接入 KCC 后，虽然执行者变成了 KCC，但很多逻辑还是按旧思路在跑。

最大的问题是：系统还没有完全承认“KCC 最终求解结果才是真实物理结果”。

## 第二阶段：整理计划文档

为了避免后续变成哪里有问题补哪里，我们先整理了一份计划：

`plans/KCC接入第二阶段改造方案.md`

这份计划把重点拆成了三个问题：

1. 跳跃或挑飞首帧可能被地面状态吃掉
2. `CurrentVelocity` 不是 KCC 最终速度
3. hit stop 时可能残留上一帧速度

它的作用不是写一堆漂亮话，而是给后续实现一个清楚的方向：

KCC 的生命周期是什么样，就按它的生命周期来组织角色运动。

## 第三个问题：跳跃或挑飞首帧可能被吃掉

这个问题的本质是时机不对。

KCC 一帧里的流程大致是：

1. 先探测地面
2. 再问角色现在想要什么速度
3. 再真正移动并处理碰撞
4. 最后结束这一帧

原来的问题是：

`Motor.ForceUnground()` 是在第 2 步里调用的，但第 1 步探地已经结束了。

所以角色明明要起跳或被挑飞了，但本 tick 里 KCC 仍然可能认为它还在地面上。然后地面逻辑会把垂直速度清零。

表现上就是：

- 跳跃第一帧不够干净
- 挑飞第一帧可能被压住
- 角色状态晚一帧才离地

解决方式是：

当 `ForceUnground` 被调用后，本 tick 里我们立刻用一个本地标记告诉业务层：

“虽然 KCC 的下一次探地还没发生，但从动作系统角度看，这一帧已经主动离地了。”

这样首帧的向上速度不会再被地面分支吃掉。

## 第四个问题：CurrentVelocity 不是最终速度

这是这次最核心的一点。

之前 `CurrentVelocity` 是在 `ActorMotor.UpdateVelocity` 里发布的。

但问题是：这个时候 KCC 还没有真正移动。

也就是说，这个速度只是“我想怎么动”，不是“我最后实际怎么动”。

举个很直观的例子：

角色朝墙跑，动作系统说“我这一帧向前 5m/s”。

但 KCC 真正移动时发现前面是墙，于是角色可能只移动了一点，甚至完全没动。

如果我们在 KCC 碰撞求解之前就发布 `CurrentVelocity`，动画系统就会以为角色还在高速向前跑。

这会导致：

- 撞墙后动画参数不准
- `VelocityCondition` 判断不准
- RootMotion 被挡住后，动作系统还以为角色真的移动了
- 调试面板显示的速度和角色实际位移不一致

解决方式是：

- `UpdateVelocity` 只负责生成“请求速度”
- `AfterCharacterUpdate` 里再发布“最终速度”

最终速度来自 KCC 移动后的结果。

也就是：

```csharp
finalVelocity = (KCC最终位置 - 本tick起点位置) / deltaTime;
```

这样 `CurrentVelocity` 的意义就清楚了：

它不是动作系统想要的速度，而是 KCC 实际求解出来的 gameplay 速度。

## 第五个问题：hit stop 时速度残留

hit stop 的时候，`MovementTimeScale` 可能变成 0。

之前代码里，如果时间缩放为 0，就直接把 KCC velocity 置零并 return。

问题是：`ActorMovement.CurrentVelocity` 没有同步更新。

所以角色物理上停住了，但动画或条件系统还可能读到上一帧速度。

解决方式是：

暂停 tick 也走统一的速度发布路径。

当 movement time scale 为 0 时，明确发布：

```csharp
CurrentVelocity = Vector3.zero;
```

这样 hit stop 的语义更一致：

角色停住，KCC 停住，动作系统读到的速度也停住。

## 第六阶段：你用 Claude Code 实现计划，我们做 review

你在 Claude Code 上按计划实现了第一版。

那一版已经解决了大部分核心问题：

- `CurrentVelocity` 发布点后移到了 `AfterCharacterUpdate`
- hit stop 会发布零速度
- 主动离地当帧会把 `isGrounded` 当成 false
- 旧的 `CharacterController` 相关组件开始标记为 obsolete
- VFX 里的命名开始从 `CharacterController` 语义过渡到更通用的 `CharacterBody`

这一步已经把接入推进到了更接近 KCC 风格的结构。

但 review 后我们又发现两个更细的问题。

## 第七个问题：主动离地事件会触发两次

第一版实现里，主动离地当帧会调用：

```csharp
ApplyForcedUnground();
```

这个方法会触发一次：

```csharp
OnLeftGround
```

但下一 tick，KCC 的探地结果也会从“稳定接地”变成“不接地”。

于是 `ApplyGroundingUpdate(false, true)` 又会触发一次 `OnLeftGround`。

这就变成了：

同一次跳跃或挑飞，离地事件发了两次。

可能造成的问题包括：

- 重复播放离地特效
- 重复清理状态
- 重复触发动作条件
- 某些动作被意外打断

我们最后的修法不是简单加一个 if，而是把事件语义收口。

现在 `ActorMovement` 里有一个标记：

```csharp
_suppressNextKccLeftGroundEvent
```

它的意思是：

“主动离地事件我这一帧已经发过了，下一次 KCC 报告 stable->unstable 的时候，只更新状态，不要再发事件。”

这样：

- 主动离地当帧：马上发一次 `OnLeftGround`
- 下一 tick KCC 追上状态：只同步状态，不重复发事件

这个改法更符合架构，因为它把“事件发射规则”放在 `ActorMovement` 的地面状态管理里，而不是散落在 `ActorMotor` 里补判断。

## 第八个问题：最终速度的起点可能取早了

第一版实现里，最终速度这样算：

```csharp
Motor.TransientPosition - _motorFrameStartPosition
```

但我们检查 KCC 源码后发现：

KCC 是先调用 `BeforeCharacterUpdate`，然后才把 transient position 重置成当前 transform 位置。

这意味着在 `BeforeCharacterUpdate` 里读取 `Motor.TransientPosition`，有可能读到上一 tick 的缓存。

大多数正常帧可能没事，因为上一 tick 的 transient position 通常等于当前 transform。

但在一些边界情况下会出问题：

- teleport
- 外部脚本直接改 transform
- 角色 disable / enable
- 场景摆位修正
- 非 KCC 方式移动了一次

解决方式是：

在 `BeforeCharacterUpdate` 里记录 `transform.position`，而不是 `Motor.TransientPosition`。

也就是：

```csharp
_motorFrameStartWorldPosition = transform.position;
```

然后在 `AfterCharacterUpdate` 里计算：

```csharp
solvedDelta = Motor.TransientPosition - _motorFrameStartWorldPosition;
```

这样起点就是这一帧真实的世界位置，终点是 KCC 求解后的 transient position。

这个边界更稳定。

## 最终结果

经过这几轮后，现在的结构更清楚了：

- `ActorMovement` 负责动作状态、地面状态、速度发布、事件语义
- `ActorMotor` 负责 KCC 生命周期桥接
- `MotionChannels` 负责速度组合
- KCC 负责最终物理结果

更具体地说：

- 跳跃和挑飞首帧不再被地面状态吃掉
- `CurrentVelocity` 现在代表 KCC 最终求解后的真实 gameplay 速度
- hit stop 时不会残留上一帧速度
- 主动离地不会重复触发 `OnLeftGround`
- solved velocity 的起点不再依赖可能过期的 transient 缓存
- 旧 CharacterController 相关逻辑开始被标记为迁移遗留

## 为什么这些修改重要

动作游戏里的运动系统最怕两套事实。

如果动作系统认为角色在高速移动，但物理系统实际没动，动画、条件、命中、取消窗口都会慢慢变乱。

这次改造的核心价值，就是把事实来源统一到 KCC：

动作系统负责表达意图。

KCC 负责回答结果。

`CurrentVelocity`、`groundState`、事件系统再基于这个结果服务动画和动作逻辑。

这样后续继续加功能时，比如：

- 移动平台
- 空中受击
- 地面弹反位移
- 撞墙反弹
- RootMotion 攻击撞墙裁剪
- 可推动刚体

系统会更稳，不容易每加一个功能就多一套例外逻辑。

## 已验证内容

我们已经跑过：

```powershell
dotnet build .\Assembly-CSharp.csproj --no-restore
```

结果是：

- 0 个编译错误
- 还有一些项目已有 warning，主要是 obsolete 和 unused 字段

这说明当前修改至少在 C# 编译层面是通过的。

## 还建议在 Unity 里手测的内容

接下来最值得手测的是：

1. 地面起跳：确认首帧立刻离地，`OnLeftGround` 只触发一次
2. 地面挑飞：确认 Y 速度不会被地面分支清零
3. 二段跳：确认空中再次向上冲量正常
4. hit stop：确认 `CurrentVelocity` 会变成零
5. 撞墙移动：确认动画参数不再显示撞墙前速度
6. RootMotion 攻击撞墙：确认 `CurrentVelocity` 反映被 KCC 裁剪后的实际位移

## 最后一句话

这次不是单纯“修了几个 bug”。

更准确地说，我们把角色运动系统的权威边界往前推进了一步：

从“动作系统自己相信自己算出来的速度”，变成了“动作系统相信 KCC 实际解出来的结果”。

这会让后面的战斗移动、受击、浮空、撞墙、RootMotion 和平台交互都更容易继续长下去。
