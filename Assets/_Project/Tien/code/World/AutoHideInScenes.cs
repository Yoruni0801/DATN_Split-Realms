using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Tự động ẩn (SetActive false) GameObject khi nằm trong các Scene được chọn từ Build Settings.
/// </summary>
public class AutoHideInScenes : MonoBehaviour
{
    [SerializeField] private List<string> scenesToHide = new List<string>();

    public List<string> ScenesToHide => scenesToHide;

    private void Awake()
    {
        CheckAndHide();
    }

    private void OnEnable()
    {
        CheckAndHide();
    }

    private void CheckAndHide()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (scenesToHide != null && scenesToHide.Contains(currentScene))
        {
            gameObject.SetActive(false);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(AutoHideInScenes))]
public class AutoHideInScenesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty scenesProp = serializedObject.FindProperty("scenesToHide");

        // Lấy danh sách scene từ Build Settings
        string[] buildScenes = EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => Path.GetFileNameWithoutExtension(s.path))
            .Distinct()
            .ToArray();

        if (buildScenes.Length == 0)
        {
            EditorGUILayout.HelpBox("Chưa có scene nào được bật trong Build Settings! Hãy thêm scene vào Build Settings trước.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("Danh sách Scene tự động ẨN GameObject:", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                string sceneName = buildScenes[i];
                bool isSelected = false;

                for (int j = 0; j < scenesProp.arraySize; j++)
                {
                    if (scenesProp.GetArrayElementAtIndex(j).stringValue == sceneName)
                    {
                        isSelected = true;
                        break;
                    }
                }

                bool toggle = EditorGUILayout.ToggleLeft(sceneName, isSelected);
                if (toggle != isSelected)
                {
                    if (toggle)
                    {
                        scenesProp.arraySize++;
                        scenesProp.GetArrayElementAtIndex(scenesProp.arraySize - 1).stringValue = sceneName;
                    }
                    else
                    {
                        for (int j = 0; j < scenesProp.arraySize; j++)
                        {
                            if (scenesProp.GetArrayElementAtIndex(j).stringValue == sceneName)
                            {
                                scenesProp.DeleteArrayElementAtIndex(j);
                                break;
                            }
                        }
                    }
                }
            }
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
