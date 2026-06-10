---
id: TASK-1
title: Impact 模块结构整理
status: Done
assignee:
  - Claude
created_date: '2026-06-10 09:00'
updated_date: '2026-06-10 09:16'
labels:
  - refactor
  - structure
dependencies: []
priority: medium
ordinal: 1000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
整理 `Assets/Scripts/Impact/` 目录结构，为后续多功能开发打好基础。

## 背景

当前 Impact 目录存在以下问题：
1. `ImpactEffectConfig.cs` (231行) 塞了抽象基类、4个活跃配置类、4个废弃配置类、3个枚举，全部混在一个文件
2. 根目录有 7 个小工具文件平铺，缺少分组
3. `ScreenOrientedImpactRotationResolver` 和 `WorldDirectionalImpactRotationResolver` 中 `TryGetPrimaryCamera` 方法完全重复（各 28 行）

## 目标

- 拆分 `ImpactEffectConfig.cs`，每类一个文件
- 废弃类隔离到 `Deprecated/`
- 小工具文件按职责分到 `Utility/VfxUtility/` 和 `Utility/RotationUtility/`
- 消除 `TryGetPrimaryCamera` 重复代码

## 最终结构

```
Impact/
├── Config/                             ← 拆 ImpactEffectConfig
│   ├── ImpactEffectConfig.cs
│   ├── SpeedEffectConfig.cs
│   ├── ScreenShakeEffectConfig.cs
│   ├── HitSoundEffectConfig.cs
│   └── HitVfxConfig.cs
├── Deprecated/                         ← 隔离 4 个废弃类
│   ├── HitStopEffectConfig.cs
│   ├── HitStickEffectConfig.cs
│   ├── ScreenOrientedImpactEffectConfig.cs
│   └── WorldDirectionalImpactEffectConfig.cs
├── Effects/                            ← 保留
│   ├── ActionSpeedEffect.cs
│   └── AttackerSpeedEffect.cs
├── Utility/
│   ├── VfxUtility/                     ← 锚点、朝向、渲染
│   │   ├── HitConfirmVfxRenderUtility.cs
│   │   ├── HitVfxAnchorUtility.cs
│   │   └── HitVfxFacingUtility.cs
│   └── RotationUtility/               ← 屏幕向/世界向旋转
│       ├── ImpactRotationUtility.cs
│       ├── ScreenOrientedImpactRotationResolver.cs
│       └── WorldDirectionalImpactRotationResolver.cs
├── VfxFacingEnums.cs                   ← 不动（被多处引用）
├── ImpactSystem.cs                     ← 不动
├── ImpactData.cs                       ← 不动
└── ImpactEffect.cs                     ← 不动
```
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 ImpactEffectConfig.cs 拆分为 5 个独立文件（基类 + SpeedEffect + ScreenShake + HitSound + HitVfx）
- [x] #2 4 个废弃配置类移入 Deprecated/ 子目录
- [x] #3 VFX 工具 3 文件移入 Utility/VfxUtility/ 子目录
- [x] #4 旋转工具 3 文件移入 Utility/RotationUtility/ 子目录
- [x] #5 TryGetPrimaryCamera 重复代码提取到 RotationUtility 共享方法
- [x] #6 所有 .meta 文件随源文件移动
- [x] #7 不修改 scene/prefab/ProjectSettings
- [x] #8 不改变任何类的 public API 和序列化字段
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
## 实施计划

**第一步：建目录**
- `mkdir Impact/Config/`, `Impact/Deprecated/`, `Impact/Utility/VfxUtility/`, `Impact/Utility/RotationUtility/`

**第二步：拆分 ImpactEffectConfig.cs**
- 提取 4 个活跃配置类到 `Config/` 各自独立文件
- 原文件只保留抽象基类 `ImpactEffectConfig` + `HitVfxOcclusionMode` enum
- `HitVfxConfig` 连带 `VfxAnchorMode`、`VfxOrientationMode` enum 一起提取

**第三步：移出废弃类**
- 4 个 `[Obsolete]` 类从 `ImpactEffectConfig.cs` 移到 `Deprecated/` 各独立文件

**第四步：移动 VFX 工具**
- `HitConfirmVfxRenderUtility.cs`、`HitVfxAnchorUtility.cs`、`HitVfxFacingUtility.cs` → `Utility/VfxUtility/`

**第五步：移动旋转工具**
- `ImpactRotationUtility.cs`、`ScreenOrientedImpactRotationResolver.cs`、`WorldDirectionalImpactRotationResolver.cs` → `Utility/RotationUtility/`

**第六步：消除重复代码**
- `TryGetPrimaryCamera` 两个 Resolver 中的 28 行完全相同，提取到 `ImpactRotationUtility.cs`，两个 Resolver 改为调用共享方法

**第七步：.meta 文件**
- 移动文件时同步移动 `.meta`；新文件 Unity 自动生成 `.meta`

**不改：**
- `VfxFacingEnums.cs`、`ImpactSystem.cs`、`ImpactData.cs`、`ImpactEffect.cs`、`Effects/` 不动
- 所有 namespace、class 名、public API 不变
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
2026-06-10: 完成所有代码结构调整。创建 Config/、Deprecated/、Utility/VfxUtility/、Utility/RotationUtility/ 四个子目录。

ImpactEffectConfig.cs 从 231 行缩减为 16 行（仅保留抽象基类和 HitVfxOcclusionMode enum）。

VFX 工具 3 文件移入 Utility/VfxUtility/，旋转工具 3 文件移入 Utility/RotationUtility/，.meta 文件同步移动。

TryGetPrimaryCamera 重复代码（2×28行）提取到 ImpactRotationUtility.TryGetPrimaryCamera()，两个 Resolver 移除私有副本。

新建的 Config/ 和 Deprecated/ 下 8 个 .cs 文件会在 Unity 下次 import 时自动生成 .meta。

未修改 scene/prefab/ProjectSettings。所有 public API 和序列化字段不变。
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Impact 模块结构整理完成。

改动：
- `ImpactEffectConfig.cs` 从 231 行拆分为 5 个独立文件（基类 + 4 个活跃配置类入 `Config/`）
- 4 个废弃类隔离到 `Deprecated/`
- VFX 工具 3 文件归入 `Utility/VfxUtility/`
- 旋转工具 3 文件归入 `Utility/RotationUtility/`
- `TryGetPrimaryCamera` 重复代码（2×28行）提取到 `ImpactRotationUtility`

影响范围：仅 `Assets/Scripts/Impact/`，未触及 Unity 序列化文件。所有 public API 不变。
<!-- SECTION:FINAL_SUMMARY:END -->
