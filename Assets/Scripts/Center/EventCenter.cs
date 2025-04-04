using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

public abstract class IEventInfo
{

}



public class EventInfo : IEventInfo
{
    private UnityAction actions = delegate { };

    public void AddAction(UnityAction action)
    {
        actions += action;
    }

    public void RemoveAction(UnityAction action)
    {
        actions -= action;
    }

    public void Invoke()
    {
        actions.Invoke();
    }
}



public class EventInfo<T> : IEventInfo
{
    private UnityAction<T> actions = delegate { };

    public void AddAction(UnityAction<T> action)
    {
        actions += action;
    }

    public void RemoveAction(UnityAction<T> action)
    {
        actions -= action;
    }

    public void Invoke(T parameter)
    {
        actions.Invoke(parameter);
    }

}


public class EventCenter
{
    private static EventCenter instance;
    public static EventCenter Instance { get => instance ?? (instance = new EventCenter()); }

    private static Dictionary<string, IEventInfo> eventDic = new Dictionary<string, IEventInfo>();

    EventInfo GetEventInfo(string eventName)
    {
        if (eventDic.ContainsKey(eventName))
        {
            if (eventDic[eventName] != null)
            {
                EventInfo eventInfo = eventDic[eventName] as EventInfo;

                // 检查 eventInfo 是否为 null
                if (eventInfo != null)
                    return eventInfo;
                else
                    Debug.LogError("eventDic 中 " + eventName + " 对应的值不是 EventInfo 类型！");
            }
            else
                Debug.LogError("eventDic 中 " + eventName + " 对应的值为 null！");
        }
        return null;
    }

    EventInfo<T> GetEventInfo<T>(string eventName)
    {
        if (eventDic.ContainsKey(eventName))
        {
            if (eventDic[eventName] != null)
            {
                EventInfo<T> eventInfo = eventDic[eventName] as EventInfo<T>;

                // 检查 eventInfo 是否为 null
                if (eventInfo != null)
                    return eventInfo;
                else
                    Debug.LogError("eventDic 中 " + eventName + " 对应的值不是 EventInfo<T> 类型！");
            }
            else
                Debug.LogError("eventDic 中 " + eventName + " 对应的值为 null！");
        }
        return null;
    }

    public void AddEventListener(string eventName, UnityAction action)
    {
        if(GetEventInfo(eventName) != null)
        {
            EventInfo eventInfo = GetEventInfo(eventName);
            eventInfo.AddAction(action);
        }
        else
        {
            EventInfo eventInfo = new EventInfo();
            eventInfo.AddAction(action);
            eventDic.Add(eventName, eventInfo);
        }
    }



    public void AddEventListener<T>(string eventName, UnityAction<T> action)
    {
        if (GetEventInfo<T>(eventName) != null)
        {
            EventInfo<T> eventInfo = GetEventInfo<T>(eventName);
            eventInfo.AddAction(action);
        }
        else
        {
            EventInfo<T> eventInfo_T = new EventInfo<T>();
            eventInfo_T.AddAction(action);
            eventDic.Add(eventName, eventInfo_T);
        }
    }



    public void EventTrigger(string eventName)
    {
        if (GetEventInfo(eventName) != null)
        {
            EventInfo eventInfo = GetEventInfo(eventName);
            eventInfo.Invoke();
        }
        else
        {
            Debug.LogWarning("eventDic 中 " + eventName + " 对应的值为 null！");
        }
    }


    public void EventTrigger<T>(string eventName, T info)
    {
        if (GetEventInfo<T>(eventName) != null)
        {
            EventInfo<T> eventInfo = GetEventInfo<T>(eventName);
            eventInfo.Invoke(info);
        }
        else
        {
            Debug.LogWarning("eventDic 中 " + eventName + " 对应的值为 null！");
        }
    }

    public void RemoveEventListener(string eventName, UnityAction action)
    {
        if (GetEventInfo(eventName) != null)
        {
            EventInfo eventInfo = GetEventInfo(eventName);
            eventInfo.RemoveAction(action);
        }
        else
        {
            Debug.LogWarning("eventDic 中 " + eventName + " 对应的值为 null！");
        }
    }

    public void RemoveEventListener<T>(string eventName, UnityAction<T> action)
    {
        if (GetEventInfo<T>(eventName) != null)
        {
            EventInfo<T> eventInfo = GetEventInfo<T>(eventName);
            eventInfo.RemoveAction(action);
        }
        else
        {
            Debug.LogWarning("eventDic 中 " + eventName + " 对应的值为 null！");
        }
    }

    public void Clear()
    {
        eventDic.Clear();
    }

}

