#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AuthoringListClipboardTests
{
    private readonly List<Object> _objects = new();
    private string _clipboard;

    [SetUp]
    public void SetUp() => _clipboard = EditorGUIUtility.systemCopyBuffer;

    [TearDown]
    public void TearDown()
    {
        EditorGUIUtility.systemCopyBuffer = _clipboard;
        foreach (Object value in _objects)
        {
            Undo.ClearUndo(value);
            Object.DestroyImmediate(value);
        }
        _objects.Clear();
    }

    private T Create<T>() where T : ScriptableObject
    {
        var value = ScriptableObject.CreateInstance<T>();
        value.hideFlags = HideFlags.HideAndDontSave;
        _objects.Add(value);
        return value;
    }

    [Test]
    public void Conditions_CopyCapturesAnIndependentGraphAndEachPasteCreatesAnotherCopy()
    {
        var source = Create<ActionAsset>();
        var first = Create<ActionAsset>();
        var second = Create<ActionAsset>();
        var reference = Create<ActionAsset>();
        var condition = new ClipboardGraphCondition
        {
            reference = reference,
            HiddenValue = 31,
            curve = AnimationCurve.Linear(0f, 2f, 1f, 4f),
            values = new List<int> { 3, 7 },
        };
        condition.link = condition;
        source.EditorRequirements.AddRange(new ActionCondition[] { condition, null, new AlwaysTrueCondition(), condition });
        using var from = new SerializedObject(source);
        using var toFirst = new SerializedObject(first);
        using var toSecond = new SerializedObject(second);
        string before = EditorJsonUtility.ToJson(source);
        bool wasDirty = EditorUtility.IsDirty(source);
        Assert.That(AuthoringListClipboard.Copy(from.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out string error), Is.True, error);
        Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(before));
        Assert.That(EditorUtility.IsDirty(source), Is.EqualTo(wasDirty));
        condition.values[0] = 99;
        condition.curve.keys = AnimationCurve.Constant(0f, 1f, 99f).keys;
        Assert.That(AuthoringListClipboard.Paste(toFirst.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out error), Is.True, error);
        Assert.That(first.Requirements.Count, Is.EqualTo(4));
        var a = (ClipboardGraphCondition)first.Requirements[0];
        Assert.That(a, Is.Not.SameAs(condition));
        Assert.That(first.Requirements[1], Is.Null);
        Assert.That(first.Requirements[2], Is.TypeOf<AlwaysTrueCondition>());
        Assert.That(first.Requirements[3], Is.SameAs(a));
        Assert.That(a.link, Is.SameAs(a));
        Assert.That(a.reference, Is.SameAs(reference));
        Assert.That(a.HiddenValue, Is.EqualTo(31));
        Assert.That(a.values, Is.EqualTo(new[] { 3, 7 }));
        Assert.That(a.curve.Evaluate(0.5f), Is.EqualTo(3f).Within(0.001f));
        a.values[0] = -1;
        a.curve.keys = AnimationCurve.Constant(0f, 1f, -1f).keys;
        Assert.That(AuthoringListClipboard.Paste(toSecond.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out error), Is.True, error);
        var b = (ClipboardGraphCondition)second.Requirements[0];
        Assert.That(b, Is.Not.SameAs(a));
        Assert.That(b.values, Is.EqualTo(new[] { 3, 7 }));
        Assert.That(b.curve.Evaluate(0.5f), Is.EqualTo(3f).Within(0.001f));
        Assert.That(condition.values[0], Is.EqualTo(99));
        Assert.That(condition.curve.Evaluate(0.5f), Is.EqualTo(99f).Within(0.001f));
    }

    [Test]
    public void CancelRules_CopyIncludesNestedConditionsAndResourcesWithoutSharingMutableData()
    {
        var source = Create<ActionAsset>();
        var target = Create<ActionAsset>();
        var reference = Create<ActionAsset>();
        var sequence = new InputSequenceCondition
        {
            maxSequenceTime = 0.75f,
            clips = new[] { new InputEventClip { type = InputEventType.DirectionEntered, direction = InputDirection.Right } },
        };
        source.EditorCancelRules.Add(new CancelRule
        {
            specificTarget = reference,
            window = new CancelWindow { startAnchor = FrameAnchor.Custom, startFrame = 3, endAnchor = FrameAnchor.Custom, endFrame = 19 },
            conditions = new List<ActionCondition> { sequence, null, new ActionAssetCondition { requiredAction = reference } },
        });
        using var from = new SerializedObject(source);
        using var to = new SerializedObject(target);
        Assert.That(AuthoringListClipboard.Copy(from.FindProperty("_cancelRules"), AuthoringListClipboard.Kind.CancelRules, out string error), Is.True, error);
        sequence.clips[0].direction = InputDirection.Left;
        Assert.That(AuthoringListClipboard.Paste(to.FindProperty("_cancelRules"), AuthoringListClipboard.Kind.CancelRules, out error), Is.True, error);
        CancelRule rule = target.EditorCancelRules[0];
        Assert.That(rule, Is.Not.SameAs(source.EditorCancelRules[0]));
        Assert.That(rule.specificTarget, Is.SameAs(reference));
        Assert.That(rule.window.startFrame, Is.EqualTo(3));
        Assert.That(rule.window.endFrame, Is.EqualTo(19));
        Assert.That(rule.conditions.Count, Is.EqualTo(3));
        Assert.That(rule.conditions[1], Is.Null);
        Assert.That(((ActionAssetCondition)rule.conditions[2]).requiredAction, Is.SameAs(reference));
        var copied = (InputSequenceCondition)rule.conditions[0];
        Assert.That(copied.clips[0].direction, Is.EqualTo(InputDirection.Right));
        copied.clips[0].direction = InputDirection.Neutral;
        Assert.That(sequence.clips[0].direction, Is.EqualTo(InputDirection.Left));
    }

    [Test]
    public void Effects_CopyPreservesOrderNullsAndConcreteTypes()
    {
        var source = Create<HitFeedbackProfile>();
        var target = Create<HitFeedbackProfile>();
        using var from = new SerializedObject(source);
        using var to = new SerializedObject(target);
        SerializedProperty effects = from.FindProperty("effects");
        effects.arraySize = 3;
        effects.GetArrayElementAtIndex(0).managedReferenceValue = new SpeedEffectConfig { duration = 0.2f, enabled = false };
        effects.GetArrayElementAtIndex(1).managedReferenceValue = null;
        effects.GetArrayElementAtIndex(2).managedReferenceValue = new HitVfxConfig { scale = 2.5f };
        Assert.That(AuthoringListClipboard.Copy(effects, AuthoringListClipboard.Kind.Effects, out string error), Is.True, error);
        Assert.That(AuthoringListClipboard.Paste(to.FindProperty("effects"), AuthoringListClipboard.Kind.Effects, out error), Is.True, error);
        to.Update();
        effects = to.FindProperty("effects");
        Assert.That(effects.arraySize, Is.EqualTo(3));
        var speed = effects.GetArrayElementAtIndex(0).managedReferenceValue as SpeedEffectConfig;
        Assert.That(speed, Is.Not.Null);
        Assert.That(speed.duration, Is.EqualTo(0.2f));
        Assert.That(speed.enabled, Is.False);
        Assert.That(effects.GetArrayElementAtIndex(1).managedReferenceValue, Is.Null);
        Assert.That(((HitVfxConfig)effects.GetArrayElementAtIndex(2).managedReferenceValue).scale, Is.EqualTo(2.5f));
    }

    [Test]
    public void Paste_KeepsPendingInspectorEditsAndIsOneUndoRedoOperation()
    {
        var source = Create<ActionAsset>();
        var target = Create<ActionAsset>();
        source.EditorRequirements.Add(new VelocityCondition { threshold = 4f });
        target.EditorRequirements.Add(new AlwaysTrueCondition());
        target.EditorCancelRules.Add(new CancelRule());
        using var from = new SerializedObject(source);
        using var to = new SerializedObject(target);
        from.FindProperty("_entryConditions").GetArrayElementAtIndex(0).FindPropertyRelative("threshold").floatValue = 8f;
        Assert.That(AuthoringListClipboard.Copy(from.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out string error), Is.True, error);
        to.FindProperty("_cancelRules").GetArrayElementAtIndex(0).FindPropertyRelative("locomotionIntentThreshold").floatValue = 0.3f;
        Assert.That(AuthoringListClipboard.Paste(to.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out error), Is.True, error);
        Assert.That(((VelocityCondition)target.Requirements[0]).threshold, Is.EqualTo(8f));
        Assert.That(target.EditorCancelRules[0].locomotionIntentThreshold, Is.EqualTo(0.3f));
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.That(target.Requirements[0], Is.TypeOf<AlwaysTrueCondition>());
        Assert.That(target.EditorCancelRules[0].locomotionIntentThreshold, Is.EqualTo(0.3f));
        Undo.PerformRedo();
        Assert.That(((VelocityCondition)target.Requirements[0]).threshold, Is.EqualTo(8f));
    }

    [Test]
    public void EmptyAndIncompatibleClipboards_DoNotUnexpectedlyModifyTheTarget()
    {
        var source = Create<ActionAsset>();
        var target = Create<ActionAsset>();
        target.EditorRequirements.Add(new AlwaysTrueCondition());
        using var from = new SerializedObject(source);
        using var to = new SerializedObject(target);
        Assert.That(AuthoringListClipboard.Copy(from.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out string error), Is.True, error);
        Assert.That(AuthoringListClipboard.CanPaste(AuthoringListClipboard.Kind.Conditions), Is.True);
        Assert.That(AuthoringListClipboard.CanPaste(AuthoringListClipboard.Kind.Effects), Is.False);
        string before = EditorJsonUtility.ToJson(target);
        Assert.That(AuthoringListClipboard.Paste(to.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Effects, out _), Is.False);
        Assert.That(EditorJsonUtility.ToJson(target), Is.EqualTo(before));
        Assert.That(AuthoringListClipboard.Paste(to.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out error), Is.True, error);
        Assert.That(target.Requirements, Is.Empty);
        EditorGUIUtility.systemCopyBuffer = "ordinary text";
        Assert.That(AuthoringListClipboard.CanPaste(AuthoringListClipboard.Kind.Conditions), Is.False);
        before = EditorJsonUtility.ToJson(target);
        to.Update();
        Assert.That(AuthoringListClipboard.Paste(to.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out _), Is.False);
        Assert.That(EditorJsonUtility.ToJson(target), Is.EqualTo(before));
        using var multiple = new SerializedObject(new Object[] { source, target });
        Assert.That(AuthoringListClipboard.Copy(multiple.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out _), Is.False);
        Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo("ordinary text"));
        Assert.That(EditorJsonUtility.ToJson(target), Is.EqualTo(before));
    }

    [Serializable, HideInTypeMenu]
    public sealed class ClipboardGraphCondition : ActionCondition
    {
        [SerializeField, HideInInspector] private int hiddenValue = 17;
        public int HiddenValue { get => hiddenValue; set => hiddenValue = value; }
        public ActionAsset reference;
        public AnimationCurve curve;
        public List<int> values;
        [SerializeReference] public ClipboardGraphCondition link;
        protected override bool OnCheck(Actor actor) => true;
    }
}
#endif
