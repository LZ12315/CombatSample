#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class DropdownManager : EditorWindow
{
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.update += Update;
    }

    static void Update()
    {
        // 检查事件是否为空
        if (Event.current == null)
            return;

        // 检查是否点击了屏幕任意位置
        if (Event.current.type == EventType.MouseDown)
        {
            // 检查是否点击了任何下拉菜单外部
            bool clickedOutsideAll = true;

            // 检查按钮下拉菜单
            foreach (var rect in ButtonInputConditionDrawer.dropdownRects.Values)
            {
                if (rect.Contains(Event.current.mousePosition))
                {
                    clickedOutsideAll = false;
                    break;
                }
            }

            // 检查摇杆下拉菜单
            if (clickedOutsideAll)
            {
                foreach (var rect in JoystickInputConditionDrawer.dropdownRects.Values)
                {
                    if (rect.Contains(Event.current.mousePosition))
                    {
                        clickedOutsideAll = false;
                        break;
                    }
                }
            }

            // 如果点击了所有下拉菜单外部，关闭所有下拉菜单
            if (clickedOutsideAll)
            {
                foreach (var key in ButtonInputConditionDrawer.dropdownStates.Keys.ToList())
                {
                    ButtonInputConditionDrawer.dropdownStates[key] = false;
                }
                
                foreach (var key in JoystickInputConditionDrawer.dropdownStates.Keys.ToList())
                {
                    JoystickInputConditionDrawer.dropdownStates[key] = false;
                }
                
                // 重绘所有窗口
                EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                foreach (EditorWindow window in windows)
                {
                    window.Repaint();
                }
            }
        }
    }
}
#endif