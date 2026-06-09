---
id: task-20260523-lock-camera-sector-gizmos
title: Lock Camera Sector Gizmos
summary: Add Scene-view Gizmos for the enemy-centered yaw sector so the sector gate can be inspected before further tuning.
status: archived
current_round: 1
planner: Codex
executor: Codex
reviewer: Codex
created_at: 2026-05-23
updated_at: 2026-06-09
claimed_at: 2026-05-23
completed_at:
---

# 任务：锁定相机扇形 Gizmos 诊断

## 0. 任务属性 / Task Metadata

| 属性 / Field | 值 / Value |
| --- | --- |
| id | `task-20260523-lock-camera-sector-gizmos` |
| status | `archived` |
| current_round | `1` |
| planner | `Codex` |
| executor | `Codex` |
| reviewer | `Codex` |
| created_at | `2026-05-23` |
| updated_at | `2026-06-09` |
| claimed_at | `2026-05-23` |
| completed_at |  |

---

## 第 1 轮 / Round 1

### 1. 计划 / Plan

Agent: Codex
Role: Planner
Date: 2026-05-23

#### 1.1 目标 / Goal

在继续调扇形 yaw gate 之前，先补充 Scene 视图诊断，让用户能直接看到：

- 敌人原点。
- `enemy -> player` 扇形中线。
- 左右扇形边界。
- `enemy -> main camera` 当前相机方向。
- 当前相机是否在扇形内。
- 当前 `sectorDelta`、`halfAngle`、`sectorTargetYaw`。

#### 1.2 非目标 / Non-goals

- 不修改 yaw gate 行为。
- 不调整 `lockYawSectorHalfAngle` 或 `lockYawSectorReturnSpeed`。
- 不修改 anchor position、distance、FOV、FrameSize、TargetGroup。
- 不修改 prefab / scene。

#### 1.3 允许修改范围 / Allowed Edit Scope

- `Assets/Scripts/Camera/ActorCameraControl.cs`
- 当前任务文件

#### 1.4 验收标准 / Acceptance Criteria

- `debugLockCameraGizmos` 开启时，Scene 视图能看到 enemy-centered yaw sector。
- inside/outside 使用不同颜色表达。
- 不新增 Inspector 可见开关。
- 不修改实际相机行为。
- `git diff --check -- Assets/Scripts/Camera agent-tasks/active/task-20260523-lock-camera-sector-gizmos.md` 通过，允许 CRLF/LF warning。

### 2. 执行报告 / Execution Report

Agent: Codex
Role: Executor
Date: 2026-05-23

#### 修改文件 / Changed Files

- `Assets/Scripts/Camera/ActorCameraControl.cs`
  - `OnDrawGizmos` 复用现有 `debugLockCameraGizmos` 开关。
  - 新增 `DrawLockYawSectorGizmo(...)`。
  - 新增 `DrawYawSectorArc(...)`。
- `agent-tasks/active/task-20260523-lock-camera-sector-gizmos.md`
  - 新建本任务并写入执行报告。

#### 行为变化 / Behavior Changes

- 默认不开启 `debugLockCameraGizmos` 时没有额外绘制。
- 开启后 Scene 视图新增：
  - 敌人原点空心球。
  - 敌人到玩家的中线。
  - 左右扇形边界线和扇形弧线。
  - 敌人到当前主相机方向线。
  - outside 时标出最近边界点。
  - Editor label 显示 `inside/outside`、`sectorDelta`、`halfAngle`、`sectorTargetYaw`。
- 没有修改 yaw gate 的运行逻辑和任何相机手感参数。

#### 剩余风险 / Remaining Risks

- 未 PlayMode 验证 Gizmo 视觉效果。
- Gizmo 使用当前 `Camera.main` 和当前位置绘制，用于诊断，不参与相机行为。

#### 已执行验证 / Verification Performed

- 已运行 `git diff --check -- Assets/Scripts/Camera agent-tasks/active/task-20260523-lock-camera-sector-gizmos.md`，通过，仅有 CRLF/LF warning。
- 已运行 `git diff --name-only -- Assets/Scripts/Actor Assets/Scripts/ActionSystem Assets/Scripts/TimelinePlayable Assets/Scripts/Combat Assets/Prefabs Assets/Scenes`，无输出。
- 已尝试 `dotnet build .\Assembly-CSharp.csproj --no-restore`，仍因缺少 Unity 生成的 `Temp\obj\Assembly-CSharp\project.assets.json` 失败。

### 3. 审查 / Review

Agent: Codex
Role: Reviewer
Date: 2026-06-09

#### 决策 / Decision

`blocked`

#### 发现或疑虑 / Findings Or Concerns

- 当前 main 的相机代码已经不再包含本任务要验收的 yaw sector Gizmo 实现。已用 `rg` 检查 `Assets/Scripts/Camera`，未找到 `debugLockCameraGizmos`、`OnDrawGizmos` 或 yaw sector 绘制 helper。
- 当前实现已拆分为 `ActorCameraControl.SoftLockComposer.cs`、`CombatLockComposer.cs`、`Diagnostics.cs` 等文件；继续按本任务执行会把旧的 sector 诊断方案重新带回当前架构，风险高于收益。

#### 必要修改 / Required Changes

- 需要项目 owner 决定：如果仍需要 Scene-view 相机诊断，应基于当前相机架构重新建一个小任务；如果这只是旧 release 过程中的历史诊断，应在单独 archive pass 中归档。

#### 是否可以标记为 done

否。当前代码无法满足本任务的验收标准，因此不能标记为 `done`。

---

## 归档说明 / Archive Note

Agent: Codex
Role: Archiver
Date: 2026-06-09

Owner 已确认本任务属于旧方向或错误方向的历史记录，归档保留，不再作为 active 开发或 review 入口。
