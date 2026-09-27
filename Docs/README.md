# Documentation Index

Updated 2026-09-28. Current repository code and the user's latest decisions take precedence over historical documents.

- [Current](Current/) describes the implemented system.
- [Proposals](Proposals/) has the frozen Locomotion v1 target and implementation roadmap; no active Action implementation plan.
- [Archive](Archive/README.md) preserves completed and superseded plans, stage handoffs and validation snapshots.

## Current Action path

- [Formal Action editor and playback architecture](Current/CombatSample_Action_Editor_Architecture_2026-09-19_zh-CN.md) — sole ActionAsset / ActionRuntime path and editor responsibilities.
- [Animation → Action → Actor closure](Current/CombatSample_Animation_Action_Actor_Closure_Audit_2026-09-23_zh-CN.md) — code review, compile result and user acceptance. The stage is closed; Unity Test Runner was not used as an acceptance gate.
- [Actor motion contract](Current/Actor_Motion_Validation.md) — still-applicable Translation, Rotation, Root Motion and ownership rules.
- [Scene ownership baseline](Current/Scene_Ownership_Baseline_2026-08-02.md) — scene roles recorded on 2026-08-02; check the scene itself before making changes.

## Active Locomotion path

- [Locomotion final architecture v1](Proposals/CombatSample_Locomotion_Final_Architecture_v1_zh-CN.md) — frozen Asset/Runtime target and fixed-tick ownership; stages 1–4 are implemented.
- [Locomotion implementation roadmap v1](Proposals/CombatSample_Locomotion_Implementation_Roadmap_v1_zh-CN.md) — stage 3/4 Unity compilation is user-confirmed; tests, resource coverage and character visual acceptance remain pending. Stage 5 Stop Distance Matching is next.

## Historical references

- [Action implementation roadmap](Archive/CombatSample_Action_Implementation_Roadmap_v1_zh-CN.md) — superseded. The planned bulk asset migration was replaced by user-directed reconfiguration from scratch.
- [Action editor redesign](Archive/CombatSample_Action_V1_Stage_5_Editor_Redesign_Draft_2026-09-01_zh-CN.md) and [Action V1 stage records](Archive/ActionV1/) — implementation history, not active Preview plans.
- [ActionSequence v3 architecture](Archive/CombatSample_ActionSequence_Final_Architecture_v3_zh-CN.md) and [E3 validation handoff](Archive/CombatSample_E3_v3_Validation_Handoff_2026-08-29_zh-CN.md) — historical Domain design and pre-cutover evidence; their Sequence content is no longer current.
- [HitStop boundary fixes](Archive/CombatSample_HitStop_Boundary_Fixes_2026-09-19_zh-CN.md) — implementation and prior validation record; obsolete Legacy checks do not remain as tasks.

The old ActionSequence, Legacy Timeline and AnimationConfig playback/editor paths have been removed. Locomotion implementation and Camera development are separate next routes; no old Action migration or Test Runner plan is carried forward.
