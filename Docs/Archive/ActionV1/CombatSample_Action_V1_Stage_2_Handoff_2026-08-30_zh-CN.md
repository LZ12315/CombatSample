# CombatSample Action V1 — Stage 2 Handoff

> Status: **Implementation and Unity workstation acceptance confirmed**  
> Date: 2026-08-30  
> Prerequisite: Stage 1 Unity workstation acceptance confirmed  
> Boundary: Runtime-core side path only; no production combat-path integration

## Delivered

- Added `ActionRuntime`, `ActionRuntimeContext`, `ActionRuntimeScheduler`, point/range runtime interfaces, range exit reason, and the three terminal results.
- `Begin` takes a single-execution 60 Hz timeline snapshot: fixed duration, animation timing records, and unmuted point/range timing records. Later authoring timing, mute, and animation timing edits do not affect that runtime.
- Frame order is fixed: Begin opens Frame 0; Point Execute → new Range Enter → active Range Tick; Finish exits at the half-open end. The final frame must finish before Completed.
- Implemented closed `[0, 1]` speed, Pause/Resume, continuous action position, Action-phase-boundary Interrupt, and any-time Abort. Abort calls only `Abort`, never normal `Exit`.
- V1 item runtime factory hooks default to null. Stage 2 test fixtures use fake item/runtimes; no real gameplay, animation, or pose work is executed.
- Runtime acquires/releases Action `SelfTags` without changing `ActionInstance` or Legacy self-tag behavior.

## Deliberately not done

- No changes to `ActionPlayer`, `ActionStateManager`, `ActorSimulationRuntime`, Combat phase, or current session paths.
- No real V1 Item runtime, HitBox/Motion/Tag receiver integration, or animation pose submission.
- No Action/Animation asset migration and no change to official Legacy/Sequence behavior.

## Automated validation

| Check | Result | Evidence |
| --- | --- | --- |
| Runtime plus every added EditMode test source compiles | Passed | `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`, using a temporary MSBuild include scoped only to the Editor project; 0 errors (existing project/package warnings remain). |
| Production source compiles | Passed | Normal `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`; pre-existing project warnings only, 0 errors. |
| Unity workstation compile and runtime smoke check | Passed | Project owner compiled and ran the project on 2026-08-30; no issue observed. |

`ActionRuntimeSchedulerTests` covers empty timeline, Point/Range lifecycle and local frame, final frame, muted duration, speed 0.5, Pause/Resume, snapshot immutability, Interrupt/Abort, lazy future range creation, same-tick replacement, and invalid speed.

## Validation policy

The project owner does not use Unity Test Runner as an acceptance gate. Future stages use successful Unity compilation plus targeted manual scene/runtime checks; test source remains a code-level contract reference, not a required runner workflow.

## Next-stage boundary

Stage 3 provides real runtimes for the seven items and integrates them with existing E3 receivers/domains. Scheduler remains polymorphic and does not gain an item-type switch, arbitration, or Player control.
