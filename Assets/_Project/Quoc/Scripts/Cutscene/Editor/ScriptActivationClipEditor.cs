#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[CustomEditor(typeof(ScriptActivationClip))]
public class ScriptActivationClipEditor : Editor
{
    private SerializedProperty modeProp;
    private SerializedProperty targetOverrideProp;
    private SerializedProperty scriptNamesProp;
    private SerializedProperty affectAllProp;
    private SerializedProperty includeChildrenProp;
    private SerializedProperty restoreOnExitProp;
    private SerializedProperty applyInEditModeProp;

    private GameObject manualTargetObject;
    private string searchFilter = "";
    private bool showAdvanced = false;

    private void OnEnable()
    {
        modeProp = serializedObject.FindProperty("mode");
        targetOverrideProp = serializedObject.FindProperty("targetObjectOverride");
        scriptNamesProp = serializedObject.FindProperty("scriptNames");
        affectAllProp = serializedObject.FindProperty("affectAllMonoBehaviours");
        includeChildrenProp = serializedObject.FindProperty("includeChildren");
        restoreOnExitProp = serializedObject.FindProperty("restoreOriginalStateOnExit");
        applyInEditModeProp = serializedObject.FindProperty("applyInEditMode");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        ScriptActivationClip clip = target as ScriptActivationClip;
        if (clip == null) return;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Thiết Lập Inactive / Active Script", EditorStyles.boldLabel);

        if (modeProp != null)
            EditorGUILayout.PropertyField(modeProp, new GUIContent("Action Mode", "Chế độ: Tắt/Bật script trong clip hoặc vĩnh viễn"));

        if (targetOverrideProp != null)
            EditorGUILayout.PropertyField(targetOverrideProp, new GUIContent("Target Override", "Chỉ định riêng GameObject cho clip này (nếu không dùng binding trên Track)"));

        if (includeChildrenProp != null)
            EditorGUILayout.PropertyField(includeChildrenProp, new GUIContent("Include Children", "Tìm và áp dụng script trên cả GameObject con"));

        if (affectAllProp != null)
            EditorGUILayout.PropertyField(affectAllProp, new GUIContent("Affect All Scripts", "Tắt TOÀN BỘ script trên đối tượng (trừ PlayableDirector)"));

        EditorGUILayout.Space(8);

        // Tìm GameObject mục tiêu
        GameObject targetGO = FindBoundGameObject(clip);
        if (targetGO == null)
        {
            targetGO = manualTargetObject;
        }

        if (affectAllProp != null && !affectAllProp.boolValue)
        {
            DrawScriptSelectionBox(clip, targetGO);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Khôi Phục & Preview", EditorStyles.boldLabel);

        if (restoreOnExitProp != null)
            EditorGUILayout.PropertyField(restoreOnExitProp, new GUIContent("Restore On Exit", "Khôi phục lại trạng thái ban đầu của script khi hết clip hoặc dừng Timeline"));

        if (applyInEditModeProp != null)
            EditorGUILayout.PropertyField(applyInEditModeProp, new GUIContent("Apply In Edit Mode", "Xem trước (preview) hiệu ứng khi kéo thanh trượt Timeline"));

        // Mục Advanced (danh sách text thủ công)
        EditorGUILayout.Space(6);
        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Danh sách tên Script (Advanced / Manual)", true);
        if (showAdvanced && scriptNamesProp != null)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(scriptNamesProp, new GUIContent("Script Names"), true);
            EditorGUI.indentLevel--;
        }

        if (serializedObject.ApplyModifiedProperties())
        {
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
        }
    }

    private void DrawScriptSelectionBox(ScriptActivationClip clip, GameObject targetGO)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Danh Sách Scripts Để Chọn Inactive / Active", EditorStyles.boldLabel);

        // Hiển thị Target Object hoặc slot kéo thủ công
        EditorGUI.BeginChangeCheck();
        manualTargetObject = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Target Object", "GameObject mục tiêu để quét danh sách script"),
            targetGO,
            typeof(GameObject),
            true
        );
        if (EditorGUI.EndChangeCheck())
        {
            Repaint();
        }

        if (manualTargetObject == null && targetGO == null)
        {
            EditorGUILayout.HelpBox(
                "Chưa tìm thấy Target GameObject từ Track Binding.\n" +
                "Kéo GameObject vào ô 'Target Object' ở trên để chọn script bằng checkbox, hoặc kéo file script .cs vào ô bên dưới.",
                MessageType.Info
            );

            DrawMonoScriptDropField();
            EditorGUILayout.EndVertical();
            return;
        }

        GameObject currentTarget = targetGO != null ? targetGO : manualTargetObject;

        // Search Filter
        EditorGUILayout.BeginHorizontal();
        searchFilter = EditorGUILayout.TextField("Tìm kiếm script:", searchFilter);
        if (GUILayout.Button("✕", GUILayout.Width(22)))
        {
            searchFilter = "";
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        // Lấy danh sách component
        bool incChildren = includeChildrenProp != null && includeChildrenProp.boolValue;
        MonoBehaviour[] scripts = null;
        try
        {
            scripts = incChildren
                ? currentTarget.GetComponentsInChildren<MonoBehaviour>(true)
                : currentTarget.GetComponents<MonoBehaviour>();
        }
        catch
        {
            scripts = new MonoBehaviour[0];
        }

        List<string> foundNames = new List<string>();
        for (int i = 0; i < scripts.Length; i++)
        {
            MonoBehaviour mb = scripts[i];
            if (mb == null || mb is PlayableDirector) continue;

            string typeName = mb.GetType().Name;
            if (!foundNames.Contains(typeName))
            {
                foundNames.Add(typeName);
            }
        }

        foundNames.Sort();

        if (foundNames.Count == 0)
        {
            EditorGUILayout.HelpBox("Không tìm thấy MonoBehaviour nào trên GameObject này.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        // Nút Select All / Clear All
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Chọn tất cả (Select All)"))
        {
            for (int i = 0; i < foundNames.Count; i++)
            {
                AddScriptNameIfMissing(foundNames[i]);
            }
        }

        if (GUILayout.Button("Bỏ chọn tất cả (Clear All)"))
        {
            if (scriptNamesProp != null)
            {
                scriptNamesProp.ClearArray();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Checkbox list
        for (int i = 0; i < foundNames.Count; i++)
        {
            string sName = foundNames[i];

            if (!string.IsNullOrEmpty(searchFilter) &&
                sName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            bool isSelected = ContainsScriptName(sName);

            EditorGUI.BeginChangeCheck();
            bool newSelected = EditorGUILayout.ToggleLeft($" {sName}", isSelected);
            if (EditorGUI.EndChangeCheck())
            {
                if (newSelected)
                {
                    AddScriptNameIfMissing(sName);
                }
                else
                {
                    RemoveScriptName(sName);
                }
            }
        }

        EditorGUILayout.Space(4);
        int currentCount = scriptNamesProp != null ? scriptNamesProp.arraySize : 0;
        EditorGUILayout.LabelField($"Đang chọn: {currentCount} script(s)", EditorStyles.miniBoldLabel);

        DrawMonoScriptDropField();

        EditorGUILayout.EndVertical();
    }

    private void DrawMonoScriptDropField()
    {
        EditorGUILayout.Space(4);
        MonoScript droppedScript = (MonoScript)EditorGUILayout.ObjectField(
            new GUIContent("Thêm từ MonoScript (.cs):", "Kéo thả file script .cs vào đây để thêm nhanh tên script"),
            null,
            typeof(MonoScript),
            false
        );

        if (droppedScript != null)
        {
            Type scriptClass = droppedScript.GetClass();
            if (scriptClass != null)
            {
                AddScriptNameIfMissing(scriptClass.Name);
            }
        }
    }

    private bool ContainsScriptName(string scriptName)
    {
        if (scriptNamesProp == null || string.IsNullOrEmpty(scriptName)) return false;
        string targetTrimmed = scriptName.Trim();

        for (int i = 0; i < scriptNamesProp.arraySize; i++)
        {
            SerializedProperty elem = scriptNamesProp.GetArrayElementAtIndex(i);
            if (elem != null && string.Equals(elem.stringValue?.Trim(), targetTrimmed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private void AddScriptNameIfMissing(string scriptName)
    {
        if (scriptNamesProp == null || string.IsNullOrEmpty(scriptName)) return;
        string targetTrimmed = scriptName.Trim();

        if (!ContainsScriptName(targetTrimmed))
        {
            int newIdx = scriptNamesProp.arraySize;
            scriptNamesProp.InsertArrayElementAtIndex(newIdx);
            SerializedProperty elem = scriptNamesProp.GetArrayElementAtIndex(newIdx);
            elem.stringValue = targetTrimmed;
        }
    }

    private void RemoveScriptName(string scriptName)
    {
        if (scriptNamesProp == null || string.IsNullOrEmpty(scriptName)) return;
        string targetTrimmed = scriptName.Trim();

        for (int i = scriptNamesProp.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty elem = scriptNamesProp.GetArrayElementAtIndex(i);
            if (elem != null && string.Equals(elem.stringValue?.Trim(), targetTrimmed, StringComparison.OrdinalIgnoreCase))
            {
                scriptNamesProp.DeleteArrayElementAtIndex(i);
            }
        }
    }

    private GameObject FindBoundGameObject(ScriptActivationClip clip)
    {
        if (clip == null) return null;

        try
        {
            PlayableDirector director = TimelineEditor.inspectedDirector;
            if (director == null) return null;

            // 1. Kiểm tra targetObjectOverride
            GameObject overrideObj = clip.targetObjectOverride.Resolve(director);
            if (overrideObj != null) return overrideObj;

            // 2. Quét Timeline Tracks
            TimelineAsset timeline = director.playableAsset as TimelineAsset;
            if (timeline == null) return null;

            foreach (TrackAsset track in timeline.GetRootTracks())
            {
                GameObject found = CheckTrackForClip(track, clip, director);
                if (found != null) return found;
            }
        }
        catch { }

        return null;
    }

    private GameObject CheckTrackForClip(TrackAsset track, ScriptActivationClip clip, PlayableDirector director)
    {
        if (track == null || clip == null || director == null) return null;

        try
        {
            if (track is ScriptActivationTrack scriptTrack)
            {
                foreach (TimelineClip c in scriptTrack.GetClips())
                {
                    if (c != null && c.asset == clip)
                    {
                        object binding = director.GetGenericBinding(track);
                        if (binding is GameObject go) return go;
                        if (binding is Component comp && comp != null) return comp.gameObject;
                    }
                }
            }

            foreach (TrackAsset child in track.GetChildTracks())
            {
                GameObject found = CheckTrackForClip(child, clip, director);
                if (found != null) return found;
            }
        }
        catch { }

        return null;
    }
}
#endif
