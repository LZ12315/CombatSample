using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class ActionAssetOpenHandler
{
    [OnOpenAsset(1)]
    public static bool OnOpenActionAsset(int instanceID, int line)
    {
        Object openedObject = EditorUtility.InstanceIDToObject(instanceID);
        return openedObject is ActionAsset actionAsset && Open(actionAsset);
    }

    public static bool Open(ActionAsset actionAsset)
    {
        if (actionAsset == null)
            return false;

        ActionTimelineWindow.Open(actionAsset);
        return true;
    }
}
