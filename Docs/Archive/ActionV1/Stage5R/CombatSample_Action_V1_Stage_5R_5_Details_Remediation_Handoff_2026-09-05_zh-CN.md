# CombatSample Action V1 — Stage 5R.5 Details Remediation Handoff

> Status: development checks passed; Unity manual acceptance is still required.
>
> Date: 2026-09-05

> Acceptance update: user accepted the 5R.5 Details experience before Stage 5R.6 began.

## Why this remediation was needed

The first 5R.5 implementation treated UI Toolkit binding notifications as user edits.
Unity can emit these notifications while a `PropertyField` attaches or rebinds, which
caused Details to refresh its complete page during normal editing. Complex legacy
property drawers (Tag, Bone and SerializeReference-based content) were also hosted
through generic UI Toolkit binding rather than an isolated native IMGUI surface.

## Implemented correction

- A Details property watch now fingerprints serialized values and notifies other V1
  windows only after a real value change. Binding/attachment notifications are ignored.
- Conditional values rebuild only their local Trigger, Cancel Rule or Config section.
  Normal leaf edits preserve the current visual tree, focus, scroll position and foldouts.
- Tag, Bone, curves, HitBox nested data, effects, Self Tags, Entry Conditions and
  Cancel windows use local `IMGUIContainer` hosts with fresh `SerializedProperty`
  resolution per draw. Simple fields remain UI Toolkit controls.
- Timeline receives ordinary Details changes through a validation-only refresh; it no
  longer clears its content tree for every Config leaf edit. Lane name/mute changes
  explicitly request Timeline presentation refresh.
- Cancel Rule callbacks now capture their rule index correctly. Foldouts ignore nested
  value-change events. Timing drafts no longer become stale merely because Item Mute
  changes.
- Validation dependency signatures include AssetDatabase dependency hashes for the
  AnimationAsset and AnimationClip inputs.

## Development evidence

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`: passed with
  0 warnings and 0 errors.
- Scoped `git diff --check` for the changed V1 editor sources: passed.
- Repository-wide `git diff --check` still reports pre-existing trailing whitespace
  in user-owned temporary Scene and ActionAsset changes; this remediation did not edit
  those files.

## Required Unity acceptance

Verify all seven Config pages, especially Tag, Bone, Curve, HitBox and
SerializeReference list editing. Confirm ordinary leaf input keeps focus/scroll, a
conditional toggle only refreshes its local section, Timeline does not flicker on
Config edits, and Undo/Redo remains one action per intentional change. Do not begin
Stage 5R.6 until this has been accepted in Unity.

## Closure corrections

- AnimationCurve fingerprints now include wrap modes and every key's time, value,
  tangents, weights and weighted mode. Curve edits with an unchanged key count now
  propagate validation and preview notifications.
- Conditional section refreshes also refresh the Details validation summary. Timeline
  updates each existing Entry's issue classes, badge and tooltip in place when Config
  validation changes.
- Selection notifications preserve the current Details page and timing draft when the
  Primary Selection is unchanged; only the selection-count header is updated.
- The Advanced foldout now ignores value-change events bubbling from controls inside it.
- The editor assembly was rebuilt after these corrections: 0 warnings, 0 errors.
