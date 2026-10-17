#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed class MultiTimelineTabsWindow : EditorWindow
{
    private const string MenuPath = "Tools/Duy/Timeline/Multi Timeline Windows";
    private const string TimelineMenuPath = "Window/Sequencing/Timeline";

    [SerializeField] private List<TimelineTab> tabs = new List<TimelineTab>();
    [SerializeField] private int selectedTab;
    [SerializeField] private Vector2 scrollPosition;

    [MenuItem(MenuPath)]
    public static void OpenWindow()
    {
        MultiTimelineTabsWindow window = GetWindow<MultiTimelineTabsWindow>();
        window.titleContent = new GUIContent("Timeline Windows");
        window.minSize = new Vector2(420f, 260f);
        window.Show();
    }

    [MenuItem("Tools/Duy/Timeline/Add Selected To Timeline Tabs")]
    public static void AddSelectedToTabs()
    {
        MultiTimelineTabsWindow window = GetWindow<MultiTimelineTabsWindow>();
        window.AddSelectionAsTab();
        window.Show();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (tabs.Count == 0)
        {
            DrawEmptyState();
            return;
        }

        selectedTab = Mathf.Clamp(selectedTab, 0, tabs.Count - 1);
        DrawTabStrip();
        DrawSelectedTab(tabs[selectedTab]);
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Add Selection", EditorStyles.toolbarButton, GUILayout.Width(100f)))
        {
            AddSelectionAsTab();
        }

        if (GUILayout.Button("Add Empty", EditorStyles.toolbarButton, GUILayout.Width(80f)))
        {
            tabs.Add(new TimelineTab { name = "Timeline" });
            selectedTab = tabs.Count - 1;
            GUI.changed = true;
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Open All Windows", EditorStyles.toolbarButton, GUILayout.Width(120f)))
        {
            OpenAllTabsInNewTimelineWindows();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawEmptyState()
    {
        EditorGUILayout.Space(18f);
        EditorGUILayout.HelpBox(
            "Chọn một GameObject có PlayableDirector trong Scene rồi bấm Add Selection.\n" +
            "Mỗi tab có thể mở thành một cửa sổ Timeline gốc đã patch để chỉnh nhiều Timeline cùng lúc.",
            MessageType.Info);
    }

    private void DrawTabStrip()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        for (int i = 0; i < tabs.Count; i++)
        {
            TimelineTab tab = tabs[i];
            string tabName = string.IsNullOrWhiteSpace(tab.name) ? "Timeline" : tab.name;
            bool isSelected = i == selectedTab;

            GUIStyle style = isSelected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
            if (GUILayout.Toggle(isSelected, tabName, style, GUILayout.MinWidth(80f), GUILayout.MaxWidth(180f)))
            {
                selectedTab = i;
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSelectedTab(TimelineTab tab)
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(8f);
        EditorGUI.BeginChangeCheck();

        tab.name = EditorGUILayout.TextField("Tab Name", tab.name);

        PlayableDirector newDirector = (PlayableDirector)EditorGUILayout.ObjectField(
            "Playable Director",
            tab.director,
            typeof(PlayableDirector),
            true);

        if (newDirector != tab.director)
        {
            tab.director = newDirector;
            if (newDirector != null)
            {
                tab.timelineAsset = newDirector.playableAsset as TimelineAsset;
                tab.name = BuildTabName(newDirector, tab.timelineAsset);
            }
        }

        TimelineAsset newTimeline = (TimelineAsset)EditorGUILayout.ObjectField(
            "Timeline Asset",
            tab.timelineAsset,
            typeof(TimelineAsset),
            false);

        if (newTimeline != tab.timelineAsset)
        {
            tab.timelineAsset = newTimeline;
            if (string.IsNullOrWhiteSpace(tab.name) || tab.name == "Timeline")
            {
                tab.name = newTimeline != null ? newTimeline.name : "Timeline";
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            Repaint();
        }

        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(tab.director == null && tab.timelineAsset == null))
        {
            if (GUILayout.Button("Open In New Native Timeline Window", GUILayout.Height(30f)))
            {
                OpenTabInNewTimelineWindow(tab);
            }
        }

        using (new EditorGUI.DisabledScope(tab.director == null && tab.timelineAsset == null))
        {
            if (GUILayout.Button("Open In Shared Unity Timeline Window", GUILayout.Height(24f)))
            {
                OpenTabInSharedTimelineWindow(tab);
            }
        }

        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(tab.director == null))
        {
            if (GUILayout.Button("Select Director"))
            {
                Selection.activeObject = tab.director.gameObject;
                EditorGUIUtility.PingObject(tab.director.gameObject);
            }
        }

        using (new EditorGUI.DisabledScope(tab.timelineAsset == null))
        {
            if (GUILayout.Button("Ping Asset"))
            {
                EditorGUIUtility.PingObject(tab.timelineAsset);
            }
        }

        if (GUILayout.Button("Remove Tab"))
        {
            tabs.RemoveAt(selectedTab);
            selectedTab = Mathf.Clamp(selectedTab - 1, 0, tabs.Count - 1);
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10f);
        DrawBindingHint(tab);

        EditorGUILayout.EndScrollView();
    }

    private void DrawBindingHint(TimelineTab tab)
    {
        if (tab.director != null)
        {
            EditorGUILayout.HelpBox(
                "Tab này mở Timeline bằng PlayableDirector trong Scene. Binding GameObject/Component vẫn giống Timeline mặc định, và cửa sổ mới sẽ được lock vào director này.",
                MessageType.None);
            return;
        }

        if (tab.timelineAsset != null)
        {
            EditorGUILayout.HelpBox(
                "Tab này chỉ có TimelineAsset nên không có scene binding. Nếu muốn kéo GameObject/Component, hãy dùng PlayableDirector trong Scene.",
                MessageType.Warning);
        }
    }

    private void AddSelectionAsTab()
    {
        UnityEngine.Object active = Selection.activeObject;

        PlayableDirector director = null;
        TimelineAsset timeline = null;

        if (active is GameObject gameObject)
        {
            director = gameObject.GetComponent<PlayableDirector>();
            timeline = director != null ? director.playableAsset as TimelineAsset : null;
        }
        else if (active is PlayableDirector selectedDirector)
        {
            director = selectedDirector;
            timeline = selectedDirector.playableAsset as TimelineAsset;
        }
        else if (active is TimelineAsset selectedTimeline)
        {
            timeline = selectedTimeline;
        }

        if (director == null && timeline == null)
        {
            ShowNotification(new GUIContent("Select a PlayableDirector or TimelineAsset first."));
            return;
        }

        tabs.Add(new TimelineTab
        {
            director = director,
            timelineAsset = timeline,
            name = BuildTabName(director, timeline)
        });

        selectedTab = tabs.Count - 1;
        GUI.changed = true;
    }

    private static string BuildTabName(PlayableDirector director, TimelineAsset timeline)
    {
        if (director != null)
        {
            return timeline != null
                ? $"{director.gameObject.name} - {timeline.name}"
                : director.gameObject.name;
        }

        return timeline != null ? timeline.name : "Timeline";
    }

    private static void OpenTabInSharedTimelineWindow(TimelineTab tab)
    {
        if (tab.director != null)
        {
            Selection.activeObject = tab.director.gameObject;
            EditorGUIUtility.PingObject(tab.director.gameObject);
            OpenUnityTimelineWindow();
            TimelineEditor.GetOrCreateWindow().SetTimeline(tab.director);
            return;
        }

        if (tab.timelineAsset != null)
        {
            Selection.activeObject = tab.timelineAsset;
            EditorGUIUtility.PingObject(tab.timelineAsset);
            OpenUnityTimelineWindow();
            TimelineEditor.GetOrCreateWindow().SetTimeline(tab.timelineAsset);
        }
    }

    private static void OpenTabInNewTimelineWindow(TimelineTab tab)
    {
        TimelineEditorWindow timelineWindow = CreateNativeTimelineWindow();
        if (timelineWindow == null)
        {
            OpenTabInSharedTimelineWindow(tab);
            return;
        }

        timelineWindow.titleContent = new GUIContent(string.IsNullOrWhiteSpace(tab.name) ? "Timeline" : tab.name);
        timelineWindow.Show();
        timelineWindow.Focus();

        if (tab.director != null)
        {
            timelineWindow.SetTimeline(tab.director);
            timelineWindow.locked = true;
            Selection.activeObject = tab.director.gameObject;
            return;
        }

        if (tab.timelineAsset != null)
        {
            timelineWindow.SetTimeline(tab.timelineAsset);
            timelineWindow.locked = true;
            Selection.activeObject = tab.timelineAsset;
        }
    }

    private void OpenAllTabsInNewTimelineWindows()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            TimelineTab tab = tabs[i];
            if (tab.director == null && tab.timelineAsset == null)
            {
                continue;
            }

            OpenTabInNewTimelineWindow(tab);
        }
    }

    private static TimelineEditorWindow CreateNativeTimelineWindow()
    {
        Type timelineWindowType = Type.GetType("UnityEditor.Timeline.TimelineWindow, Unity.Timeline.Editor");
        if (timelineWindowType == null)
        {
            return null;
        }

        return ScriptableObject.CreateInstance(timelineWindowType) as TimelineEditorWindow;
    }

    private static void OpenUnityTimelineWindow()
    {
        if (!EditorApplication.ExecuteMenuItem(TimelineMenuPath))
        {
            EditorApplication.ExecuteMenuItem("Window/Timeline");
        }
    }

    [Serializable]
    private sealed class TimelineTab
    {
        public string name;
        public PlayableDirector director;
        public TimelineAsset timelineAsset;
    }
}
#endif
