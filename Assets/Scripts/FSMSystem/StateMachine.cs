using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

# region 变量定义

[System.Serializable]
public struct TransitionStatePair
{
    public Transition TransitionRef;
    public State StateRef;

    public TransitionStatePair(Transition transition, State state)
    {
        TransitionRef = transition ?? throw new ArgumentNullException(nameof(transition));
        StateRef = state ?? throw new ArgumentNullException(nameof(state));
    }
}

[System.Serializable]
public struct Transitions
{
    public List<TransitionStatePair> transitions;
}

[System.Serializable]
public class TransitionMapEntry
{
    public State SourceState;
    public Transitions Transitions;
}

[System.Serializable]
public class SerializableTransitionMap
{
    private List<TransitionMapEntry> entries = new List<TransitionMapEntry>();

    private Dictionary<State, Transitions> _lookup;

    public Dictionary<State, Transitions> AsDictionary
    {
        get
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<State, Transitions>();
                foreach (var entry in entries)
                {
                    _lookup[entry.SourceState] = entry.Transitions;
                }
            }
            return _lookup;
        }
    }

    public void AddTransition(State sourceState, TransitionStatePair statePair)
    {
        foreach (var entry in entries) 
        { 
            if(entry.SourceState == sourceState)
            {
                if(!entry.Transitions.transitions.Contains(statePair))
                    entry.Transitions.transitions.Add(statePair);
                break;
            }
        }
    }

    public void AddMapEntry(TransitionMapEntry newEntry)
    {
        foreach (var entry in entries)
        {
            if (entry.SourceState == newEntry.SourceState)
            {
                foreach (var transition in newEntry.Transitions.transitions)
                {
                    if(!entry.Transitions.transitions.Contains(transition))
                        entry.Transitions.transitions.Add(transition);
                }
                return;
            }
        }

        entries.Add(newEntry);
    }

}

# endregion

public class StateMachine : MonoBehaviour
{
    [SerializeField] 
    private SerializableTransitionMap transitionMap = new SerializableTransitionMap();
    State currentState;
    Transitions currentTransitions;

    private void Update()
    {
        if (currentState == null) return;

        transitionMap.AsDictionary.TryGetValue(currentState, out currentTransitions);

        foreach (var transition in currentTransitions.transitions)
        {
            if(transition.TransitionRef.ToTransition())
            {
                SetState(transition.StateRef);
                break;
            }
        }

        currentState.OnUpdate();
    }

    void SetState(State state)
    {
        currentState = state;
    }
}


