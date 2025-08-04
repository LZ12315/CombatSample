using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public interface IEventHolder<T>
{
    public Dictionary<T, EventInfo> EventDic { get;}

    public EventInfo GetEventInfo(T key);

    public void AddEventListener(T key, UnityAction action);

    public void EventTrigger(T key);

    public void RemoveEventListener(T key, UnityAction action);
}
