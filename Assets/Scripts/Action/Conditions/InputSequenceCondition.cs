using System;
using System.Collections.Generic;
using UnityEngine;

public enum InputEventType { ButtonPressed = 0, ButtonReleased = 1, DirectionEntered = 2 }

[Serializable]
public struct InputEventClip
{
    public InputEventType type;
    public Enums.InputButton button;
    [Tooltip("Enter a direction, not merely hold it. Neutral means return to center.")]
    public InputDirection direction;

    internal bool IsValid => type == InputEventType.DirectionEntered
        ? direction >= InputDirection.Neutral && direction <= InputDirection.Right
        : (type == InputEventType.ButtonPressed || type == InputEventType.ButtonReleased)
            && PlayerInputBuffer.IsButton(button);

    internal bool Matches(PlayerInputEvent input, InputDirection entered)
    {
        if (type == InputEventType.DirectionEntered) return input.IsMove && direction == entered;
        return !input.IsMove && input.Button == button && (int)input.Kind == (int)type;
    }
}

/// <summary>Ordered physical events only. Intervening records are allowed; states belong outside.</summary>
[Serializable]
public sealed class InputSequenceCondition : PlayerInputCondition
{
    [Tooltip("Whole sequence span in real seconds, not an added delay or a per-step timeout. Supported range: 0–2 seconds.")]
    [Range(0f, 2f)] public float maxSequenceTime = 0.5f;
    public InputEventClip[] clips = Array.Empty<InputEventClip>();
    public override bool ConsumesInput => true;

    protected override bool IsInputConfigurationValid
    {
        get
        {
            if (float.IsNaN(maxSequenceTime) || float.IsInfinity(maxSequenceTime)
                || maxSequenceTime < 0f || maxSequenceTime > PlayerInputBuffer.MaxContextSeconds
                || clips == null || clips.Length == 0) return false;
            foreach (InputEventClip clip in clips) if (!clip.IsValid) return false;
            return true;
        }
    }

    private readonly struct Record
    {
        public readonly PlayerInputEvent Input;
        public readonly InputDirection Direction;
        public Record(PlayerInputEvent input, InputDirection direction = default) { Input = input; Direction = direction; }
    }

    internal override bool OnCheckInput(PlayerInputBuffer input, PlayerInputDirections directions, out PlayerInputMatch match)
    {
        match = default;
        bool needsDirections = false;
        foreach (InputEventClip clip in clips) needsDirections |= clip.type == InputEventType.DirectionEntered;
        if (needsDirections && (directions == null || !directions.IsCurrentFor(input) || !directions.HasReference)) return false;

        var records = new List<Record>();
        foreach (PlayerInputEvent item in input.Events)
            if (input.IsAvailable(item) && (!needsDirections || directions.IsInReference(item))) records.Add(new Record(item));
        if (needsDirections)
            foreach (PlayerInputDirections.Entry entry in directions.Events)
                if (input.IsAvailable(entry.Source)) records.Add(new Record(entry.Source, entry.Direction));
        records.Sort((a, b) => PlayerInputBuffer.Compare(a.Input.Time, a.Input.Id, b.Input.Time, b.Input.Id));

        // Oldest fresh terminal first, preserving rapid clicks as distinct requests.
        for (int end = 0; end < records.Count; end++)
        {
            Record terminal = records[end];
            if (input.Now - terminal.Input.Time > input.BufferSeconds + PlayerInputBuffer.TimeEpsilon
                || !clips[clips.Length - 1].Matches(terminal.Input, terminal.Direction)) continue;
            var selected = new PlayerInputEventMatch[clips.Length];
            double validUntil = terminal.Input.Time + input.BufferSeconds;
            selected[selected.Length - 1] = new PlayerInputEventMatch(input, terminal.Input, validUntil);
            int position = end - 1;
            int step = clips.Length - 2;
            // Latest predecessor at each step maximizes the first event's time.
            // Therefore, if this chain exceeds the span, no earlier chain for this terminal can fit.
            for (; step >= 0; step--)
            {
                while (position >= 0 && !clips[step].Matches(records[position].Input, records[position].Direction)) position--;
                if (position < 0) break;
                selected[step] = new PlayerInputEventMatch(input, records[position].Input, validUntil);
                position--;
            }
            if (step >= 0 || terminal.Input.Time - selected[0].Input.Time > maxSequenceTime + PlayerInputBuffer.TimeEpsilon) continue;
            match = new PlayerInputMatch(selected, needsDirections ? directions : null);
            return match.IsCurrentFor(input);
        }
        return false;
    }
}
