# 战斗锚点配表（方案 A：角色 Prefab 挂载 + List）

> **更新**：锚点**不单独挂组件**，已定为嵌入 **`Actor` 上的序列化列表**。全链路请以  
> **`plans/攻击吸附与战斗锚点_最终实现方案.md`** 为准。

## 1. 目标

- **默认**：从角色身上的配表按 `CombatAnchorId` 解析出 `Transform`（如剑身中心），供吸附、后续其他系统复用。
- **覆盖**：Timeline / Clip 上可选 `ExposedReference<Transform>`，非空则**优先于配表**。
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

## 4. 与 Magnetism（V2）的协作

### 4.1 `MagnetismConfig` 扩展（逻辑字段）

| 字段 | 类型 | 说明 |
|------|------|------|
| `weaponAnchorId` | `CombatAnchorId` | 默认从角色配表取武器参考点，如 `WeaponBladeMid` |
| `useExposedAnchorOverride` | bool | 是否启用 Timeline 覆盖（可选，也可用「Exposed 非空即覆盖」） |

### 4.2 `ActionMagnetismV2Clip`

- `ExposedReference<Transform> anchorOverride`（名字随意）
- **解析顺序**（固定，便于排查）：
  1. 若 `anchorOverride` 在 Director 上解析成功且非 null → **用覆盖**。
  2. 否则 `actor.combatAnchors.TryGetAnchor(weaponAnchorId, out t)` → **用配表**。
  3. 否则：吸附里「表面间隙」分支**跳过**或打 Log，回退为仅根节点 `attachDistance`（实现时二选一，建议 **Log + 跳过表面修正**）。

### 4.3 `ActionMagnetismSession`（未来从 Behavior 抽出时）

- 构造或 `Tick` 入参：`(Actor actor, Transform target, MagnetismConfig config, Transform weaponOverride)`
- `weaponOverride` 由 **V2Behavior** 在 `OnClipStart` / 每帧前按上面顺序解析好传入。

---

## 5. Prefab 工作流（策划 / 程序）

1. 打开角色 Prefab（如 `Player`）。
2. 选中挂有 `Actor` 的物体，在 **Combat Anchors** 列表里为 `WeaponBladeMid` 拖入「剑身中心」空物体或骨。
3. Timeline 里 Magnetism Clip：**不配 Exposed** 即走配表；特殊招式再绑 `anchorOverride`。

---

## 6. 与 HitBox 的关系（避免两套真理）

- **Prefab 表**：放**长期稳定**、与具体招式无关的点（剑身中心、手）。
- **HitBox Clip**：招式相关的碰撞体父骨；若某招吸附必须与 HitBox 骨一致，有两种做法：
  - **做法 1**：该招 `weaponAnchorId` 仍用 `WeaponBladeMid`，但 Prefab 上该点与 HitBox 父骨**父子对齐**（美术摆空物体）。
  - **做法 2（第二版）**：Clip 增加「使用当前 HitBox bone」标志，由 Action 运行时注入（需 Action 上下文，实现成本高）。

第一版推荐 **做法 1**。

---

## 7. 校验与调试

- `Actor.OnValidate`（可选）：检查锚点列表重复 id、空引用。
- 可选：`[ContextMenu("Log Anchors")]` 打印缓存。
- Magnetism `debugLog`：打印最终选用的是 **Override** 还是 **AnchorId:xxx**。

---

## 8. 实现顺序建议（代码阶段）

1. `CombatAnchorId` + `CombatAnchorEntry` + **`Actor` 内列表与 TryGet**。
2. 给 `Player`（等）Prefab 填 `WeaponBladeMid`。
3. `MagnetismConfig` + `ActionMagnetismV2Clip` 增加 `weaponAnchorId` 与 `ExposedReference`。
4. `ActionMagnetismV2Behavior` 解析武器 Transform 并传入 Session（或内联逻辑）。
5. 再接入 `MagnetismCapsuleGeometry` 表面间隙（与本文档第 4 节衔接）。

---

## 9. 文档版本

- 初稿：方案 A Prefab List + 枚举，默认表 + Exposed 覆盖，与 Magnetism V2 协作顺序。
