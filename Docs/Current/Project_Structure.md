# Project Structure

> Status: Current  
> Verified against repository contents: 2026-08-02

This file describes the repository as it exists today. Proposed reorganizations belong in a separate proposal and must not be presented here as completed structure.

## Root

- `Assets/`: Unity project content.
- `Packages/`: package manifest, lock file, and checked-in packages.
- `ProjectSettings/`: Unity project settings.
- `Docs/`: current documentation, archived records, and unapproved proposals.
- `Tools/`: local inspection and maintenance utilities.
- `bin/`: tracked legacy resources and archived experiments kept outside Unity imports.
- `AGENTS.md`: repository-level instructions for AI-assisted work.

Unity and IDE generated folders such as `Library/`, `Temp/`, `Logs/`, `obj/`, `.vs/`, `.csproj`, and `.sln` are generated output and should stay uncommitted.

## Assets

- `Assets/Create/`: authored gameplay data currently used by the project.
  - `ActionAssets/`: `ActionAsset` assets and their Timeline `.playable` assets.
  - `ActionLists/`: action lists assigned to actors.
  - `Animancer/`: Animancer transition assets referenced by actions.
  - `Graphs/`: NodeCanvas and related graph assets.
- `Assets/Prefabs/`: reusable actors, camera rigs, effects, gameplay objects, and supporting prefabs.
- `Assets/Resources/`: runtime and art content still loaded or organized through Unity Resources.
- `Assets/Scenes/`: release and targeted validation scenes.
- `Assets/Scripts/`: runtime and editor C# code.
- `Assets/Settings/`: Unity package and project asset settings.
- `Assets/Plugins/`: third-party packages stored under Assets.
- `Assets/Other/`: miscellaneous project content that has not been placed elsewhere.

`Assets/GameData/` does not currently exist. Moving `Assets/Create/` is a separate migration because it would touch many Unity assets and `.meta` references.

## Scene Entry Points

- `Assets/Scenes/MiHoYo_Release.unity`: canonical release/build scene and the only enabled BuildSettings scene.
- `Assets/Scenes/Test/Combat_Test.unity`: targeted combat/action validation.
- `Assets/Scenes/Test/KCC_Migration_Test.unity`: targeted KCC and actor-motion validation.
- `Assets/Scenes/Test/Camera_Test.unity`: targeted camera validation.
- Other scenes under `Assets/Scenes/Test/` serve their named validation domains.

There is currently no `Assets/Scenes/MiHoYo_Test.unity`. Until a new broad development scene is explicitly designated, select the targeted test scene that matches the change and use `MiHoYo_Release` for release-level checks.

## Working Rules

- Treat this document as a factual inventory, not a desired end state.
- Use plural folder names for collections when adding new folders.
- Preserve Unity `.meta` files whenever moving assets.
- Do not move `Assets/Create`, `Assets/Resources`, scenes, prefabs, or `ProjectSettings` without a dedicated reference and validation pass.
- Do not edit generated Unity or IDE output.
