using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class StateMachine<T>
{
    private SerializableTransitionMap transitionMap = new SerializableTransitionMap();

    T _owner;
    State<T> currentState;

    public StateMachine(T owner)
    {
        _owner = owner;
    }

    public void ChangeState(State<T> state)
    {
        if(currentState != null) 
            currentState.OnExit();
        currentState = state;
        currentState.OnEnter(_owner);
    }

    public void ExcuteState()
    {
        if (currentState == null) return;

        //Transitions currentTransitions;
        //transitionMap.AsDictionary.TryGetValue(currentState, out currentTransitions);

        //if (currentTransitions.transitions == null) return;

        //foreach (var transition in currentTransitions.transitions)
        //{
        //    if(transition.TransitionRef.ToTransition())
        //    {
        //        ChangeState(transition.targetStateRef);
        //        break;
        //    }
        //}

        currentState.OnUpdate();
    }

    #region 变量定义

    [System.Serializable]
    public struct TransitionLine
    {
        public Transition TransitionRef;
        public State<T> targetStateRef;

        public TransitionLine(Transition transition, State<T> state)
        {
            TransitionRef = transition ?? throw new ArgumentNullException(nameof(transition));
            targetStateRef = state ?? throw new ArgumentNullException(nameof(state));
        }
    }

    [System.Serializable]
    public struct Transitions
    {
        public List<TransitionLine> transitions;
    }

    [System.Serializable]
    public class TransitionMapEntry
    {
        public State<T> SourceState;
        public Transitions Transitions;
    }

    [System.Serializable]
    public class SerializableTransitionMap
    {
        private List<TransitionMapEntry> entries = new List<TransitionMapEntry>();

        private Dictionary<State<T>, Transitions> _lookup;

        public Dictionary<State<T>, Transitions> AsDictionary
        {
            get
            {
                if (_lookup == null)
                {
                    _lookup = new Dictionary<State<T>, Transitions>();
                    foreach (var entry in entries)
                    {
                        _lookup[entry.SourceState] = entry.Transitions;
                    }
                }
                return _lookup;
            }
        }

        public void AddTransition(State<T> sourceState, TransitionLine statePair)
        {
            foreach (var entry in entries)
            {
                if (entry.SourceState == sourceState)
                {
                    if (!entry.Transitions.transitions.Contains(statePair))
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
                        if (!entry.Transitions.transitions.Contains(transition))
                            entry.Transitions.transitions.Add(transition);
                    }
                    return;
                }
            }

            entries.Add(newEntry);
        }

    }

    #endregion
}


