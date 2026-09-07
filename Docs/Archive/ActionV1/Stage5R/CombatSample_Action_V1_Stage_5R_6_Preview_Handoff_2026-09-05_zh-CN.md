# CombatSample Action V1 — Stage 5R.6 Preview Handoff

> **2026-09-08 归档说明：本实现已从当前代码路径撤下，未通过完整验收，不是当前 Preview 方案。**

> Status: incomplete / superseded implementation direction. Compilation passed previously; development closure and Unity acceptance have not passed.
>
> Date: 2026-09-05

## 2026-09-06 status correction and replacement plan

The implementation notes below are historical claims, not proof of completed acceptance.
Review found unresolved sampling-root, dependency-query, conflict-diagnostic, input-lifecycle
and baseline-restoration gaps. The user also reported severe editor slowdown. No Profiler
capture has yet established the dominant cost, and no measured performance pass is recorded.
The earlier claim that only Unity acceptance remained was too broad.

The approved replacement direction is SceneView + isolated Preview Scene + independent
AnimancerGraph. See the [replacement implementation plan](CombatSample_Action_V1_Stage_5R_6_SceneView_Animancer_Preview_Plan_2026-09-06_zh-CN.md).
Its first implementation batch is P0/P1 (source/lifecycle audit and single-Clip foundation).
RootMotion, SelfRotation and HitBox integration follows only after the foundation gate;
Stage 5R.7 remains deferred. This documentation update does not implement the replacement.

## Implemented

### 2026-09-06 correctness and lifecycle closure

- Preview motion ownership is now evaluated as ordered half-open intervals. When
  ranges overlap, an earlier item owns only the shared interval; a later item still
  contributes its remaining interval after that boundary. The diagnostic identifies
  the contributing interval instead of implying that the complete later item was
  disabled.
- Root-motion translation is evaluated in immutable baseline space. Root-motion
  SelfRotation now uses the shared `RootMotionYawUtility` yaw contract.
- Prepared timeline records, ownership intervals, authoring validation and bake
  status are cached across frame/input scrubbing. Camera-only redraws do not run
  evaluation, and project-change handling rebuilds the clone only when the selected
  source dependency hash changed.
- The isolated clone restores active state, transforms, renderer state, skinned local
  bounds and BlendShape state before each evaluation. Animator/Bone lookup now prefers
  the selected source Actor's Animancer binding and reports ambiguous fallback rigs.
- Invalid HitBox shapes are skipped with diagnostics rather than silently clamped;
  root-motion paths are sampled with a bounded, endpoint-preserving representation.

- The Preview window now constructs an isolated renderer and animation clone from a
  whitelist of Transform, MeshFilter, MeshRenderer, SkinnedMeshRenderer and Animator.
  It never instantiates the source GameObject or its gameplay behaviours.
- Evaluation restores transform and BlendShape baselines, then samples Animation Pose,
  RootMotion, SelfRotation and active HitBox gizmos in the frozen order.
- RootMotion and RootMotion-based rotation use `AnimationAssetBakeWorkflow.GetStatus`.
  Missing, stale, invalid and out-of-clip source windows are skipped with diagnostics.
- Target and Direction handles appear only when an active SelfRotation item needs them.
  Diagnostics contain severity and authoring location, and can select or locate content.
- Preview camera orbit, pan, distance, diagnostics state and clone lifecycle are
  session-only. Preview Character and Preview Loop notifications no longer rebuild
  Timeline or discard an unapplied Details timing draft.

## Development evidence

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`: passed with
  0 warnings and 0 errors.
- 2026-09-06 closure compile: passed with 0 warnings and 0 errors.
- Scoped whitespace check for the changed Preview, Editor Core, Timeline and stylesheet
  sources: passed.
- Unity Test Runner is not an acceptance gate for this stage.

## Unity manual acceptance

Verify prefab and scene-character sources remain unchanged; test animation gaps/holds,
trim and play rate, root-motion path, all SelfRotation input modes, overlap diagnostics,
HitBox bone failure and muted exclusion. Confirm preview handles only appear when needed,
diagnostic rows locate their authoring content, and closing or reloading the window leaves
no Console exceptions or preview clone residue. Stage 5R.7 begins only after acceptance.
