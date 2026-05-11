# Status Guide

Use these statuses in task front matter.

## `todo`
The task exists and is ready to be claimed.

## `claimed`
An executor has claimed the task but may not have started edits yet.

## `in_progress`
The executor is actively working on the task.

## `review`
Execution is complete for the current round and waiting for review.

## `changes_requested`
Review found required changes. A new round should be added before more execution.

## `blocked`
The task cannot continue without user input, missing context, or a revised plan.

## `done`
The task has been accepted but not yet archived.

## `archived`
The task has been moved to `agent-tasks/archive/` and should not be selected for execution.

## Typical Flow

```text
todo -> claimed -> in_progress -> review -> done -> archived
```

If more work is needed:

```text
review -> changes_requested -> in_progress -> review
```

If the task cannot continue:

```text
in_progress -> blocked
```
