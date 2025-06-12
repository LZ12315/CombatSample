using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public abstract class StateMachine<T> : MonoBehaviour where T :Component
{
    [SerializeField] protected State<T> startState;
    [SerializeField] protected List<TransitionMapBranch> stateMachineMap = new List<TransitionMapBranch>();

    protected Dictionary<State<T>, List<TransitionLine>> mapDictionary;

    protected T _owner;
    public State<T> CurrentState { get; protected set; }
    public Dictionary<State<T>, List<TransitionLine>> AsDictionary
    {
        get
        {
            if (mapDictionary == null)
            {
                mapDictionary = new Dictionary<State<T>, List<TransitionLine>>();
                foreach (var branch in stateMachineMap)
                {
                    mapDictionary[branch.sourceState] = branch.transitionLines;
                }
            }
            return mapDictionary;
        }
    }

    protected virtual void Awake()
    {
        if(startState != null)
            CurrentState = startState;
        else if (stateMachineMap[0].sourceState != null)
            CurrentState = stateMachineMap[0].sourceState;

        _owner = GetComponent<T>();
    }

    protected virtual void Update()
    {
        if (CurrentState == null) return;

        List<TransitionLine> currentTransitions;
        AsDictionary.TryGetValue(CurrentState, out currentTransitions);

        if (currentTransitions != null)
        {
            foreach (var transition in currentTransitions)
            {
                if (transition.transition.ToTransition(_owner))
                {
                    ChangeState(transition.targetState);
                    break;
                }
            }

        }

        CurrentState.OnUpdate();
    }

    protected virtual void FixedUpdate()
    {
        if (CurrentState == null) return;

        CurrentState.OnFixedUpdate();
    }

    public virtual void ChangeState(State<T> state)
    {
        if (AsDictionary.ContainsKey(state)) return;

        if(CurrentState != null) 
            CurrentState.OnExit();

        CurrentState = state;
        CurrentState.OnEnter(_owner);
    }

    #region 变量定义

    [System.Serializable]
    public struct TransitionLine
    {
        public Transition<T> transition;
        public State<T> targetState;

        public TransitionLine(Transition<T> transition, State<T> state)
        {
            this.transition = transition ?? throw new ArgumentNullException(nameof(transition));
            targetState = state ?? throw new ArgumentNullException(nameof(state));
        }
    }

    [System.Serializable]
    public class TransitionMapBranch
    {
        public string branchName;
        public State<T> sourceState;
        public List<TransitionLine> transitionLines;
    }

    #endregion

}


