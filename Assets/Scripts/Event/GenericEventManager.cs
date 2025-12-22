using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用事件管理器的默认实现。可以单独实例化使用，也可作为其他类的基类。
/// </summary>
/// <typeparam name="TEventData">事件数据的类型</typeparam>
public class GenericEventManager<TEventData> : IGenericEventManager<TEventData>
{
    // 使用字典来跟踪注册者和其对应的委托引用，确保能精确取消注册
    private Dictionary<object, Action<TEventData>> _registrantHandlers = new Dictionary<object, Action<TEventData>>();

    /// <summary>
    /// 订阅事件
    /// </summary>
    public virtual void Subscribe(object registrant, Action<TEventData> callback)
    {
        if (registrant == null)
        {
            Debug.LogError("[GenericEventManager] 注册者标识不能为Null。");
            return;
        }
        if (callback == null)
        {
            Debug.LogError("[GenericEventManager] 回调方法不能为Null。");
            return;
        }

        // 先移除可能存在的旧注册，防止重复
        Unsubscribe(registrant);

        // 将回调方法存入字典
        _registrantHandlers[registrant] = callback;
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    public virtual void Unsubscribe(object registrant)
    {
        if (registrant != null && _registrantHandlers.ContainsKey(registrant))
        {
            _registrantHandlers.Remove(registrant);
        }
    }

    /// <summary>
    /// 触发事件，通知所有订阅者
    /// </summary>
    public virtual void Publish(TEventData eventData)
    {
        // 防止在遍历过程中集合被修改（例如在回调中取消注册）
        // 创建一份副本进行遍历
        var handlersCopy = new List<Action<TEventData>>(_registrantHandlers.Values);

        foreach (var handler in handlersCopy)
        {
            try
            {
                handler?.Invoke(eventData); // 安全调用
            }
            catch (Exception e)
            {
                // 非常重要：捕获并记录单个回调的异常，避免一个回调出错导致后续回调无法执行
                Debug.LogError($"[GenericEventManager] 事件回调执行出错: {e.Message}");
            }
        }
    }

    public virtual void PublishSingle(object registrant, TEventData eventData)
    {
        if(registrant != null && _registrantHandlers.ContainsKey(registrant))
        {
            try
            {
                _registrantHandlers[registrant]?.Invoke(eventData); // 安全调用
            }
            catch (Exception e)
            {
                // 非常重要：捕获并记录单个回调的异常，避免一个回调出错导致后续回调无法执行
                Debug.LogError($"[GenericEventManager] 事件回调执行出错: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 清空所有订阅者（通常在管理器销毁时调用）
    /// </summary>
    public virtual void ClearAllSubscriptions()
    {
        _registrantHandlers.Clear();
    }
}