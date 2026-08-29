# Documentation Index

> Last documentation authority audit: 2026-08-29 (`FrameWork`)

`Docs` is divided by document authority. Directory placement is part of document status: `Current/` is verified fact/reference, `Proposals/` contains approved architecture baselines and active implementation/design work that is not yet fully reflected by runtime code, and `Archive/` is historical context. Do not treat an old plan as current just because its technical details are still useful.

## Architecture Authority

The current CombatSample E3 architecture authority is:

1. [Final Architecture v3](Proposals/CombatSample_ActionSequence_Final_Architecture_v3_zh-CN.md) — **Approved Architecture Baseline**. This is the highest authority for E3 domain boundaries, ownership, arbitration, Root Motion / Rotation, ActorLocomotion, ActorAnimation, ActorMotor, Hit ordering, Combat simulation time, and the Driver 7-Phase Model.
2. Current verified code facts — describe what the branch actually implements today. Where current code conflicts with approved v3 architecture, treat the difference as an **Implementation Gap**, not as an automatic reason to change v3.
3. `Archive/` — historical design inputs and implementation records only. Historical documents may explain why a rule exists, but they do not override v3.

Reopening a frozen v3 rule requires an explicit Architecture Review; implementation convenience alone is not sufficient.

## Current

These documents may be used as current repository/editor references, subject to their own verification dates.

- [Project Structure](Current/Project_Structure.md) — factual repository layout, active Sequence content, animation/locomotion profiles and Actor runtime structure. Last verified 2026-08-29.
- [Scene Ownership Baseline](Current/Scene_Ownership_Baseline_2026-08-02.md) — current release and targeted validation scene ownership. Refresh before changing scene/build ownership.
- [Actor Motion v3 Validation](Current/Actor_Motion_Validation.md) — current Translation, Rotation, Root Motion, time-domain and lifecycle validation contract.
- [E3 v3 Validation Handoff](Current/CombatSample_E3_v3_Validation_Handoff_2026-08-29_zh-CN.md) — completed E3 static, asset and differential acceptance evidence.
- [ActionSequence Editor Design Spec](Current/ActionSequence_Editor_Design_Spec.md) — ActionSequence fixed-frame editor product-language baseline.
- [ActionSequence Editor V2 Architecture](Current/ActionSequence_Editor_V2_Architecture.md) — implemented UI Toolkit editor architecture.
- [ActionSequence 编辑器 V2 架构（中文）](Current/ActionSequence_Editor_V2_Architecture_zh-CN.md) — implemented V2 editor architecture, Chinese edition.

The former Actor Motion validation checklist, Stage 0–7 retrospective, and Prototype implementation audit remain in `Archive/` because their runtime/current-status sections predate the E1/E2/E3 architecture changes.

## Architecture Authority / Completed Migration

- [Final Architecture v3](Proposals/CombatSample_ActionSequence_Final_Architecture_v3_zh-CN.md) — approved and implemented long-term architecture baseline. Frozen Domain, authority, arbitration and phase rules remain the highest architecture authority.
- [E3 Architecture Migration Roadmap](Archive/CombatSample_E3_Architecture_Migration_Roadmap_zh-CN.md) — completed E3-A～H migration record and Exit Criteria evidence; archived on 2026-08-29.

The archived Roadmap remains subordinate to Final Architecture v3 and is no longer an active implementation plan.

## Archive

`Archive/` contains completed implementation plans, superseded runtime designs, historical audits, retrospectives, and design inputs already consolidated into newer authority documents. They may explain why code or architecture evolved, but they are not executable/current plans.

The v3 consolidation inputs are now historical and archived:

- [E3 Architecture Migration Roadmap](Archive/CombatSample_E3_Architecture_Migration_Roadmap_zh-CN.md) — completed E3-A～H stage record and final evidence.
- [E3 Pre-Design Checkpoint](Archive/CombatSample_E3_PreDesign_Checkpoint_zh-CN.md) — decision checkpoint that v3 consolidated and superseded as top-level authority.
- [v3 Source Audit](Archive/CombatSample_Final_v3_Source_Audit_zh-CN.md) — record of which older material was retained or rejected while authoring v3.
- [ActionSequence Final Architecture v2](Archive/CombatSample_ActionSequence_Final_Architecture_v2_zh-CN.md) — previous approved baseline and implementation history; superseded by v3.
- [Root Motion Final Design v1](Archive/CombatSample_RootMotion_Final_Design_v1.md) — historical Root Motion design source; its retained bake/data principles were consolidated into v3, while its older runtime ownership rules are superseded.

Other notable archived records include:

- `CombatSample_ActionSequence_Stage_D5_Simplification_zh-CN.md` — completed D5 implementation plan.
- `CombatSample_ActionSequence_Stage_E1_ASM_Fixed_Tick_zh-CN.md` — completed E1 implementation record.
- `CombatSample_ActionSequence_Stage_E2_Player_Input_Locomotion_zh-CN.md` — completed E2 implementation record.
- `帧表迁移完整落地方案_历史草案.md` — explicitly unapproved historical migration draft.
- `Actor_Motion_Validation.md` — old ActorMotionRuntime/GravityAccumulator/Animator-RM behavior checklist; superseded as a current validation contract by the v3 motor design.
- `ActionSequence_Iteration_Retrospective_2026-08-07_zh-CN.md` — Stage 0–7 historical retrospective.
- `ActionSequence_Editor_Implementation_Audit_2026-08-02.md` — Prototype historical issue snapshot.
- KCC migration/refactor reports and older project recommendations already archived before this audit.

## Current Follow-ups

These are maintenance follow-ups, not open E3 architecture work:

- Use Final Architecture v3 and the Current validation documents when extending Actor, ActionSequence, Motion or Hit behavior.
- Refresh `Project_Structure.md` and scene ownership dates only when repository or build ownership facts change.
- Keep editor architecture documents current only for editor/runtime-boundary changes that actually affect them; E3-H did not change the editor model.
- Treat inactive Legacy Timeline content as compatibility-only unless a future scope explicitly activates and migrates it.

## Maintenance Rules

- `Current/` documents must describe verified repository facts, implemented architecture, or repeatable current validation procedures.
- `Proposals/` may contain approved architecture baselines and active implementation/design work that is not yet fully implemented; every such document must state its approval/authority status, assumptions, migration scope, and validation requirements.
- `Archive/` documents are immutable historical context except for status banners or broken-link corrections.
- Add the verification date when refreshing a Current document.
- Completed stage implementation plans belong in `Archive/`, not `Proposals/`.
- Do not infer that a document is current from its Git modification date alone.
- When architecture and implementation differ: **Final Architecture v3 > current verified implementation facts as a description of present code > Archive history**. Code differences are regressions or new implementation gaps unless v3 is explicitly reopened through Architecture Review.
