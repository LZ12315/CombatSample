using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 运行时速度修正 token。由速度所有者创建，只能交还给同一个所有者移除。
/// </summary>
public readonly struct SpeedModifierToken : IEquatable<SpeedModifierToken>
{
    public readonly int Id;

    public bool IsValid => Id > 0;

    public SpeedModifierToken(int id)
    {
        Id = id;
    }

    public bool Equals(SpeedModifierToken other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object obj)
    {
        return obj is SpeedModifierToken other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public static readonly SpeedModifierToken Invalid = new SpeedModifierToken(0);
}

/// <summary>
/// 可重入的速度修正栈。它只保存倍率，不直接操作具体系统。
/// 常规 HitStop / SpeedVFX 使用 Min：多个效果重叠时取最慢值。
/// Buff / Debuff 可使用 Multiply：多个倍率相乘后，再被 Min 类效果覆盖。
/// </summary>
public sealed class SpeedModifierStack
{
    private struct Entry
    {
        public float Scale;
        public SpeedModifierBlendMode BlendMode;
        public string DebugName;
    }

    private readonly Dictionary<int, Entry> _entries = new();
    private int _nextId = 1;

    public float Value { get; private set; } = 1f;
    public int Count => _entries.Count;

    public SpeedModifierToken Add(
        float scale,
        SpeedModifierBlendMode blendMode = SpeedModifierBlendMode.Min,
        string debugName = null)
    {
        int id = _nextId++;

        _entries.Add(id, new Entry
        {
            Scale = SanitizeScale(scale),
            BlendMode = blendMode,
            DebugName = debugName
        });

        Recalculate();
        return new SpeedModifierToken(id);
    }

    public bool Update(
        SpeedModifierToken token,
        float scale,
        SpeedModifierBlendMode blendMode = SpeedModifierBlendMode.Min,
        string debugName = null)
    {
        if (!token.IsValid || !_entries.TryGetValue(token.Id, out var entry))
            return false;

        entry.Scale = SanitizeScale(scale);
        entry.BlendMode = blendMode;
        if (debugName != null)
            entry.DebugName = debugName;

        _entries[token.Id] = entry;
        Recalculate();
        return true;
    }

    public bool Remove(SpeedModifierToken token)
    {
        if (!token.IsValid)
            return false;

        bool removed = _entries.Remove(token.Id);
        if (removed)
            Recalculate();

        return removed;
    }

    public void Clear()
    {
        if (_entries.Count == 0)
            return;

        _entries.Clear();
        Recalculate();
    }

    private void Recalculate()
    {
        if (_entries.Count == 0)
        {
            Value = 1f;
            return;
        }

        float multiplyValue = 1f;
        float minValue = 1f;
        bool hasMinEntry = false;

        foreach (var entry in _entries.Values)
        {
            switch (entry.BlendMode)
            {
                case SpeedModifierBlendMode.Multiply:
                    multiplyValue *= entry.Scale;
                    break;
                case SpeedModifierBlendMode.Min:
                    minValue = hasMinEntry ? Mathf.Min(minValue, entry.Scale) : entry.Scale;
                    hasMinEntry = true;
                    break;
            }
        }

        float result = multiplyValue;
        if (hasMinEntry)
            result = Mathf.Min(result, minValue);

        Value = SanitizeScale(result);
    }

    private static float SanitizeScale(float scale)
    {
        if (float.IsNaN(scale) || float.IsInfinity(scale))
            return 1f;

        return Mathf.Clamp(scale, 0f, 10f);
    }

#if UNITY_EDITOR
    public string GetDebugText()
    {
        if (_entries.Count == 0)
            return "No speed modifiers";

        var builder = new StringBuilder();

        foreach (var pair in _entries)
        {
            Entry entry = pair.Value;
            builder.AppendLine($"#{pair.Key} {entry.DebugName} | {entry.BlendMode} | {entry.Scale}");
        }

        builder.AppendLine($"Final: {Value}");
        return builder.ToString();
    }
#endif
}

public enum SpeedModifierBlendMode
{
    Min,
    Multiply
}
