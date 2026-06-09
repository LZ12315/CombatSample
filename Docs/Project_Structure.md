# Project Structure

This file describes the intended repository layout after the 2026-06-09 main cleanup.

## Root

- `Assets/`: Unity project content.
- `Packages/`: Unity package manifest and checked-in packages.
- `ProjectSettings/`: Unity project settings.
- `Docs/`: current project notes, handoff reports, and planning documents.
- `Tools/`: local utility scripts for project inspection or maintenance.
- `agent-system/`: AI collaboration rules and protocols.
- `agent-tasks/`: active and archived AI task records.
- `bin/`: tracked old resources and archived experiments kept outside Unity imports.

Unity and IDE generated folders such as `Library/`, `Temp/`, `Logs/`, `obj/`, `.vs/`, `.csproj`, and `.sln` are local/generated and should stay uncommitted.

## Assets

- `Assets/GameData/`: authored gameplay data.
  - `ActionAssets/`: ActionAsset assets and related Timeline `.playable` assets.
  - `ActionLists/`: action lists assigned to actors.
  - `Animancer/`: Animancer transition assets referenced by actions.
  - `NodeCanvasGraphs/`: AI and behavior graph assets.
- `Assets/Prefabs/`: reusable scene objects, actors, camera rigs, VFX, and environment prefabs.
- `Assets/Resources/`: imported runtime/art content that has not yet been migrated out of Resources.
- `Assets/Scenes/`: release, broad test, and targeted validation scenes.
- `Assets/Scripts/`: runtime and editor C# code.
- `Assets/Settings/`: Unity package and project asset settings.

## Scene Entry Points

- `Assets/Scenes/MiHoYo_Release.unity`: canonical release/build scene.
- `Assets/Scenes/MiHoYo_Test.unity`: broad development validation scene.
- `Assets/Scenes/Test/`: targeted validation scenes.

## Naming Rules

- Use descriptive content folders rather than tool-menu names. For example, authored data belongs under `Assets/GameData`, not `Assets/Create`.
- Use plural folder names for collections: `ActionAssets`, `ActionLists`, `NodeCanvasGraphs`.
- Preserve Unity `.meta` files whenever moving assets.
- Do not move `Assets/Resources`, scenes, prefabs, or `ProjectSettings` without a dedicated validation pass.
