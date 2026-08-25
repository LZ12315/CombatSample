# Agent Instructions

## Scope

This repository is the Unity CombatSample project. Treat this repository as the project boundary and source of truth for project work.

Do not import task IDs, plans, assumptions, or facts from another workspace unless the user explicitly asks for cross-workspace work.

## Collaboration

Keep changes small, focused, and reviewable.

Do not rely on historical task systems or chat history as active project state. Use the current repository contents and the user's latest request as the working context.

At handoff, summarize the changed files, validation performed, and any remaining risks or manual checks.

## Unity Guardrails

Do not edit generated output such as `Library/`, `Temp/`, `obj/`, `.csproj`, or `.sln` files.

Avoid unrelated prefab, scene, `.meta`, `ProjectSettings`, and package changes.

Preserve serialized field names, public APIs, prefab references, and scene references unless the task explicitly requires a migration.

## Testing

Keep the default Test Runner suite small and deterministic. Test stable gameplay/data contracts, not private method names, field names, temporary class structure, or behavior already covered by compilation/static analysis.

Prefer pure EditMode contract tests. Use PlayMode, scene, prefab, importer, UI geometry, or repository-asset-dependent checks only when they protect a contract that cannot be validated more simply; otherwise record a manual validation step instead.

Reflection-based tests should be exceptional. Do not preserve obsolete production APIs only to keep an old test green.
