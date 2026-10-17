using UnityEditor;
using UnityEngine;

namespace Pixelate
{
    public static class PixelateMenuItems
    {
        private const string CaptureManagerName = "Pixelate Capture Manager";

        [MenuItem("GameObject/Pixelate/Pixelate Capture Manager", false, 10)]
        private static void CreateCaptureManagerFromGameObjectMenu(MenuCommand menuCommand)
        {
            CreateCaptureManager(menuCommand);
        }

        private static void CreateCaptureManager(MenuCommand menuCommand)
        {
            GameObject captureManagerObject = new GameObject(CaptureManagerName);

            if (menuCommand != null)
            {
                GameObjectUtility.SetParentAndAlign(captureManagerObject, menuCommand.context as GameObject);
            }
            else if (Selection.activeGameObject != null)
            {
                GameObjectUtility.SetParentAndAlign(captureManagerObject, Selection.activeGameObject);
            }

            captureManagerObject.AddComponent<PixelateCaptureManager>();

            Undo.RegisterCreatedObjectUndo(captureManagerObject, "Create Pixelate Capture Manager");
            Selection.activeGameObject = captureManagerObject;
        }
    }
}
