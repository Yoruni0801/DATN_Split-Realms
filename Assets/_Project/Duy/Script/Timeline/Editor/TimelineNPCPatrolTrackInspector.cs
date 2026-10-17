using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TimelineNPCPatrolTrack))]
public class TimelineNPCPatrolTrackInspector : Editor
{
    private static class Styles
    {
        public static readonly GUIContent PostPlaybackStateText =
            new GUIContent("Post-playback state",
                "Trạng thái enablePatrol của NPC sau khi Timeline dừng chạy.");
    }

    private SerializedProperty postPlaybackProperty;

    private void OnEnable()
    {
        postPlaybackProperty = serializedObject.FindProperty("m_PostPlaybackState");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (postPlaybackProperty != null)
        {
            EditorGUILayout.PropertyField(postPlaybackProperty, Styles.PostPlaybackStateText);
        }

        if (serializedObject.hasModifiedProperties)
        {
            serializedObject.ApplyModifiedProperties();
        }
    }
}
