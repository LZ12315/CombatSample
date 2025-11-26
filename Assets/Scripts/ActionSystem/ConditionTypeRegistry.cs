using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// 条件类型注册表
public static class ConditionTypeRegistry
{
    private static Dictionary<string, Type> conditionTypes = new Dictionary<string, Type>();
    private static bool isInitialized = false;

    [RuntimeInitializeOnLoadMethod]
    public static void Initialize()
    {
        if (isInitialized) return;

        conditionTypes.Clear();

        // 自动发现所有 TransitionCondition 的子类
        var baseType = typeof(TransitionCondition);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (baseType.IsAssignableFrom(type) && type != baseType)
                    {
                        string typeName = GetDisplayName(type);
                        conditionTypes[typeName] = type;
                        Debug.Log($"注册条件类型: {typeName}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"扫描程序集 {assembly.FullName} 时出错: {e.Message}");
            }
        }

        isInitialized = true;
    }

    public static List<string> GetAvailableConditionTypes()
    {
        Initialize();
        return new List<string>(conditionTypes.Keys);
    }

    public static Type GetConditionType(string typeName)
    {
        Initialize();
        return conditionTypes.ContainsKey(typeName) ? conditionTypes[typeName] : null;
    }

    public static TransitionCondition CreateInstance(string typeName)
    {
        var type = GetConditionType(typeName);
        if (type != null)
        {
            return Activator.CreateInstance(type) as TransitionCondition;
        }
        return null;
    }

    private static string GetDisplayName(Type type)
    {
        // 尝试获取自定义属性中的显示名称
        var displayAttr = type.GetCustomAttribute<ConditionDisplayNameAttribute>();
        if (displayAttr != null)
        {
            return displayAttr.DisplayName;
        }

        // 默认使用类型名称（去掉"Condition"后缀）
        string name = type.Name;
        if (name.EndsWith("Condition"))
        {
            name = name.Substring(0, name.Length - "Condition".Length);
        }
        return name;
    }
}

// 自定义属性用于设置显示名称
[AttributeUsage(AttributeTargets.Class)]
public class ConditionDisplayNameAttribute : Attribute
{
    public string DisplayName { get; private set; }

    public ConditionDisplayNameAttribute(string displayName)
    {
        DisplayName = displayName;
    }
}