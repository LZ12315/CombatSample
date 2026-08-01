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

Prefer focused EditMode or PlayMode tests when practical. If they are not practical, record exact manual validation steps.
