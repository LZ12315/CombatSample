using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using UnityEngine.Events;

public class ActorLogicInput : MonoBehaviour, IEventHolder<Enums.InputType>
{
    public Actor actor;
    Dictionary<Enums.InputType, EventInfo> inputActionEvents = new ();
    List<Enums.InputType> inputThisFrame = new ();

    public void InputMove(Vector3 moveDir, float distance)
    {
        actor.movement.UpdateTurn(moveDir);

        if (distance > 0.1f)
            AddInputThisFrame(Enums.InputType.Move);
        else
            AddInputThisFrame(Enums.InputType.MoveCancel);
    }

    private void LateUpdate()
    {
        if(inputThisFrame.Count == 0) return;

        Enums.InputType execType = Enums.InputType.None;
        foreach (var key in inputThisFrame)
        {
            if(execType == Enums.InputType.None || key > execType)
                execType = key;
        }
        inputThisFrame.Clear();
        EventTrigger(execType);
    }

    #region Input事件

    public Dictionary<Enums.InputType, EventInfo> EventDic => inputActionEvents;

    public EventInfo GetEventInfo(Enums.InputType key)
    {
        if (inputActionEvents.ContainsKey(key))
        {
            if (inputActionEvents[key] != null)
            {
                EventInfo eventInfo = inputActionEvents[key];

                if (eventInfo != null)
                    return eventInfo;
                else
                    Debug.LogError("eventDic 中 " + key.ToString() + " 对应的值不是 EventInfo 类型！");
            }
            else
                Debug.LogError("eventDic 中 " + key.ToString() + " 对应的值为null！");
        }
        return null;
    }

    public void AddEventListener(Enums.InputType key, UnityAction action)
    {
        var holder = (IEventHolder<Enums.InputType>)this;
        if (GetEventInfo(key) != null)
        {
            EventInfo eventInfo = GetEventInfo(key);
            eventInfo.AddAction(action);
        }
        else
        {
            EventInfo eventInfo = new EventInfo();
            eventInfo.AddAction(action);
            inputActionEvents.Add(key, eventInfo);
        }
    }

    void AddInputThisFrame(Enums.InputType key)
    {
        inputThisFrame.Add(key);
    }

    public void EventTrigger(Enums.InputType key)
    {
        if (GetEventInfo(key) != null)
        {
            EventInfo eventInfo = GetEventInfo(key);
            eventInfo?.Invoke();
        }
    }

    public void RemoveEventListener(Enums.InputType key, UnityAction action)
    {
        if (GetEventInfo(key) != null)
        {
            EventInfo eventInfo = GetEventInfo(key);
            eventInfo.RemoveAction(action);
        }
        else
        {
            Debug.LogWarning("eventDic 中 " + key.ToString() + " 对应的值为 null！");
        }
    }

    #endregion

}

public static partial class Enums
{
    public enum InputType
    {
        None,
        Move,
        MoveCancel
    }
}
