using System;
using UnityEngine;

/// <summary>
/// 泛型事件管理器接口。
/// 任何需要事件发布/订阅功能的类都可以实现此接口。
/// TEventData: 事件发生时传递的数据类型。
/// </summary>
/// <typeparam name="TEventData">事件数据的类型</typeparam>
public interface IGenericEventManager<TEventData>
{
    /// <summary>
    /// 订阅事件。当事件触发时，指定的回调方法会被调用。
    /// </summary>
    /// <param name="registrant">注册者标识（通常为调用者本身，用于后续取消注册）</param>
    /// <param name="callback">事件触发时的回调方法（接收TEventData类型参数）</param>
    void Subscribe(object registrant, Action<TEventData> callback);

    /// <summary>
    /// 取消订阅事件。移除之前注册的回调。
    /// </summary>
    /// <param name="registrant">要取消注册的标识对象</param>
    void Unsubscribe(object registrant);

    /// <summary>
    /// 触发事件，并传递数据给所有已订阅的回调。
    /// </summary>
    /// <param name="eventData">要传递的事件数据</param>
    void Publish(TEventData eventData);
}