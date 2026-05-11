# CombatSample Agent Rules

## Project Stance
- This is a Unity combat project. Keep edits small, local, and easy to review.
- Prefer existing project patterns over new architecture.
- Do not edit generated Unity/IDE output such as `Library/`, `Temp/`, `obj/`, `.csproj`, or `.sln` files.
- Treat unrelated working tree changes as user work. Do not revert them.

## Codex Planning Mode
When the user asks Codex to write a plan for Claude, DeepSeek, another AI coding agent, or an execution model, output a compact task contract instead of a long implementation script.

Default shape:
- Goal
- Non-goals
- Files or areas to inspect first
- Architecture constraints
- Allowed edit scope
- Forbidden changes
- Expected behavior
- Acceptance criteria
- Verification steps
- Required final report format
- Known risks or questions

Planning rules:
- Prefer constraints, boundaries, and acceptance criteria over step-by-step code instructions.
- Default to 500-1000 Chinese characters unless the user asks for more detail.
- Make the contract specific enough for another model to execute without drifting.
- If the task is unclear, include the smallest useful clarification questions and a provisional safe contract.
- Do not prescribe exact code unless an interface, invariant, or migration detail must be preserved.

## Unity Guardrails
- Do not make unrelated prefab, scene, `.meta`, `ProjectSettings`, or package changes.
- Do not move input-reading logic into low-level motor, domain, or gameplay state components. Low-level components should receive intent/data from higher layers.
- Preserve existing Animator parameter names, serialized field names, public APIs, prefab references, and scene references unless the task explicitly requires changing them.
- Avoid new global singletons or service locators unless they already match the local pattern and the task clearly needs them.
- If a prefab, scene, or serialized asset must change, explain why and list the exact asset paths.
- Keep Unity serialization stable: be cautious with renames, moves, and type changes that could break inspector references.
- Prefer focused EditMode/PlayMode tests or concrete manual validation notes when automated tests are not practical.

## Final Report Expectations
For implementation work, report:
- Changed files
- Behavior changes
- Verification performed
- Remaining risks or follow-up checks
