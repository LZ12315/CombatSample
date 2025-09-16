using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CombatSample.Consts;
using UnityEngine.Events;

public class ActorLogicInput : MonoBehaviour, IEventHolder<string>
{
    public Actor actor;
    Dictionary<string, EventInfo> inputActionEvents = new ();

    private Vector2 lastMoveInput = Vector2.zero;
    public Vector2 MoveInput => lastMoveInput;


    public void InputMove(Vector2 moveInput)
    {
        lastMoveInput = moveInput;
        Vector3 moveDir = actor.cameraControl.CalculateFaceDirection(moveInput);
        actor.movement.UpdateTurn(moveDir);
    }

    public void GetInputData(InputData inputData)
    {

    }

    #region Input事件

    public Dictionary<string, EventInfo> EventDic => inputActionEvents;

    public EventInfo GetEventInfo(string key)
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

    public void AddEventListener(string key, UnityAction action)
    {
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

    public void EventTrigger(string key)
    {
        if (GetEventInfo(key) != null)
        {
            EventInfo eventInfo = GetEventInfo(key);
            eventInfo?.Invoke();
        }
    }

    public void RemoveEventListener(string key, UnityAction action)
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
