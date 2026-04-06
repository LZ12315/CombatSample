using UnityEditor;
using UnityEngine;
using DeiveEx.TagTree;

[CustomEditor(typeof(Actor))]
public class ActorEditor : Editor
{
    public override bool RequiresConstantRepaint()
    {
        return true; 
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        Actor actor = (Actor)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("=== 🏷️ 运行时 Tag 监控面板 ===", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("请运行游戏以查看动态生成的 TagContainer 数据。", MessageType.Info);
            return;
        }

        if (actor.persistentTags == null && actor.transientTags == null)
        {
            EditorGUILayout.HelpBox("TagContainer 尚未初始化。", MessageType.Warning);
            return;
        }

        DrawContainer("Persistent Tags", actor.persistentTags);
        EditorGUILayout.Space(6);
        DrawContainer("Transient Tags", actor.transientTags);
    }

    private static void DrawContainer(string title, TagContainer container)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        if (container == null)
        {
            EditorGUILayout.HelpBox("容器未初始化。", MessageType.None);
            return;
        }

        var tags = container.Tags;
        if (tags.Count == 0)
        {
            EditorGUILayout.HelpBox("当前没有任何 Tag。", MessageType.None);
            return;
        }

        EditorGUILayout.BeginVertical("helpbox");

        int displayCount = 0;

        // 右对齐的 GUI 样式，专门给 Counter 用
        GUIStyle rightAlignStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleRight
        };

        foreach (var tag in tags)
        {
            // 🌟 核心过滤：如果这个 Tag 不是叶子节点（说明它只是被顺带激活的父节点），直接跳过不显示！
            if (!container.IsLeafTagInContainer(tag))
            {
                continue;
            }

            displayCount++;
            int count = container.GetTagCount(tag);
            
            // 🌟 核心排版：开启水平布局
            EditorGUILayout.BeginHorizontal();
            
            // 左边：Tag 名字，自动拉伸占据大部分空间
            EditorGUILayout.LabelField(tag.ToString(), GUILayout.ExpandWidth(true));
            
            // 右边：Counter 文本，固定宽度并靠右对齐
            EditorGUILayout.LabelField($"Count: {count}", rightAlignStyle, GUILayout.Width(70));
            
            EditorGUILayout.EndHorizontal();
        }

        if (displayCount == 0)
        {
            EditorGUILayout.HelpBox("正在处理 Tag 数据...", MessageType.None);
        }
        
        EditorGUILayout.EndVertical();
    }
}