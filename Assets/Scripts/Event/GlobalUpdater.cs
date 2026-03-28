using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalUpdater : MonoBehaviour
{
    private static GlobalUpdater _instance;
    public static GlobalUpdater Instance
    {
        get
        {
            if (_instance == null)
            {
                // 尝试在场景中查找现有实例
                _instance = FindObjectOfType<GlobalUpdater>();

                // 如果没有找到，创建新实例
                if (_instance == null)
                {
                    GameObject go = new GameObject("GlobalUpdater");
                    _instance = go.AddComponent<GlobalUpdater>();
                    DontDestroyOnLoad(go); // 跨场景不销毁
                }
            }
            return _instance;
        }
    }

    private GenericEventManager<float> _updateEventManager = new GenericEventManager<float>();
    public GenericEventManager<float> UpdateManager => _updateEventManager;

    private void Awake()
    {
        // 确保只有一个实例
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        RaiseInputEvent(Time.deltaTime);
    }

    public static void RegisterForUpdater(object registrant, Action<float> callback)
    {
        Instance.UpdateManager.Subscribe(registrant, callback);
    }

    public static void UnregisterFromInputEvent(object registrant)
    {
        Instance.UpdateManager.Unsubscribe(registrant);
    }

    void RaiseInputEvent(float deltaTime)
    {
        _updateEventManager.Publish(deltaTime);
    }
}