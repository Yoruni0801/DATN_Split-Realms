#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class DispersalCartridgeUiBaker
{
    private const string ScenePath = "Assets/_Project/Quoc/Scene/MiniGame/DispersalCartridge.unity";

    [MenuItem("Tools/Fire Cave/Bake Dispersal Cartridge UI")]
    private static void BakeFromMenu()
    {
        Bake();
    }

    public static void Bake()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        DispersalCartridgeController controller = Object.FindFirstObjectByType<DispersalCartridgeController>();
        if (controller == null)
        {
            Debug.LogError("Không tìm thấy DispersalCartridgeController trong scene.");
            return;
        }

        MethodInfo buildUi = typeof(DispersalCartridgeController).GetMethod("GenerateUIInEditMode", BindingFlags.Instance | BindingFlags.NonPublic);
        buildUi.Invoke(controller, null);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        EditorSceneManager.SaveScene(controller.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Đã bake UI Dispersal Cartridge vào scene.");
    }
}
#endif
