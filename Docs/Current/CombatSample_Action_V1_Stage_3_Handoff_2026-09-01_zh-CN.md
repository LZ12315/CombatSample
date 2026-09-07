# CombatSample Action V1 — Stage 3 Handoff

> Status: **Implementation and Unity workstation acceptance confirmed**
> Date: 2026-09-01
> Boundary: V1 Runtime side path only; no ActionPlayer or combat-loop cutover

## Delivered

- Added real polymorphic runtimes for `TagItem`, `MotionPolicyItem`, `ImpulseItem`, `VelocityOverrideItem`, `HitBoxItem`, `RootMotionItem`, and `SelfRotationItem`.
- Each range runtime owns and releases only its own tag, motion owner, velocity owner, rotation owner, trajectory owner, or hitbox handle. All active items submit independently; existing E3 receiver/domain composition remains authoritative.
- `RootMotionItem` and root-motion `SelfRotationItem` read validated `AnimationAsset.RootMotionData` using 60 Hz local-frame source windows. They do not use `AnimationConfig` or perform runtime baking.
- `ActionRuntimeContext` now supplies the existing internal HitBox receiver in addition to Actor, ActionContext, and Motor. It still does not expose Scheduler, Player, StateManager, or other item runtimes.
- `ActionRuntime.Begin` preflights required V1 ActionContext fields for unmuted items before Frame 0.
- Authoring validation now rejects V1 `ImpulseItem.overrideGravityScale` and verifies the complete RootMotion/SelfRotation source window stays inside its clip.

## Deliberately unchanged

- `ActionPlayer`, `ActionStateManager`, `ActorSimulationRuntime`, Combat phase ordering, Legacy/Sequence sessions, existing Action assets, and asset migration are untouched.
- No developer harness and no formal V1 production path were added. Stage 4 remains responsible for animation pose submission.

## Validation

| Check | Result | Evidence |
| --- | --- | --- |
| Runtime/editor source compile | Passed | `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`, with a temporary build-only include for the newly added runtime file; no Stage 3 compile errors. |
| Unity Test Runner | Not used | Project validation policy is Unity compilation plus targeted manual runtime checks. |

## Acceptance update

The project owner compiled and ran the Unity project on 2026-09-01 with no issue observed. Stage 3 remains on the side path; the acceptance does not constitute an ActionPlayer or combat-loop cutover.

## Next-stage boundary

Stage 4 consumes Scheduler animation timing records and submits pose sampling through `ActorAnimation`; pose remains separate from the gameplay root-motion path introduced here.
