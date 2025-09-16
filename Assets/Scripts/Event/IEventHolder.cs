using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public interface IEventHolder<T>
{
    public Dictionary<T, IEventInfo> EventDic { get; set; }

    public EventInfo GetEventInfo(T key);

    public EventInfo<Y> GetEventInfo<Y>(T key);

    public void AddEventListener(T key, UnityAction action);

    public void AddEventListener<Y>(T key, UnityAction<Y> action);

    public void EventTrigger(T key);

    public void EventTrigger<Y>(T key, Y parameter);

    public void RemoveEventListener(T key, UnityAction action);

    public void RemoveEventListener<Y>(T key, UnityAction<Y> action);

}
