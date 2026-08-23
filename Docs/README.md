# Documentation Index

> Last documentation authority audit: 2026-08-24 (`FrameWork`)

`Docs` is divided by document authority. Directory placement is part of document status: `Current/` is verified fact/reference, `Proposals/` is active design work, and `Archive/` is historical context. Do not treat an old plan as current just because its technical details are still useful.

## Current

These documents may be used as current repository/editor references, subject to their own verification dates.

- [Project Structure](Current/Project_Structure.md) — factual repository layout and scene entry points. Last verified 2026-08-02; refresh before relying on it for broad file-migration planning.
- [Scene Ownership Baseline](Current/Scene_Ownership_Baseline_2026-08-02.md) — current release and targeted validation scene ownership. Refresh before changing scene/build ownership.
- [ActionSequence Editor Design Spec](Current/ActionSequence_Editor_Design_Spec.md) — ActionSequence fixed-frame editor product-language baseline.
- [ActionSequence Editor V2 Architecture](Current/ActionSequence_Editor_V2_Architecture.md) — implemented UI Toolkit editor architecture.
- [ActionSequence 编辑器 V2 架构（中文）](Current/ActionSequence_Editor_V2_Architecture_zh-CN.md) — implemented V2 editor architecture, Chinese edition.

The former Actor Motion validation checklist, Stage 0–7 retrospective, and Prototype implementation audit have been moved to `Archive/` because their runtime/current-status sections predate the E1/E2/E3 architecture changes.

## Active Proposals / Architecture Work

Authority for the current E3 design work is:

1. [E3 Pre-Design Checkpoint](Proposals/CombatSample_E3_PreDesign_Checkpoint_zh-CN.md) — highest current authority for the E3 Input / ActorLocomotion / ActorMotor / Animation / MotionPolicy / HitDetection decisions. It explicitly supersedes conflicting E2-era directions.
2. [v3 Source Audit](Proposals/CombatSample_Final_v3_Source_Audit_zh-CN.md) — classifies which parts of older architecture/design documents are retained, superseded, historical, or still need verification before writing Final Architecture v3.
3. [ActionSequence Final Architecture v2](Proposals/CombatSample_ActionSequence_Final_Architecture_v2_zh-CN.md) — legacy approved baseline and implementation history. It remains an important source, but is **partially superseded by the E3 checkpoint** and must not be treated as the current final architecture where they conflict.

`Final Architecture v3` has not yet been written. Until it exists, the E3 checkpoint wins over conflicting v2 runtime responsibilities.

## Design Source Pending v3 Consolidation

- [Root Motion Final Design v1](CombatSample_RootMotion_Final_Design_v1.md) — retain its Baker approach, cumulative trajectory data model, SE(3) extraction/composition mathematics, and requested-vs-actual motion distinction as v3 source material. Its older runtime ownership/policy, Animator Root Motion compatibility, and Facing-era rules are not current authority.

## Archive

`Archive/` contains completed implementation plans, superseded runtime designs, historical audits, retrospectives, and old recommendations. They may explain why code or architecture evolved, but they are not executable/current plans.

Notable archived records now include:

- `CombatSample_ActionSequence_Stage_D5_Simplification_zh-CN.md` — completed D5 implementation plan.
- `CombatSample_ActionSequence_Stage_E1_ASM_Fixed_Tick_zh-CN.md` — completed E1 implementation record.
- `CombatSample_ActionSequence_Stage_E2_Player_Input_Locomotion_zh-CN.md` — completed E2 implementation record.
- `帧表迁移完整落地方案_历史草案.md` — explicitly unapproved historical migration draft.
- `Actor_Motion_Validation.md` — old ActorMotionRuntime/GravityAccumulator/Animator-RM behavior checklist; superseded as a current validation contract by the E3 motor design.
- `ActionSequence_Iteration_Retrospective_2026-08-07_zh-CN.md` — Stage 0–7 historical retrospective.
- `ActionSequence_Editor_Implementation_Audit_2026-08-02.md` — Prototype historical issue snapshot.
- KCC migration/refactor reports and older project recommendations already archived before this audit.

## Current Follow-ups

These are observations or documentation tasks, not automatically approved implementation work:

- Write Final Architecture v3 from the E3 checkpoint plus the retained portions classified in the v3 Source Audit.
- After v3 is approved, archive Final Architecture v2 and the E3 checkpoint as historical design inputs.
- Rebuild the Actor Motion validation checklist against the v3 Translation / Rotation / Ballistic / MotionPolicy contracts before the corresponding refactor is considered complete.
- Refresh `Project_Structure.md` and scene ownership dates when implementation planning begins if repository/scene facts are material to that plan.
- Keep editor architecture documents current only for editor/runtime-boundary changes that actually affect them; do not fold ActorMotor design into editor docs.

## Maintenance Rules

- `Current/` documents must describe verified repository facts, implemented architecture, or repeatable current validation procedures.
- `Proposals/` documents must state approval/authority status, assumptions, migration scope, and validation requirements.
- `Archive/` documents are immutable historical context except for status banners or broken-link corrections.
- Add the verification date when refreshing a Current document.
- Completed stage implementation plans belong in `Archive/`, not `Proposals/`.
- Do not infer that a document is current from its Git modification date alone.
- When architecture sources conflict during the v3 transition: **E3 Checkpoint > current verified code facts > v2/root-motion legacy design sources > Archive history**.
