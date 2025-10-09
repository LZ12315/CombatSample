using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;


public abstract class MultiTransition<T> : Transition<T>
{
    [SerializeField] private MultiChoice multiChoice = MultiChoice.AllTrue;
    [SerializeField] private List<Transition<T>> transitions = new List<Transition<T>>();

    public override void OnStateEnter(T owner)
    {
        base.OnStateEnter(owner);
        for (int i = 0; i < transitions.Count; i++)
            transitions[i].OnStateEnter(owner);
    }

    public override bool ToTransition()
    {
        base.ToTransition();

        switch (multiChoice)
        {
            case MultiChoice.AllTrue:
                for (int i = 0; i < transitions.Count; i++)
                {
                    if(transitions[i].ToTransition() == false)
                        return false;
                }
                return true;
            case MultiChoice.AllFalse:
                for (int i = 0; i < transitions.Count; i++)
                {
                    if (transitions[i].ToTransition() == true)
                        return false;
                }
                return true;
            case MultiChoice.OneTure:
                for (int i = 0; i < transitions.Count; i++)
                {
                    if (transitions[i].ToTransition() == true)
                        return true;
                }
                return false;
            case MultiChoice.OneFalse:
                for (int i = 0; i < transitions.Count; i++)
                {
                    if (transitions[i].ToTransition() == false)
                        return true;
                }
                return false;
            case MultiChoice.All:
                return true;
        }

        return false;
    }

    public override void OnStateExit()
    {
        base.OnStateExit();
        for (int i = 0; i < transitions.Count; i++)
            transitions[i].OnStateExit();
    }

    private enum MultiChoice
    {
        AllTrue, AllFalse, OneTure, OneFalse, All
    }

}
