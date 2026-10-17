using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BuildSceneDependencyHighlighter
{
    private const string EnabledPrefsKey = "Duy.BuildSceneDependencyHighlighter.Enabled";
    private const string AutoRefreshPrefsKey = "Duy.BuildSceneDependencyHighlighter.AutoRefresh";

    private static readonly Color OutlineColor = new Color(1f, 0.1f, 0.05f, 1f);
    private static readonly HashSet<string> UsedAssetPaths = new HashSet<string>();
    private static readonly HashSet<string> UnusedAssetPaths = new HashSet<string>();
    private static readonly HashSet<string> BuildScenePaths = new HashSet<string>();

    private static bool hasScanResult;

    static BuildSceneDependencyHighlighter()
    {
        EditorApplication.projectWindowItemOnGUI -= DrawProjectItemOverlay;
        EditorApplication.projectWindowItemOnGUI += DrawProjectItemOverlay;

        EditorBuildSettings.sceneListChanged -= HandleBuildSceneListChanged;
        EditorBuildSettings.sceneListChanged += HandleBuildSceneListChanged;

        if (AutoRefreshEnabled)
        {
            AutoRefreshEnabled = false;
            Debug.Log("[BuildSceneDependencyHighlighter] Auto Refresh was enabled and has been disabled to avoid scanning all Assets after Unity reloads.");
        }
    }

    private static bool HighlightEnabled
    {
        get => EditorPrefs.GetBool(EnabledPrefsKey, true);
        set => EditorPrefs.SetBool(EnabledPrefsKey, value);
    }

    private static bool AutoRefreshEnabled
    {
        get => EditorPrefs.GetBool(AutoRefreshPrefsKey, false);
        set => EditorPrefs.SetBool(AutoRefreshPrefsKey, value);
    }

    [MenuItem("Tools/Duy/Dependency Highlighter/Scan Build Scene Dependencies")]
    public static void ScanBuildSceneDependencies()
    {
        ScanBuildSceneDependencies("Manual menu");
    }

    private static void ScanBuildSceneDependencies(string trigger)
    {
        Debug.Log($"[BuildSceneDependencyHighlighter] Dependency scan started by {trigger}. This may scan all project Assets.");

        UsedAssetPaths.Clear();
        UnusedAssetPaths.Clear();
        BuildScenePaths.Clear();

        string[] enabledScenePaths = GetEnabledBuildScenePaths();
        foreach (string scenePath in enabledScenePaths)
        {
            BuildScenePaths.Add(scenePath);
        }

        if (enabledScenePaths.Length > 0)
        {
            string[] dependencies = AssetDatabase.GetDependencies(enabledScenePaths, true);
            for (int i = 0; i < dependencies.Length; i++)
            {
                string dependencyPath = NormalizePath(dependencies[i]);
                if (!string.IsNullOrEmpty(dependencyPath))
                {
                    UsedAssetPaths.Add(dependencyPath);
                }
            }
        }

        string[] allAssetGuids = AssetDatabase.FindAssets(string.Empty, new[] { "Assets" });
        for (int i = 0; i < allAssetGuids.Length; i++)
        {
            string assetPath = NormalizePath(AssetDatabase.GUIDToAssetPath(allAssetGuids[i]));
            if (!ShouldCheckAsset(assetPath))
            {
                continue;
            }

            if (!UsedAssetPaths.Contains(assetPath))
            {
                UnusedAssetPaths.Add(assetPath);
            }
        }

        hasScanResult = true;
        HighlightEnabled = true;
        EditorApplication.RepaintProjectWindow();

        Debug.Log(
            $"[BuildSceneDependencyHighlighter] Scan complete. " +
            $"Trigger: {trigger}, Scenes: {enabledScenePaths.Length}, Used assets: {UsedAssetPaths.Count}, Unused assets: {UnusedAssetPaths.Count}.");
    }

    [MenuItem("Tools/Duy/Dependency Highlighter/Clear Highlight")]
    public static void ClearHighlight()
    {
        UsedAssetPaths.Clear();
        UnusedAssetPaths.Clear();
        BuildScenePaths.Clear();
        hasScanResult = false;
        HighlightEnabled = false;
        EditorApplication.RepaintProjectWindow();
    }

    [MenuItem("Tools/Duy/Dependency Highlighter/Toggle Highlight")]
    public static void ToggleHighlight()
    {
        HighlightEnabled = !HighlightEnabled;
        EditorApplication.RepaintProjectWindow();
    }

    [MenuItem("Tools/Duy/Dependency Highlighter/Toggle Auto Refresh")]
    public static void ToggleAutoRefresh()
    {
        AutoRefreshEnabled = !AutoRefreshEnabled;

        Debug.Log(AutoRefreshEnabled
            ? "[BuildSceneDependencyHighlighter] Auto Refresh enabled. It will scan only when Build Settings scene list changes."
            : "[BuildSceneDependencyHighlighter] Auto Refresh disabled.");
    }

    [MenuItem("Tools/Duy/Dependency Highlighter/Toggle Highlight", true)]
    private static bool ValidateToggleHighlight()
    {
        Menu.SetChecked("Tools/Duy/Dependency Highlighter/Toggle Highlight", HighlightEnabled);
        return true;
    }

    [MenuItem("Tools/Duy/Dependency Highlighter/Toggle Auto Refresh", true)]
    private static bool ValidateToggleAutoRefresh()
    {
        Menu.SetChecked("Tools/Duy/Dependency Highlighter/Toggle Auto Refresh", AutoRefreshEnabled);
        return true;
    }

    private static void DrawProjectItemOverlay(string guid, Rect selectionRect)
    {
        if (!HighlightEnabled || !hasScanResult)
        {
            return;
        }

        string assetPath = NormalizePath(AssetDatabase.GUIDToAssetPath(guid));
        if (string.IsNullOrEmpty(assetPath) || !UnusedAssetPaths.Contains(assetPath))
        {
            return;
        }

        DrawOutline(selectionRect);
    }

    private static void DrawOutline(Rect rect)
    {
        Rect outlineRect = rect;
        outlineRect.xMin += 1f;
        outlineRect.xMax -= 1f;
        outlineRect.yMin += 1f;
        outlineRect.yMax -= 1f;

        Handles.BeginGUI();
        Color previousColor = Handles.color;
        Handles.color = OutlineColor;

        Vector3 topLeft = new Vector3(outlineRect.xMin, outlineRect.yMin);
        Vector3 topRight = new Vector3(outlineRect.xMax, outlineRect.yMin);
        Vector3 bottomRight = new Vector3(outlineRect.xMax, outlineRect.yMax);
        Vector3 bottomLeft = new Vector3(outlineRect.xMin, outlineRect.yMax);

        Handles.DrawAAPolyLine(3f, topLeft, topRight, bottomRight, bottomLeft, topLeft);
        Handles.color = previousColor;
        Handles.EndGUI();
    }

    private static string[] GetEnabledBuildScenePaths()
    {
        List<string> scenePaths = new List<string>();
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            EditorBuildSettingsScene scene = scenes[i];
            if (scene == null || !scene.enabled)
            {
                continue;
            }

            string scenePath = NormalizePath(scene.path);
            if (!string.IsNullOrEmpty(scenePath) && scenePath.StartsWith("Assets/"))
            {
                scenePaths.Add(scenePath);
            }
        }

        return scenePaths.ToArray();
    }

    private static bool ShouldCheckAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/"))
        {
            return false;
        }

        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return false;
        }

        if (BuildScenePaths.Contains(assetPath))
        {
            return false;
        }

        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
        if (extension == ".meta" || extension == ".unity" || extension == ".lighting")
        {
            return false;
        }

        string lowerPath = assetPath.ToLowerInvariant();
        if (lowerPath.Contains("/editor/") ||
            lowerPath.Contains("/editor default resources/") ||
            lowerPath.Contains("/gizmos/") ||
            lowerPath.Contains("/lightingdata") ||
            lowerPath.Contains("/lighting data") ||
            lowerPath.Contains("/occlusion") ||
            lowerPath.Contains("/navmesh") ||
            lowerPath.Contains("/generated") ||
            lowerPath.Contains("/settings/") ||
            lowerPath.Contains("/scenesettings/") ||
            lowerPath.Contains("/scene settings/"))
        {
            return false;
        }

        string typeName = AssetDatabase.GetMainAssetTypeAtPath(assetPath)?.Name ?? string.Empty;
        return !IsIgnoredUnityGeneratedType(typeName);
    }

    private static bool IsIgnoredUnityGeneratedType(string typeName)
    {
        switch (typeName)
        {
            case "LightingDataAsset":
            case "LightmapParameters":
            case "OcclusionCullingData":
            case "NavMeshData":
            case "RenderPipelineGlobalSettings":
            case "UniversalRenderPipelineGlobalSettings":
            case "HDRenderPipelineGlobalSettings":
                return true;
            default:
                return false;
        }
    }

    private static void HandleBuildSceneListChanged()
    {
        if (AutoRefreshEnabled)
        {
            ScanBuildSceneDependencies("Build Settings scene list changed");
        }
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/');
    }
}
