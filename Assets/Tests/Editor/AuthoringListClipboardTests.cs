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
        value.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        _objects.Add(value);
        return value;
    }

    private static void SetEntryConditions(ActionAsset action, params ActionCondition[] conditions)
    {
        using var serialized = new SerializedObject(action);
        SerializedProperty list = serialized.FindProperty("_entryConditions");
        list.arraySize = conditions.Length;
        for (int i = 0; i < conditions.Length; i++)
            list.GetArrayElementAtIndex(i).managedReferenceValue = conditions[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
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
        SetEntryConditions(source, condition, null, new AlwaysTrueCondition(), condition);
        using var from = new SerializedObject(source);
        using var toFirst = new SerializedObject(first);
        using var toSecond = new SerializedObject(second);
        string before = EditorJsonUtility.ToJson(source);
        bool wasDirty = EditorUtility.IsDirty(source);
        Assert.That(AuthoringListClipboard.Copy(from.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out string error), Is.True, error);
        Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(before));
        Assert.That(EditorUtility.IsDirty(source), Is.EqualTo(wasDirty));
        // The source graph is live serialized data, not necessarily the object passed into SetEntryConditions.
        condition = (ClipboardGraphCondition)source.EntryConditions[0];
        condition.values[0] = 99;
        condition.curve.keys = AnimationCurve.Constant(0f, 1f, 99f).keys;
        Assert.That(AuthoringListClipboard.Paste(toFirst.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out error), Is.True, error);
        Assert.That(first.EntryConditions.Count, Is.EqualTo(4));
        var a = (ClipboardGraphCondition)first.EntryConditions[0];
        Assert.That(a, Is.Not.SameAs(condition));
        Assert.That(first.EntryConditions[1], Is.Null);
        Assert.That(first.EntryConditions[2], Is.TypeOf<AlwaysTrueCondition>());
        Assert.That(first.EntryConditions[3], Is.SameAs(a));
        Assert.That(a.link, Is.SameAs(a));
        Assert.That(a.reference, Is.SameAs(reference));
        Assert.That(a.HiddenValue, Is.EqualTo(31));
        Assert.That(a.values, Is.EqualTo(new[] { 3, 7 }));
        Assert.That(a.curve.Evaluate(0.5f), Is.EqualTo(3f).Within(0.001f));
        a.values[0] = -1;
        a.curve.keys = AnimationCurve.Constant(0f, 1f, -1f).keys;
        // Entry and Exit are the same clipboard family; their runtime semantics remain unchanged.
        Assert.That(AuthoringListClipboard.Paste(toSecond.FindProperty("_exitConditions"), AuthoringListClipboard.Kind.Conditions, out error), Is.True, error);
        var b = (ClipboardGraphCondition)second.ExitConditions[0];
        Assert.That(b, Is.Not.SameAs(a));
        Assert.That(b.values, Is.EqualTo(new[] { 3, 7 }));
        Assert.That(b.curve.Evaluate(0.5f), Is.EqualTo(3f).Within(0.001f));
        Assert.That(condition.values[0], Is.EqualTo(99));
        Assert.That(condition.curve.Evaluate(0.5f), Is.EqualTo(99f).Within(0.001f));
    }

    [Test]
    public void CancelRules_CopyPreservesConfigurationAndResourcesWithoutSharingMutableData()
    {
        var source = Create<ActionAsset>();
        var target = Create<ActionAsset>();
        var reference = Create<ActionAsset>();
        source.EditorCancelRules.Add(new CancelRule
        {
            targetKind = CancelTargetKind.SpecificAction,
            specificTarget = reference,
            locomotionIntentThreshold = 0.4f,
            window = new CancelWindow { startAnchor = FrameAnchor.Custom, startFrame = 3, endAnchor = FrameAnchor.Custom, endFrame = 19 },
        });
        using var from = new SerializedObject(source);
        using var to = new SerializedObject(target);
        Assert.That(AuthoringListClipboard.Copy(from.FindProperty("_cancelRules"), AuthoringListClipboard.Kind.CancelRules, out string error), Is.True, error);
        source.EditorCancelRules[0].window.endFrame = 27;
        Assert.That(AuthoringListClipboard.Paste(to.FindProperty("_cancelRules"), AuthoringListClipboard.Kind.CancelRules, out error), Is.True, error);
        CancelRule rule = target.CancelRules[0];
        Assert.That(rule, Is.Not.SameAs(source.CancelRules[0]));
        Assert.That(rule.targetKind, Is.EqualTo(CancelTargetKind.SpecificAction));
        Assert.That(rule.specificTarget, Is.SameAs(reference));
        Assert.That(rule.locomotionIntentThreshold, Is.EqualTo(0.4f));
        Assert.That(rule.window.startAnchor, Is.EqualTo(FrameAnchor.Custom));
        Assert.That(rule.window.endAnchor, Is.EqualTo(FrameAnchor.Custom));
        Assert.That(rule.window.startFrame, Is.EqualTo(3));
        Assert.That(rule.window.endFrame, Is.EqualTo(19));
        rule.window.endFrame = 35;
        Assert.That(source.CancelRules[0].window.endFrame, Is.EqualTo(27));
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
        SetEntryConditions(source, new VelocityCondition { threshold = 4f });
        SetEntryConditions(target, new AlwaysTrueCondition());
        target.EditorCancelRules.Add(new CancelRule());
        using var from = new SerializedObject(source);
        using var to = new SerializedObject(target);
        from.FindProperty("_entryConditions").GetArrayElementAtIndex(0).FindPropertyRelative("threshold").floatValue = 8f;
        Assert.That(AuthoringListClipboard.Copy(from.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out string error), Is.True, error);
        to.FindProperty("_cancelRules").GetArrayElementAtIndex(0).FindPropertyRelative("locomotionIntentThreshold").floatValue = 0.3f;
        Assert.That(AuthoringListClipboard.Paste(to.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out error), Is.True, error);
        Assert.That(((VelocityCondition)target.EntryConditions[0]).threshold, Is.EqualTo(8f));
        Assert.That(target.CancelRules[0].locomotionIntentThreshold, Is.EqualTo(0.3f));
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.That(target.EntryConditions[0], Is.TypeOf<AlwaysTrueCondition>());
        Assert.That(target.CancelRules[0].locomotionIntentThreshold, Is.EqualTo(0.3f));
        Undo.PerformRedo();
        Assert.That(((VelocityCondition)target.EntryConditions[0]).threshold, Is.EqualTo(8f));
    }

    [Test]
    public void EmptyAndIncompatibleClipboards_DoNotUnexpectedlyModifyTheTarget()
    {
        var source = Create<ActionAsset>();
        var target = Create<ActionAsset>();
        SetEntryConditions(target, new AlwaysTrueCondition());
        using var from = new SerializedObject(source);
        using var to = new SerializedObject(target);
        Assert.That(AuthoringListClipboard.Copy(from.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out string error), Is.True, error);
        Assert.That(AuthoringListClipboard.CanPaste(AuthoringListClipboard.Kind.Conditions), Is.True);
        Assert.That(AuthoringListClipboard.CanPaste(AuthoringListClipboard.Kind.Effects), Is.False);
        string before = EditorJsonUtility.ToJson(target);
        Assert.That(AuthoringListClipboard.Paste(to.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Effects, out _), Is.False);
        Assert.That(EditorJsonUtility.ToJson(target), Is.EqualTo(before));
        Assert.That(AuthoringListClipboard.Paste(to.FindProperty("_entryConditions"), AuthoringListClipboard.Kind.Conditions, out error), Is.True, error);
        Assert.That(target.EntryConditions, Is.Empty);
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
