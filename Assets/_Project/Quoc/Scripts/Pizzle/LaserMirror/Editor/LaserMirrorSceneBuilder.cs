using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LaserMirrorSceneBuilder
{
    private const string ScenePath = "Assets/_Project/Quoc/Scene/MiniGame/LaserMirror.unity";
    private const string GeneratedFolder = "Assets/_Project/Quoc/Scene/MiniGame/Generated";
    private const string RootName = "LaserMirrorMiniGame_AutoSetup";

    [MenuItem("Tools/Quoc/MiniGame/Build Laser Mirror Arch Scene")]
    public static void BuildScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject oldRoot = GameObject.Find(RootName);
        if (oldRoot != null)
        {
            Object.DestroyImmediate(oldRoot);
        }

        GameObject oldBootstrap = GameObject.Find("LaserMirrorMiniGame_Bootstrap");
        if (oldBootstrap != null)
        {
            LaserMirrorSceneBootstrap2D bootstrapComp = oldBootstrap.GetComponent<LaserMirrorSceneBootstrap2D>();
            if (bootstrapComp != null)
            {
                bootstrapComp.SetupDefaultArchLayout();
                bootstrapComp.Build();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log($"Rebuilt Laser Mirror Arch Minigame in {ScenePath}", oldBootstrap);
                return;
            }
        }

        EnsureFolder(GeneratedFolder);

        GameObject root = new GameObject(RootName);
        root.transform.position = Vector3.zero;

        LaserMirrorSceneBootstrap2D bootstrap = root.AddComponent<LaserMirrorSceneBootstrap2D>();
        bootstrap.SetupDefaultArchLayout();
        bootstrap.Build();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Built new Laser Mirror Arch Scene in {ScenePath}", root);
    }

    private static void EnsureFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
            string child = Path.GetFileName(folder);
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
