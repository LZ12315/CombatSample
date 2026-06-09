# AI Device Setup

This document covers the normal per-device setup for AI tooling in this repository.

## What Lives In Git

These files are part of the project and should sync through Git:

- `backlog/`
- `backlog.config.yml`
- `AGENTS.md`
- `CLAUDE.md`

These define the shared workflow and task data.

## What Does Not Live In Git

These are local machine concerns and should be configured per device:

- Node.js
- `backlog.md` CLI
- Codex MCP registration for `backlog`
- Claude MCP registration for `backlog`
- Codex `grill-me` skill
- Any optional Claude-local skills

## 1. Install Backlog Prerequisites

Install Node.js LTS on the device first.

Then install the Backlog CLI:

```powershell
npm install -g backlog.md
```

Verify:

```powershell
backlog --version
```

## 2. Codex Setup

Register the Backlog MCP server for Codex:

```powershell
codex mcp add backlog -- "%APPDATA%\\npm\\backlog.cmd" mcp start
```

Verify:

```powershell
codex mcp list
```

Install the `grill-me` skill for Codex:

```powershell
python "%USERPROFILE%\\.codex\\skills\\.system\\skill-installer\\scripts\\install-skill-from-github.py" --repo mattpocock/skills --path skills/productivity/grill-me
```

After installing a Codex skill, restart Codex so it is discovered.

## 3. Claude Setup

Register the Backlog MCP server for Claude in this project:

```powershell
claude mcp add --scope project backlog -- "%APPDATA%\\npm\\backlog.cmd" mcp start
```

Verify:

```powershell
claude mcp list
```

This project does not commit Claude-local skills by default. If a device needs Claude-side `grill-me`, install it locally for that Claude environment instead of storing it in the repository.

## 4. Usage Notes

- Backlog tasks should be the source of truth for active work.
- `Review` is the handoff state before `Done`.
- `agent-system/` and `agent-tasks/archive/` remain as legacy historical records only.

## 5. After Cloning On A New Device

Minimal checklist:

1. Install Node.js LTS.
2. Install `backlog.md` globally.
3. Configure Codex MCP for `backlog`.
4. Install Codex `grill-me` if that device should use it.
5. Configure Claude MCP for `backlog` if Claude will be used on that device.
