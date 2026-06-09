---
id: task-20260609-main-structure-naming-cleanup
title: Main structure naming cleanup
summary: Consolidate unclear project folders so main can serve as a cleaner base for future feature branches.
status: review
current_round: 1
planner: Codex
executor: Codex
reviewer:
created_at: 2026-06-09
updated_at: 2026-06-09
claimed_at: 2026-06-09
completed_at: 2026-06-09
---

# Main Structure Naming Cleanup

## Round 1

### Plan

Agent: Codex
Role: Planner/Executor
Date: 2026-06-09

Goal:
- Make the repository layout clearer for future branches by consolidating ambiguous folder names.

Non-goals:
- Do not remove or stop tracking root `bin/`; it contains old resources that may be useful later.
- Do not move `Assets/Resources`, scenes, prefabs, or project settings in this pass.
- Do not change gameplay behavior.

Allowed scope:
- Move `Assets/Create` to `Assets/GameData` while preserving `.meta` files.
- Rename `GameData` child folders to clearer collection names.
- Fix obvious asset naming typos in files and `m_Name` values.
- Move old planning documents under `Docs/Plans`.
- Rename the root utility folder from `Tool` to `Tools`.
- Update current documentation references for the renamed paths.

Acceptance criteria:
- Unity asset `.meta` files are moved with their assets.
- No scene, prefab, or `ProjectSettings` files are edited.
- Current docs point to the new folder names.
- Worktree diff shows only structural moves and supporting docs/task notes.

### Execution Report

Completed by Codex on 2026-06-09.

Moved structure:
- `Assets/Create` -> `Assets/GameData`
- `Assets/GameData/ActionAsset` -> `Assets/GameData/ActionAssets`
- `Assets/GameData/ActionList` -> `Assets/GameData/ActionLists`
- `Assets/GameData/Graph` -> `Assets/GameData/NodeCanvasGraphs`
- `Document/plans` -> `Docs/Plans`
- `Tool` -> `Tools`

Naming fixes:
- `Boxing_NormaLoco` -> `Boxing_NormalLoco`
- `Jarger_*` -> `Jaeger_*`
- `Sword_LightAttak_*` -> `Sword_LightAttack_*`
- `Sword/Locmotion` -> `Sword/Locomotion`

Documentation updates:
- Updated `Docs/Main_Cleanup_Baseline_2026-06-09.md`.
- Added `Docs/Project_Structure.md`.
- Updated `agent-tasks/active/README.md`.

Validation:
- Confirmed old root paths `Assets/Create`, `Document`, and `Tool` no longer exist.
- Confirmed new root paths `Assets/GameData`, `Docs/Plans`, and `Tools` exist.
- Confirmed no `Jarger`, `Locmotion`, `LightAttak`, or `NormaLoco` strings remain under `Assets/GameData`.
- Confirmed no scene, prefab, or `ProjectSettings` files were changed.
- Restored `Docs/Plans/帧表迁移完整落地方案.md` after an attempted path-string update exposed an encoding risk; it remains moved only, with content unchanged.

### Review

Pending owner review.
