# ActionSequence Editor V2 Architecture

> Status: Approved architecture baseline; Stage 0, Stage 1 core foundations, Stage 2 read-only UI Toolkit rendering, Stage 3 non-drag authoring, and Stage 4 timing interactions are implemented pending full Unity EditMode verification  
> Version: 1.0  
> Date: 2026-08-03  
> Target: Unity 2022.3.62f3  
> Scope: ActionSequence authoring editor and its editor-facing data requirements  
> Runtime boundary: keep the existing fixed-frame ActionSequence runtime; do not integrate ActionPlayer in this project stage  
> Chinese edition: [ActionSequence 编辑器 V2 架构](ActionSequence_Editor_V2_Architecture_zh-CN.md)

## 1. Decision

The current `ActionSequenceEditorWindow` is a prototype. It proved the data model and basic interactions, but it must not become the foundation of the production editor through continued local patches.

ActionSequence Editor V2 will be built as a separate UI Toolkit editor with explicit architecture boundaries:

```text
serialized domain data
        ↓
serialized access + identity resolution
        ↓
commands ────────────── validation
        ↓                    ↓
editor state ────────── change notifications
        ↓
UI Toolkit views + manipulators
        ↓
Unity Inspector selection bridge
```

The prototype remains available while V2 is developed. It receives compile fixes only. V2 replaces it only after the complete authoring loop passes the acceptance gate in section 16.

## 2. Product Position

ActionSequence is a deterministic, fixed-frame combat-action authoring tool. It is not a generic cinematic sequencer and it does not recreate Unity Timeline runtime.

We adopt Unity Timeline's successful editing language:

- Tracks are first-class authoring lanes.
- Track type determines which clip types are legal.
- The timeline window edits structure and time.
- The Inspector edits the selected sequence, track, or clip.
- Selection, context menus, lock, mute, Undo/Redo, zoom, pan, and frame snapping behave consistently.

We keep our own runtime rules:

- No `PlayableGraph` dependency for combat-critical execution.
- No Timeline scene-binding model as runtime truth.
- No Timeline clip or track subassets.
- Runtime scheduling remains integer-frame based.
- Existing frame-0, `[startFrame, endFrame)`, cancellation, and cleanup semantics remain unchanged.

## 3. V2 Goals and Non-goals

### 3.1 Required V2 authoring loop

V2 is complete only when a user can:

1. Open a Sequence-backed `ActionAsset` or an `ActionSequenceAsset`.
2. Add, rename, reorder, mute, lock, collapse, and delete tracks.
3. Add only legal clip types to a track.
4. Select a sequence, track, or clip and edit the correct configuration in Inspector.
5. Move and resize clips with integer-frame snapping.
6. Scrub the playhead, zoom around the cursor, pan, and fit the authored range.
7. Use Fixed and Auto duration without manually extending the visible workspace.
8. Undo or redo every persistent edit as one understandable operation.
9. See validation errors without silent deletion or repair of authored data.
10. Save, reload, recompile scripts, and reopen the window without losing selection identity or corrupting layout state.

### 3.2 V2 first-release non-goals

- ActionPlayer integration.
- Real Animancer, force, or HitBox preview.
- Multi-selection and box selection.
- Cross-track clip dragging.
- Copy/paste and duplication workflows.
- Clip blending, curves, easing, or overlap resolution.
- Nested/group tracks.
- Markers or signals.
- Per-track custom height.
- Runtime bindings to scene objects.
- A GraphView-based implementation.

These features require their own design decisions. They must not be smuggled into the first V2 implementation.

## 4. Authoring Language

### 4.1 Window layout

```text
┌───────────────────────────────────────────────────────────────────────┐
│ Target | Add Track | ◀ ■ ▶ | FPS | Duration | Frame | Zoom | Fit    │
├──────────────────────┬────────────────────────────────────────────────┤
│ Track controls       │ 0  1  2  3  4  5 ...        frame ruler      │
├──────────────────────┼────────────────────────────────────────────────┤
│ ▾ Animation  M L  +  │      [ Animancer Clip             ]           │
│ ▾ Motion     M L  +  │              [ Impulse ]                       │
│ ▾ HitBox     M L  +  │                    [ HitBox ]                   │
│ ▾ State      M L  +  │ [ Tags ]                                      │
├──────────────────────┴────────────────────────────────────────────────┤
│ status / validation summary                                           │
└───────────────────────────────────────────────────────────────────────┘
```

The left column does not scroll horizontally. The ruler and lanes share one horizontal frame transform. Track headers and lanes share one vertical scroll position.

### 4.2 Selection language

Only one item is selected in the first V2 release:

| Click target | Timeline result | Inspector result |
| --- | --- | --- |
| Clip | Clip highlighted | Clip timing and type-specific config |
| Track header or empty lane | Track highlighted | Track settings |
| Empty background | Item selection cleared | Sequence settings |
| Different asset | Target changes or window becomes unbound | Normal Inspector for that asset |

Selection priority is `Clip > Track > Sequence`.

Tracks and clips are managed-reference objects, not `UnityEngine.Object` instances. Therefore V2 cannot make a clip a genuine Unity object selection without changing the data model. V2 deliberately keeps the owning asset as `Selection.activeObject` and makes its custom Inspector render the selected managed reference as the primary subject. No hidden proxy objects or subassets are introduced.

### 4.3 Track language

- Add Track is available only from the toolbar and the timeline background context menu.
- Track creation types come from safe reflected subclasses of `ActionSequenceTrackDefinition`.
- A track's Add Clip menu contains only `AllowedClipTypes`.
- Mute affects runtime participation and visually subdues the lane.
- Lock prevents structural, timing, and configuration edits, but the lock control remains usable.
- Collapse changes editor presentation only.
- Delete always requires an explicit command; deleting a non-empty track shows a confirmation with clip count.
- Track order is authored only inside the same Phase group.

The editor displays tracks grouped by `Phase`, then by serialized order inside each phase. New tracks are inserted at the end of their phase group. Existing assets with unusual cross-phase serialized order are displayed in execution order without silently rewriting their arrays; an explicit repair command may canonicalize serialized order.

This keeps visible top-to-bottom order consistent with runtime order:

```text
Phase -> order inside the phase -> clip start frame -> clip order in track
```

### 4.4 Clip language

- Clicking selects; double-click may frame the clip but does not open a second editor.
- Dragging the center previews a same-track move.
- Dragging the left or right handle previews a resize.
- All timing is integer-frame snapped.
- A valid interval is always `[startFrame, endFrame)` with at least one frame.
- Escape cancels an active manipulation.
- Pointer release commits the entire manipulation as one Undo operation.
- Right-click opens legal clip operations for the owning track.
- Overlap is allowed in the first release; runtime ordering remains deterministic. V2 does not invent blend or collision semantics.

Locked tracks allow track selection and unlocking, but their clips cannot be selected or edited. This follows Unity Timeline's protection language and prevents Inspector edits from bypassing the lock.

### 4.5 Time, duration, and viewport language

Sequence duration and editor workspace are separate concepts:

- `FixedFrames` is an authored hard duration.
- `AutoFromClips` derives action duration from the greatest clip end frame.
- `viewEndFrame` is editor-only and may extend beyond either duration.
- Adding or dragging a clip near the right edge grows the visible workspace automatically.
- Fit frames the authored range with padding; it does not rewrite duration.
- Zoom keeps the frame under the pointer stationary.
- The ruler chooses minor and major tick spacing from zoom level.
- Every frame receives a minor grid line when zoom is sufficiently close; labels are shown only where readable.

New Sequence assets should default to `AutoFromClips`. Existing assets retain their serialized duration mode. Changing this default is a data-factory change, not an implicit migration.

The preview transport controls only the editor playhead in this stage. They do not execute Action runtime, animation, motion, or hit detection.

## 5. Existing Domain and Runtime Contract

V2 preserves this ownership model:

```text
ActionAsset / ActionSequenceAsset
└── ActionSequenceData
    ├── frameRate
    ├── durationMode
    ├── durationFrames
    ├── tracks[] : SerializeReference
    │   └── clips[] : SerializeReference
    └── legacy clips[]
```

The runtime source of truth remains `tracks[].clips[]`. The legacy flat clip list remains migration/debug data only.

The editor must never silently:

- Recreate default tracks after a user deletes all tracks.
- Delete an illegal or unknown managed-reference type.
- Move a clip to another track.
- Sort serialized arrays as a side effect of drawing or validation.
- Clamp a value merely because an Inspector or window was opened.

Normalization may clamp safe scalar invariants. Validation reports structural problems. Destructive repair is always an explicit command.

## 6. Required Data Foundation: Stable Identity

The current selection bridge identifies items by array index and `SerializedProperty.propertyPath`. Both change after reorder, insertion, deletion, Undo, and some SerializeReference operations. They are unsuitable as V2 identity.

Before building V2 interactions, add hidden serialized IDs:

```csharp
ActionSequenceTrackDefinition.editorId
ActionSequenceClipDefinition.editorId
```

The current clip `Guid` is backed by a non-serialized field and is regenerated after reload, so it is not stable identity. Stage 1 must replace that storage with the serialized ID while preserving the public getter if existing code depends on it. Tracks need the same identity mechanism.

Contract:

- IDs are opaque strings generated as 32-character GUID values.
- IDs are authoring identity only and do not affect runtime sorting or behavior.
- Factory commands assign IDs to new tracks and clips.
- Opening an old asset in V2 runs a non-destructive identity upgrade for missing IDs and marks the asset dirty once.
- Duplicate IDs are validation errors. An explicit repair regenerates only duplicates after the first occurrence.
- IDs are preserved during move/reorder and Undo/Redo.
- Duplication commands, when later added, must generate new IDs.

During the upgrade transition, resolution may fall back to a property path for the current session. Persistent selection is considered valid only after IDs exist.

Selection becomes:

```text
target asset + selection kind + track ID + optional clip ID
```

Indices and property paths become short-lived resolution results, never stored identity.

## 7. Architectural Layers

### 7.1 Domain layer

Owns runtime-capable serialized definitions:

- `ActionSequenceData`
- `ActionSequenceTrackDefinition`
- `ActionSequenceClipDefinition`
- concrete tracks and clips
- validation issue value types

The domain has no dependency on UI Toolkit, Inspector state, pointer events, or window lifetime.

### 7.2 Serialized access layer

`ActionSequenceSerializedDocument` wraps the active Unity asset and owns:

- the `SerializedObject`;
- discovery of the root `ActionSequenceData` property for both supported asset types;
- ID-to-`SerializedProperty` resolution;
- safe enumeration snapshots for tracks and clips;
- target revision/change detection;
- managed-reference type creation helpers;
- no persistent mutations outside commands.

All view code receives read models or resolved handles. Views do not concatenate property-path strings.

Resolved properties are used within one operation and then discarded. A `SerializedProperty` is never cached across structural mutation, Undo/Redo, domain reload, or target replacement.

### 7.3 Command layer

`ActionSequenceEditorCommands` is the only entry point for persistent changes from the V2 window:

- add/delete/rename/reorder track;
- toggle mute/lock/collapse;
- add/delete clip;
- set clip timing;
- edit duration settings;
- assign/repair stable IDs;
- explicit validation repairs.

Each command:

1. Resolves IDs against a fresh serialized document state.
2. Checks legality and lock state.
3. Starts or joins one named Undo group.
4. Mutates through `SerializedProperty` where supported.
5. Uses `Undo.RecordObject` only when direct managed-reference mutation is unavoidable.
6. Applies properties, marks the asset dirty when required, and publishes one change set.
7. Returns a result containing success, changed IDs, new selection, and validation messages.

Continuous drag/resize uses transient preview values. It commits once on pointer release, so one gesture produces one Undo entry. Escape discards the preview.

### 7.4 Editor state layer

`ActionSequenceEditorState` is per window and contains no authored data:

- target asset;
- selected track/clip IDs;
- current frame;
- playback state and editor timestamp;
- pixels per frame;
- horizontal and vertical scroll;
- editor workspace end frame;
- active interaction preview;
- hover and context-menu state;
- validation summary;
- layout/view preferences.

Target, selection, and active manipulation are session state. Zoom, splitter width, and optional display preferences use UI Toolkit view-data persistence or `EditorPrefs` with a project-specific key. Authored tracks, clips, mute, lock, collapse, and duration never live in `EditorPrefs`.

The `EditorWindow` serializes a minimal domain-reload snapshot containing target, selected IDs, current frame, zoom, and scroll. `CreateGUI()` reconstructs `ActionSequenceEditorState` from that snapshot and validates every ID before restoring selection. Active pointer manipulation and playback are never restored.

### 7.5 Selection bridge

`ActionSequenceEditorSelection` publishes immutable selection values and a `selectionChanged` event. It uses stable IDs and the target asset. The selected asset remains `Selection.activeObject`.

The bridge is global only because Unity Inspector selection is global. Timeline viewport state remains per window. When two V2 windows exist, the last window that publishes selection owns Inspector context; the other window keeps its local highlight but does not overwrite Inspector until interacted with.

### 7.6 Validation layer

`ActionSequenceValidator` is read-only. It reports:

- missing or duplicate IDs;
- null managed references;
- missing managed-reference types;
- clip type not accepted by track;
- clip Phase mismatch;
- invalid frame ranges;
- clips outside Fixed duration;
- legacy clips not yet migrated;
- runtime-order/display-order anomalies.

Issues carry severity, target IDs, message, and an optional explicit repair command ID. Validation never repairs during repaint, binding, deserialization, or window opening, except the one-time non-destructive assignment of missing editor IDs.

## 8. UI Toolkit Strategy

V2 targets the APIs available in Unity 2022.3.62f3.

### 8.1 What uses UXML, USS, and C#

- UXML defines the stable window shell: toolbar host, ruler corner, viewport hosts, overlays, and status bar.
- USS defines dimensions, colors, state classes, hover/selection/locked/muted styles, and theme variants.
- C# custom `VisualElement` controls implement dynamic track, ruler, grid, clip, and interaction behavior.

Dynamic timeline geometry is not authored in UI Builder. It is data-driven and belongs in reusable C# controls.

### 8.2 Rendering split

Use `generateVisualContent` for dense, non-interactive graphics:

- ruler ticks and labels background;
- vertical frame grid;
- phase separators;
- selection/drag guides;
- playhead line background.

Use retained `VisualElement` instances for interactive objects:

- track headers and controls;
- clip blocks;
- clip labels;
- resize handles;
- playhead handle;
- menus and tooltips.

Do not create one VisualElement per frame tick or grid line. Canvas controls repaint through `MarkDirtyRepaint()` when zoom, scroll, size, theme, or current frame changes.

### 8.3 View composition

```text
ActionSequenceEditorWindowV2
└── ActionSequenceEditorRoot
    ├── ActionSequenceToolbar
    ├── TimelineHeader
    │   ├── TrackHeaderCorner
    │   └── ActionSequenceRulerView
    ├── TimelineBody
    │   ├── ActionSequenceTrackHeaderView
    │   │   └── ActionSequenceTrackHeaderRow[]
    │   └── ActionSequenceViewport
    │       ├── ActionSequenceGridView
    │       ├── ActionSequenceTrackLaneView[]
    │       │   └── ActionSequenceClipView[]
    │       ├── ActionSequenceGuideOverlay
    │       └── ActionSequencePlayheadView
    ├── HorizontalScroller
    └── ActionSequenceStatusBar
```

The header column and viewport use synchronized vertical scrolling with a reentrancy guard. The ruler, lanes, overlays, and horizontal scrollbar share one `TimelineTransform`:

```text
screenX = headerWidth + frame * pixelsPerFrame - scrollX
frame   = round((screenX - headerWidth + scrollX) / pixelsPerFrame)
```

There is exactly one implementation of these conversions.

### 8.4 Track rows and virtualization

V2 first release uses reconciled custom rows keyed by stable track ID. Combat actions are expected to have tens of tracks, not thousands. This gives deterministic row pairing between header and lane and avoids the complexity of synchronizing two independently virtualized lists.

Refresh does not clear and rebuild the whole visual tree. It performs an ID-keyed reconcile:

- reuse existing row and clip views;
- add/remove only changed views;
- rebind content after structural changes;
- update geometry only for timing/zoom changes;
- repaint canvas layers only when their inputs change.

If profiling later proves row count to be a problem, virtualization should use one composite row model or coordinated pooling. It must not be added pre-emptively.

### 8.5 Why not GraphView

GraphView solves node/edge graph editing. A timeline needs a shared one-dimensional time transform, synchronized rows, interval manipulation, and dense grid rendering. Using GraphView would add graph semantics without solving the central timeline problems. UI Toolkit custom controls are the correct foundation.

## 9. Interaction Architecture

Manipulation logic lives in dedicated UI Toolkit manipulators:

- `TimelinePanManipulator`
- `TimelineZoomManipulator`
- `PlayheadScrubManipulator`
- `ClipMoveManipulator`
- `ClipResizeManipulator`
- `TrackReorderManipulator`

Each manipulator follows the same lifecycle:

```text
PointerDown -> validate -> capture pointer -> create transient preview
PointerMove -> update preview state -> update affected geometry only
PointerUp   -> release pointer -> execute one command -> reconcile
Escape      -> release pointer -> discard preview -> repaint
CaptureLost -> deterministic cancel unless commit already occurred
```

Manipulators never edit serialized fields directly.

Keyboard commands use Unity Shortcut Management with a window/context guard. First-release shortcuts:

- Space: play/pause editor playhead.
- S: stop and reset playhead.
- Delete/Backspace: delete selected unlocked item.
- F: frame selected item; frame all when nothing is selected.
- L: lock/unlock selected track.
- M: mute/unmute selected track.
- Left/Right: move playhead by one frame.
- Shift + Left/Right: move playhead by the current major tick step.

Text fields and active property editors take precedence so shortcuts never consume typing.

## 10. Inspector Architecture

The V2 custom Inspectors use UI Toolkit `CreateInspectorGUI()` and `PropertyField` binding.

Inspector modes:

```text
Clip selected
  Selected Clip
    identity/type (read-only)
    timing
    type-specific config
  Owning Sequence [compact]
  ActionAsset Settings [collapsed]
  Debug Raw Data [collapsed]

Track selected
  Selected Track
    identity/type/phase (read-only)
    name, mute, lock, collapse
  Owning Sequence [compact]
  ActionAsset Settings [collapsed]
  Debug Raw Data [collapsed]

No item selected
  Sequence Settings
  ActionAsset Settings
  Validation
  Debug Raw Data [collapsed]
```

The Inspector resolves the selected IDs to fresh properties whenever selection or serialized structure changes. It never holds a property from before a list mutation.

Bound fields provide standard Unity serialization and Undo behavior. A `SerializedPropertyChangeEvent` publishes an immediate editor change notification so the timeline updates without waiting for mouse hover. The window also tracks serialized-object changes as a safety net for Undo/Redo and external edits.

The Debug Raw Data foldout remains an escape hatch. Its labels are derived from managed-reference type/display names, never managed-reference numeric IDs.

## 11. Change and Refresh Model

V2 has explicit invalidation categories:

```text
TargetChanged      -> rebuild document and entire view model
StructureChanged   -> resolve selection, reconcile tracks/clips, validate
ContentChanged     -> update labels/styles/Inspector, validate affected item
TimingChanged      -> update clip geometry, duration, ruler range
ViewportChanged    -> update transform and repaint canvases
SelectionChanged   -> update highlight and Inspector only
PlaybackChanged    -> update playhead only
```

No general-purpose `Refresh()` may rebuild everything for every event.

Change sources:

- successful editor command results;
- UI Toolkit serialized-property change callbacks;
- `Undo.undoRedoPerformed`;
- target replacement/destruction;
- domain reload and `CreateGUI()` reconstruction;
- geometry changes;
- editor update only while preview playback is active.

This removes the prototype's dependency on repainting when the pointer happens to enter the window.

## 12. Lifecycle and Failure Handling

- `CreateGUI()` may run repeatedly after domain reload; construction is idempotent.
- Event subscriptions are paired in attach/detach or enable/disable paths.
- A destroyed or incompatible target returns the window to an empty state.
- Missing SerializeReference types render an error placeholder and raw diagnostic data; they are not deleted.
- Invalid selection IDs clear selection without changing authored data.
- Undo/Redo resolves selection by ID; if the item no longer exists, selection falls back to its owning track, then sequence.
- Pointer capture loss cannot leave a partially committed timing change.
- Multiple windows do not share zoom, scroll, current frame, or active drag state.

## 13. Proposed File Layout

V2 is isolated from the prototype:

```text
Assets/Scripts/ActionSequence/Editor/V2/
├── ActionSequenceEditorWindowV2.cs
├── ActionSequenceEditorWindowV2.uxml
├── Styles/
│   └── ActionSequenceEditorV2.uss
├── Core/
│   ├── ActionSequenceEditorState.cs
│   ├── ActionSequenceEditorSelection.cs
│   ├── ActionSequenceEditorChangeSet.cs
│   ├── ActionSequenceEditorCommands.cs
│   ├── ActionSequenceSerializedDocument.cs
│   ├── ActionSequenceTimelineTransform.cs
│   └── ActionSequenceValidator.cs
├── Views/
│   ├── ActionSequenceEditorRoot.cs
│   ├── ActionSequenceToolbar.cs
│   ├── ActionSequenceRulerView.cs
│   ├── ActionSequenceGridView.cs
│   ├── ActionSequenceTrackHeaderView.cs
│   ├── ActionSequenceTrackHeaderRow.cs
│   ├── ActionSequenceTrackLaneView.cs
│   ├── ActionSequenceClipView.cs
│   ├── ActionSequencePlayheadView.cs
│   └── ActionSequenceStatusBar.cs
├── Manipulators/
│   ├── TimelinePanManipulator.cs
│   ├── TimelineZoomManipulator.cs
│   ├── PlayheadScrubManipulator.cs
│   ├── ClipMoveManipulator.cs
│   ├── ClipResizeManipulator.cs
│   └── TrackReorderManipulator.cs
└── Inspectors/
    ├── ActionAssetSequenceInspectorV2.cs
    └── ActionSequenceAssetInspectorV2.cs
```

Do not add an Editor assembly definition by itself while the runtime still compiles into the predefined `Assembly-CSharp`; an asmdef assembly cannot depend on a predefined assembly. Runtime/editor assembly separation is a separate repository-wide migration.

## 14. Implementation Stages

### Stage 0 — Freeze and baseline

- Mark the current window as prototype in code comments and documentation.
- Add a separate V2 menu entry.
- Record current test assets and manual authoring scenarios.

Status: implemented for the prototype code marker and V2 preview entry.

Exit: prototype behavior remains available and V2 can open an empty shell independently.

### Stage 1 — Identity, document, commands

- Add serialized stable IDs with non-destructive upgrade.
- Implement `ActionSequenceSerializedDocument`.
- Implement command results, Undo grouping, lock checks, and validation.
- Add EditMode tests for every structural command and ID migration.

Status: implemented for Stable Identity, Serialized Document, Type Registry, read-only Validator, Command/Result/ChangeSet, and non-destructive Normalize semantics. Full EditMode verification is pending because the project was open in another Unity instance during batchmode validation.

Exit: all mutations can be tested without an EditorWindow.

### Stage 2 — UI Toolkit shell and rendering foundation

- Add UXML/USS shell.
- Implement timeline transform, ruler, grid, scroll synchronization, zoom, pan, and fit.
- Render read-only track/clip views by stable ID.

Status: implemented for the V2 preview window shell, timeline transform, read-only track/clip rendering, zoom/pan/fit, synchronized scrolling, status summaries, and focused EditMode coverage. Full Unity verification is pending.

Exit: existing assets render correctly at different sizes and zoom levels without full-tree rebuilds.

### Stage 3 — Selection and Inspector

- Implement stable-ID selection bridge.
- Implement V2 UI Toolkit Inspectors.
- Add immediate serialized change notifications.
- Implement Add Track/Add Clip/context menus and lock/mute/collapse.

Status: implemented for stable-ID selection, V2 local selection highlights, Add Track/Add Clip/context menu authoring, lock/mute/collapse, UI Toolkit Inspector modes, command-backed timing/track fields, and immediate content change notifications. Full Unity verification is pending because the project was open in another Unity instance during batchmode validation.

Exit: complete non-drag authoring loop, Inspector synchronization, and Undo/Redo work.

### Stage 4 — Timing interactions

- Add playhead scrubbing.
- Add clip move/resize transient previews and single-command commits.
- Add automatic workspace growth and cursor-centered zoom.
- Add keyboard shortcuts.

Status: implemented for editor playhead state, play/pause/stop, ruler scrubbing, same-track clip move/resize previews, single `SetClipTiming` commits, frame shortcuts, and focused timing calculation tests. Full Unity EditMode verification is pending because batchmode licensing was unavailable in the current environment.

Exit: pointer capture, Escape cancellation, snapping, lock protection, and Undo grouping pass manual and focused tests.

### Stage 5 — Validation and polish

- Add inline issue badges, status summary, and explicit repair commands.
- Add missing-type/error placeholders.
- Verify dark/light theme, narrow window, high-DPI, and domain reload behavior.
- Profile repaint and structural reconciliation.

Exit: no silent authored-data mutation and no hover-dependent refresh.

### Stage 6 — Cutover

- Run the acceptance gate.
- Redirect double-click/open helpers to V2.
- Keep the prototype temporarily behind a diagnostic menu.
- Remove the prototype only after a stabilization period and explicit approval.

Exit: V2 is the only normal authoring entry point; LegacyTimeline still opens Unity Timeline.

## 15. Verification Strategy

### 15.1 EditMode tests

- Missing and duplicate ID upgrade/repair.
- ID stability through reorder and Undo/Redo.
- Track creation type filtering.
- Add Clip legality for every concrete track.
- Locked-track rejection for every mutation path.
- Non-empty track deletion command behavior.
- Same-phase track reorder and cross-phase rejection.
- Clip move/resize frame invariants.
- Fixed and Auto duration calculations.
- Validation does not mutate source data.
- Existing deterministic runtime sorting and muted-track behavior.
- Multiple runtime/editor document instances do not share state.

### 15.2 Manual Editor matrix

- `ActionAsset` Sequence backend and standalone `ActionSequenceAsset`.
- Empty sequence, one track, duplicate track types, zero tracks, and legacy migrated asset.
- Add/delete/reorder/rename/mute/lock/collapse tracks.
- Add/select/delete/move/resize all clip types.
- Inspector edits update labels and geometry immediately without pointer hover.
- Undo/Redo after each structural and timing operation.
- Script recompile/domain reload with window open.
- Save/reopen Unity and verify serialized data and selection fallback.
- Horizontal zoom/pan/fit at minimum and maximum zoom.
- Vertical scroll with headers and lanes remaining aligned.
- Fixed duration boundary and Auto workspace growth.
- Missing type and invalid-track data remain visible and recoverable.
- LegacyTimeline double-click still opens Unity Timeline.

### 15.3 Performance checks

Use representative stress assets, not synthetic thousands-of-items targets:

- 30 tracks / 300 clips.
- Continuous pan and zoom without layout spikes.
- Clip drag updates only the previewed clip and canvas overlays.
- Inspector typing does not rebuild every row.
- Idle window performs no continuous full refresh; editor update runs only for preview playback.

## 16. Cutover Acceptance Gate

V2 may replace the prototype only when all are true:

- No compile errors on Unity 2022.3.62f3.
- Stage 1 EditMode command/identity tests pass.
- The complete required authoring loop in section 3.1 passes for both target asset types.
- All persistent operations support Undo/Redo.
- Track/clip selection survives reorder and resolves correctly after Undo/Redo.
- Inspector edits repaint the timeline immediately.
- Lock cannot be bypassed through window, context menu, shortcut, or Inspector.
- Zero tracks stays zero.
- Validation and window opening never silently delete authored data.
- LegacyTimeline routing remains unchanged.
- The prototype remains recoverable until the team explicitly approves removal.

## 17. Architecture Rules for Future Changes

Every proposed feature must answer these questions before implementation:

1. Is it authored domain data or per-window editor state?
2. Which command owns its persistent mutation?
3. What is its stable identity?
4. Which invalidation category does it publish?
5. How does Undo/Redo behave?
6. How does lock affect it?
7. What does Inspector show when it is selected?
8. What is the runtime semantic, if any?
9. What focused test proves it?

If these answers are missing, the feature is not ready to enter V2.

## 18. Unity References

The architecture uses Unity's official 2022.3 guidance as its platform baseline:

- [Create a custom Editor window](https://docs.unity3d.com/2022.3/Documentation/Manual/UIE-HowTo-CreateEditorWindow.html)
- [Support for Editor UI](https://docs.unity3d.com/2022.3/Documentation/Manual/UIE-support-for-editor-ui.html)
- [SerializedObject data binding](https://docs.unity3d.com/2022.3/Documentation/Manual/UIE-Binding.html)
- [Create a custom Inspector](https://docs.unity3d.com/2022.3/Documentation/Manual/UIE-HowTo-CreateCustomInspector.html)
- [Generate 2D visual content](https://docs.unity3d.com/2022.3/Documentation/Manual/UIE-generate-2d-visual-content.html)
- [Undo API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Undo.html)
- [Shortcut Manager](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ShortcutManagement.ShortcutManager.html)
- [Timeline package compatibility for Unity 2022.3](https://docs.unity3d.com/2022.3/Documentation/Manual/com.unity.timeline.html)
- [Timeline Inspector selection language](https://docs.unity3d.com/Packages/com.unity.timeline@1.5/manual/insp_about.html)
- [Timeline track locking language](https://docs.unity3d.com/2019.1/Documentation/Manual/TimelineLockingTracks.html)
