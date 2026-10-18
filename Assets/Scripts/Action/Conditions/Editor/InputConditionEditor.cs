#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(InputSequenceCondition))]
public sealed class InputSequenceConditionDrawer : PropertyDrawer
{
    private static bool ShowTime(SerializedProperty property)
    {
        var clips = property.FindPropertyRelative(nameof(InputSequenceCondition.clips));
        float time = property.FindPropertyRelative(nameof(InputSequenceCondition.maxSequenceTime)).floatValue;
        // Keep an invalid value editable rather than hiding the cause of a rejected single step.
        return clips.arraySize > 1 || float.IsNaN(time) || float.IsInfinity(time)
            || time < 0f || time > PlayerInputBuffer.MaxContextSeconds;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var clips = property.FindPropertyRelative(nameof(InputSequenceCondition.clips));
        return EditorGUI.GetPropertyHeight(clips, true) + (ShowTime(property)
            ? EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing : 0f);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        if (ShowTime(property))
        {
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(row, property.FindPropertyRelative(nameof(InputSequenceCondition.maxSequenceTime)),
                new GUIContent("Max Sequence Time (s)", "Maximum real-time span from the first event to the last. Not an added delay or the command buffer duration."));
            position.yMin += row.height + EditorGUIUtility.standardVerticalSpacing;
        }
        EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(InputSequenceCondition.clips)),
            new GUIContent("Input Steps", "Events must occur in order; intervening inputs are allowed. A single step is still subject to the command buffer duration."), true);
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(InputEventClip))]
public sealed class InputEventClipDrawer : PropertyDrawer
{
    private static readonly GUIContent[] Categories = { new GUIContent("Button"), new GUIContent("Direction") };
    private static readonly GUIContent[] Edges = { new GUIContent("Pressed"), new GUIContent("Released") };
    private static readonly int[] EdgeValues = { (int)InputEventType.ButtonPressed, (int)InputEventType.ButtonReleased };
    private static readonly GUIContent[] Buttons =
    {
        new GUIContent("Not Selected"), new GUIContent("Dodge"), new GUIContent("Defence"), new GUIContent("Light Attack"),
        new GUIContent("Heavy Attack"), new GUIContent("Jump"), new GUIContent("Lock"),
    };
    private static readonly int[] ButtonValues =
    {
        (int)Enums.InputButton.None, (int)Enums.InputButton.Dodge, (int)Enums.InputButton.Defence,
        (int)Enums.InputButton.LightAttack, (int)Enums.InputButton.HeavyAttack,
        (int)Enums.InputButton.Jump, (int)Enums.InputButton.Lock,
    };
    private static readonly GUIContent[] Directions =
    {
        new GUIContent("Return to Center"), new GUIContent("Enter Forward"), new GUIContent("Enter Back"), new GUIContent("Enter Left"), new GUIContent("Enter Right"),
    };
    private static readonly int[] DirectionValues =
    {
        (int)InputDirection.Neutral, (int)InputDirection.Forward, (int)InputDirection.Back,
        (int)InputDirection.Left, (int)InputDirection.Right,
    };

    private static int Category(int type) => type == (int)InputEventType.DirectionEntered ? 1
        : type == (int)InputEventType.ButtonPressed || type == (int)InputEventType.ButtonReleased ? 0 : -1;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int category = Category(property.FindPropertyRelative(nameof(InputEventClip.type)).intValue);
        int rows = !property.isExpanded ? 1 : category == 0 ? 4 : category == 1 ? 3 : 2;
        return rows * EditorGUIUtility.singleLineHeight + (rows - 1) * EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, StepLabel(property), true);
        if (property.isExpanded)
        {
            int indent = EditorGUI.indentLevel;
            try
            {
                EditorGUI.indentLevel++;
                Next(ref row);
                var type = property.FindPropertyRelative(nameof(InputEventClip.type));
                int category = Category(type.intValue);
                EditorGUI.BeginChangeCheck();
                int selected = EditorGUI.Popup(row, new GUIContent("Input Category"), category, Categories);
                if (EditorGUI.EndChangeCheck())
                {
                    category = selected;
                    type.intValue = selected == 1 ? (int)InputEventType.DirectionEntered : (int)InputEventType.ButtonPressed;
                }
                Next(ref row);
                if (category == 1)
                    DrawChoice(row, property.FindPropertyRelative(nameof(InputEventClip.direction)), "Operation", Directions, DirectionValues);
                else if (category == 0)
                {
                    DrawChoice(row, property.FindPropertyRelative(nameof(InputEventClip.button)), "Button", Buttons, ButtonValues);
                    Next(ref row);
                    DrawChoice(row, type, "Event", Edges, EdgeValues);
                }
            }
            finally { EditorGUI.indentLevel = indent; }
        }
        EditorGUI.EndProperty();
    }

    private static GUIContent StepLabel(SerializedProperty property)
    {
        int type = property.FindPropertyRelative(nameof(InputEventClip.type)).intValue;
        string summary;
        if (type == (int)InputEventType.DirectionEntered)
            summary = ChoiceLabel(property.FindPropertyRelative(nameof(InputEventClip.direction)).intValue, Directions, DirectionValues, "Invalid Direction");
        else if (Category(type) == 0)
            summary = ChoiceLabel(property.FindPropertyRelative(nameof(InputEventClip.button)).intValue, Buttons, ButtonValues, "Invalid Button")
                + " " + Edges[type].text;
        else summary = "Select Input Category";
        return new GUIContent(summary, "Expand to configure this input. Directions represent entry events, not current held states.");
    }

    private static string ChoiceLabel(int value, GUIContent[] labels, int[] values, string invalid)
    {
        for (int i = 0; i < values.Length; i++) if (values[i] == value) return labels[i].text;
        return invalid;
    }

    private static void DrawChoice(Rect row, SerializedProperty property, string label, GUIContent[] labels, int[] values)
    {
        EditorGUI.BeginChangeCheck();
        int value = EditorGUI.IntPopup(row, new GUIContent(label), property.intValue, labels, values);
        if (EditorGUI.EndChangeCheck()) property.intValue = value;
    }

    private static void Next(ref Rect row) => row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
}

[CustomPropertyDrawer(typeof(InputStateCondition))]
public sealed class InputStateConditionDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        bool direction = property.FindPropertyRelative(nameof(InputStateCondition.type)).intValue == (int)InputStateType.Direction;
        bool held = property.FindPropertyRelative(nameof(InputStateCondition.held)).boolValue;
        int rows = direction ? 2 : held ? 4 : 3;
        return rows * EditorGUIUtility.singleLineHeight + (rows - 1) * EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var type = property.FindPropertyRelative(nameof(InputStateCondition.type));
        Draw(ref row, type, "Current Input State");
        if (type.intValue == (int)InputStateType.Direction)
            Draw(ref row, property.FindPropertyRelative(nameof(InputStateCondition.direction)), "Current Direction / Center");
        else
        {
            Draw(ref row, property.FindPropertyRelative(nameof(InputStateCondition.button)), "Button");
            var held = property.FindPropertyRelative(nameof(InputStateCondition.held));
            Draw(ref row, held, "Held (off = Not Held)");
            if (held.boolValue) Draw(ref row, property.FindPropertyRelative(nameof(InputStateCondition.minimumHoldSeconds)), "Minimum Hold Seconds");
        }
        EditorGUI.EndProperty();
    }

    private static void Draw(ref Rect row, SerializedProperty property, string label)
    {
        EditorGUI.PropertyField(row, property, new GUIContent(label));
        row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
    }
}
#endif
