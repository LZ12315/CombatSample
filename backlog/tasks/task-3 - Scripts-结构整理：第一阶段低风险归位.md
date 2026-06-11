---
id: TASK-3
title: Input 生成代码隔离与 Editor 依赖小修
status: Done
assignee:
  - Codex
created_date: '2026-06-11 05:06'
updated_date: '2026-06-11 13:14'
labels:
  - refactor
  - structure
  - unity
dependencies: []
modified_files:
  - Assets/Scripts/Input/Generated.meta
  - Assets/Scripts/Input/Generated/PlayerInputControl.cs
  - Assets/Scripts/Input/Generated/PlayerInputControl.cs.meta
  - Assets/Settings/Input/PlayerInputControl.inputactions.meta
  - Assets/Scripts/Combat/AttackHandler.cs
  - Assets/Scripts/TimelinePlayable/HitBox/ActionHitBoxBehavior.cs
priority: medium
ordinal: 3000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
执行一次极小范围的 Scripts 结构整理，只解决明确会误导或影响编译边界的问题，不做架构重塑。

已确认范围：隔离 Input System 生成的 PlayerInputControl.cs；同步 Input System wrapperCodePath；修正两个 Runtime 文件里的 UnityEditor 引用边界。

本任务不整理 ActionSystem、Actor、Combat 目录层级，不触碰 Camera 既有未提交整理，不调整 Impact、NodeCanvas、TimelinePlayable 目录结构，不引入 namespace 或 asmdef，不拆大类，不改变运行逻辑。
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 PlayerInputControl.cs 移动到 Assets/Scripts/Input/Generated/PlayerInputControl.cs，且对应 .meta 同步移动以保留 MonoScript GUID。
- [x] #2 Assets/Settings/Input/PlayerInputControl.inputactions.meta 的 wrapperCodePath 同步更新为 Assets/Scripts/Input/Generated/PlayerInputControl.cs。
- [x] #3 Assets/Scripts/Combat/AttackHandler.cs 移除未使用的 using UnityEditor，不做其他逻辑改动。
- [x] #4 Assets/Scripts/TimelinePlayable/HitBox/ActionHitBoxBehavior.cs 的 UnityEditor using 仅在 UNITY_EDITOR 条件编译下可见，不拆 HitBoxUpdater 或改生命周期逻辑。
- [x] #5 不整理 ActionSystem、Actor、Combat 子目录、Camera、Impact、NodeCanvas 或 TimelinePlayable 目录结构；不新增 namespace、asmdef 或运行逻辑变更。
- [x] #6 完成后检查 git diff 和 UnityEditor 引用扫描；若无法运行 Unity 编译，记录未验证风险。
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
Approved narrow plan:
1. Create Assets/Scripts/Input/Generated/ if it does not exist.
2. Move Assets/Scripts/Input/PlayerInputControl.cs and PlayerInputControl.cs.meta into Input/Generated/.
3. Update Assets/Settings/Input/PlayerInputControl.inputactions.meta wrapperCodePath to Assets/Scripts/Input/Generated/PlayerInputControl.cs.
4. Remove the unused UnityEditor using from Assets/Scripts/Combat/AttackHandler.cs.
5. Wrap the UnityEditor using in Assets/Scripts/TimelinePlayable/HitBox/ActionHitBoxBehavior.cs with #if UNITY_EDITOR / #endif; do not change HitBoxUpdater logic.
6. Verify with git diff and an rg scan for unguarded UnityEditor references in touched runtime files. Unity compile validation should be recorded if available; otherwise note manual validation steps/risk.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implemented the approved narrow scope only.

Changes made:
- Created Assets/Scripts/Input/Generated/ and added Generated.meta.
- Moved PlayerInputControl.cs and PlayerInputControl.cs.meta into Assets/Scripts/Input/Generated/.
- Updated Assets/Settings/Input/PlayerInputControl.inputactions.meta wrapperCodePath to Assets/Scripts/Input/Generated/PlayerInputControl.cs.
- Removed unused using UnityEditor from Assets/Scripts/Combat/AttackHandler.cs.
- Wrapped using UnityEditor in Assets/Scripts/TimelinePlayable/HitBox/ActionHitBoxBehavior.cs with #if UNITY_EDITOR / #endif.

Validation performed:
- Confirmed new generated wrapper file and its .meta exist; old Input root copies no longer exist.
- Confirmed moved PlayerInputControl.cs.meta preserved guid 0d65965e0bdf92140ac76a097536ccfe.
- Confirmed wrapperCodePath now points to Assets/Scripts/Input/Generated/PlayerInputControl.cs.
- Reviewed git diff for the three text edits; no logic changes were made.
- rg scan confirms AttackHandler.cs no longer references UnityEditor; ActionHitBoxBehavior.cs has UnityEditor using behind UNITY_EDITOR and existing EditorApplication calls remain behind UNITY_EDITOR.

Unity Editor compile was not run from this session. Manual validation before Done: open the project in Unity 2022.3.62f3, let it reimport, confirm there are no compile errors, and confirm Input System regeneration keeps PlayerInputControl.cs under Assets/Scripts/Input/Generated/.
<!-- SECTION:NOTES:END -->

## Comments

<!-- COMMENTS:BEGIN -->
author: Codex
created: 2026-06-11 05:07
---
Initial analysis / proposed approach pending user approval:

Observed structure:
- No .asmdef files under Assets/Scripts.
- Most project scripts are in the global namespace; introducing namespaces now would be a higher-risk compile/API change and should be deferred.
- Impact and Settings/Input already had recent structure tasks completed.
- Current git worktree already contains uncommitted Camera folder moves plus ProjectSettings edits; execution should not revert or mix unrelated changes.
- PlayerInputControl.cs is Input System generated; its source asset meta currently contains wrapperCodePath: Assets/Scripts/Input/PlayerInputControl.cs.
- Runtime files with UnityEditor dependency found: Assets/Scripts/Combat/AttackHandler.cs has unused using UnityEditor; Assets/Scripts/TimelinePlayable/HitBox/ActionHitBoxBehavior.cs uses EditorApplication under UNITY_EDITOR but has an unguarded using UnityEditor.

Suggested first-pass target:
- Input: move generated wrapper to Input/Generated/ and update wrapperCodePath; group checks/data separately if approved.
- ActionSystem: keep Conditions and Editor; group root data/config/runtime/cancel files into small responsibility folders.
- Actor: split root files into Core, Motor/Input, Tags/Feedback, Physics or similar local folders while leaving Motion and Editor intact.
- Combat: group runtime handlers, data/config, and feedback files.
- Leave Impact/NodeCanvas mostly untouched because they are already structured; leave Camera untouched until existing uncommitted Camera changes are confirmed.

Validation focus:
- Move .cs with .meta to preserve GUIDs.
- Re-scan for unguarded UnityEditor references after movement.
- Verify Unity compilation; if Unity is unavailable, record exact manual checks and risk.
---
<!-- COMMENTS:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Input 生成代码隔离与 Editor 依赖小修完成。

改动：
- `PlayerInputControl.cs` 和对应 `.meta` 移到 `Assets/Scripts/Input/Generated/`，保留 MonoScript GUID。
- `PlayerInputControl.inputactions.meta` 的 `wrapperCodePath` 更新到新路径，避免 Input System 下次生成回旧目录。
- `AttackHandler.cs` 移除未使用的 `using UnityEditor`。
- `ActionHitBoxBehavior.cs` 的 `using UnityEditor` 改为 `#if UNITY_EDITOR` 条件编译保护，未改 HitBoxUpdater 生命周期逻辑。

范围控制：没有整理 ActionSystem、Actor、Combat 子目录、Camera、Impact、NodeCanvas 或 TimelinePlayable 目录结构；没有新增 namespace、asmdef 或运行逻辑变更。

验证：完成路径检查、`.meta` GUID 检查、wrapperCodePath 检查、git diff 检查和 UnityEditor 引用扫描。Unity Editor 编译由用户 review 完成并接受。
<!-- SECTION:FINAL_SUMMARY:END -->
