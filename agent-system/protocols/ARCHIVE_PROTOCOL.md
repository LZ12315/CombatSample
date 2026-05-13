# Archive Protocol

## Purpose
Archives preserve completed task history.

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

Archiving is the final phase of this collaboration system.
