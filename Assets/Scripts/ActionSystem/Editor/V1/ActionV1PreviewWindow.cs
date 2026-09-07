using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Stable placeholder kept while the Action V1 preview design is reconsidered.
/// It intentionally owns no preview scene, character clone, animation graph,
/// editor update callback, or asset mutation path.
/// </summary>
public sealed class ActionV1PreviewWindow : EditorWindow
{
    [MenuItem("Tools/Combat/Action V1/Action Preview")]
    public static void OpenShared()
    {
        ActionV1PreviewWindow window = GetWindow<ActionV1PreviewWindow>();
        window.titleContent = new GUIContent("Action Preview");
        window.minSize = new Vector2(360f, 180f);
        window.Show();
    }

    private void CreateGUI()
    {
        titleContent = new GUIContent("Action Preview");
        rootVisualElement.Clear();
        rootVisualElement.style.flexGrow = 1f;
        rootVisualElement.style.justifyContent = Justify.Center;
        rootVisualElement.style.alignItems = Align.Center;
        rootVisualElement.style.paddingLeft = 24f;
        rootVisualElement.style.paddingRight = 24f;

        var title = new Label("Action Preview is paused")
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 16f,
                marginBottom = 8f,
            },
        };

        var message = new Label(
            "This checkpoint provides the Action Timeline and Action Details editors. " +
            "Preview implementation has been withdrawn while its design is reconsidered.")
        {
            style =
            {
                maxWidth = 540f,
                whiteSpace = WhiteSpace.Normal,
                unityTextAlign = TextAnchor.MiddleCenter,
            },
        };

        rootVisualElement.Add(title);
        rootVisualElement.Add(message);
    }
}
