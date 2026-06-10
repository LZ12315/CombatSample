---
id: TASK-2
title: Settings 目录整理：消除 Create 命名冲突
status: Done
assignee: []
created_date: '2026-06-10 09:03'
updated_date: '2026-06-10 09:24'
labels:
  - refactor
  - structure
dependencies: []
priority: low
ordinal: 2000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
消除 `Settings/Create/` 与 `Assets/Create/` 的命名冲突。

## 背景

- `Assets/Create/` 是游戏数据目录
- `Assets/Settings/Create/` 也是 "Create"，但放的是 Input System 配置和生成的 C# 代码
- 这种重名容易混淆，且 `PlayerInputControl.cs` 作为 C# 源码不该放在 Settings 目录

## 目标

```
Settings/Create/                     Settings/Input/
├── PlayerInputControl.cs       →   ├── (移到 Scripts/Input/)
├── PlayerInputControl.inputactions  ├── PlayerInputControl.inputactions
├── InputSystem.inputsettings.asset  ├── InputSystem.inputsettings.asset
└── *.meta                           └── *.meta
```

## 注意

- 必须在 Unity Editor 内操作，因为 `.inputactions` 资产内部记录了 C# 生成路径
- 直接 mv 文件会断开 Input System 的代码生成引用
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Settings/Create/ 重命名为 Settings/Input/
- [x] #2 PlayerInputControl.cs 移动到 Scripts/Input/
- [x] #3 PlayerInputControl.inputactions 留在 Settings/Input/
- [x] #4 Unity Editor 内操作，确保 .inputactions 内部引用不丢失
- [x] #5 不修改 scene/prefab/ProjectSettings 其他文件
<!-- AC:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Settings 目录整理完成。

- `Settings/Create/` → `Settings/Input/`（消除与 `Assets/Create/` 的命名冲突）
- `PlayerInputControl.cs` 从 Settings 移到 `Scripts/Input/`（C# 源码归位）
- `.inputactions` 和 `.inputsettings.asset` 留在 `Settings/Input/`
- 自动生成注释中的旧路径已手动更正
<!-- SECTION:FINAL_SUMMARY:END -->
