#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A transient Unity serialization host, never an asset. Unity handles polymorphism, nested graphs,
// curves and Object references; the clipboard does not implement its own object serializer.
internal sealed class AuthoringListClipboardPayload : ScriptableObject
{
    [SerializeReference] internal List<ActionCondition> conditions = new();
    [SerializeField] internal List<CancelRule> rules = new();
    [SerializeReference] internal List<ImpactEffectConfig> effects = new();

    internal IList Items(AuthoringListClipboard.Kind kind) => kind switch
    {
        AuthoringListClipboard.Kind.Conditions => conditions,
        AuthoringListClipboard.Kind.CancelRules => rules,
        AuthoringListClipboard.Kind.Effects => effects,
        _ => null,
    };
}
#endif
