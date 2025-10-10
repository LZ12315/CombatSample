using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public abstract class StateMachine<T> : MonoBehaviour where T : class
{
    [field: SerializeField] public State<T> CurrentState { get; protected set; }
    [SerializeField] protected List<TransitionMapBranch> stateMachineMap = new List<TransitionMapBranch>();
    [SerializeField] protected State<T> startState;

    protected T _owner;
    protected Dictionary<State<T>, List<TransitionLine>> mapDictionary;
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
        _owner = GetComponent<T>();

        if (startState != null)
            ChangeState(startState);
        else if (stateMachineMap[0].sourceState != null)
            ChangeState(stateMachineMap[0].sourceState);
    }

    protected virtual void Update()
    {
        if (CurrentState == null) return;

        List<TransitionLine> currentTransitions;
        AsDictionary.TryGetValue(CurrentState, out currentTransitions);

        if (currentTransitions != null)
        {
            foreach (var transitionLine in currentTransitions)
            {
                if (transitionLine.transition.ToTransition())
                {
                    ChangeState(transitionLine.targetState);
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
        if (!AsDictionary.ContainsKey(state)) return;

        if (CurrentState != null)
            ExitState();

        EnterState(state);
    }

    public virtual bool IsInState(Type stateType)
    {
        if(stateType == null) return false;

        return CurrentState.GetType() == stateType;   
    }

    void ExitState()
    {
        CurrentState.OnStateExit();

        List<TransitionLine> currentTransitions;
        AsDictionary.TryGetValue(CurrentState, out currentTransitions);
        if (currentTransitions != null)
        {
            foreach (var transitionLine in currentTransitions)
                transitionLine.transition.OnStateExit();
        }
    }

    void EnterState(State<T> state)
    {
        CurrentState = state;
        CurrentState.OnStateEnter(_owner);

        List<TransitionLine> currentTransitions;
        AsDictionary.TryGetValue(CurrentState, out currentTransitions);
        if (currentTransitions != null)
        {
            foreach (var transitionLine in currentTransitions)
                transitionLine.transition.OnStateEnter(_owner);
        }
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


