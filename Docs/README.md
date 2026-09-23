# Documentation Index

Verified against the repository on 2026-09-24. Current code and the user's latest decisions take precedence over older plans.

- [Current](Current/) contains descriptions of the implemented system and current validation records.
- [Proposals](Proposals/) contains design history and future work; the old Action roadmap is superseded.
- [Archive](Archive/) contains retired ActionSequence and Timeline documents, Action V1 stage records, and historical audits.

## Action and animation

- [Formal Action editor and playback architecture](Current/CombatSample_Action_Editor_Architecture_2026-09-19_zh-CN.md) describes the sole ActionAsset / ActionRuntime path, editor ownership, Preview boundary and code layout.
- [Animation → Action → Actor closure audit](Current/CombatSample_Animation_Action_Actor_Closure_Audit_2026-09-23_zh-CN.md) records the code review, current validation status and remaining manual checks.
- [Actor motion validation](Current/Actor_Motion_Validation.md) records the motion-domain and ownership contract.
- [HitStop boundary fixes](Current/CombatSample_HitStop_Boundary_Fixes_2026-09-19_zh-CN.md) records the current fixed-tick effect rules.

## Project context

- [Scene ownership baseline](Current/Scene_Ownership_Baseline_2026-08-02.md).
- [E3 v3 validation handoff](Current/CombatSample_E3_v3_Validation_Handoff_2026-08-29_zh-CN.md).

## Historical Action records

- [Action V1 stage and editor checkpoints](Archive/ActionV1/) document the route by which the current system was built. Their placeholder Preview, old validator and side-path Runtime statements no longer describe current code.
- [ActionSequence editor design](Archive/ActionSequence_Editor_Design_Spec.md) and [V2 architecture](Archive/ActionSequence_Editor_V2_Architecture_zh-CN.md) describe a deleted editor.
- [Old project structure](Archive/Project_Structure.md) and [editor formalization inventory](Archive/CombatSample_Action_Editor_Formalization_Inventory_2026-09-19_zh-CN.md) are snapshots.
- [Action final architecture proposal](Proposals/CombatSample_Action_Final_Architecture_v1_zh-CN.md) is design history, not a substitute for the formal current architecture.

The ActionSequence, Legacy Timeline and AnimationConfig playback/editor paths have been removed. Locomotion animation presentation remains a separate follow-up; do not revive the old AnimationConfig lookup to fill that gap.
