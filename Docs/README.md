# Documentation Index

> Last documentation authority audit: 2026-08-30 (`FrameWork`)

`Docs` is divided by document authority:

- `Current/` — verified repository facts, implemented editor/runtime behavior, repeatable current validation.
- `Proposals/` — approved architecture targets and active implementation work that are not yet fully reflected by runtime code.
- `Archive/` — historical context, completed plans, superseded designs and migration records.

Directory placement is part of document status. Do not treat an old plan as current just because some technical details remain useful.

## Architecture Authority During Action V1 Migration

The Action V1 migration uses a **split authority model** until Stage 7 cutover is complete:

1. [Action Final Architecture v1](Proposals/CombatSample_Action_Final_Architecture_v1_zh-CN.md) — **Approved / Frozen Design Target** for the future Action authoring and playback architecture: `ActionAsset` inline Timeline, `ActionRuntime`, `ActionRuntimeScheduler`, `AnimationAsset`, GameplayLane / GameplayItem, editor workflow, migration target and Legacy retirement rules.
2. [Action Implementation Roadmap v1](Proposals/CombatSample_Action_Implementation_Roadmap_v1_zh-CN.md) — **Approved Implementation Roadmap** for moving the current `FrameWork` baseline to Action Final Architecture v1. It defines Stage 0–7 scope, validation and Exit Criteria; it may not override the frozen architecture.
3. [ActionSequence Final Architecture v3](Proposals/CombatSample_ActionSequence_Final_Architecture_v3_zh-CN.md) — **current implemented E3 Runtime / Domain baseline**. It remains authoritative for already validated `CombatSimulationDriver` phase order, ActorAnimation / ActorMotor ownership, Translation / Rotation / MotionPolicy domains, Hit ordering, fixed combat time and related E3 runtime semantics until an explicit migration stage replaces a specific Action authoring / playback path.
4. Current verified code facts — describe what the branch actually implements today. Differences from approved targets are implementation gaps, not automatic architecture changes.
5. `Archive/` — historical input only.

`Action Final Architecture v1` is a new Action architecture version line; its `v1` does not mean it is older than `ActionSequence Final Architecture v3`.

If the new Action v1 target conflicts with v3 specifically on Action authoring / playback data structures, Action v1 is the migration target. If the question concerns E3 Domain authority / phase semantics that Action v1 explicitly preserves, the validated v3 rule remains authoritative unless separately reopened through Architecture Review.

## Current

These documents describe verified current repository/editor state and remain current until the Action V1 migration reaches the corresponding cutover stage.

- [Project Structure](Current/Project_Structure.md) — factual repository layout, active Sequence content, animation/locomotion profiles and Actor runtime structure. Last verified 2026-08-29.
- [Scene Ownership Baseline](Current/Scene_Ownership_Baseline_2026-08-02.md) — current release and targeted validation scene ownership.
- [Actor Motion v3 Validation](Current/Actor_Motion_Validation.md) — current Translation, Rotation, Root Motion, time-domain and lifecycle validation contract.
- [E3 v3 Validation Handoff](Current/CombatSample_E3_v3_Validation_Handoff_2026-08-29_zh-CN.md) — completed E3 static, asset and differential acceptance evidence.
- [ActionSequence Editor Design Spec](Current/ActionSequence_Editor_Design_Spec.md) — current implemented ActionSequence fixed-frame editor product-language baseline. This becomes historical when Stage 7 replaces the old editor.
- [ActionSequence Editor V2 Architecture](Current/ActionSequence_Editor_V2_Architecture.md) — current implemented UI Toolkit editor architecture. This becomes historical when Stage 7 replaces the old editor.
- [ActionSequence 编辑器 V2 架构（中文）](Current/ActionSequence_Editor_V2_Architecture_zh-CN.md) — current implemented V2 editor architecture, Chinese edition. This becomes historical when Stage 7 replaces the old editor.

## Proposals / Approved Migration Work

- [Action Final Architecture v1](Proposals/CombatSample_Action_Final_Architecture_v1_zh-CN.md) — frozen target for the new Action authoring / playback architecture.
- [Action Implementation Roadmap v1](Proposals/CombatSample_Action_Implementation_Roadmap_v1_zh-CN.md) — active Stage 0–7 migration plan and acceptance criteria.
- [ActionSequence Final Architecture v3](Proposals/CombatSample_ActionSequence_Final_Architecture_v3_zh-CN.md) — approved and implemented E3 baseline whose Domain / authority / phase rules remain in force during migration where explicitly preserved.

Implementation convenience alone is not sufficient to reopen a frozen Action v1 rule or an E3 v3 Domain rule.

## Completed Migration / Archive References

- [E3 Architecture Migration Roadmap](Archive/CombatSample_E3_Architecture_Migration_Roadmap_zh-CN.md) — completed E3-A～H migration record and Exit Criteria evidence; archived on 2026-08-29.
- [E3 Pre-Design Checkpoint](Archive/CombatSample_E3_PreDesign_Checkpoint_zh-CN.md) — historical decision checkpoint consolidated by v3.
- [v3 Source Audit](Archive/CombatSample_Final_v3_Source_Audit_zh-CN.md) — historical consolidation record.
- [ActionSequence Final Architecture v2](Archive/CombatSample_ActionSequence_Final_Architecture_v2_zh-CN.md) — previous baseline superseded by v3.
- [Root Motion Final Design v1](Archive/CombatSample_RootMotion_Final_Design_v1.md) — historical Root Motion design source; retained bake/data principles were consolidated into later architecture.

Other notable archived records include completed D5 / E1 / E2 implementation plans, the historical frame-table migration draft, older Actor Motion validation, Stage 0–7 retrospective, prototype editor audit, KCC migration/refactor reports and older project recommendations.

## Action V1 Migration Maintenance Rules

- New Action authoring / playback work must follow Action Final Architecture v1 and the active Roadmap.
- Do not add new dependencies on Legacy Sequence / typed Track / Clip / PlaybackBackend / ActionMotionConfig / Action-side `animationKey` paths unless a migration stage explicitly requires temporary read access.
- Migration-time coexistence is a temporary construction state, not a long-term Runtime compatibility architecture.
- Before Stage 7, current Sequence editor documents remain factual references for the currently implemented editor; after Stage 7 they must move to `Archive/`.
- Stage 7 must update this index again so the completed Action v1 implementation becomes the current Action authoring / playback authority and the completed Roadmap is archived or marked completed.
- Locomotion Animation remains outside the Action V1 scope; existing Locomotion `AnimationConfig` usage may remain until its separate redesign.

## General Maintenance Rules

- `Current/` documents must describe verified repository facts, implemented architecture, or repeatable current validation procedures.
- `Proposals/` may contain approved architecture targets and active implementation/design work not yet fully implemented; such documents must state status, assumptions, migration scope and validation requirements.
- `Archive/` documents are historical context and should not override approved Current / Proposal authority.
- Add or refresh verification dates when current repository facts change.
- Completed stage implementation plans belong in `Archive/`, not as active Roadmaps.
- Do not infer document authority from Git modification date alone.
- When architecture and implementation differ, record the difference as an implementation gap unless an explicit Architecture Review changes the approved rule.
