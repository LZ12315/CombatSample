# 战斗锚点配表（方案 A：角色 Prefab 挂载 + List）

> **更新**：锚点**不单独挂组件**，已定为嵌入 **`Actor` 上的序列化列表**。吸附与迁移请以  
> **`plans/攻击吸附_根节点到表面_方案与迁移.md`** 为准；本文件侧重锚点配表本身。

## 1. 目标

- **默认**：从角色身上的配表按 `CombatAnchorId` 解析出 `Transform`（如剑身中心），供 **HitBox / VFX / UI** 等复用。
- **Magnetism V2**：**不再**使用武器锚点；根节点↔敌人胶囊表面间隙见迁移文档。
- **覆盖**（其他 Track）：Timeline / Clip 上可选 `ExposedReference<Transform>`，非空则**优先于配表**。
- **不**增加额外 MonoBehaviour：配表在 **`Actor` 组件内**一块 Inspector 区域。

---

## 2. 键：`CombatAnchorId`（枚举）

与 `CombatAnchorEntry` 放在**同一文件** `Assets/Scripts/Actor/CombatAnchors.cs`（见最终实现方案文档）。

### 2.1 第一版最小集合（可后续追加）

| 枚举值 | 含义 | 典型绑定 |
|--------|------|----------|
| `WeaponBladeMid` | 剑身中心（吸附表面间隙用） | 武器骨或空物体 |
| `RightHand` | 右手（兜底 / 非武器招式） | 右手骨 |
| `LeftHand` | 左手 | 左手骨 |

**约定**：新增槽位只加枚举 + 在对应 Prefab 里填一行，不改 Timeline。

可选扩展（第二版）：`HitBoxFollow` — 若与 HitBox  bone 强一致，可运行时从 Action 配置注入，**不一定**进 Prefab 表（见第 6 节）。

---

## 3. 表结构（序列化）

### 3.1 存放位置（做法一）

- **嵌入 `Actor`**：`[SerializeField] List<CombatAnchorEntry> _combatAnchors`，`Awake` 构建 `Dictionary`。
- **不**新增 `ActorCombatAnchors` 组件。

### 3.2 单条记录（Serializable）

```text
[Serializable]
struct CombatAnchorEntry {
    public CombatAnchorId id;
    public Transform transform;   // 可空：表示本角色不提供该锚点
}
```

### 3.3 Actor 内字段

```text
// Actor.cs 内
[SerializeField] List<CombatAnchorEntry> _combatAnchors;
Dictionary<CombatAnchorId, Transform> _combatAnchorCache; // Awake 填充
```

**规则**：

- 同一 `id` 在 List 中**最多一条**；若重复，**Awake 打 Warning**，以后条覆盖或忽略（实现时二选一，文档固定）。
- `transform == null`：表示该角色**未配置**此锚点，`TryGet` 返回 false。

### 3.4 对外 API（Actor 或组件上）

```text
bool TryGetAnchor(CombatAnchorId id, out Transform t);
Transform GetAnchorOrNull(CombatAnchorId id);
```

**缓存**：`Awake` 里从 `anchors` 构建 `Dictionary`，避免每帧遍历 List。

**API**：`Actor.TryGetCombatAnchor(CombatAnchorId id, out Transform t)`。

---

## 4. 与 Magnetism V2 的关系（当前实现）

- **吸附不再读战斗锚点**：V2 用 **Actor 根节点 ↔ 敌人 `CapsuleCollider` 壳** 的间隙带，配置在 `MagnetismConfig`（`idealSurfaceGap`、`gapDeadZone`、`pullSpeed`、`pushSpeed` 等）。详见 **`plans/攻击吸附_根节点到表面_方案与迁移.md`**。
- **`ActionMagnetismV2Clip`** 无 `ExposedReference` 武器覆盖；目标仍为 `CombatTarget` 或 `customTarget`。

---

## 5. Prefab 工作流（策划 / 程序）

1. 打开角色 Prefab（如 `Player`）。
2. 选中挂有 `Actor` 的物体，在 **Combat Anchors** 列表里为 `WeaponBladeMid` / 手骨等拖入 Transform（供 **HitBox、特效、UI** 等）。
3. Timeline 使用 **Action Magnetism V2** 轨道，在 Clip 的 `MagnetismConfig` 里调间隙与速度。

---

## 6. 与 HitBox 的关系（避免两套真理）

- **Prefab 表**：放**长期稳定**、与具体招式无关的点（剑身中心、手）。
- **HitBox Clip**：招式相关的碰撞体父骨。若希望「视觉参考点」与 HitBox 父骨一致，可在 Prefab 上让 `WeaponBladeMid` 与 HitBox 父骨**父子对齐**（美术摆空物体）。这与吸附几何**无强制耦合**。

---

## 7. 校验与调试

- `Actor.OnValidate`（可选）：检查锚点列表重复 id、空引用。
- 可选：`[ContextMenu("Log Anchors")]` 打印缓存。
- Magnetism V2：`MagnetismConfig.debugLog` 可打距离门控、缺胶囊等（见 `ActionMagnetismSession`）。

---

## 8. 实现状态（代码）

1. `CombatAnchorId` + `CombatAnchorEntry` + **`Actor` 内列表与 TryGet** — 已完成。  
2. Magnetism V2 根表面方案 — 已完成（见迁移文档）。  
3. 旧轨道 `ActionMagnetismTrack` / `ActionMagnetismClip` — 已标 `[Obsolete]`，新招式请只用 V2。

---

## 9. 文档版本

- 初稿：方案 A Prefab List + 枚举。  
- 更新：吸附改为根表面方案后，第 4～8 节已同步；Magnetism 与锚点解耦。
