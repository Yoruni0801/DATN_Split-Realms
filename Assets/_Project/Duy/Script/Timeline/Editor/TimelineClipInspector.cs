using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TimelineActionClip))]
public class TimelineClipInspector : Editor
{
    private static readonly Dictionary<TimelineClipType, string> SettingsPropertyNames =
        new Dictionary<TimelineClipType, string>
        {
            { TimelineClipType.SpineMove,        "spineMove" },
            { TimelineClipType.SpineAnimation,   "spineAnimation" },
            { TimelineClipType.CameraFollow,     "cameraFollow" },
            { TimelineClipType.LoadScene,         "loadScene" },
            { TimelineClipType.Petrify,           "petrify" },
            { TimelineClipType.Unpetrify,         "unpetrify" },
            { TimelineClipType.EnemyNpcAttack,   "enemyNpcAttack" },
            { TimelineClipType.AshwingPickupDrop, "ashwingPickupDrop" },
        };

    // Fields shown outside the Advanced foldout for each clip type.
    private static readonly HashSet<string> CoreFields = new HashSet<string>
    {
        // SpineMove
        "target", "startTarget", "moveTiming", "moveState", "moveSpeed", "bindingMode",
        // SpineAnimation
        "state", "exitMode",
        // CameraFollow
        "action", "targetMode",
        // LoadScene
        "mode", "sceneName",
        // Petrify & Unpetrify
        "targetOverride", "targetMode",
        // EnemyNpcAttack
        "attackType", "enemyOverride", "npcTarget",
        // AshwingPickupDrop
        "ashwingOverride", "itemTarget",
    };

    private SerializedProperty clipTypeProp;

    private void OnEnable()
    {
        clipTypeProp = serializedObject.FindProperty("clipType");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(clipTypeProp);

        var type = (TimelineClipType)clipTypeProp.enumValueIndex;
        if (!SettingsPropertyNames.TryGetValue(type, out string propName))
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        SerializedProperty groupProp = serializedObject.FindProperty(propName);
        if (groupProp == null)
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        SerializedProperty endProp = groupProp.GetEndProperty();
        SerializedProperty iterator = groupProp.Copy();

        if (!iterator.NextVisible(true))
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // Draw core fields first, then collect advanced fields.
        var advancedProps = new List<SerializedProperty>();

        do
        {
            if (SerializedProperty.EqualContents(iterator, endProp))
                break;

            if (CoreFields.Contains(iterator.name))
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
            else
            {
                advancedProps.Add(iterator.Copy());
            }
        } while (iterator.NextVisible(false));

        if (advancedProps.Count > 0)
        {
            EditorGUILayout.Space(4f);
            groupProp.isExpanded = EditorGUILayout.Foldout(groupProp.isExpanded, "Advanced", true);

            if (groupProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (var prop in advancedProps)
                {
                    EditorGUILayout.PropertyField(prop, true);
                }
                EditorGUI.indentLevel--;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(TimelinePlayController))]
public class TimelinePlayControllerInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty checkpointProperty = serializedObject.FindProperty("selectedCheckpointId");
        DrawPropertiesExcluding(serializedObject, "m_Script", "selectedCheckpointId");

        TimelinePlayController controller = (TimelinePlayController)target;
        string[] checkpointIds = controller.GetCheckpointIds();
        EditorGUILayout.Space(4f);

        if (checkpointIds.Length == 0)
        {
            EditorGUILayout.PropertyField(checkpointProperty);
            EditorGUILayout.HelpBox("The assigned Timeline has no skip checkpoints.", MessageType.Info);
        }
        else
        {
            int selectedIndex = System.Array.IndexOf(checkpointIds, checkpointProperty.stringValue);
            selectedIndex = Mathf.Max(0, selectedIndex);
            int newIndex = EditorGUILayout.Popup("Selected Checkpoint", selectedIndex, checkpointIds);
            checkpointProperty.stringValue = checkpointIds[newIndex];
        }

        serializedObject.ApplyModifiedProperties();

        using (new EditorGUI.DisabledScope(!Application.isPlaying || checkpointIds.Length == 0))
        {
            if (GUILayout.Button("Skip To Selected Checkpoint"))
            {
                controller.SkipToSelectedCheckpoint();
            }
        }
    }
}

[CustomEditor(typeof(TimelineEmbedMarker))]
public class TimelineEmbedMarkerInspector : Editor
{
    private void OnEnable()
    {
        EnsureCheckpointIds();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        if (EnsureCheckpointIds())
        {
            Repaint();
        }
    }

    private bool EnsureCheckpointIds()
    {
        bool changed = false;
        foreach (Object selectedTarget in targets)
        {
            TimelineEmbedMarker marker = selectedTarget as TimelineEmbedMarker;
            if (marker == null
                || !marker.isSkipCheckpoint
                || !string.IsNullOrWhiteSpace(marker.checkpointId))
            {
                continue;
            }

            Undo.RecordObject(marker, "Generate Timeline Checkpoint ID");
            marker.checkpointId = $"checkpoint_{System.Guid.NewGuid():N}";
            EditorUtility.SetDirty(marker);
            changed = true;
        }

        if (changed)
        {
            serializedObject.Update();
        }

        return changed;
    }
}
