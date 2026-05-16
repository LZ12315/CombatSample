#!/usr/bin/env python3
"""Small console for repository-local AI task files.

This tool intentionally uses only Python's standard library so Codex, Claude,
or another agent can run it without installing project-specific packages.

Commands:
  python Tool/agent_task.py list
  python Tool/agent_task.py next
  python Tool/agent_task.py validate agent-tasks/active/task-xxx.md
"""

from __future__ import annotations

import argparse
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

VALID_STATUSES = {
    "todo",
    "claimed",
    "in_progress",
    "review",
    "changes_requested",
    "blocked",
    "done",
    "archived",
}

REQUIRED_FIELDS = [
    "id",
    "title",
    "summary",
    "status",
    "current_round",
    "planner",
    "executor",
    "reviewer",
    "created_at",
    "updated_at",
    "claimed_at",
    "completed_at",
]

EXECUTABLE_STATUS_RANK = {
    "todo": 0,
    "changes_requested": 1,
}


@dataclass
class TaskFile:
    path: Path
    front_matter: dict[str, str]
    body: str
    has_front_matter: bool


def repo_root() -> Path:
    """Return the repository root based on this script's location."""
    return Path(__file__).resolve().parents[1]


def active_tasks_dir() -> Path:
    return repo_root() / "agent-tasks" / "active"


def read_task(path: Path) -> TaskFile:
    text = path.read_text(encoding="utf-8-sig")
    lines = text.splitlines()

    if not lines or lines[0].strip() != "---":
        return TaskFile(path=path, front_matter={}, body=text, has_front_matter=False)

    end_index = None
    for index, line in enumerate(lines[1:], start=1):
        if line.strip() == "---":
            end_index = index
            break

    if end_index is None:
        return TaskFile(path=path, front_matter={}, body=text, has_front_matter=False)

    front_matter_lines = lines[1:end_index]
    body = "\n".join(lines[end_index + 1 :])
    front_matter: dict[str, str] = {}

    for raw_line in front_matter_lines:
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        if ":" not in line:
            continue
        key, value = line.split(":", 1)
        value = value.strip()
        if (
            len(value) >= 2
            and ((value[0] == value[-1] == '"') or (value[0] == value[-1] == "'"))
        ):
            value = value[1:-1]
        front_matter[key.strip()] = value

    return TaskFile(path=path, front_matter=front_matter, body=body, has_front_matter=True)


def iter_active_tasks() -> list[TaskFile]:
    tasks_dir = active_tasks_dir()
    if not tasks_dir.exists():
        return []
    return [read_task(path) for path in sorted(tasks_dir.glob("*.md"))]


def display_value(task: TaskFile, key: str, default: str = "-") -> str:
    value = task.front_matter.get(key, "").strip()
    return value if value else default


def print_table(rows: list[dict[str, str]]) -> None:
    headers = ["STATUS", "ROUND", "EXECUTOR", "UPDATED", "ID", "TITLE"]
    keys = ["status", "round", "executor", "updated", "id", "title"]

    widths = {
        key: max(
            len(header),
            *(len(row.get(key, "")) for row in rows),
        )
        for key, header in zip(keys, headers)
    }

    header_line = "  ".join(header.ljust(widths[key]) for key, header in zip(keys, headers))
    print(header_line)
    print("  ".join("-" * widths[key] for key in keys))
    for row in rows:
        print("  ".join(row.get(key, "").ljust(widths[key]) for key in keys))


def command_list(_args: argparse.Namespace) -> int:
    tasks = iter_active_tasks()
    if not tasks:
        print("No active task files found in agent-tasks/active/.")
        return 0

    rows = []
    for task in tasks:
        rows.append(
            {
                "status": display_value(task, "status"),
                "round": display_value(task, "current_round"),
                "executor": display_value(task, "executor"),
                "updated": display_value(task, "updated_at"),
                "id": display_value(task, "id", task.path.stem),
                "title": display_value(task, "title"),
            }
        )

    print_table(rows)
    return 0


def task_sort_key(task: TaskFile) -> tuple[int, str, str]:
    status = display_value(task, "status", "")
    return (
        EXECUTABLE_STATUS_RANK.get(status, 99),
        display_value(task, "updated_at", "9999-99-99"),
        display_value(task, "id", task.path.stem),
    )


def is_executable(task: TaskFile) -> bool:
    status = display_value(task, "status", "")
    if status not in EXECUTABLE_STATUS_RANK:
        return False

    # Be conservative if an executable-looking task is already assigned.
    # The protocol says agents should not claim a task assigned to another
    # executor unless the user explicitly asks.
    executor = task.front_matter.get("executor", "").strip()
    if status == "todo" and executor:
        return False

    return True


def command_next(_args: argparse.Namespace) -> int:
    tasks = iter_active_tasks()
    executable_tasks = sorted((task for task in tasks if is_executable(task)), key=task_sort_key)

    if executable_tasks:
        task = executable_tasks[0]
        print("Next executable task:")
        print(f"- path: {task.path.relative_to(repo_root())}")
        print(f"- id: {display_value(task, 'id', task.path.stem)}")
        print(f"- status: {display_value(task, 'status')}")
        print(f"- current_round: {display_value(task, 'current_round')}")
        print(f"- executor: {display_value(task, 'executor')}")
        print(f"- updated_at: {display_value(task, 'updated_at')}")
        print(f"- title: {display_value(task, 'title')}")
        return 0

    print("No executable task found.")
    if tasks:
        counts = Counter(display_value(task, "status", "unknown") for task in tasks)
        summary = ", ".join(f"{status}={count}" for status, count in sorted(counts.items()))
        print(f"Active task status summary: {summary}")
        print("Executable statuses for this MVP: todo, changes_requested.")
    else:
        print("No active task files found in agent-tasks/active/.")
    return 0


def body_has_execution_report(task: TaskFile) -> bool:
    body = task.body
    marker_candidates = [
        "### 2. 执行报告 / Execution Report",
        "## 执行报告 / Execution Report",
        "### Execution Report",
        "## Execution Report",
    ]
    marker_index = -1
    for marker in marker_candidates:
        marker_index = body.find(marker)
        if marker_index != -1:
            break
    if marker_index == -1:
        return False

    report_text = body[marker_index:]
    review_index = report_text.find("### 3. 审查 / Review")
    if review_index != -1:
        report_text = report_text[:review_index]

    stripped = report_text.strip()
    return bool(stripped) and "未开始" not in stripped


def metadata_row_value(body: str, field: str) -> str | None:
    prefix = f"| {field} |"
    for line in body.splitlines():
        stripped = line.strip()
        if not stripped.startswith(prefix):
            continue
        cells = [cell.strip().strip("`") for cell in stripped.strip("|").split("|")]
        if len(cells) >= 2 and cells[0] == field:
            return cells[1]
    return None


def validate_task(task: TaskFile) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []

    if not task.has_front_matter:
        errors.append("missing YAML front matter delimited by ---")
        return errors, warnings

    for field in REQUIRED_FIELDS:
        if field not in task.front_matter:
            errors.append(f"missing required field: {field}")

    task_id = task.front_matter.get("id", "").strip()
    if task_id and task_id != task.path.stem:
        warnings.append(f"front matter id does not match file name: {task_id} != {task.path.stem}")

    status = task.front_matter.get("status", "").strip()
    if status and status not in VALID_STATUSES:
        errors.append(f"invalid status: {status}")
    elif not status:
        errors.append("missing value for field: status")

    current_round = task.front_matter.get("current_round", "").strip()
    if current_round:
        try:
            parsed_round = int(current_round)
            if parsed_round < 1:
                errors.append("current_round must be >= 1")
        except ValueError:
            errors.append(f"current_round must be a number: {current_round}")
    elif "current_round" in task.front_matter:
        errors.append("missing value for field: current_round")

    completed_at = task.front_matter.get("completed_at", "").strip()
    if status == "done" and not completed_at:
        errors.append("status is done but completed_at is empty")

    normalized_parts = set(task.path.as_posix().split("/"))
    if status == "archived" and "active" in normalized_parts:
        warnings.append("status is archived but file is still under agent-tasks/active/")

    executor = task.front_matter.get("executor", "").strip()
    if status == "review" and not executor:
        errors.append("status is review but executor is empty")

    if status == "review" and not body_has_execution_report(task):
        warnings.append("status is review but execution report appears empty or missing")

    for field in ["id", "status", "current_round", "planner", "executor", "reviewer", "updated_at", "completed_at"]:
        visible_value = metadata_row_value(task.body, field)
        front_value = task.front_matter.get(field, "").strip()
        if visible_value is not None and visible_value != front_value:
            warnings.append(
                f"metadata table may be out of sync for {field}: table={visible_value!r}, front matter={front_value!r}"
            )

    return errors, warnings


def resolve_task_path(raw_path: str) -> Path:
    path = Path(raw_path)
    if path.is_absolute():
        return path
    cwd_path = Path.cwd() / path
    if cwd_path.exists():
        return cwd_path
    return repo_root() / path


def command_validate(args: argparse.Namespace) -> int:
    path = resolve_task_path(args.task_file)
    if not path.exists():
        print(f"ERROR: task file does not exist: {path}")
        return 1
    if not path.is_file():
        print(f"ERROR: path is not a file: {path}")
        return 1

    task = read_task(path)
    errors, warnings = validate_task(task)

    relative_path = path
    try:
        relative_path = path.relative_to(repo_root())
    except ValueError:
        pass

    if not errors and not warnings:
        print(f"OK: {relative_path} is valid")
        return 0

    print(f"Validation result for {relative_path}:")
    if errors:
        print("ERROR:")
        for error in errors:
            print(f"- {error}")
    if warnings:
        print("WARNING:")
        for warning in warnings:
            print(f"- {warning}")

    return 1 if errors else 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Inspect and validate repository AI task files.")
    subparsers = parser.add_subparsers(dest="command", required=True)

    list_parser = subparsers.add_parser("list", help="List active task files.")
    list_parser.set_defaults(func=command_list)

    next_parser = subparsers.add_parser("next", help="Show the next executable active task.")
    next_parser.set_defaults(func=command_next)

    validate_parser = subparsers.add_parser("validate", help="Validate one task file.")
    validate_parser.add_argument("task_file", help="Path to a task markdown file.")
    validate_parser.set_defaults(func=command_validate)

    return parser


def main(argv: Iterable[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
