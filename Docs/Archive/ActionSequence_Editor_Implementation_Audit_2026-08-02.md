# ActionSequence Editor Implementation Audit

> Date: 2026-08-02  
> Audited against: [ActionSequence Editor Design Spec](ActionSequence_Editor_Design_Spec.md)  
> Scope: current `Assets/Scripts/ActionSequence`, `ActionAsset` backend integration, editor window, inspector, runtime tests  
> Result: data model direction is correct; editor usability is not yet aligned with the design spec

> Status note: This document is a prototype implementation snapshot. The approved replacement architecture is [ActionSequence Editor V2 Architecture](ActionSequence_Editor_V2_Architecture.md).

> Update: P0 repair passes were applied after this audit. `Normalize()` no longer silently deletes illegal clips, locked tracks are treated as read-only in the current editor paths, selected Track/Clip Inspector noise was reduced, Add Track reflection now filters to safe creatable track types, and the editor window now has a basic timeline viewport with vertical track scroll plus horizontal frame zoom/scroll.

> Update: The first v2 timeline editing pass is now implemented in the editor window: clips can be moved horizontally within their current track, resized from the left/right edge, and display frame feedback while editing. Cross-track dragging, multi-select, and copy/paste remain intentionally out of scope.

> Update: Timeline language was expanded with fixed/auto duration modes, minor and major frame ticks, per-track frame grid lines, Ctrl/Command + mouse wheel timeline zoom, and a Fit button. Auto duration uses the maximum clip end frame for runtime while the editor keeps a minimum visible working range.

> Update: The main `ActionSequenceEditorWindow` was migrated from IMGUI `OnGUI` drawing to a UI Toolkit `CreateGUI` window. The UI Toolkit pass preserves the current core loop: toolbar, duration/zoom/fit controls, dynamic track rows, ruler/grid, clip blocks, selection bridge, Add Track/Add Clip menus, locked-track guards, and same-track clip move/resize through pointer capture. This is now the forward path; the old IMGUI surface is no longer the primary implementation.

## 1. Executive Summary

The current implementation has the right architectural direction:

- `tracks -> clips` exists.
- Track type controls allowed clip types.
- Runtime reads tracks and clips.
- Runtime sorting is deterministic.
- Sequence editor is no longer a raw property editor.
- Inspector selection bridge exists.
- LegacyTimeline double-click still routes to Unity Timeline.

But the editor is still not a coherent Timeline-like authoring tool. It is a minimal visual surface over serialized data.

The main problem is not one bug. The problem is that several important rules from the design spec are only partially implemented:

- Track/Clip editing is not yet safe enough.
- Lock semantics are incomplete.
- Inspector selection language is too noisy.
- Timeline viewport has no scroll/zoom foundation.
- Normalize can silently delete invalid clip data.
- Reflection menus can expose track classes that are not safe to instantiate.
- There is no editor validation layer.

Do not add drag/resize/preview features before the P0 items below are fixed. Those features would sit on unstable editor semantics.

## 2. What Currently Matches the Spec

### 2.1 Data model: mostly aligned

Current:

- `ActionSequenceData` owns `tracks`.
- Tracks own `clips`.
- Legacy flat `clips` remains hidden/debug.
- Track classes are strong types:
  - `ActionSequenceStateTrack`
  - `ActionSequenceAnimationTrack`
  - `ActionSequenceMotionTrack`
  - `ActionSequenceHitBoxTrack`
  - `ActionSequenceCleanupTrack`
- Each track exposes `Phase` and `AllowedClipTypes`.

This matches the spec's core model.

### 2.2 Default tracks: fixed after the latest correction

Current:

- `defaultTracksInitialized` prevents default tracks from being recreated after the user deletes all tracks.
- New empty sequences can still receive default tracks once.

This now matches the spec.

### 2.3 Runtime ordering: aligned

Current runtime builds records from:

```text
data.Tracks -> track.Clips
```

Sorting is:

```text
track.Phase -> trackIndex -> clip.StartFrame -> clipIndex
```

This matches the spec.

### 2.4 Muted track runtime behavior: aligned

Current runtime skips muted tracks.

This matches the spec.

### 2.5 LegacyTimeline routing: aligned

Current:

- `ActionAssetHelper.OnOpenActionAsset` opens ActionSequence editor only when `actionAsset.UsesSequence`.
- LegacyTimeline assets still open Unity Timeline.

This matches the spec.

## 3. P0 Issues: Fix Before Further Feature Work

### P0-1: `Normalize()` can silently delete clip data

Current implementation:

```csharp
if (clip.Phase != Phase || !AllowsClipType(clip.GetType()))
{
    clips.RemoveAt(i);
    continue;
}
```

Problem:

- If a clip is under the wrong track, it is deleted.
- If a future legacy clip migrates into a track that does not allow it, it is deleted.
- If a developer changes `AllowedClipTypes`, existing assets can lose data during `Normalize()`.
- There is no warning, quarantine list, or debug record.

Spec violation:

- The spec says migration/normalization should not hide data loss.
- Invalid migration should prefer visible warning/debug path over silent deletion.

Required fix:

- Do not delete illegal clips silently.
- Introduce a validation result or editor-only warning path.
- Prefer marking invalid clips visually in Debug/Inspector first.
- If deletion is needed, make it an explicit repair action.

Recommended v1 rule:

```text
Normalize clamps safe scalar invariants.
Validate reports structural errors.
Repair performs destructive cleanup only by explicit command.
```

### P0-2: Lock semantics are incomplete

Spec rule:

Locked track means:

- Cannot add clips.
- Cannot delete clips.
- Cannot delete the track.
- Cannot reorder the track.
- Cannot edit timing/config of clips on that track.

Current:

- Add Clip is disabled in the track header.
- Track move/delete is disabled in the menu.
- Clip delete is disabled in context menu.

But still allowed:

- Rename locked track in the track header.
- Toggle muted/collapsed on a locked track.
- Edit locked track settings in Inspector.
- Edit selected clip config in Inspector even if its owning track is locked.
- Right-click/select clips on locked track.

This is inconsistent. It makes "Lock" look functional while still allowing meaningful edits.

Required fix:

- Centralize lock checks.
- Inspector must know whether selected clip belongs to a locked track.
- Disable clip config fields when owning track is locked.
- Decide whether locked tracks can still be selected. Unity Timeline generally prevents editing locked track contents; selection behavior should be explicit in our spec/implementation.

### P0-3: Inspector selection language is still too noisy

Current:

- Selected Track/Clip appears at the top.
- Then the full ActionAsset inspector continues below.

Problem:

- When the user clicks a Clip, the Inspector still feels like "ActionAsset with a selected clip section", not "Clip Inspector".
- This is exactly the confusion that started the earlier discussion.
- Unity Timeline's successful language is that the Inspector reflects the selected timeline object first.

Spec conflict:

The spec says selection priority is:

```text
Clip selection > Track selection > Sequence settings
```

Current implementation uses:

```text
Selected clip/track section + full ActionAsset fields
```

Required fix:

- When a clip is selected, make Inspector focus on clip config first and collapse/secondary-display ActionAsset fields.
- When a track is selected, focus on track config first and collapse/secondary-display ActionAsset fields.
- Consider an `ActionAsset` foldout below selected item, collapsed by default.

Recommended v1 layout:

```text
Selected Clip
  Type
  Timing
  Config

Owning Sequence
  Frame Rate
  Duration

ActionAsset Settings [collapsed]
Debug Raw Sequence Data [collapsed]
```

### P0-4: Add Track reflection can expose unsafe track types

Current:

```csharp
TypeCache.GetTypesDerivedFrom<ActionSequenceTrackDefinition>()
```

Filters:

- non-abstract
- non-generic

Problem:

- It does not require public visibility.
- It does not require a parameterless constructor.
- It does not require `[Serializable]`.
- It can discover editor/test/internal track classes.
- `CreateTrack` uses `Activator.CreateInstance(trackType)` and can fail at runtime.

The current tests define a nested `ProbeTrackDefinition` with a non-default constructor. Depending on Unity's TypeCache behavior and assembly loading, this kind of type can appear in reflection results and break the menu.

Required fix:

Filter track types by:

- non-abstract
- non-generic
- serializable
- public or intentionally editor-visible
- has public parameterless constructor
- not nested private test/helper type

Recommended:

Introduce an explicit editor registry method:

```csharp
ActionSequenceTrackTypeCache.GetCreatableTrackTypes()
```

and test it.

### P0-5: Timeline viewport lacks scroll foundation

Current:

- Track rows are drawn into a fixed available rect.
- No vertical scroll.
- No horizontal scroll.
- No zoom.
- Track height is fixed.

Problem:

- With enough tracks, the editor becomes unusable.
- With long duration, clips become too narrow.
- With short duration, timeline wastes space.
- This blocks real authoring before drag/resize is even considered.

Spec conflict:

The editor is supposed to be a timeline surface. A timeline surface needs a viewport model.

Required fix before adding drag/resize:

- Add vertical scroll for tracks.
- Add horizontal scale/zoom model for frames.
- Add timeline content rect independent from window rect.
- Keep playhead/ruler aligned with scroll/zoom.

This is a structural UI foundation, not polish.

## 4. P1 Issues: Required for v1 Completion

### P1-1: Track header does not clearly show track type identity

Current:

- Header shows editable display name.
- Color is based on phase in clip lane, not strongly represented in header.

Problem:

- If user renames "Animation" to "Upper Body", type identity disappears.
- Add Clip legality then becomes invisible until menu is opened.

Required fix:

- Show track type label/icon/accent separately from display name.
- Example:

```text
[Animation] Upper Body     M L + x ...
```

### P1-2: Clip config Inspector still relies on raw `PropertyField` shape

Current:

```csharp
EditorGUILayout.PropertyField(clipProperty, label, true);
```

Problem:

- This is simple, but not a designed clip Inspector.
- Timing fields are mixed with config fields.
- Type label and display name are not separated.
- If SerializeReference drawer renders type selector/details unexpectedly, user language can degrade again.

Required fix:

- Draw a standard clip header:
  - Type
  - Display Name
  - Start Frame
  - End Frame
  - Duration
- Then draw type-specific fields.

This keeps all clip editors consistent.

### P1-3: Track/Clip selection can become stale after Undo/Redo or structural changes

Current:

- Selection bridge stores target, index, and property path.
- Move/delete clears selection in direct operations.

Risk:

- Undo/Redo can restore/delete/reorder arrays without notifying the selection bridge.
- A stale index can point to a different track/clip.
- Property path fallback can resolve to an unintended object after array changes.

Required fix:

- Listen to `Undo.undoRedoPerformed`.
- Validate selected object identity after undo/redo.
- Prefer a stable serialized id on tracks/clips if selection stability matters.

### P1-4: `AddClip` API does not enforce track legality

Current:

```csharp
public void AddClip(ActionSequenceClipDefinition clip)
{
    clips.Add(clip);
}
```

Problem:

- The editor menu restricts types, but the data API does not.
- Invalid clips can enter through migration, tests, debug foldout, or future code.
- `Normalize()` later deletes them silently, which is worse.

Required fix:

- Replace or supplement with:

```csharp
bool CanAddClip(ActionSequenceClipDefinition clip)
bool TryAddClip(ActionSequenceClipDefinition clip)
```

- Make direct unsafe add editor-only/debug-only if needed.

### P1-5: Null clips are not removed or reported consistently

Current:

```csharp
if (clip == null)
    continue;
```

Problem:

- Null entries remain in track clip lists.
- Runtime skips them, but editor/debug data can accumulate holes.

Spec allows removing null clips, but this should be explicit and safe.

Required fix:

- Either remove null clips in Normalize with a clear rule, or report them in validation.

### P1-6: Runtime initialization mutates source data

Current:

```csharp
data.Normalize();
definition.NormalizeFrames(data.DurationFrames);
```

Problem:

- Runtime initialization changes the data object.
- In editor/play mode this can dirty assets or hide authoring mistakes.
- Runtime should ideally consume validated data, not repair authoring data silently.

Required fix:

- Decide whether runtime is allowed to normalize source assets.
- Preferred:
  - Editor/import path normalizes/clamps.
  - Runtime validates and builds records without mutating source data.

### P1-7: Editor validation surface is missing

Current:

- Invalid state is either silently corrected or not shown.

Missing validation examples:

- Track contains illegal clip type.
- Clip has missing required config.
- Animancer clip has no transition asset.
- HitBox clip has no config/effects.
- Duration/frame rate abnormal.
- Track has null clip.

Required fix:

- Add validation model and display warnings in:
  - Track header
  - Clip block
  - Inspector

This is necessary before production authoring.

### P1-8: Tests cover runtime but not editor rules

Current tests cover:

- frame 0 and half-open interval
- phase ordering
- tick accumulator
- cancel behavior
- runtime instance state separation
- legacy migration
- default tracks not recreated after deletion
- track type permission
- same-phase track order
- muted track runtime skip

Missing tests:

- reflection menu only returns creatable track types
- invalid clip is not silently lost
- locked track cannot be edited/deleted
- selection bridge clears/validates after structural changes
- create clip menu only shows legal types per track

Some of these may need EditMode tests against editor-only utility methods rather than full GUI tests.

## 5. P2 Issues: Important but Not Blocking v1 Stabilization

### P2-1: No keyboard shortcuts

Missing:

- Delete selected clip/track.
- Mute selected track.
- Lock selected track.
- Frame stepping.

This is usability work after v1 semantics stabilize.

### P2-2: No ruler scrubbing

Current:

- Current frame can be typed.
- Playhead is visual only.

Missing:

- Click/drag ruler to scrub.
- Click lane to move playhead.

This should come after scroll/zoom foundation.

### P2-3: Preview controls are visually present but not semantically meaningful

Current:

- Play/Pause/Stop only advances the playhead.
- It does not evaluate runtime records.
- It does not preview Animancer, HitBox, or tags.

This is acceptable as a placeholder only if clearly understood as "playhead preview".

Do not expand preview until runtime/editor validation is stable.

### P2-4: Visual design is still placeholder

Current:

- Rect-based immediate mode drawing.
- Minimal color coding.
- No icons.
- No warning marks.
- No row selection affordance beyond color.

This can wait, but it contributes to the feeling that the editor is not yet a tool.

## 6. Current Acceptance Checklist Status

| Spec acceptance item | Status | Notes |
| --- | --- | --- |
| New Sequence ActionAsset gets default common tracks once | Partial | Data logic exists; needs Unity verification. |
| Deleting all tracks keeps zero tracks | Implemented | Test added, but Unity test run blocked by open editor instance. |
| Add Track can create multiple tracks of same class | Partial | Should work, but reflection filtering is unsafe. |
| Track rename persists | Partial | SerializedProperty edit exists; needs Unity verification. |
| Track reorder persists | Partial | Move Up/Down exists; needs Unity verification. |
| Track delete deletes contained clips | Implemented | Deleting track removes nested list. |
| Locked track cannot be structurally edited | Partial | Some structural operations blocked; Inspector edits still allowed. |
| Muted track is skipped by runtime | Implemented | Runtime and test exist. |
| Add Clip menu only shows legal clip types for that track | Implemented for built-in tracks | Depends on `AllowedClipTypes`; needs tests. |
| Clip labels show readable display names | Implemented | Uses `clip.GetDisplayName()`. |
| Clicking Track shows only Track settings in Inspector | Partial | Track section is focused, but ActionAsset fields still continue below. |
| Clicking Clip shows only Clip config in Inspector | Partial | Clip section exists, but ActionAsset fields still continue below. |
| Debug Raw Sequence Data is collapsed by default | Implemented | Foldout defaults false. |
| Runtime ordering matches `Phase -> Track order -> startFrame -> clip order` | Implemented | Runtime and test exist. |
| LegacyTimeline asset double-click still opens Unity Timeline | Implemented | Routing exists. |

## 7. Recommended Repair Order

Do not start with drag/resize.

Recommended order:

1. Separate Normalize / Validate / Repair.
   - Stop silent clip deletion.
   - Add validation warnings.
2. Fix lock semantics.
   - Make locked track truly read-only in window and Inspector.
3. Fix Inspector language.
   - When Clip/Track selected, make it feel like the selected object's Inspector, not full ActionAsset plus a small section.
4. Harden Add Track reflection.
   - Only show safe creatable track types.
5. Add scroll/viewport model.
   - Vertical scroll first.
   - Then horizontal scale/zoom.
6. Add editor utility tests.
   - Creatable track type filtering.
   - Default track deletion behavior.
   - Validation behavior.
   - Runtime ordering stays unchanged.

After these six items, the editor will have a stable v1 base. Then drag/resize becomes a reasonable next investment.

## 8. Immediate Code Targets

Likely files for next repair pass:

- `Assets/Scripts/ActionSequence/ActionSequenceData.cs`
  - Split Normalize/Validate/Repair responsibility.
- `Assets/Scripts/ActionSequence/ActionSequenceTrackDefinition.cs`
  - Add safe clip add/validation APIs.
- `Assets/Scripts/ActionSequence/Editor/ActionSequenceEditorSelection.cs`
  - Harden reflection filtering and selection validation.
- `Assets/Scripts/ActionSequence/Editor/ActionSequenceEditorWindow.cs`
  - Lock behavior, viewport/scroll, safer UI state.
- `Assets/Scripts/ActionSequence/Editor/ActionSequenceAssetInspectors.cs`
  - Inspector selection language and locked read-only display.
- `Assets/Tests/Editor/ActionSequenceRuntimeTests.cs`
  - Keep runtime tests.
- New editor utility test file
  - Track type discovery and validation tests.

## 9. Validation Status

Unity EditMode tests have not been successfully run in this session because Unity batchmode was blocked by another Unity instance already having the project open.

Manual validation still required:

- Open Sequence ActionAsset.
- Delete all tracks and confirm they stay deleted after selection changes and domain reload.
- Add every built-in track type.
- Add legal clips from each track.
- Confirm illegal clip types are not offered.
- Confirm locked track cannot be edited once lock semantics are fixed.
- Confirm LegacyTimeline ActionAsset double-click still opens Unity Timeline.
