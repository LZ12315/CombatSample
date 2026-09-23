# CombatSample Action V1 — Stage 4 Handoff

> Status: **Accepted; post-review Unity compilation confirmed**
> Date: 2026-09-01
> Boundary: V1 Runtime side path only; no ActionPlayer or combat-loop cutover

## Delivered

- `ActionRuntimeScheduler` now resolves a fixed-time `AnimationClip` sample from its immutable `AnimationSegment` snapshot and continuous 60 Hz position.
- Segment sampling starts at `SourceStartTime`; during an in-segment position it applies `PlayRate`; before the first segment no pose is submitted; between segments and after the final segment it holds the preceding segment's `SourceEndTime`.
- `ActionRuntime.Begin` defensively rejects malformed AnimationSegment snapshots (missing clip, invalid timing/source range/play rate, or overlap), then owns an `ActorAnimation` action override for the Runtime lifetime when an active ActorAnimation is available.
- `ActionRuntime` submits its initial pose after Frame 0 and refreshes pose on every `Advance`, including sub-frame speed, Speed 0, and Pause. It releases the animation owner on completion, interruption, abort, or Begin failure.
- `ActorAnimation` now has an internal direct-clip pose receiver. It samples the Action override layer directly and does not resolve `AnimationConfig`, TransitionAsset, Legacy animation keys, Timeline, or Root Motion data. Legacy transition submission remains separate.

## Deliberately unchanged

- `ActionPlayer`, `ActionStateManager`, `ActorSimulationRuntime`, `CombatSimulationDriver`, Legacy/Sequence playback sessions, and asset migration remain untouched.
- Stage 3 Gameplay receivers, RootMotionItem, SelfRotationItem, ActorMotor, bake data, and RootMotionTrajectory are unchanged.
- No CrossFade, loop policy, blend-tree parameters, formal V1 test asset, or developer runtime harness was added.

## Validation

| Check | Result | Evidence |
| --- | --- | --- |
| Runtime/editor source compile | Blocked locally | `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q` reached project evaluation but Unity-generated `Temp/obj/Assembly-CSharp-Editor/project.assets.json` is absent. No restore was run because generated `Temp/` output is outside this change. |
| Unity Test Runner | Not used | Project validation policy is Unity compilation plus targeted manual runtime checks. |
| Unity workstation smoke | Passed | Project owner compiled and ran the project after the initial Stage 4 implementation and confirmed the Stage 0–4 review follow-up also compiles and runs without error. |

## Next-stage boundary

Stage 5 builds the new Action authoring editor on the Stage 1–4 data/runtime contracts. Stage 6 migrates assets; formal ActionPlayer/combat-loop cutover remains Stage 7 work.
