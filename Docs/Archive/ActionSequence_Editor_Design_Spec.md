# ActionSequence Editor Design Spec

> Status: Product-language baseline; V2 implementation architecture is defined by [ActionSequence Editor V2 Architecture](ActionSequence_Editor_V2_Architecture.md)  
> Created: 2026-08-02  
> Scope: ActionSequence asset/editor/runtime carrier design for the Unity CombatSample project  
> Reference model: Unity Timeline editor language, not Unity Timeline runtime

## 1. Purpose

ActionSequence exists to replace Unity Timeline as the runtime carrier for combat actions while keeping the parts of Timeline that are already proven to work as an editor language.

The objective is not to build a generic sequencer. The objective is to build a stable, fixed-frame combat action authoring tool.

The editor must make these questions obvious:

- What happens?
- On which frames does it happen?
- In which execution channel does it happen?
- Which data/config belongs to the selected item?
- What will runtime execute, and in what order?

If a feature does not help answer those questions, it is not v1.

## 2. Core Position

### 2.1 What we copy from Unity Timeline

We copy Unity Timeline's editing language:

- A timeline window edits time relationships.
- Track is a first-class authoring object.
- Clip lives inside a Track.
- Track type defines what clips can be created on that track.
- Track header owns track-level operations: name, mute, lock, collapse, context menu.
- Clip block owns clip-level selection and timing.
- Inspector edits the currently selected asset, track, or clip.
- Selection in the timeline and Inspector must remain synchronized.
- Context menus should expose legal operations from the clicked location.
- Undo/Redo is part of the editor contract, not a polish item.

### 2.2 What we do not copy from Unity Timeline

We do not copy Unity Timeline's runtime model:

- No PlayableGraph as the combat action runtime.
- No dependency on Timeline evaluation timing for combat-critical events.
- No scene binding model as the source of runtime truth.
- No TimelineClip/TrackAsset subasset model for v1.
- No generic cinematic feature set.

ActionSequence uses fixed-frame scheduling. Timeline is the UX precedent, not the runtime dependency.

## 3. Mental Model

The authoring model is:

```text
ActionAsset
└── ActionSequenceData
    ├── frameRate
    ├── durationFrames
    └── tracks[]
        ├── Track: State
        │   └── clips[]
        ├── Track: Animation
        │   └── clips[]
        ├── Track: Motion
        │   └── clips[]
        ├── Track: HitBox
        │   └── clips[]
        └── Track: Cleanup
            └── clips[]
```

Definitions:

- ActionAsset: one combat action.
- ActionSequenceData: fixed-frame script for that action.
- Track: execution channel and authoring lane.
- Clip: a frame interval that runs one behavior.
- Clip config: serialized behavior data for the selected clip.
- Runtime: deterministic scheduler built from tracks and clips.

The user should think: "I add tracks for categories of behavior, then place clips on legal tracks."

## 4. Product Boundaries

### 4.1 v1 goals

v1 is successful when the editor supports a complete non-drag authoring loop:

- Create Sequence ActionAsset.
- Open ActionSequence Editor.
- Add, delete, rename, mute, lock, collapse, and reorder tracks.
- Add legal clips from each track.
- Delete clips.
- Select track or clip in the timeline.
- Edit selected track or clip in Inspector.
- Edit sequence timing.
- See clip names, positions, and durations on the timeline.
- Save serialized data.
- Undo/Redo for create/delete/reorder/field edit.
- Runtime builds deterministic records from tracks and clips.
- LegacyTimeline ActionAsset still opens Unity Timeline.

### 4.2 v1 non-goals

Do not add these until the v1 loop is stable:

- Dragging clips horizontally.
- Resizing clips by handles.
- Moving clips between tracks.
- Multi-select.
- Copy/paste.
- Snapping UI.
- Real animation preview through Animancer.
- Live HitBox visualization.
- Runtime integration into ActionPlayer.
- Generic marker/signals system.
- Per-track custom heights.
- Blend/overlap semantics.
- Timeline import/export.

These can be v2 or v3. Implementing them early will hide flaws in the basic editing language.

## 5. Reference UX Rules from Unity Timeline

Unity Timeline establishes several useful rules:

- The Timeline window is primarily spatial/time editing, not deep property editing.
- Track headers provide track identity and track state controls.
- The Inspector changes according to the selected timeline object.
- Lock prevents editing a track and its contents.
- Mute disables the track's effect while leaving it editable.
- Moving a clip to another track is only valid when the target track accepts that clip type.
- Track/clip data is saved in an asset; scene bindings are a separate concept.

ActionSequence should follow those rules unless combat-specific determinism requires otherwise.

## 6. Data Model Rules

### 6.1 Sequence data

`ActionSequenceData` owns:

- `frameRate`
- `durationFrames`
- `tracks`
- legacy/debug `clips`

`tracks` is the source of truth.

Legacy flat `clips` exists only for migration/debug. New authoring must not write to the legacy list.

### 6.2 Track data

Each track has:

- `displayName`
- `muted`
- `locked`
- `collapsed`
- `Phase`
- `AllowedClipTypes`
- `clips`

Track type is semantic. Do not replace strong track classes with a `TrackKind enum` unless there is a clear migration reason.

Allowed clip type is part of the track contract. A clip should never be creatable on an invalid track.

### 6.3 Clip data

Each clip has:

- `displayName`
- `startFrame`
- `endFrame`
- type-specific config

Clip interval semantics are `[startFrame, endFrame)`.

`endFrame` must always be greater than `startFrame`.

Clip labels in the timeline should use `clip.GetDisplayName()`, not the raw SerializeReference managed ID.

### 6.4 Default track initialization

Default tracks are a creation convenience, not a normalization invariant.

Rules:

- A newly created empty Sequence may receive the default common tracks once.
- If the user deletes all tracks, the editor must keep zero tracks.
- `Normalize()` must not recreate default tracks after user deletion.
- Legacy clip migration may create tracks needed to hold migrated clips.

This distinction matters. "Empty because newly created" and "empty because user deleted everything" are different states.

## 7. Runtime Rules

Runtime is deterministic and frame-based.

### 7.1 Runtime source

Runtime reads:

```text
data.tracks[].clips[]
```

Runtime does not read the legacy flat clips list except indirectly through migration before initialization.

### 7.2 Sorting

Runtime record order is:

```text
Track Phase -> Track order -> clip.startFrame -> clip order inside track
```

This means Track is not just an editor row. It affects deterministic execution order.

### 7.3 Muted/locked/collapsed

- `muted`: excluded from runtime.
- `locked`: editor-only, does not affect runtime.
- `collapsed`: editor-only, does not affect runtime.

### 7.4 Frame semantics

Keep existing semantics:

- Frame 0 is processed.
- Clip interval is `[startFrame, endFrame)`.
- Active clips tick only while containing the current frame.
- Clips exiting on the same frame exit before clips entering that frame.
- Cancel exits active clips as interrupted.
- Completion exits active clips as completed.

## 8. Editor Window Rules

The ActionSequence Editor Window is a timeline surface.

It should display:

- Target asset.
- Add Track menu.
- Play/Pause/Stop preview controls.
- Frame rate.
- Duration frames.
- Current frame.
- Ruler.
- Playhead.
- Track rows.
- Clip blocks.

It should not display:

- Full raw `tracks` property tree.
- Full raw `clips` property tree.
- Full selected clip config.
- Full selected track clips list.
- Debug serialization details by default.

The window is for structural and timing edits. Inspector is for detailed config edits.

## 9. Track Header Rules

Every track row has a header.

The header should expose:

- Foldout/collapse.
- Display name.
- Track type identity.
- Add Clip.
- Mute.
- Lock.
- Delete.
- More/context menu.

The context menu should expose:

- Select.
- Move Up.
- Move Down.
- Delete.
- Future: duplicate, copy, paste, lock/unlock, mute/unmute.

### 9.1 Lock behavior

Locked track means:

- Cannot add clips.
- Cannot delete clips.
- Cannot delete the track.
- Cannot reorder the track.
- Cannot edit timing/config of clips on that track.

The editor should visibly communicate the locked state.

### 9.2 Mute behavior

Muted track means:

- Track remains editable.
- Track remains selectable.
- Track clips remain visible, but visually subdued.
- Runtime skips the track.
- Editor preview should eventually skip the track as runtime does.

Mute is not lock.

### 9.3 Collapse behavior

Collapsed track means:

- Header remains visible.
- Clip lane contents are hidden or compressed.
- Runtime is unaffected.
- Serialized clip data is unaffected.

## 10. Clip Authoring Rules

### 10.1 Creating clips

Clips are created from:

- Track header `+ Clip`.
- Track lane right-click context menu.

The Add Clip menu must only show the current track's `AllowedClipTypes`.

If no clip types are allowed, the menu should say that explicitly.

### 10.2 Selecting clips

Clicking a clip:

- Selects that clip in the timeline.
- Sets the ActionAsset or ActionSequenceAsset as the active Unity selection.
- Makes Inspector display the selected clip config.

The editor should not create a proxy object in v1.

### 10.3 Deleting clips

Deleting a clip removes it from its owning track.

If the deleted clip was selected, selection clears.

Undo/Redo must restore both the clip data and a valid editor state.

### 10.4 Moving/resizing clips

Not v1.

When added later:

- Move is legal only within the same track or to a compatible track.
- Resize must preserve `[start, end)` validity.
- Drag should show frame feedback.
- Drag should be undoable as one operation.

## 11. Inspector Rules

Inspector is the detail editor.

### 11.1 Selection states

Inspector has three sequence states:

1. Selected clip.
2. Selected track.
3. No selected timeline item.

Priority:

```text
Clip selection > Track selection > Sequence settings
```

### 11.2 Selected clip Inspector

When a clip is selected, Inspector shows:

- Header: `Selected Clip`
- Clip display name/type.
- Clip timing fields.
- Clip type-specific config.

It should not show:

- The whole track.
- The whole sequence.
- Raw SerializeReference managed IDs as user-facing labels.

### 11.3 Selected track Inspector

When a track is selected, Inspector shows:

- Header: `Selected Track`
- Track type.
- Phase.
- Display name.
- Muted.
- Locked.
- Collapsed.

It should not expand and edit the track's clips by default.

Clip editing belongs to clip selection.

### 11.4 No timeline item selected

When no track or clip is selected, Inspector shows normal ActionAsset fields and Sequence settings.

Sequence settings include:

- frameRate
- durationFrames

Raw data belongs in a collapsed debug foldout.

### 11.5 Debug Raw Sequence Data

Debug foldout exists as an escape hatch.

It may show:

- raw `tracks`
- legacy raw `clips`

It must be collapsed by default.

The ordinary workflow should not require opening this foldout.

## 12. Selection Bridge Rules

Because clips and tracks are plain SerializeReference objects, Unity cannot select them as independent Project assets.

v1 uses a selection bridge:

- Stores target asset.
- Stores selection kind: none, track, clip.
- Stores track index.
- Stores clip index.
- Stores SerializedProperty paths when available.
- Sets `Selection.activeObject` to the owning asset.

This is acceptable for v1.

Do not introduce proxy ScriptableObjects unless these requirements become impossible:

- stable Inspector drawing
- Undo/Redo
- multi-window selection
- context operations

Proxy objects are a larger architecture decision, not a small fix.

## 13. Undo/Redo Rules

Undo/Redo must cover:

- Add Track.
- Delete Track.
- Reorder Track.
- Rename Track.
- Toggle mute/lock/collapse.
- Add Clip.
- Delete Clip.
- Edit Clip fields.
- Edit Sequence timing.

Rules:

- Use `Undo.RecordObject` or equivalent before mutation.
- Use SerializedObject/SerializedProperty for inspector/window mutations where possible.
- After structural deletion, clear invalid selection.
- Avoid continuing to draw invalid SerializedProperty references after deletion in the same GUI event.

Undo/Redo is a correctness requirement because this is an authoring tool.

## 14. Normalize and Migration Rules

`Normalize()` is allowed to enforce invariants.

It may:

- Clamp frameRate and durationFrames.
- Ensure lists are non-null.
- Clamp clip frame ranges.
- Migrate legacy flat clips into legal tracks.

It must not:

- Recreate default tracks after the user deletes them.
- Silently reorder user tracks.
- Silently create editor convenience objects except during first initialization or legacy migration.
- Change Track display names after user rename.
- Hide data loss from invalid migrations.
- Silently remove null, missing-type, or illegal Track/Clip entries; validation and explicit repair own structural cleanup.

Migration should be explicit and predictable.

If migration cannot place a clip legally, prefer a visible warning or debug path over silent deletion.

## 15. LegacyTimeline Compatibility

LegacyTimeline remains valid.

Rules:

- Existing ActionAsset with `LegacyTimeline` opens Unity Timeline.
- Existing TimelineAsset fields remain visible and editable.
- Sequence editor should not become the main editor for LegacyTimeline assets.
- Sequence data may exist on the asset for serialization compatibility, but should not dominate the Inspector unless backend is Sequence.

The upgrade path is opt-in.

## 16. Implementation Phases

### Phase 1: Stabilize v1 language

Deliver:

- Correct track/clip data model.
- Correct default track initialization.
- Correct Inspector selection language.
- Legal Add Track/Add Clip menus.
- Stable Undo/Redo for structural edits.
- Runtime ordering tests.

Exit criteria:

- User can author a simple action without opening Debug Raw Sequence Data.
- User can delete all tracks and they stay deleted.
- User can tell why a clip type appears or does not appear in a menu.

### Phase 2: Basic timeline editing

Deliver:

- Drag clips horizontally.
- Resize clip start/end.
- Frame snapping.
- Visual frame feedback while dragging.
- Keyboard delete.
- Duplicate clip.
- Move clip to compatible track.

Exit criteria:

- Common timing edits do not require Inspector numeric fields.

### Phase 3: Preview

Deliver:

- Runtime-like preview pass.
- Muted tracks skipped in preview.
- Animancer preview if safe.
- HitBox preview gizmos if safe.
- Event/debug overlay per frame.

Exit criteria:

- Preview is useful but cannot be mistaken for authoritative combat simulation unless it uses the same runtime scheduler.

### Phase 4: Production authoring polish

Deliver:

- Multi-select.
- Copy/paste.
- Search/filter.
- Track color/icon.
- Validation panel.
- Warnings for invalid/missing config.
- Better layout/scroll/zoom.

Exit criteria:

- Tool is usable for real action production, not just prototype validation.

## 17. Acceptance Checklist for Current Work

Before continuing feature work, the current editor should pass this checklist:

- New Sequence ActionAsset gets default common tracks once.
- Deleting all tracks keeps zero tracks.
- Add Track can create multiple tracks of same class.
- Track rename persists.
- Track reorder persists.
- Track delete deletes contained clips.
- Locked track cannot be structurally edited.
- Muted track is skipped by runtime.
- Add Clip menu only shows legal clip types for that track.
- Clip labels show readable display names.
- Clicking Track shows only Track settings in Inspector.
- Clicking Clip shows only Clip config in Inspector.
- Debug Raw Sequence Data is collapsed by default.
- Runtime ordering matches `Phase -> Track order -> startFrame -> clip order`.
- LegacyTimeline asset double-click still opens Unity Timeline.

## 18. Design Decisions That Should Not Be Reopened Casually

These are current decisions. Reopen only with a concrete failure case.

- Track classes are strong types, not enum rows.
- Clip classes remain SerializeReference C# objects.
- Clips are not ScriptableObject subassets in v1.
- No editor proxy object in v1.
- Timeline window does not draw deep config.
- Inspector is the primary config editor.
- Runtime is fixed-frame and independent from Unity Timeline runtime.

## 19. Open Questions

These are not blockers for v1, but they need decisions before v2/v3:

- Should overlapping clips on the same track be allowed?
- If overlaps are allowed, are they parallel, override, or invalid?
- Should Cleanup be a real clip track or a derived runtime phase?
- Should Track order override Phase, or should Phase always dominate Track order?
- Should sequence timing be editable only from toolbar, only Inspector, or both?
- Should clip timing fields live at top of every clip Inspector regardless of clip type?
- Should invalid legacy migration preserve bad clips in a quarantine/debug list?
- Should ActionSequence eventually become the only backend, or remain side-by-side with LegacyTimeline indefinitely?

## 20. Reference Material

Unity documentation used as UX reference:

- Timeline overview: separates Timeline Asset data from Timeline instance scene bindings.  
  https://docs.unity3d.com/cn/2018.3/Manual/TimelineOverview.html
- Timeline Editor window: describes how the window changes based on selected Timeline asset/instance.  
  https://docs.unity3d.com/es/2018.4/Manual/TimelineEditorWindow.html
- Timeline Inspector properties: selected Timeline asset, track, clip, or marker changes what Inspector displays.  
  https://docs.unity.cn/Packages/com.unity.timeline%401.8/manual/insp-overview.html
- Track Header: track identity, name, lock, mute, and more menu live in the header.  
  https://docs.unity.cn/Packages/com.unity.timeline%401.8/manual/trk-header.html
- Lock and mute tracks: lock prevents editing; mute disables effect while remaining editable.  
  https://docs.unity.cn/Packages/com.unity.timeline%401.8/manual/trk-lock-mute.html
- Positioning clips: clips can move only within compatible track constraints.  
  https://docs.unity.cn/2018.1/Documentation/Manual/TimelinePositioningClips.html
