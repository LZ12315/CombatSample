using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InputSequenceCondition : ActionCondition
{
    [Header("Settings")]
    [Tooltip("Max time gap between inputs.")]
    [SerializeField] private float maxInputInterval = 0.25f;

    [SerializeReference, SubclassSelector]
    private List<InputCheckBase> inputSequence = new List<InputCheckBase>();

    [NonSerialized] private readonly List<PlayerInputController.BufferedInput> _matchedInputs =
        new List<PlayerInputController.BufferedInput>(8);

    protected override bool OnCheck(Actor actor)
    {
        _matchedInputs.Clear();

        if (actor == null || inputSequence == null || inputSequence.Count == 0)
            return false;

        PlayerInputController input = PlayerInputController.Instance;
        if (input == null || !input.IsControlledActor(actor))
            return false;

        IReadOnlyList<PlayerInputController.BufferedInput> buffer = input.InputHistory;
        if (buffer.Count == 0)
            return false;

        int seqIndex = inputSequence.Count - 1;
        float lastMatchedTime = -1f;

        for (int i = buffer.Count - 1; i >= 0; i--)
        {
            PlayerInputController.BufferedInput item = buffer[i];
            if (item == null || item.IsConsumed)
                continue;

            if (lastMatchedTime >= 0f && lastMatchedTime - item.Timestamp > maxInputInterval)
            {
                _matchedInputs.Clear();
                return false;
            }

            if (!inputSequence[seqIndex].CheckInput(item.Data))
                continue;

            lastMatchedTime = item.Timestamp;
            _matchedInputs.Add(item);
            seqIndex--;

            if (seqIndex < 0)
                return true;
        }

        _matchedInputs.Clear();
        return false;
    }

    public override void OnClaim(Actor actor)
    {
        if (_matchedInputs.Count == 0)
            return;

        PlayerInputController input = PlayerInputController.Instance;
        if (input != null && input.IsControlledActor(actor))
            input.TryConsumeInputHistory(_matchedInputs);

        _matchedInputs.Clear();
    }
}
