# Documentation Index

> Last audited against `main`: 2026-08-02

`Docs` is divided by document authority. A file's directory is part of its status; do not treat archived records or proposals as active work.

## Current

- [Project Structure](Current/Project_Structure.md) — factual repository layout and scene entry points.
- [Scene Ownership Baseline](Current/Scene_Ownership_Baseline_2026-08-02.md) — current release and targeted validation scenes.
- [Actor Motion Validation](Current/Actor_Motion_Validation.md) — current behavior checklist; historical PlayMode results are explicitly marked and need rerunning.

## Proposals

- [帧表迁移完整落地方案（历史草案）](Proposals/帧表迁移完整落地方案_历史草案.md) — not approved and not executable as written. Requires a new go/no-go decision and redesign against the current Timeline-based action system.

## Archive

- [Camera SoftLock 交接报告](Archive/Camera_SoftLock_Handoff_Report.md) — obsolete branch handoff and failed experiment snapshot.
- [KCC 接入第二阶段改造方案](Archive/KCC接入第二阶段改造方案.md) — completed implementation plan.
- [KCC 接入复盘总结](Archive/KCC接入复盘总结.md) — historical implementation review.
- [ActorMotor Runtime 架构落地计划](Archive/actor-motor-runtime-refactor.md) — completed and subsequently superseded refactor plan.
- [项目最终建议（帧表迁移之外）](Archive/项目最终建议_帧表迁移之外.md) — historical recommendation list, mostly completed.

## Current Follow-ups

These are observations from the 2026-08-02 documentation audit, not approved implementation tasks:

- Rerun the Actor Motion PlayMode checklist, especially double jump and current External RootMotion rotation behavior.
- Decide whether a new broad development scene is needed; currently only release and targeted validation entry points are designated.
- Consider an Inspector warning for Poll actions with empty entry conditions.
- Consider an Action arbitration diagnostics tool if action-table debugging cost justifies it.
- Confirm in Unity that the obsolete, currently unreferenced `CharacterControllerRigidbodyPush` component can be deleted.
- Make an explicit go/no-go decision before investing further in Timeline-to-FrameEvent migration.

## Maintenance Rules

- `Current/` documents must describe verified repository facts or repeatable current validation procedures.
- `Proposals/` documents must state approval status, assumptions, migration scope, and validation requirements.
- `Archive/` documents are immutable historical context except for status banners or broken-link corrections.
- Add the verification date when refreshing a current document.
- Do not infer that a document is current from its Git modification date alone.
