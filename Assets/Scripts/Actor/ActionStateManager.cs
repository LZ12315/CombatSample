using System.Collections.Generic;
using UnityEngine;

public enum ControlState
{
    None,
    Action,
    Locomotion
}

[RequireComponent(typeof(Actor))]
public class ActionStateManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ActionPlayer _actionPlayer;
    [SerializeField] private Actor _actor;

    [Header("Settings")]
    [SerializeField] private ActionAssetList _actionList;

    [Header("Runtime")]
    [SerializeField] private ControlState _currentControl = ControlState.None;
    public ControlState CurrentControl => _currentControl;

    private List<ActionAsset> _validCandidatesCache = new List<ActionAsset>(10);
    private readonly List<ActionAsset> _externalCandidatesThisFrame = new List<ActionAsset>(4);

    private void OnEnable()
    {
        _actionPlayer.OnActionFinished += HandleActionFinished;
    }

    private void OnDisable()
    {
        _actionPlayer.OnActionFinished -= HandleActionFinished;
    }

    private void Update()
    {
        ActionAsset chosen = CheckForTransition();

        if (chosen != null)
        {
            if (_currentControl == ControlState.Locomotion)
                _actor.locomotion.StopLocomotion();

            _currentControl = ControlState.Action;
            PlayNewAction(chosen);
        }
        else if (_actor.locomotion.CheckConditions())
        {
            if (_currentControl != ControlState.Locomotion)
            {
                StopCurrentAction();

                _currentControl = ControlState.Locomotion;
                _actor.locomotion.StartLocomotion();
            }
        }

        _externalCandidatesThisFrame.Clear();
    }

    public void RegisterExternalCandidate(ActionAsset action)
    {
        if (action == null) return;
        _externalCandidatesThisFrame.Add(action);
    }

    private void PlayNewAction(ActionAsset actionToPlay)
    {
        _actionPlayer.BeginAction(actionToPlay);
    }

    private void StopCurrentAction()
    {
        _actionPlayer.StopAction();
    }

    private void HandleActionFinished(ActionInstance _)
    {
        _currentControl = ControlState.Locomotion;
        _actor.locomotion.StartLocomotion();
    }

    #region Action切换
    private ActionAsset CheckForTransition()
    {
        _validCandidatesCache.Clear();
        if (_actionList != null)
        {
            var allActions = _actionList.GetAllAvailableActions();

            for (int i = 0; i < allActions.Count; i++)
                TryAddCandidate(allActions[i]);
        }

        var currentInstance = _actionPlayer.CurrentAction;
        if (currentInstance != null)
        {
            var branches = currentInstance.Config.Branches;
            for (int i = 0; i < branches.Count; i++)
                TryAddCandidate(branches[i]);
        }

        for (int i = 0; i < _externalCandidatesThisFrame.Count; i++)
            TryAddCandidate(_externalCandidatesThisFrame[i]);

        if (_validCandidatesCache.Count > 0)
            return SelectHighestPriorityAction(_validCandidatesCache);
        return null;
    }

    private void TryAddCandidate(ActionAsset action)
    {
        if (action == null || _actor == null) return;
        if (!action.CheckEntry(_actor)) return;
        if (!_validCandidatesCache.Contains(action))
            _validCandidatesCache.Add(action);
    }

    private ActionAsset SelectHighestPriorityAction(List<ActionAsset> actions)
    {
        if (actions == null || actions.Count == 0) return null;
        if (actions.Count == 1) return actions[0];

        int bestLayerInt = (int)actions[0].PriorityLayer;
        for (int i = 1; i < actions.Count; i++)
        {
            int layer = (int)actions[i].PriorityLayer;
            if (layer > bestLayerInt)
                bestLayerInt = layer;
        }

        ActionAsset best = null;
        int bestValue = int.MinValue;
        for (int i = 0; i < actions.Count; i++)
        {
            var a = actions[i];
            if ((int)a.PriorityLayer != bestLayerInt) continue;
            if (best == null || a.PriorityValue > bestValue)
            {
                best = a;
                bestValue = a.PriorityValue;
            }
        }

        return best ?? actions[0];
    }
    #endregion
}
