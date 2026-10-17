using UnityEditor;

[CustomEditor(typeof(NPCRandomPatrol))]
public class NPCRandomPatrolEditor : Editor
{
    private static readonly string[] PatrolFieldNames =
    {
        "canMove",
        "groundLayer",
        "detectionOrigin",
        "patrolRange",
        "moveSpeed",
        "wallCheckDistance",
        "ledgeCheckDistance",
        "guiOffsetHeight"
    };

    private SerializedProperty enablePatrolProperty;

    private void OnEnable()
    {
        enablePatrolProperty = serializedObject.FindProperty("enablePatrol");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (property.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(property, true);
                }

                continue;
            }

            if (!ShouldDrawProperty(property))
            {
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private bool ShouldDrawProperty(SerializedProperty property)
    {
        if (enablePatrolProperty == null || enablePatrolProperty.boolValue)
        {
            return true;
        }

        for (int i = 0; i < PatrolFieldNames.Length; i++)
        {
            if (property.propertyPath == PatrolFieldNames[i])
            {
                return false;
            }
        }

        return true;
    }
}
