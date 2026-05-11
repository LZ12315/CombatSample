# Unity Rules

This is a Unity combat project. Keep edits small, local, and easy to review.

## General
- Prefer existing project patterns over new architecture.
- Treat unrelated working tree changes as user work. Do not revert them.
- Do not edit generated Unity or IDE output such as `Library/`, `Temp/`, `obj/`, `.csproj`, or `.sln` files.
- Avoid broad refactors unless the task explicitly requires them.

## Assets And Serialization
- Do not make unrelated prefab, scene, `.meta`, `ProjectSettings`, or package changes.
- If a prefab, scene, or serialized asset must change, explain why and list the exact asset paths.
- Preserve existing Animator parameter names, serialized field names, public APIs, prefab references, and scene references unless the task explicitly requires changing them.
- Be cautious with renames, moves, and type changes that could break inspector references.

## Architecture
- Do not move input-reading logic into low-level motor, domain, or gameplay state components.
- Low-level components should receive intent or data from higher layers.
- Avoid new global singletons or service locators unless they already match local patterns and the task clearly needs them.

## Verification
- Prefer focused EditMode or PlayMode tests when practical.
- If automated tests are not practical, write concrete manual validation steps in the task report.
