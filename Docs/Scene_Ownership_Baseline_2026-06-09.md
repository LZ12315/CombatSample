# Scene Ownership Baseline - 2026-06-09

## 1. Purpose

This document records scene ownership after the release merge cleanup.

It does not edit Unity scenes, prefabs, build settings, serialized references, or `.meta` files. It only defines which existing scene should be treated as the current release, development, and validation entry point.

## 2. Evidence Checked

Commands and files inspected:

```text
Get-ChildItem -LiteralPath Assets/Scenes -File -Recurse
Get-Content -Encoding UTF8 ProjectSettings/EditorBuildSettings.asset
Select-String -Path Assets/Scenes/**/*.unity.meta -Pattern "^guid:"
Get-Content -Encoding UTF8 Assets/Scripts/Editor/ReleaseArenaBaker.cs
rg -n "MiHoYo_Release|MiHoYo_Test|SampleScene|Camera_Test|Combat_Test|EnemyAI_Test|KCC_Migration_Test|Photo_Test|VFX_Test|Assets/Scenes/MiHoYo\.unity" Docs Document agent-tasks Assets
```

Key evidence:

- `ProjectSettings/EditorBuildSettings.asset` includes only `Assets/Scenes/MiHoYo_Release.unity`.
- `Assets/Scenes/MiHoYo_Release.unity.meta` guid is `b62b50ce1ab55d74b92d45282849a02b`, matching BuildSettings.
- `Assets/Scripts/Editor/ReleaseArenaBaker.cs` uses `Assets/Scenes/MiHoYo_Release.unity` as `DefaultScenePath`.
- Historical task and handoff records still mention `Assets/Scenes/MiHoYo.unity`, but that scene path is not present in the current scene inventory.

## 3. Scene Inventory And Ownership

| Scene | Current role | Build settings | Ownership decision |
| --- | --- | --- | --- |
| `Assets/Scenes/MiHoYo_Release.unity` | Release scene | Included and enabled | Canonical release/build scene. Keep as the only BuildSettings scene for now. |
| `Assets/Scenes/MiHoYo_Test.unity` | Development validation scene | Not included | Canonical broad gameplay validation scene for main cleanup and future feature checks. |
| `Assets/Scenes/Test/Camera_Test.unity` | Camera validation scene | Not included | Keep for targeted camera experiments and camera regressions. |
| `Assets/Scenes/Test/Combat_Test.unity` | Combat validation scene | Not included | Keep for targeted combat/action validation. |
| `Assets/Scenes/Test/EnemyAI_Test.unity` | Enemy AI validation scene | Not included | Keep for enemy targeting and AI validation. |
| `Assets/Scenes/Test/KCC_Migration_Test.unity` | KCC/movement validation scene | Not included | Keep for KCC and actor movement validation. |
| `Assets/Scenes/Test/Photo_Test.unity` | Visual/photo validation scene | Not included | Keep as a visual validation scene until the owner says it is obsolete. |
| `Assets/Scenes/Test/VFX_Test/VFX_Test.unity` | VFX validation scene | Not included | Keep with its local lighting/probe assets. |
| `Assets/Scenes/SampleScene.unity` | Legacy/sample scene | Not included | Archive candidate, but do not delete or move until references are checked in Unity. |

## 4. Working Rules

- New gameplay, camera, combat, or UI work should state which scene it was verified in.
- Default broad validation should use `Assets/Scenes/MiHoYo_Test.unity` unless a more specific `Assets/Scenes/Test/` scene is clearly better.
- Release validation should use `Assets/Scenes/MiHoYo_Release.unity`.
- Do not recreate `Assets/Scenes/MiHoYo.unity`; treat it as old branch context.
- Do not change BuildSettings without a separate owner-confirmed task.
- Do not hand-edit scene YAML except for a tiny, reviewed metadata-only change. Use Unity for real scene edits.
- `ReleaseArenaBaker` owns generated objects under `BossArena_RuinedSanctum_Environment`; it should not be used as a general scene cleanup tool.

## 5. Cleanup Implications

- Scene ownership is now clear enough to continue code review and validation planning.
- No scene files need to be moved or renamed in this pass.
- `SampleScene.unity` should be revisited later as a low-priority archive candidate after a Unity reference check.
- Asset cleanup should still wait until scene/prefab/action asset references are audited.
