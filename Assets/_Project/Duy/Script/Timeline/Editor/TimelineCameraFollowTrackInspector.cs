using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;

[CustomEditor(typeof(TimelineCameraFollowTrack))]
class TimelineCameraFollowTrackInspector : Editor
{
    static class Styles
    {
        public static readonly GUIContent PostPlaybackStateText =
            new GUIContent("Post-playback state",
                "Specify what state to leave the CinemachineCamera.Follow in after the Timeline has finished playing.");
    }

    SerializedProperty m_PostPlaybackProperty;

    public void OnEnable()
    {
        m_PostPlaybackProperty = serializedObject.FindProperty("m_PostPlaybackState");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (m_PostPlaybackProperty != null)
        {
            EditorGUILayout.PropertyField(m_PostPlaybackProperty, Styles.PostPlaybackStateText);
        }

        if (serializedObject.hasModifiedProperties)
        {
            serializedObject.ApplyModifiedProperties();
        }
    }
}
