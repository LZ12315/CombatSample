# Archive Protocol

## Purpose
Archives preserve how the project learned, not just what changed.

## When To Archive
Archive a task after:
- `status: done`
- The user or reviewer accepts the result
- The final report and review are present

## How To Archive
Move the task from:

```text
agent-tasks/active/
```

to:

```text
agent-tasks/archive/YYYY/
```

After moving, set:

```yaml
status: archived
```

Keep all rounds intact. Do not clean up failed plans, rejected reports, or review criticism.

## Lessons
When a completed task contains reusable project knowledge, add a concise lesson to:

```text
agent-tasks/lessons/
```

Lessons should be short and actionable. Link back to the archived task when possible.

Archive is evidence. Lessons are distilled project memory.
