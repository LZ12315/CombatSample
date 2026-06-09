# Active Tasks

Put current AI task markdown files here.

Executors should scan this directory for tasks with:
- `status: todo`
- `status: changes_requested`

Do not execute archived tasks from here.

Old task bodies may reflect earlier conventions. Use `agent-system/templates/TASK_TEMPLATE.md` and current `agent-system/protocols/` as the authoritative format for new tasks.

## Current Task Note - 2026-06-09

The old release/camera task backlog was archived during the 2026-06-09 cleanup pass.

There are currently no active task files waiting for execution or review.

When new work starts:
- Create fresh task files from `agent-system/templates/TASK_TEMPLATE.md`.
- Base camera tasks on the current `Assets/Scripts/Camera` architecture, not the archived sector/inertia experiments.
- Use current project paths as they exist in this workspace, such as `Assets/Create/`, `Docs/Plans/`, and `Tools/`.

Archive cleanup performed on 2026-06-09:
- Moved `task-20260522-camera-follow-lock-composition.md` to `agent-tasks/archive/2026/`.
- Moved `task-20260524-lock-camera-sector-soft-edge.md` to `agent-tasks/archive/2026/`.
- Removed the duplicate active copy of `task-20260515-actor-motor-timescale-motion-delta.md`; the more complete archived copy remains under `agent-tasks/archive/2026/`.
- Archived `task-20260523-lock-camera-sector-gizmos.md` as an old/wrong-direction camera diagnostic record.
- Archived `task-20260523-lock-camera-sector-yaw-gate.md` as an old/wrong-direction camera sector record.
- Archived `task-20260523-lock-camera-stable-combat-focus.md` as an old/wrong-direction camera diagnostic record.
- Archived `task-20260523-soft-lock-composition-inertia.md` as an old/wrong-direction soft-lock experiment record.
- Archived `task-20260609-release-code-hotspot-review.md` after owner acceptance.
- Archived `task-20260609-combat-hud-disable-hide.md` after Claude review accepted the HUD lifecycle fix.
- Archived `task-20260609-main-structure-naming-cleanup.md` after Claude review recorded the final retained path choices in this workspace.

See `Docs/Main_Cleanup_Baseline_2026-06-09.md` for the current cleanup baseline.
