# Main Cleanup Baseline - 2026-06-09

## 1. Scope

This document records the current `main` state after the release merge.

This cleanup pass is limited to documentation and AI task-index cleanup. It does not move Unity assets, edit scenes, edit prefabs, change serialized references, or update `ProjectSettings`.

## 2. Git Baseline

Checked with:

```text
git status --short --branch
```

Current result:

```text
## main...origin/main [ahead 107, behind 3]
```

The local side contains the large Unity release merge and follow-up local commits. The remote side currently has three commits that add or update non-Unity workspace files under `00_Raw/Findings/` and `99_Templates/`.

Do not run a blind pull or merge until the owner decides whether those remote non-Unity files belong in this Unity project workspace.

## 3. Project Layout Snapshot

### Unity Content

- `Assets/Create/`: authored action assets, action lists, Animancer assets, and NodeCanvas graph assets.
- `Assets/Prefabs/`: actor, camera, manager, object, environment, and VFX prefabs.
- `Assets/Resources/`: most imported runtime/art content currently lives here.
- `Assets/Scenes/`: release, test, sample, and feature validation scenes.
- `Assets/Scripts/`: gameplay, action system, camera, combat, input, impact, NodeCanvas, Timeline, UI, and editor scripts.
- `Assets/Settings/`: input, URP, Animancer, DOTween, and TagTree settings.
- `Packages/`: Unity package manifest and a checked-in Animancer package folder.
- `ProjectSettings/`: Unity project settings.

### Collaboration And Documentation

- `agent-system/`: current AI collaboration protocols and rules.
- `agent-tasks/active/`: active or pending task records.
- `agent-tasks/archive/`: accepted or archived task history.
- `Docs/`: handoff reports and current project notes.
- `Document/plans/`: older design and migration planning notes.
- `Tool/`: local project utility scripts.

### Generated Or Local Files

Unity and IDE generated files such as `Library/`, `Logs/`, `obj/`, `.vs/`, `.csproj`, and `.sln` are ignored by `.gitignore` and should not be committed.

The root-level `Bin/` directory exists on disk and appears to contain old assets, disabled graphs, and archived scripts. It did not appear in `git status --porcelain` during this pass, so it is treated as local workspace history until the owner explicitly asks to classify or remove it.

## 4. Release Merge Hotspots

Checked with:

```text
git diff --stat origin/main..HEAD
git diff --name-status --no-renames origin/main..HEAD
```

High-level result:

- `git diff --stat origin/main..HEAD` reports `1571 files changed`.
- The no-rename grouping is dominated by `Assets/` path entries.
- `Assets/Resources/` has the largest change set.
- `Assets/Create/` has the second largest change set.
- `Assets/Scripts/`, `Assets/Prefabs/`, `Assets/Scenes/`, and `ProjectSettings/` also changed.

No-rename path-entry grouping from the scan:

| Area | Path entries |
| --- | ---: |
| `Assets/Resources/` | 1183 |
| `Assets/Create/` | 452 |
| `Assets/Scripts/` | 48 |
| `Assets/Prefabs/` | 28 |
| `Assets/Scenes/` | 8 |

Notable code introduced or changed by the release branch includes:

- `Assets/Scripts/Actor/ActorCollisionResolver.cs`
- `Assets/Scripts/Actor/SpeedModifierStack.cs`
- `Assets/Scripts/Camera/ActorCameraControl.SoftLockComposer.cs`
- `Assets/Scripts/Editor/ReleaseArenaBaker.cs`
- `Assets/Scripts/UI/CombatHudController.cs`

These should be reviewed separately from any asset-folder cleanup.

## 5. Resource Baseline

Checked with:

```text
Get-ChildItem -LiteralPath 'Assets/Resources' -File -Recurse | Measure-Object Length -Sum
Get-ChildItem -LiteralPath 'Assets' -File -Recurse | Measure-Object Length -Sum
rg -n "Resources\.Load|LoadAll\(|Addressables|AssetDatabase\.LoadAssetAtPath|AssetDatabase\.FindAssets" Assets/Scripts Assets/Other -g '*.cs'
```

Current result:

- `Assets/Resources/` contains about `2.89 GB` of files.
- `Assets/` contains about `2.95 GB` of files.
- The only direct runtime-style load found in scripts was an editor icon load:

```text
Assets/Scripts/TimelinePlayable/Editor/AnimancerPlayableEditor.cs: Texture2D trackIcon = Resources.Load<Texture2D>("Textures/AnimancerIcon");
```

This means the current `Resources` layout is probably historical and broad, not necessarily a strict runtime loading requirement. However, many scenes, prefabs, action assets, and timelines reference assets by GUID, so files must not be moved casually.

Any future asset cleanup should preserve `.meta` files and should preferably be done through Unity or after a GUID/reference audit.

## 6. Scene Baseline

Checked with:

```text
Get-ChildItem -LiteralPath 'Assets/Scenes' -File -Recurse
Get-Content -Raw -LiteralPath 'ProjectSettings/EditorBuildSettings.asset'
```

Current scene set includes:

- `Assets/Scenes/MiHoYo_Release.unity`
- `Assets/Scenes/MiHoYo_Test.unity`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Scenes/Test/Camera_Test.unity`
- `Assets/Scenes/Test/Combat_Test.unity`
- `Assets/Scenes/Test/EnemyAI_Test.unity`
- `Assets/Scenes/Test/KCC_Migration_Test.unity`
- `Assets/Scenes/Test/Photo_Test.unity`
- `Assets/Scenes/Test/VFX_Test/VFX_Test.unity`

Current build settings include only:

```text
Assets/Scenes/MiHoYo_Release.unity
```

Historical task files and `Docs/Camera_SoftLock_Handoff_Report.md` still refer to `Assets/Scenes/MiHoYo.unity`. Treat that path as old branch context unless the owner confirms otherwise.

## 7. Task Baseline

Checked with:

```text
Get-ChildItem -Name agent-tasks/active
Get-ChildItem -Path agent-tasks/active -Filter 'task-*.md'
rg -n "^status:" agent-tasks/archive/2026/task-20260523-*.md
```

Current `agent-tasks/active/` contains no task markdown files after the 2026-06-09 cleanup/archive pass. Only `README.md` remains there.

Old or wrong-direction camera records archived from active on 2026-06-09:

- `agent-tasks/archive/2026/task-20260523-lock-camera-sector-gizmos.md`
- `agent-tasks/archive/2026/task-20260523-lock-camera-sector-yaw-gate.md`
- `agent-tasks/archive/2026/task-20260523-lock-camera-stable-combat-focus.md`
- `agent-tasks/archive/2026/task-20260523-soft-lock-composition-inertia.md`

Archive cleanup performed on 2026-06-09:

- `agent-tasks/active/task-20260522-camera-follow-lock-composition.md` moved to `agent-tasks/archive/2026/` and was marked `status: archived`.
- `agent-tasks/active/task-20260524-lock-camera-sector-soft-edge.md` moved to `agent-tasks/archive/2026/` and was marked `status: archived`.
- The duplicate active copy of `task-20260515-actor-motor-timescale-motion-delta.md` was removed after confirming the archive copy contains the accepted Claude review and completion metadata.
- The four remaining camera `review` records were reviewed, marked `blocked`, then archived after the owner confirmed they represent old or wrong directions.
- No task records remain in `agent-tasks/active/`.

## 8. Recommended Cleanup Order

### Phase 1 - Documentation And Task Index

Low risk. No Unity serialization changes.

- Keep this baseline document updated while cleanup proceeds.
- Add historical-context notes to stale handoff docs.
- Create fresh task files for new work instead of reviving the archived camera sector/inertia experiments.
- Keep archived task records in `agent-tasks/archive/2026/` as the source of completed AI task history.

### Phase 2 - Scene Ownership

Medium risk. May touch scenes and build settings.

- Choose the canonical development validation scene.
- Choose the canonical release scene.
- Decide whether `MiHoYo_Test` is a temporary experiment, a dev scene, or an archive candidate.
- Update docs and build settings only after that decision.

### Phase 3 - Resource Classification

High risk because Unity GUID references can break if files move incorrectly.

- Identify which assets actually require `Resources`.
- Classify character art, animations, audio, VFX, models, and textures.
- Move assets through Unity or with a reference-preserving process.
- Verify scene, prefab, action asset, timeline, and material references after each small move.

### Phase 4 - Code Review

Medium risk. Keep separate from asset moves.

- Review release-only tooling such as `ReleaseArenaBaker`.
- Review runtime additions such as `ActorCollisionResolver`, `SpeedModifierStack`, `CombatHudController`, and SoftLock camera changes.
- Compile and test in Unity after each code cleanup slice.

## 9. Guardrails

- Do not move, rename, or delete Unity assets without preserving `.meta` files.
- Do not edit scene or prefab YAML by hand unless the change is tiny, understood, and reviewed.
- Do not change `ProjectSettings` until the target scene/build baseline is explicit.
- Do not merge `origin/main` into local `main` until the owner decides whether the remote `00_Raw` and `99_Templates` content belongs here.
- Keep cleanup commits small and grouped by concern.
